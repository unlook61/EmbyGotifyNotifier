using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Emby.Notifications;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Notifications;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

namespace Emby.Plugin.GotifyNotifications
{
    /// <summary>
    /// 读取插件配置
    /// GET /emby/GotifyNotifier/Configuration
    /// </summary>
    [Route("/GotifyNotifier/Configuration", "GET", Summary = "读取 Gotify 通知插件配置", IsHidden = true)]
    [Authenticated(Roles = "Admin")]
    public class GetConfigurationRequest : IReturn<ConfigurationResponse>
    {
    }

    /// <summary>
    /// 返回给配置页的配置视图。
    /// Token 只返回掩码，绝不返回明文。
    /// </summary>
    public class ConfigurationResponse
    {
        public string ServerUrl { get; set; }

        /// <summary>Token 掩码，如 A1b2********wxyz</summary>
        public string Token { get; set; }

        /// <summary>是否已保存过 Token（前端据此判断必填校验）</summary>
        public bool HasToken { get; set; }

        public int Priority { get; set; }

        public bool EnableImages { get; set; }

        public bool DisableTlsValidation { get; set; }

        public string TitleTemplate { get; set; }

        public string MessageTemplate { get; set; }

        public bool MergeLibraryNotifications { get; set; }

        public int MergeWindowSeconds { get; set; }

        public bool UseMarkdown { get; set; }

        public bool UseBackdropImage { get; set; }

        public string ExternalUrl { get; set; }

        public bool FilterEnabled { get; set; }

        public string IncludeItemTypes { get; set; }

        public string IncludeLibraryIds { get; set; }

        public string IncludeTags { get; set; }

        public int MinCommunityRating { get; set; }

        public int MinYear { get; set; }

        public string ExcludeKeywords { get; set; }

        public bool FollowOnly { get; set; }
    }

    /// <summary>
    /// 保存插件配置
    /// POST /emby/GotifyNotifier/Configuration
    /// </summary>
    [Route("/GotifyNotifier/Configuration", "POST", Summary = "保存 Gotify 通知插件配置", IsHidden = true)]
    [Authenticated(Roles = "Admin")]
    public class UpdateConfigurationRequest : IReturnVoid
    {
        /// <summary>要保存的配置</summary>
        public PluginOptions Options { get; set; }

        /// <summary>
        /// 为 true 时清空已保存的 Token（配置页的「清除」按钮）。
        /// 普通保存时 Token 提交的是掩码，表示"保持不变"。
        /// </summary>
        public bool ClearToken { get; set; }
    }

    /// <summary>
    /// 发送测试通知（用当前配置向 Gotify 推送一条测试消息）
    /// POST /emby/GotifyNotifier/Test
    /// </summary>
    [Route("/GotifyNotifier/Test", "POST", Summary = "发送 Gotify 测试通知", IsHidden = true)]
    [Authenticated(Roles = "Admin")]
    public class TestNotificationRequest : IReturn<TestNotificationResult>
    {
        /// <summary>
        /// 配置页当前表单里的配置。传入时优先使用它，
        /// 这样用户填完地址和 Token 可以直接测试，不必先保存。
        /// </summary>
        public PluginOptions Options { get; set; }
    }

    /// <summary>
    /// 测试发送结果
    /// </summary>
    public class TestNotificationResult
    {
        /// <summary>是否发送成功</summary>
        public bool Success { get; set; }

        /// <summary>给用户展示的结果说明</summary>
        public string Message { get; set; }

        /// <summary>技术细节（HTTP 状态码 / 错误原因）</summary>
        public string Detail { get; set; }

        /// <summary>实际使用的 Gotify 地址（已脱敏）</summary>
        public string Target { get; set; }
    }

    /// <summary>
    /// 读取诊断信息
    /// GET /emby/GotifyNotifier/Diagnostics
    /// </summary>
    [Route("/GotifyNotifier/Diagnostics", "GET", Summary = "读取 Gotify 通知诊断信息", IsHidden = true)]
    [Authenticated(Roles = "Admin")]
    public class GetDiagnosticsRequest : IReturn<DiagnosticsResponse>
    {
    }

    /// <summary>
    /// 模拟一次入库事件（走 Emby 通知管理器 → 用户通知配置 → 插件），用于排查真实入库不推送
    /// POST /emby/GotifyNotifier/SimulateIngest
    /// </summary>
    [Route("/GotifyNotifier/SimulateIngest", "POST", Summary = "模拟一次入库通知", IsHidden = true)]
    [Authenticated(Roles = "Admin")]
    public class SimulateIngestRequest : IReturn<TestNotificationResult>
    {
        /// <summary>可选：指定媒体 Id，不传则自动挑一个媒体项</summary>
        public string ItemId { get; set; }

