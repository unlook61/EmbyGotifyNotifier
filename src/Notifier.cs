using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emby.Notifications;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugin.GotifyNotifications
{
    /// <summary>
    /// Gotify 通知器。被 Emby 通知系统自动发现（MEF 导出），
    /// 用户在「设置 - 通知」中启用本通知器并勾选事件后，
    /// 对应事件发生时会调用 <see cref="SendNotification"/>。
    /// </summary>
    public class Notifier : IUserNotifier, IDisposable
    {
        /// <summary>新媒体入库事件（Emby 内置）</summary>
        private const string ItemAddedEventId = "library.new";

        /// <summary>媒体信息更新事件</summary>
        private const string ItemUpdatedEventId = "library.updated";

        /// <summary>媒体移除事件</summary>
        private const string ItemRemovedEventId = "library.deleted";

        /// <summary>Emby「设置 → 通知 → 发送测试通知」使用的事件，不带媒体项</summary>
        private const string TestNotificationEventId = "system.notificationtest";

        /// <summary>合并通知列表中最多展示的条目数</summary>
        private const int MaxMergedListLines = 10;

        private readonly ILogger _logger;
        private readonly IApplicationHost _applicationHost;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IHttpClient _httpClient;
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;

        /// <summary>待合并的入库通知缓冲</summary>
        private readonly List<PendingNotification> _pendingItems = new List<PendingNotification>();

        /// <summary>合并定时器</summary>
        private Timer _flushTimer;

        /// <summary>合并缓冲锁</summary>
        private readonly object _syncLock = new object();

        /// <summary>同一「事件 + 媒体」的去重窗口（秒）</summary>
        private const int DuplicateWindowSeconds = 60;

        /// <summary>最近处理过的事件，用于跨用户订阅去重</summary>
        private readonly Dictionary<string, DateTime> _recentEvents =
            new Dictionary<string, DateTime>(StringComparer.Ordinal);

        private readonly object _recentLock = new object();

        /// <summary>缓冲中第一条通知使用的配置（flush 时以最新配置为准）</summary>
        private PluginOptions _pendingOptions;

        private bool _disposed;

        public Notifier(
            ILogger logger,
            IApplicationHost applicationHost,
            IJsonSerializer jsonSerializer,
            IHttpClient httpClient,
            ILibraryManager libraryManager,
            IUserManager userManager,
            IUserDataManager userDataManager)
        {
            _logger = logger;
            _applicationHost = applicationHost;
            _jsonSerializer = jsonSerializer;
            _httpClient = httpClient;
            _libraryManager = libraryManager;
            _userManager = userManager;
            _userDataManager = userDataManager;
        }

        /// <summary>
        /// 通知器名称，显示在 Emby 通知设置页面
        /// </summary>
        public string Name => "Gotify";

        /// <summary>
        /// 通知器唯一标识（用于存储用户级通知配置）
        /// </summary>
        public string Key => "gotifynotifications";

        /// <summary>
        /// 通知条目编辑器模块名。Emby 的「设置 → 通知」点击本服务后，
        /// 会 require 该模块来渲染弹窗；返回 null 会导致点击无任何反应。
        /// </summary>
        public string SetupModuleUrl => Plugin.EntryEditorName;

        /// <summary>
        /// 发送通知到 Gotify
        /// </summary>
        public async Task SendNotification(InternalNotificationRequest request, CancellationToken cancellationToken)
        {
            try
            {
                PluginOptions options = GetOptions();
                if (options == null)
                {
                    NotificationHistory.Add(request.Event, request.Title, false, "skipped", "未找到插件实例，无法读取配置");
                    return;
                }

                // 通知条目可以单独覆盖优先级/模板，不影响全局配置
                options = ApplyEntryOverrides(options, request);

                string titleLine = BuildTitleLine(request);

                // Emby 自带的「发送测试通知」不带媒体项，直接套模板会得到
                // “📺 ｜ (0)” 这类空变量，这里改用示意数据渲染
                if (request.Item == null
                    && string.Equals(request.Event, TestNotificationEventId, StringComparison.OrdinalIgnoreCase))
                {
                    string demoTitle;
                    string demoMessage;
                    string demoServerName = request.Server != null
                        ? request.Server.Name
                        : (_applicationHost != null ? _applicationHost.Name : string.Empty);

                    SampleNotification.Build(options, demoServerName, out demoTitle, out demoMessage);

                    GotifySendResult demoResult = await SendToGotifyAsync(
                        options, demoTitle, demoMessage, null, null, cancellationToken).ConfigureAwait(false);

                    if (demoResult.Success)
                    {
                        _logger.Info("[GotifyNotifier] Emby 测试通知发送成功: {0}", demoTitle);
                        NotificationHistory.Add(request.Event, demoTitle, true, "sent",
                            "来自「设置 → 通知」的测试按钮（HTTP " + demoResult.StatusCode + "）");
                    }
                    else
                    {
                        _logger.Error("[GotifyNotifier] Emby 测试通知发送失败: {0}", demoResult.Error);
                        NotificationHistory.Add(request.Event, demoTitle, false, "failed", demoResult.Error);
                    }

                    return;
                }

                if (string.IsNullOrWhiteSpace(options.ServerUrl) || string.IsNullOrWhiteSpace(options.Token))
                {
                    _logger.Warn("[GotifyNotifier] 未配置 Gotify 服务器地址或 Token，跳过发送。事件: {0}", request.Event);
                    NotificationHistory.Add(request.Event, titleLine, false, "skipped", "未配置服务器地址或 Token，已跳过");
                    return;
                }

                // 过滤规则（媒体类型 / 媒体库 / 标签 / 评分 / 年份 / 关键字 / 追剧）
                string filterReason;
                if (!NotificationFilter.ShouldSend(
                        request, options, _libraryManager, _userManager, _userDataManager, _logger, out filterReason))
                {
                    _logger.Info("[GotifyNotifier] 通知被规则过滤: {0} -> {1}", titleLine, filterReason);
                    NotificationHistory.Add(request.Event, titleLine, false, "skipped", "已被过滤：" + filterReason);
                    return;
                }

                // Emby 会为每个订阅了该事件的用户各调用一次通知器；
                // 关闭合并时，同一次入库还可能重复触发 ItemAdded。
                // 这里按「事件 + 媒体 Id」做 60 秒去重，避免手机收到多条完全相同的推送。
                string dedupItemKey = request.Item != null ? request.Item.Id : null;
                if (IsRecentDuplicate(request.Event, dedupItemKey))
                {
                    _logger.Info("[GotifyNotifier] 跳过重复事件: {0} / {1}", request.Event, titleLine);
                    NotificationHistory.Add(request.Event, titleLine, false, "skipped",
                        "同一媒体在 " + DuplicateWindowSeconds + " 秒内已处理过，跳过重复推送");
                    return;
                }

                // 入库通知可选择合并发送，避免一次扫描产生大量推送
                if (options.MergeLibraryNotifications
                    && string.Equals(request.Event, ItemAddedEventId, StringComparison.OrdinalIgnoreCase))
                {
                    string imageUrl = await TryGetImageUrlAsync(request, options, cancellationToken).ConfigureAwait(false);
                    string itemKey = dedupItemKey;

                    // 入队时就按当前模板渲染好标题与正文，
                    // 这样合并窗口过期后即使只剩一条，也能发出与模板一致的内容
                    Dictionary<string, string> itemValues = BuildTokenValues(request, titleLine);
                    string renderedTitle = BuildTitle(request, options, itemValues);
                    string renderedMessage = BuildMessage(request, options, itemValues);

                    lock (_syncLock)
                    {
                        // 同一次扫描中，Emby 可能对同一媒体重复触发 ItemAdded
                        // （发现文件一次、元数据刷新后再一次），这里按媒体 Id 去重，
                        // 避免出现“新片入库（2 项）”但两条内容一模一样。
                        if (!string.IsNullOrEmpty(itemKey))
                        {
                            bool duplicated = false;
                            foreach (PendingNotification pending in _pendingItems)
                            {
                                if (string.Equals(pending.ItemKey, itemKey, StringComparison.Ordinal))
                                {
                                    duplicated = true;
                                    break;
                                }
                            }

                            if (duplicated)
                            {
                                _logger.Debug("[GotifyNotifier] 忽略重复的入库事件: {0} ({1})", titleLine, itemKey);
                                return;
                            }
                        }

                        _pendingItems.Add(new PendingNotification
                        {
                            ItemKey = itemKey,
                            TitleLine = titleLine,
                            Title = renderedTitle,
                            Message = renderedMessage,
                            Values = itemValues,
                            Overview = GetOverview(request),
                            ImageUrl = imageUrl
                        });
                        _pendingOptions = options;

                        int windowSeconds = Math.Max(2, options.MergeWindowSeconds);
                        if (_flushTimer == null)
                        {
                            _flushTimer = new Timer(FlushCallback, null, TimeSpan.FromSeconds(windowSeconds), Timeout.InfiniteTimeSpan);
                        }
                        else
                        {
                            // 滑动窗口：每次有新通知都重置计时
                            _flushTimer.Change(TimeSpan.FromSeconds(windowSeconds), Timeout.InfiniteTimeSpan);
                        }

                        _logger.Info("[GotifyNotifier] 入库通知已加入合并队列: {0}（当前 {1} 条，{2} 秒后发送）",
                            titleLine, _pendingItems.Count, windowSeconds);

                        NotificationHistory.Add(
                            request.Event,
                            titleLine,
                            true,
                            "queued",
                            "已加入合并队列（当前 " + _pendingItems.Count + " 条，" + windowSeconds + " 秒后发送）");
                    }

                    return;
                }

                Dictionary<string, string> values = BuildTokenValues(request, titleLine);
                string title = BuildTitle(request, options, values);
                string message = BuildMessage(request, options, values);
                string singleImageUrl = options.EnableImages
                    ? await TryGetImageUrlAsync(request, options, cancellationToken).ConfigureAwait(false)
                    : null;
                string clickUrl = BuildClickUrl(request, options);

                GotifySendResult sendResult = await SendToGotifyAsync(options, title, message, singleImageUrl, clickUrl, cancellationToken).ConfigureAwait(false);

                if (sendResult.Success)
                {
                    _logger.Info("[GotifyNotifier] 通知发送成功: {0}", title);
                    NotificationHistory.Add(request.Event, title, true, "sent", "HTTP " + sendResult.StatusCode);
                }
                else
                {
                    _logger.Error("[GotifyNotifier] 通知发送失败: {0} -> {1}", title, sendResult.Error);
                    NotificationHistory.Add(request.Event, title, false, "failed", sendResult.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[GotifyNotifier] 发送通知时发生异常", ex);
            }
        }

        /// <summary>
        /// 合并窗口到期，将缓冲的入库通知合并为一条发送
        /// </summary>
        private void FlushCallback(object state)
        {
            List<PendingNotification> items;
            PluginOptions options;

            lock (_syncLock)
            {
                items = new List<PendingNotification>(_pendingItems);
                _pendingItems.Clear();
                options = _pendingOptions;
                _pendingOptions = null;
            }

            if (items.Count == 0 || options == null)
            {
                return;
            }

            try
            {
                if (items.Count == 1)
                {
                    // 只有一条时用入队时按模板渲染好的内容发送
                    PendingNotification single = items[0];

                    string title = string.IsNullOrWhiteSpace(single.Title) ? DefaultTitle : single.Title;
                    string message = string.IsNullOrEmpty(single.Message)
                        ? (string.IsNullOrEmpty(single.Overview) ? single.TitleLine : single.TitleLine + "\n\n" + single.Overview)
                        : single.Message;

                    GotifySendResult singleResult = SendToGotifyAsync(options, title, message, single.ImageUrl, null, CancellationToken.None)
                        .GetAwaiter().GetResult();

                    if (singleResult.Success)
                    {
                        _logger.Info("[GotifyNotifier] 通知发送成功: {0}", title);
                        NotificationHistory.Add(ItemAddedEventId, title, true, "sent", "HTTP " + singleResult.StatusCode);
                    }
                    else
                    {
                        _logger.Error("[GotifyNotifier] 通知发送失败: {0} -> {1}", title, singleResult.Error);
                        NotificationHistory.Add(ItemAddedEventId, title, false, "failed", singleResult.Error);
                    }

                    return;
                }

                string mergedTitle;
                if (string.IsNullOrWhiteSpace(options.TitleTemplate))
                {
                    mergedTitle = DefaultTitle + "（" + items.Count + " 项）";
                }
                else if (options.TitleTemplate.Contains("{Count}"))
                {
                    // 合并时也要把其余变量替换掉，否则会出现 “{TypeName}” 之类的原文
                    Dictionary<string, string> mergedValues =
                        new Dictionary<string, string>(items[0].Values ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
                    mergedValues["{Count}"] = items.Count.ToString(CultureInfo.InvariantCulture);
                    mergedTitle = ReplaceTokens(options.TitleTemplate, mergedValues);
                }
                else
                {
                    string firstTitle = string.IsNullOrWhiteSpace(items[0].Title) ? DefaultTitle : items[0].Title;
                    mergedTitle = firstTitle + "（" + items.Count + " 项）";
                }

                StringBuilder messageBuilder = new StringBuilder();
                for (int i = 0; i < Math.Min(items.Count, MaxMergedListLines); i++)
                {
                    messageBuilder.AppendLine(items[i].TitleLine);
                }

                if (items.Count > MaxMergedListLines)
                {
                    messageBuilder.Append("……等共 ").Append(items.Count).AppendLine(" 项");
                }

                string firstOverview = items.Select(i => i.Overview).FirstOrDefault(o => !string.IsNullOrEmpty(o));
                if (!string.IsNullOrEmpty(firstOverview))
                {
                    messageBuilder.AppendLine().Append(firstOverview);
                }

                GotifySendResult mergedResult = SendToGotifyAsync(options, mergedTitle, messageBuilder.ToString().TrimEnd('\r', '\n'), items[0].ImageUrl, null, CancellationToken.None)
                    .GetAwaiter().GetResult();

                if (mergedResult.Success)
                {
                    _logger.Info("[GotifyNotifier] 已合并发送 {0} 条入库通知", items.Count);
                    NotificationHistory.Add(ItemAddedEventId, mergedTitle, true, "sent",
                        "已合并 " + items.Count + " 条（HTTP " + mergedResult.StatusCode + "）");
                }
                else
                {
                    _logger.Error("[GotifyNotifier] 合并通知发送失败（{0} 条）: {1}", items.Count, mergedResult.Error);
                    NotificationHistory.Add(ItemAddedEventId, mergedTitle, false, "failed",
                        "合并 " + items.Count + " 条失败：" + mergedResult.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[GotifyNotifier] 发送合并通知时发生异常", ex);
            }
        }

        /// <summary>
        /// 判断「同一事件 + 同一媒体」是否在去重窗口内已处理过
        /// </summary>
        private bool IsRecentDuplicate(string eventId, string itemKey)
        {
            if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(itemKey))
            {
                return false;
            }

            string key = eventId + "|" + itemKey;
            DateTime now = DateTime.UtcNow;

            lock (_recentLock)
            {
                if (_recentEvents.Count > 500)
                {
                    List<string> expired = new List<string>();
                    foreach (KeyValuePair<string, DateTime> pair in _recentEvents)
                    {
                        if ((now - pair.Value).TotalSeconds >= DuplicateWindowSeconds)
                        {
                            expired.Add(pair.Key);
                        }
                    }

                    foreach (string item in expired)
                    {
                        _recentEvents.Remove(item);
                    }
                }

                DateTime last;
                if (_recentEvents.TryGetValue(key, out last)
                    && (now - last).TotalSeconds < DuplicateWindowSeconds)
                {
                    return true;
                }

                _recentEvents[key] = now;
                return false;
            }
        }

        /// <summary>
        /// 从宿主中取得本插件的配置
        /// </summary>
        private PluginOptions GetOptions()
        {
            Plugin plugin = Plugin.GetPluginInstance(_applicationHost);
            if (plugin == null)
            {
                _logger.Error("[GotifyNotifier] 未找到插件实例");
                return null;
            }

            return plugin.CurrentOptions;
        }

        private const string DefaultTitle = "\U0001F3AC 新片入库";

        private const string DefaultMessageTemplate = "{TitleLine}\n\n{Overview}";

        /// <summary>
        /// 生成通知标题：自定义模板优先，否则按事件类型使用内置中文标题
        /// </summary>
        private static string BuildTitle(
            InternalNotificationRequest request,
            PluginOptions options,
            Dictionary<string, string> values)
        {
            string template = options.TitleTemplate;

            bool customized = !string.IsNullOrWhiteSpace(template)
                && !string.Equals(template.Trim(), DefaultTitle, StringComparison.OrdinalIgnoreCase);

            if (customized)
            {
                return ReplaceTokens(template, values);
            }

            return GetEventDefaultTitle(request, values);
        }

        /// <summary>
        /// 各事件的内置标题
        /// </summary>
        private static string GetEventDefaultTitle(InternalNotificationRequest request, Dictionary<string, string> values)
        {
            string userName = values["{UserName}"];
            string eventId = (request.Event ?? string.Empty).ToLowerInvariant();

            switch (eventId)
            {
                case ItemAddedEventId:
                    return DefaultTitle;
                case ItemRemovedEventId:
                    return "\U0001F5D1 已删除";
                case ItemUpdatedEventId:
                    return "\U0001F504 媒体已更新";
                case "playback.start":
                    return "\u25B6 " + WithUser(userName) + "开始播放";
                case "playback.pause":
                    return "\u23F8 " + WithUser(userName) + "暂停播放";
                case "playback.unpause":
                    return "\u25B6 " + WithUser(userName) + "继续播放";
                case "playback.stop":
                    return "\u23F9 " + WithUser(userName) + "停止播放";
                case "scheduledtasks.completed":
                    return "\u2705 计划任务完成";
                case "scheduledtasks.failed":
                    return "\u274C 计划任务失败";
                case "system.serverstartup":
                    return "\U0001F680 Emby 已启动";
                case "system.updateavailable":
                    return "\u2B06 Emby 有新版本";
                case "system.serverrestartrequired":
                    return "\U0001F501 Emby 需要重启";
                case "item.rate":
                    return "\u2B50 " + WithUser(userName) + "评分";
                default:
                    return string.IsNullOrWhiteSpace(request.Title) ? (request.Event ?? "通知") : request.Title;
            }
        }

        private static string WithUser(string userName)
        {
            return string.IsNullOrEmpty(userName) ? string.Empty : userName + " ";
        }

        /// <summary>
        /// 事件的中文名称（不含 emoji 与用户名），供 {EventName} 变量使用
        /// </summary>
        private static string GetEventName(string eventId)
        {
            switch ((eventId ?? string.Empty).ToLowerInvariant())
            {
                case ItemAddedEventId:
                    return "新媒体入库";
                case ItemRemovedEventId:
                    return "媒体被删除";
                case ItemUpdatedEventId:
                    return "媒体已更新";
                case "playback.start":
                    return "开始播放";
                case "playback.pause":
                    return "暂停播放";
                case "playback.unpause":
                    return "继续播放";
                case "playback.stop":
                    return "停止播放";
                case "scheduledtasks.completed":
                    return "计划任务完成";
                case "scheduledtasks.failed":
                    return "计划任务失败";
                case "system.serverstartup":
                    return "Emby 已启动";
                case "system.updateavailable":
                    return "Emby 有新版本";
                case "system.serverrestartrequired":
                    return "Emby 需要重启";
                case "item.rate":
                    return "评分";
                case TestNotificationEventId:
                    return "测试通知";
                default:
                    return string.IsNullOrWhiteSpace(eventId) ? "通知" : eventId;
            }
        }

        /// <summary>
        /// 事件对应的 emoji（不含文字），供 {EventEmoji} 变量使用
        /// </summary>
        private static string GetEventEmoji(string eventId)
        {
            switch ((eventId ?? string.Empty).ToLowerInvariant())
            {
                case ItemAddedEventId:
                    return "\U0001F3AC";
                case ItemRemovedEventId:
                    return "\U0001F5D1";
                case ItemUpdatedEventId:
                    return "\U0001F504";
                case "playback.start":
                    return "\u25B6";
                case "playback.pause":
                    return "\u23F8";
                case "playback.unpause":
                    return "\u25B6";
                case "playback.stop":
                    return "\u23F9";
                case "scheduledtasks.completed":
                    return "\u2705";
                case "scheduledtasks.failed":
                    return "\u274C";
                case "system.serverstartup":
                    return "\U0001F680";
                case "system.updateavailable":
                    return "\u2B06";
                case "system.serverrestartrequired":
                    return "\U0001F501";
                case "item.rate":
                    return "\u2B50";
                case TestNotificationEventId:
                    return "\U0001F9EA";
                default:
                    return "\U0001F514";
            }
        }

        /// <summary>
        /// 生成通知正文：自定义模板优先，否则按事件类型生成默认排版
        /// </summary>
        private static string BuildMessage(
            InternalNotificationRequest request,
            PluginOptions options,
            Dictionary<string, string> values)
        {
            string template = options.MessageTemplate;

            bool customized = !string.IsNullOrWhiteSpace(template)
                && !string.Equals(template.Trim(), DefaultMessageTemplate, StringComparison.OrdinalIgnoreCase);

            if (customized)
            {
                return CleanMessage(ReplaceTokens(template, values));
            }

            if (options.UseMarkdown)
            {
                return CleanMessage(BuildMarkdownMessage(request, values));
            }

            return CleanMessage(BuildPlainMessage(request, values));
        }

        /// <summary>
        /// 默认纯文本正文
        /// </summary>
        private static string BuildPlainMessage(InternalNotificationRequest request, Dictionary<string, string> values)
        {
            string eventId = (request.Event ?? string.Empty).ToLowerInvariant();
            string titleLine = values["{TitleLine}"];

            // 播放类事件：谁、在哪台设备、看到哪
            if (eventId.StartsWith("playback.", StringComparison.OrdinalIgnoreCase))
            {
                StringBuilder builder = new StringBuilder(titleLine);

                string user = values["{UserName}"];
                if (!string.IsNullOrEmpty(user))
                {
                    builder.Append("\n\U0001F464 ").Append(user);
                }

                string device = values["{DeviceName}"];
                string client = values["{ClientName}"];
                if (!string.IsNullOrEmpty(device))
                {
                    builder.Append("\n\U0001F4F1 ").Append(device);
                    if (!string.IsNullOrEmpty(client))
                    {
                        builder.Append(" · ").Append(client);
                    }
                }
                else if (!string.IsNullOrEmpty(client))
                {
                    builder.Append("\n\U0001F4F1 ").Append(client);
                }

                string progress = values["{Progress}"];
                if (!string.IsNullOrEmpty(progress))
                {
                    builder.Append("\n\u23F1 ").Append(progress);
                }

                return builder.ToString();
            }

            // 媒体类事件：标题 + 一行元信息 + 简介
            StringBuilder media = new StringBuilder(titleLine);

            string typeName = values["{TypeName}"];
            string year = values["{Year}"];
            string genres = values["{Genres}"];
            string rating = values["{Rating}"];
            string runtime = values["{Runtime}"];

            List<string> meta = new List<string>();
            if (!string.IsNullOrEmpty(typeName))
            {
                meta.Add("\U0001F4FA " + typeName);
            }

            if (!string.IsNullOrEmpty(year) && year != "0")
            {
                meta.Add(year);
            }

            if (!string.IsNullOrEmpty(rating))
            {
                meta.Add("\u2B50 " + rating);
            }

            if (!string.IsNullOrEmpty(runtime))
            {
                meta.Add("\u23F1 " + runtime);
            }

            if (!string.IsNullOrEmpty(genres))
            {
                meta.Add(genres);
            }

            string overview = values["{Overview}"];
            string description = request.Description;

            if (meta.Count == 0
                && string.IsNullOrEmpty(overview)
                && string.IsNullOrEmpty(description))
            {
                // 没有任何附加信息，保持原样
                return titleLine;
            }

            if (meta.Count > 0)
            {
                media.Append("\n").Append(string.Join(" · ", meta.ToArray()));
            }

            if (!string.IsNullOrEmpty(overview))
            {
                media.Append("\n\n").Append(overview);
            }
            else if (!string.IsNullOrEmpty(description))
            {
                media.Append("\n\n").Append(description);
            }

            return media.ToString();
        }

        /// <summary>
        /// 默认 Markdown 正文。仅在 Gotify 应用内查看时渲染；
        /// 系统通知栏显示纯文本，因此除标题粗体外不使用其他语法，减少符号噪音。
        /// </summary>
        private static string BuildMarkdownMessage(InternalNotificationRequest request, Dictionary<string, string> values)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("**").Append(values["{TitleLine}"]).Append("**");

            List<string> meta = new List<string>();
            string rating = values["{Rating}"];
            string year = values["{Year}"];
            string genres = values["{Genres}"];
            string runtime = values["{Runtime}"];

            if (!string.IsNullOrEmpty(rating))
            {
                meta.Add("\u2B50 " + rating);
            }

            if (!string.IsNullOrEmpty(year) && year != "0")
            {
                meta.Add(year);
            }

            if (!string.IsNullOrEmpty(runtime))
            {
                meta.Add(runtime);
            }

            if (!string.IsNullOrEmpty(genres))
            {
                meta.Add(genres);
            }

            if (meta.Count > 0)
            {
                builder.Append("\n\n").Append(string.Join(" · ", meta.ToArray()));
            }

            string eventId = (request.Event ?? string.Empty).ToLowerInvariant();
            if (eventId.StartsWith("playback.", StringComparison.OrdinalIgnoreCase))
            {
                string device = values["{DeviceName}"];
                string progress = values["{Progress}"];
                if (!string.IsNullOrEmpty(device))
                {
                    builder.Append("\n\n设备：").Append(device);
                }

                if (!string.IsNullOrEmpty(progress))
                {
                    builder.Append("　进度：").Append(progress);
                }
            }

            string overview = values["{Overview}"];
            if (!string.IsNullOrEmpty(overview))
            {
                builder.Append("\n\n").Append(overview);
            }

            return builder.ToString();
        }

        /// <summary>
        /// 生成点击跳转地址（优先使用配置的外部访问地址）
        /// </summary>
        private static string BuildClickUrl(InternalNotificationRequest request, PluginOptions options)
        {
            if (request.Item == null || string.IsNullOrWhiteSpace(options.ExternalUrl))
            {
                return request.Url;
            }

            string baseUrl = options.ExternalUrl.Trim().TrimEnd('/');
            if (baseUrl.Length == 0)
            {
                return request.Url;
            }

            string serverId = request.Server != null ? request.Server.Id : string.Empty;
            return baseUrl + "/web/index.html#!/item?id=" + request.Item.Id
                + (string.IsNullOrEmpty(serverId) ? string.Empty : "&serverId=" + serverId);
        }

        /// <summary>
        /// 按通知条目（用户订阅）覆盖优先级与模板
        /// </summary>
        private static PluginOptions ApplyEntryOverrides(PluginOptions options, InternalNotificationRequest request)
        {
            NotificationInfo configuration = request.Configuration;
            if (configuration == null || configuration.Options == null || configuration.Options.Count == 0)
            {
                return options;
            }

            PluginOptions clone = options.Clone();

            int priority;
            string value;
            if (TryGetInt(configuration.Options, "Priority", out priority))
            {
                clone.Priority = priority;
            }

            if (TryGetString(configuration.Options, "TitleTemplate", out value) && !string.IsNullOrWhiteSpace(value))
            {
                clone.TitleTemplate = value;
            }

            if (TryGetString(configuration.Options, "MessageTemplate", out value) && !string.IsNullOrWhiteSpace(value))
            {
                clone.MessageTemplate = value;
            }

            return clone;
        }

        private static bool TryGetInt(IDictionary<string, string> options, string key, out int result)
        {
            result = 0;

            string raw;
            if (!options.TryGetValue(key, out raw) || string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            return int.TryParse(raw.Trim(), out result);
        }

        private static bool TryGetString(IDictionary<string, string> options, string key, out string result)
        {
            result = null;

            string raw;
            if (!options.TryGetValue(key, out raw) || raw == null)
            {
                return false;
            }

            result = raw;
            return true;
        }

        /// <summary>
        /// 清理多余空行，避免手机通知出现大片空白
        /// </summary>
        private static string CleanMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            string[] lines = message
                .Split('\n')
                .Select(line => line.TrimEnd())
                .SkipWhile(line => line.Length == 0)
                .ToArray();

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (lines[i].Length == 0)
                {
                    lines = lines.Take(i).ToArray();
                }
                else
                {
                    break;
                }
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// 拼装适合阅读的中文标题行，如：
        /// 《阿凡达》(2009)   /   《某剧集》S01E01 第一集
        /// </summary>
        private static string BuildTitleLine(InternalNotificationRequest request)
        {
            BaseItemDto item = request.Item;

            if (item != null)
            {
                string name = item.Name ?? string.Empty;
                string seriesName = item.SeriesName;

                if (string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase))
                {
                    int season = item.ParentIndexNumber ?? 0;
                    int episode = item.IndexNumber ?? 0;
                    string episodePart = string.Format(
                        CultureInfo.InvariantCulture,
                        "S{0:00}E{1:00}",
                        season,
                        episode);

                    if (!string.IsNullOrEmpty(seriesName))
                    {
                        return string.Format(
                            CultureInfo.InvariantCulture,
                            "《{0}》 {1} {2}",
                            seriesName,
                            episodePart,
                            name);
                    }

                    return episodePart + " " + name;
                }

                if (string.Equals(item.Type, "Movie", StringComparison.OrdinalIgnoreCase))
                {
                    if (item.ProductionYear.HasValue)
                    {
                        return string.Format(CultureInfo.InvariantCulture, "《{0}》({1})", name, item.ProductionYear.Value);
                    }

                    return "《" + name + "》";
                }

                if (!string.IsNullOrEmpty(seriesName))
                {
                    return "《" + seriesName + "》";
                }

                return name;
            }

            // 其他事件：直接使用 Emby 生成的标题
            return request.Title ?? request.Event ?? "通知";
        }

        /// <summary>
        /// 构造模板变量字典
        /// </summary>
        private static Dictionary<string, string> BuildTokenValues(InternalNotificationRequest request, string titleLine)
        {
            BaseItemDto item = request.Item;
            string serverName = request.Server != null ? request.Server.Name : string.Empty;

            string[] genres = item?.Genres;

            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["{TitleLine}"] = titleLine,
                ["{Name}"] = item?.Name ?? string.Empty,
                ["{SeriesName}"] = item?.SeriesName ?? string.Empty,
                ["{Season}"] = (item?.ParentIndexNumber ?? 0).ToString(CultureInfo.InvariantCulture),
                ["{Episode}"] = (item?.IndexNumber ?? 0).ToString(CultureInfo.InvariantCulture),
                ["{Year}"] = (item?.ProductionYear ?? 0).ToString(CultureInfo.InvariantCulture),
                ["{Type}"] = item?.Type ?? string.Empty,
                ["{TypeName}"] = GetTypeName(item?.Type),
                ["{Overview}"] = GetOverview(request),
                ["{Title}"] = request.Title ?? string.Empty,
                ["{ServerName}"] = serverName,
                ["{Url}"] = request.Url ?? string.Empty,
                ["{Event}"] = request.Event ?? string.Empty,
                ["{EventName}"] = GetEventName(request.Event),
                ["{EventEmoji}"] = GetEventEmoji(request.Event),
                ["{Date}"] = request.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                ["{Count}"] = "1",

                // 事件相关
                ["{UserName}"] = request.User != null ? (request.User.Name ?? string.Empty) : string.Empty,
                ["{DeviceName}"] = request.Session != null ? (request.Session.DeviceName ?? string.Empty) : string.Empty,
                ["{ClientName}"] = request.Session != null ? (request.Session.Client ?? string.Empty) : string.Empty,
                ["{Progress}"] = FormatProgress(request),

                // 元数据
                ["{Genres}"] = genres != null && genres.Length > 0 ? string.Join(" / ", genres) : string.Empty,
                ["{Rating}"] = item != null && item.CommunityRating.HasValue
                    ? item.CommunityRating.Value.ToString("0.0", CultureInfo.InvariantCulture)
                    : string.Empty,
                ["{Runtime}"] = FormatRuntime(item != null ? item.RunTimeTicks : null),
                ["{OfficialRating}"] = item?.OfficialRating ?? string.Empty
            };

            return values;
        }

        /// <summary>
        /// 播放进度：12:34 形式
        /// </summary>
        private static string FormatProgress(InternalNotificationRequest request)
        {
            long? ticks = request.PlaybackInfo != null ? request.PlaybackInfo.PositionTicks : null;
            if (!ticks.HasValue || ticks.Value <= 0)
            {
                return string.Empty;
            }

            TimeSpan position = TimeSpan.FromTicks(ticks.Value);
            return position.TotalHours >= 1
                ? string.Format(CultureInfo.InvariantCulture, "{0:0}:{1:00}:{2:00}", (int)position.TotalHours, position.Minutes, position.Seconds)
                : string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", position.Minutes, position.Seconds);
        }

        /// <summary>
        /// 片长：128 分钟
        /// </summary>
        private static string FormatRuntime(long? runTimeTicks)
        {
            if (!runTimeTicks.HasValue || runTimeTicks.Value <= 0)
            {
                return string.Empty;
            }

            int minutes = (int)TimeSpan.FromTicks(runTimeTicks.Value).TotalMinutes;
            return minutes > 0 ? minutes + " 分钟" : string.Empty;
        }

        /// <summary>
        /// 获取媒体简介（优先 item 自身，其次 Emby 通知描述）
        /// </summary>
        private static string GetOverview(InternalNotificationRequest request)
        {
            if (!string.IsNullOrEmpty(request.Item?.Overview))
            {
                return request.Item.Overview;
            }

            return request.Description ?? string.Empty;
        }

        /// <summary>
        /// 简单的模板变量替换
        /// </summary>
        private static string ReplaceTokens(string template, Dictionary<string, string> values)
        {
            // 统一实现，变量名大小写不敏感（{name} 与 {Name} 等价）
            return TemplateRenderer.ReplaceTokens(template, values);
        }

        /// <summary>
        /// 媒体类型中文化
        /// </summary>
        private static string GetTypeName(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return string.Empty;
            }

            switch (type.ToLowerInvariant())
            {
                case "movie":
                    return "电影";
                case "episode":
                    return "剧集";
                case "series":
                    return "剧集";
                case "season":
                    return "季";
                case "musicalbum":
                    return "音乐专辑";
                case "audio":
                case "music":
                    return "音乐";
                case "musicvideo":
                    return "MV";
                case "video":
                    return "视频";
                case "photo":
                    return "照片";
                case "photoalbum":
                    return "相册";
                case "book":
                    return "图书";
                default:
                    return type;
            }
        }

        /// <summary>
        /// 尝试获取海报图片的远程 URL（需要 Emby 配置了远程访问地址）
        /// </summary>
        private async Task<string> TryGetImageUrlAsync(InternalNotificationRequest request, PluginOptions options, CancellationToken cancellationToken)
        {
            if (!options.EnableImages)
            {
                return null;
            }

            try
            {
                // 剧集/专辑优先取所属剧集/专辑的图片，其次取媒体自身图片
                ImageType imageType = options.UseBackdropImage ? ImageType.Backdrop : ImageType.Primary;

                ApiImageInfo image = request.GetSeriesImageInfo(imageType)
                    ?? request.GetImageInfo(imageType);

                // 没有横版剧照时回退到海报
                if (image == null && options.UseBackdropImage)
                {
                    image = request.GetSeriesImageInfo(ImageType.Primary)
                        ?? request.GetImageInfo(ImageType.Primary);
                }

                if (image == null)
                {
                    return null;
                }

                ApiImageOptions imageOptions = new ApiImageOptions
                {
                    Format = "jpg",
                    MaxWidth = options.UseBackdropImage ? 1280 : 800
                };

                string url = await image.GetRemoteApiImageUrl(imageOptions, cancellationToken).ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(url) ? null : url;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[GotifyNotifier] 获取图片地址失败，将发送无图通知", ex);
                return null;
            }
        }

        /// <summary>
        /// 调用 Gotify REST API 发送消息（实际发送逻辑见 <see cref="GotifyClient"/>，
        /// 与配置页「发送测试通知」按钮共用）
        /// </summary>
        private Task<GotifySendResult> SendToGotifyAsync(
            PluginOptions options,
            string title,
            string message,
            string imageUrl,
            string clickUrl,
            CancellationToken cancellationToken)
        {
            return GotifyClient.SendAsync(
                _httpClient,
                _jsonSerializer,
                _logger,
                options,
                title,
                message,
                imageUrl,
                clickUrl,
                cancellationToken);
        }

        /// <summary>
        /// 释放合并定时器
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lock (_syncLock)
            {
                _flushTimer?.Dispose();
                _flushTimer = null;
            }
        }

        /// <summary>
        /// 待合并的入库通知条目
        /// </summary>
        private class PendingNotification
        {
            /// <summary>媒体 Id，用于合并窗口内去重</summary>
            public string ItemKey { get; set; }

            public string TitleLine { get; set; }

            /// <summary>入队时按模板渲染好的标题</summary>
            public string Title { get; set; }

            /// <summary>入队时按模板渲染好的正文</summary>
            public string Message { get; set; }

            /// <summary>模板变量值，合并多条目时用于补全标题中的变量</summary>
            public Dictionary<string, string> Values { get; set; }

            public string Overview { get; set; }

            public string ImageUrl { get; set; }
        }
    }
}