        /// <summary>可选：要模拟的事件 Id，默认 library.new</summary>
        public string EventId { get; set; }

        /// <summary>可选：以哪个用户的身份模拟（播放/评分类事件需要）</summary>
        public string UserId { get; set; }
    }

    /// <summary>
    /// 诊断信息
    /// </summary>
    public class DiagnosticsResponse
    {
        public string PluginVersion { get; set; }

        /// <summary>插件通知器是否已被 Emby 注册</summary>
        public bool NotifierRegistered { get; set; }

        public string ServerUrl { get; set; }

        public bool HasToken { get; set; }

        public bool MergeLibraryNotifications { get; set; }

        public int MergeWindowSeconds { get; set; }

        /// <summary>启用了 Gotify 通知的用户及事件</summary>
        public List<UserNotificationStatus> Users { get; set; }

        /// <summary>最近的通知处理记录</summary>
        public List<NotificationHistoryEntry> Recent { get; set; }

        /// <summary>针对当前状态给出的排查建议</summary>
        public string Hint { get; set; }
    }

    /// <summary>
    /// 某个用户的 Gotify 通知配置
    /// </summary>
    public class UserNotificationStatus
    {
        public string Name { get; set; }

        public string UserId { get; set; }

        public bool Enabled { get; set; }

        public string[] Events { get; set; }

        public bool GroupItems { get; set; }
    }

    /// <summary>
    /// 插件 API 服务。经 <see cref="MediaBrowser.Model.Services.IService"/> 导出，
    /// 由 Emby HTTP 服务器自动注册为端点，供配置页前端调用。
    /// </summary>
    public class GotifyNotifierService : BaseApiService
    {
        private const string NotifierKey = "gotifynotifications";
        private const string LibraryNewEventId = "library.new";
        private const string UserNotificationConfigKey = "usernotifications";

        private readonly IApplicationHost _applicationHost;
        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger _logger;
        private readonly IUserManager _userManager;
        private readonly INotificationManager _notificationManager;
        private readonly ILibraryManager _libraryManager;

        public GotifyNotifierService(
            IApplicationHost applicationHost,
            IHttpClient httpClient,
            IJsonSerializer jsonSerializer,
            ILogManager logManager,
            IUserManager userManager,
            INotificationManager notificationManager,
            ILibraryManager libraryManager)
        {
            _applicationHost = applicationHost;
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
            _logger = logManager.GetLogger("GotifyNotifier");
            _userManager = userManager;
            _notificationManager = notificationManager;
            _libraryManager = libraryManager;
        }

        /// <summary>
        /// 取得当前配置（Token 打码）
        /// </summary>
        public ConfigurationResponse Get(GetConfigurationRequest request)
        {
            Plugin plugin = Plugin.GetPluginInstance(_applicationHost);
            if (plugin == null)
            {
                throw new InvalidOperationException("Gotify Notifications 插件实例未加载");
            }

            PluginOptions options = plugin.CurrentOptions;

            return new ConfigurationResponse
            {
                ServerUrl = options.ServerUrl,
                Token = TokenHelper.Mask(options.Token),
                HasToken = !string.IsNullOrWhiteSpace(options.Token),
                Priority = options.Priority,
                EnableImages = options.EnableImages,
                DisableTlsValidation = options.DisableTlsValidation,
                TitleTemplate = options.TitleTemplate,
                MessageTemplate = options.MessageTemplate,
                MergeLibraryNotifications = options.MergeLibraryNotifications,
                MergeWindowSeconds = options.MergeWindowSeconds,
                UseMarkdown = options.UseMarkdown,
                UseBackdropImage = options.UseBackdropImage,
                ExternalUrl = options.ExternalUrl,
                FilterEnabled = options.FilterEnabled,
                IncludeItemTypes = options.IncludeItemTypes,
                IncludeLibraryIds = options.IncludeLibraryIds,
                IncludeTags = options.IncludeTags,
                MinCommunityRating = options.MinCommunityRating,
                MinYear = options.MinYear,
                ExcludeKeywords = options.ExcludeKeywords,
                FollowOnly = options.FollowOnly
            };
        }

        /// <summary>
        /// 保存配置。Token 提交的是掩码时，保留已保存的真实 Token。
        /// </summary>
        public object Post(UpdateConfigurationRequest request)
        {
            Plugin plugin = Plugin.GetPluginInstance(_applicationHost);
            if (plugin == null)
            {
                throw new InvalidOperationException("Gotify Notifications 插件实例未加载");
            }

            PluginOptions current = plugin.CurrentOptions;
            PluginOptions options = request.Options ?? new PluginOptions();

            options.Token = request.ClearToken
                ? string.Empty
                : TokenHelper.Resolve(options.Token, current.Token);

            if (request.ClearToken)
            {
                _logger.Info("[GotifyNotifier] 已按请求清除保存的 Token");
            }

            plugin.SaveOptions(options);

            return new
            {
                Success = true,
                Token = TokenHelper.Mask(options.Token),
                HasToken = !string.IsNullOrWhiteSpace(options.Token)
            };
        }

        /// <summary>
        /// 发送一条测试通知，用于验证服务器地址与 Token 是否可用
        /// </summary>
        public async Task<object> Post(TestNotificationRequest request)
        {
            Plugin plugin = Plugin.GetPluginInstance(_applicationHost);
            if (plugin == null)
            {
                return new TestNotificationResult
                {
                    Success = false,
                    Message = "插件实例未加载"
                };
            }

            PluginOptions savedOptions = plugin.CurrentOptions;
            PluginOptions options = request != null && request.Options != null ? request.Options : savedOptions;
            bool usingSavedConfig = request == null || request.Options == null;
            bool usingSavedToken = options != null && TokenHelper.IsMasked(options.Token);

            // Token 提交的是掩码（用户没改）时，用已保存的真实 Token
            options.Token = TokenHelper.Resolve(options.Token, savedOptions.Token);

            if (string.IsNullOrWhiteSpace(options.ServerUrl))
            {
                options.ServerUrl = savedOptions.ServerUrl;
                usingSavedConfig = true;
            }

            string configSource = usingSavedConfig || usingSavedToken
                ? "；地址/Token 沿用已保存的配置"
                : "；使用页面当前填写内容";

            if (string.IsNullOrWhiteSpace(options.ServerUrl) || string.IsNullOrWhiteSpace(options.Token))
            {
                return new TestNotificationResult
                {
                    Success = false,
                    Message = "请先填写 Gotify 服务器地址和应用 Token。",
                    Detail = "服务器地址与 Token 均为必填项。"
                };
            }

            string serverUrl = options.ServerUrl.Trim();
            if (!serverUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !serverUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return new TestNotificationResult
                {
                    Success = false,
                    Message = "Gotify 服务器地址格式不正确。",
                    Detail = "地址需要以 http:// 或 https:// 开头，例如 https://push.example.com",
                    Target = GotifyClient.MaskUrl(serverUrl)
                };
            }

            string target = GotifyClient.MaskUrl(serverUrl);
            string serverName = _applicationHost != null ? _applicationHost.Name : string.Empty;

            // 按用户当前模板生成示意通知，手机上的排版与真实入库通知一致
            string title;
            string message;
            SampleNotification.Build(options, serverName, out title, out message);

            _logger.Info(
                "[GotifyNotifier] 正在发送示意通知到 {0}{1}，标题: {2}",
                target,
                configSource,
                title);

            GotifySendResult result = await GotifyClient.SendAsync(
                _httpClient,
                _jsonSerializer,
                _logger,
                options,
                title,
                message,
                null,
                null,
                System.Threading.CancellationToken.None).ConfigureAwait(false);

            if (result.Success)
            {
                _logger.Info("[GotifyNotifier] 测试通知发送成功: {0}", target);
                return new TestNotificationResult
                {
                    Success = true,
                    Message = "示意通知已发送，手机通知的排版与真实入库通知一致。",
                    Detail = "标题：" + title
                        + "\n目标: " + target + "（HTTP " + result.StatusCode + "）" + configSource,
                    Target = target
                };
            }

            _logger.Error("[GotifyNotifier] 测试通知发送失败: {0}", result.Error);
            return new TestNotificationResult
            {
                Success = false,
                Message = "发送失败：" + (result.Error ?? "未知错误"),
                Detail = "目标: " + target + (result.StatusCode > 0 ? "（HTTP " + result.StatusCode + "）" : string.Empty)
                    + "；详细日志见 Emby 控制台 > 日志。",
                Target = target
            };
        }

        /// <summary>
        /// 诊断：通知器是否注册、哪些用户启用了 Gotify、最近的处理记录
        /// </summary>
        public DiagnosticsResponse Get(GetDiagnosticsRequest request)
        {
            Plugin plugin = Plugin.GetPluginInstance(_applicationHost);
            PluginOptions options = plugin != null ? plugin.CurrentOptions : new PluginOptions();

            bool notifierRegistered = false;
            try
            {
                notifierRegistered = _applicationHost.GetExports<IUserNotifier>()
                    .Any(x => string.Equals(x.Key, NotifierKey, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[GotifyNotifier] 检查通知器注册状态失败", ex);
            }

            List<UserNotificationStatus> users = GetUserNotificationStatuses();

            string hint;
            if (!notifierRegistered)
            {
                hint = "插件通知器未注册：请确认 DLL 已放入插件目录并重启 Emby。";
            }
            else if (users.Count == 0)
            {
                hint = "没有任何用户启用 Gotify 通知。请到「设置 → 通知」新增一条通知，服务选择 Gotify，"
                    + "勾选「新媒体入库」，然后点页面底部的「保存」。这是最容易漏掉的一步。";
            }
            else if (!users.Any(u => u.Enabled))
            {
                hint = "已添加 Gotify 通知，但开关处于关闭状态，请在「设置 → 通知」中打开并保存。";
            }
            else if (!users.Any(u => u.Enabled && HasLibraryNew(u.Events)))
            {
                hint = "已启用 Gotify 通知，但没有勾选「新媒体入库（library.new）」事件。";
            }
            else if (string.IsNullOrWhiteSpace(options.ServerUrl) || string.IsNullOrWhiteSpace(options.Token))
            {
                hint = "通知已启用，但插件里还没填写服务器地址或 Token。";
            }
            else
            {
                hint = "配置看起来正常。可点「模拟一次入库」验证整条链路；若模拟能收到、真实入库收不到，"
                    + "通常是因为媒体未被判定为新增（文件早已入库、只是元数据更新，或添加时 Emby 未在运行）。";
            }

            return new DiagnosticsResponse
            {
                PluginVersion = plugin != null ? plugin.Version.ToString() : string.Empty,
                NotifierRegistered = notifierRegistered,
                ServerUrl = options.ServerUrl,
                HasToken = !string.IsNullOrWhiteSpace(options.Token),
                MergeLibraryNotifications = options.MergeLibraryNotifications,
                MergeWindowSeconds = options.MergeWindowSeconds,
                Users = users,
                Recent = NotificationHistory.Snapshot(15),
                Hint = hint
            };
        }

        /// <summary>
        /// 模拟一次真实入库：走 Emby 通知管理器 → 用户通知配置 → 插件发送，
        /// 与真实入库唯一的差别是事件来源。
        /// </summary>
        public TestNotificationResult Post(SimulateIngestRequest request)
        {
            Plugin plugin = Plugin.GetPluginInstance(_applicationHost);
            PluginOptions options = plugin != null ? plugin.CurrentOptions : new PluginOptions();

            string eventId = request != null && !string.IsNullOrWhiteSpace(request.EventId)
                ? request.EventId.Trim()
                : LibraryNewEventId;

            bool isLibraryNew = string.Equals(eventId, LibraryNewEventId, StringComparison.OrdinalIgnoreCase);

            if (isLibraryNew && !HasEnabledUserWithLibraryNew())
            {
                return new TestNotificationResult
                {
                    Success = false,
                    Message = "没有用户启用 Gotify 的「新媒体入库」通知，模拟不会产生推送。",
                    Detail = "请先到「设置 → 通知」添加 Gotify 通知并勾选「新媒体入库」后保存。"
                };
            }

            User simulatedUser = null;
            if (request != null && !string.IsNullOrWhiteSpace(request.UserId))
            {
                simulatedUser = _userManager.GetUserById(request.UserId);
            }

            BaseItem item = null;
            try
            {
                if (request != null && !string.IsNullOrWhiteSpace(request.ItemId))
                {
                    item = _libraryManager.GetItemById(request.ItemId);
                }

                if (item == null)
                {
                    BaseItem[] items = _libraryManager.GetItemList(new InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { "Movie", "Episode" },
                        Recursive = true,
                        Limit = 5
                    });

                    item = items != null ? items.FirstOrDefault() : null;
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[GotifyNotifier] 模拟入库时查找媒体失败", ex);
            }

            // 系统/任务类事件可以没有媒体项
            if (item == null && isLibraryNew)
            {
                return new TestNotificationResult
                {
                    Success = false,
                    Message = "媒体库里没有可用于模拟的影片或剧集。",
                    Detail = "请先入库至少一部影片/剧集，再试一次。"
                };
            }

            try
            {
                _notificationManager.SendNotification(new NotificationRequest
                {
                    EventId = eventId,
                    Item = item,
                    User = simulatedUser,
                    Title = item != null
                        ? item.GetStandaloneDisplayName() + " 已添加到服务器"
                        : eventId,
                    Description = item != null ? item.Overview : null,
                    Date = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[GotifyNotifier] 模拟入库触发失败", ex);
                return new TestNotificationResult
                {
                    Success = false,
                    Message = "触发模拟通知失败：" + ex.Message,
                    Detail = "详细堆栈见 Emby 控制台 > 日志。"
                };
            }

            string mergeNote = isLibraryNew && options.MergeLibraryNotifications
                ? "；合并开启，约 " + Math.Max(2, options.MergeWindowSeconds) + " 秒后发送"
                : "；已直接发送";

            string mediaText = item != null ? item.GetStandaloneDisplayName() : "（无媒体项）";
            string userText = simulatedUser != null ? "；用户：" + simulatedUser.Name : string.Empty;

            _logger.Info("[GotifyNotifier] 已触发模拟事件 {0}: {1}", eventId, mediaText);

            return new TestNotificationResult
            {
                Success = true,
                Message = "已通过 Emby 通知系统触发一次模拟事件。",
                Detail = "事件：" + eventId + "；媒体：" + mediaText + userText + mergeNote
                    + "。请查看下方「最近通知记录」确认结果。",
                Target = GotifyClient.MaskUrl(options.ServerUrl)
            };
        }

        /// <summary>
        /// 读取启用了 Gotify 通知的用户
        /// </summary>
        private List<UserNotificationStatus> GetUserNotificationStatuses()
        {
            List<UserNotificationStatus> result = new List<UserNotificationStatus>();

            try
            {
                long[] userIds = _userManager.GetUsersWithSettings(UserNotificationConfigKey);
                if (userIds == null)
                {
                    return result;
                }

                foreach (long userId in userIds)
                {
                    try
                    {
                        User user = _userManager.GetUserById(userId);
                        if (user == null)
                        {
                            continue;
                        }

                        // 用户通知配置的强类型定义在 Emby.Notifications 内部，
                        // 这里通过 JSON 中转成插件自己的轻量 DTO 读取
                        object rawOptions = _userManager.GetTypedUserSetting(userId, UserNotificationConfigKey);
                        if (rawOptions == null)
                        {
                            continue;
                        }

                        string json = _jsonSerializer.SerializeToString(rawOptions);
                        LocalUserNotificationOptions userOptions =
                            _jsonSerializer.DeserializeFromString<LocalUserNotificationOptions>(json);

                        if (userOptions == null || userOptions.Notifications == null)
                        {
                            continue;
                        }

                        foreach (LocalUserNotificationInfo notification in userOptions.Notifications)
                        {
                            if (notification == null
                                || !string.Equals(notification.NotifierKey, NotifierKey, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            result.Add(new UserNotificationStatus
                            {
                                Name = user.Name,
                                UserId = user.Id.ToString(),
                                Enabled = notification.Enabled,
                                Events = notification.EventIds ?? new string[0],
                                GroupItems = notification.GroupItems
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorException("[GotifyNotifier] 读取用户通知配置失败: " + userId, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[GotifyNotifier] 枚举用户通知配置失败", ex);
            }

            return result;
        }

        private bool HasEnabledUserWithLibraryNew()
        {
            return GetUserNotificationStatuses()
                .Any(u => u.Enabled && HasLibraryNew(u.Events));
        }

        private static bool HasLibraryNew(string[] events)
        {
            return events != null
                && events.Any(e => string.Equals(e, LibraryNewEventId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>用户通知配置的轻量映射（仅取插件关心的字段）</summary>
        private class LocalUserNotificationOptions
        {
            public LocalUserNotificationInfo[] Notifications { get; set; }
        }

        private class LocalUserNotificationInfo
        {
            public string NotifierKey { get; set; }

            public bool Enabled { get; set; }

            public string[] EventIds { get; set; }

            public bool GroupItems { get; set; }
        }
    }
}
