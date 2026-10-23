using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Notifications;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;

namespace Emby.Plugin.GotifyNotifications
{
    /// <summary>
    /// 推送过滤规则：媒体类型、媒体库、标签、评分、年份、关键字、追剧模式。
    /// 任何规则异常都按“放行”处理，避免因为过滤逻辑把通知全吞掉。
    /// </summary>
    public static class NotificationFilter
    {
        /// <summary>
        /// 判断该通知是否应该发送
        /// </summary>
        public static bool ShouldSend(
            InternalNotificationRequest request,
            PluginOptions options,
            ILibraryManager libraryManager,
            IUserManager userManager,
            IUserDataManager userDataManager,
            ILogger logger,
            out string reason)
        {
            reason = null;

            if (options == null || !options.FilterEnabled || request == null)
            {
                return true;
            }

            try
            {
                BaseItemDto item = request.Item;

                // 系统/计划任务类事件没有媒体项，不参与媒体过滤
                if (item == null)
                {
                    return true;
                }

                string type = item.Type ?? string.Empty;

                // 1) 媒体类型白名单
                List<string> types = SplitList(options.IncludeItemTypes);
                if (types.Count > 0 && !types.Any(t => string.Equals(t, type, StringComparison.OrdinalIgnoreCase)))
                {
                    reason = "媒体类型 " + type + " 不在白名单";
                    return false;
                }

                // 2) 媒体库白名单
                List<string> libraryIds = SplitList(options.IncludeLibraryIds);
                if (libraryIds.Count > 0 && !IsInLibraries(request, libraryIds, libraryManager))
                {
                    reason = "媒体库不在白名单";
                    return false;
                }

                // 3) 标签
                List<string> tags = SplitList(options.IncludeTags);
                if (tags.Count > 0 && !HasAnyTag(request, tags))
                {
                    reason = "没有匹配的标签";
                    return false;
                }

                // 4) 最低评分（未评分视为未知，不拦截）
                if (options.MinCommunityRating > 0
                    && item.CommunityRating.HasValue
                    && item.CommunityRating.Value < options.MinCommunityRating)
                {
                    reason = "评分 " + item.CommunityRating.Value.ToString("0.0") + " 低于 " + options.MinCommunityRating;
                    return false;
                }

                // 5) 最早年份（无年份视为未知，不拦截）
                if (options.MinYear > 0
                    && item.ProductionYear.HasValue
                    && item.ProductionYear.Value < options.MinYear)
                {
                    reason = "年份 " + item.ProductionYear.Value + " 早于 " + options.MinYear;
                    return false;
                }

                // 6) 忽略关键字
                List<string> keywords = SplitList(options.ExcludeKeywords);
                if (keywords.Count > 0)
                {
                    string hit = FindKeyword(request, keywords);
                    if (hit != null)
                    {
                        reason = "命中忽略关键字「" + hit + "」";
                        return false;
                    }
                }

                // 7) 追剧模式
                if (options.FollowOnly && !IsBeingFollowed(request, libraryManager, userManager, userDataManager, logger))
                {
                    reason = "追剧模式：尚无人观看该剧";
                    return false;
                }

                return true;
            }
            catch
            {
                // 过滤出错时不阻断通知
                return true;
            }
        }

        private static bool IsInLibraries(
            InternalNotificationRequest request,
            List<string> libraryIds,
            ILibraryManager libraryManager)
        {
            BaseItem original = request.OriginalItem;
            if (original == null || libraryManager == null)
            {
                return true;
            }

            Folder[] folders = libraryManager.GetCollectionFolders(original);
            if (folders == null || folders.Length == 0)
            {
                return true;
            }

            foreach (Folder folder in folders)
            {
                string id = folder.Id.ToString();
                foreach (string configured in libraryIds)
                {
                    if (string.Equals(NormalizeId(id), NormalizeId(configured), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string NormalizeId(string id)
        {
            return string.IsNullOrEmpty(id) ? string.Empty : id.Replace("-", string.Empty).Trim();
        }

        private static bool HasAnyTag(InternalNotificationRequest request, List<string> tags)
        {
            string[] itemTags = request.Item?.Tags;
            if (itemTags == null && request.OriginalItem != null)
            {
                itemTags = request.OriginalItem.Tags;
            }

            if (itemTags == null)
            {
                return false;
            }

            foreach (string tag in itemTags)
            {
                foreach (string wanted in tags)
                {
                    if (string.Equals(tag, wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string FindKeyword(InternalNotificationRequest request, List<string> keywords)
        {
            BaseItemDto item = request.Item;

            string[] fields =
            {
                item?.Name,
                item?.SeriesName,
                item?.OriginalTitle,
                item?.Overview
            };

            foreach (string keyword in keywords)
            {
                foreach (string field in fields)
                {
                    if (!string.IsNullOrEmpty(field)
                        && field.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return keyword;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 剧集是否“有人在追”：任一用户看过该剧（已看完或有播放进度）
        /// </summary>
        private static bool IsBeingFollowed(
            InternalNotificationRequest request,
            ILibraryManager libraryManager,
            IUserManager userManager,
            IUserDataManager userDataManager,
            ILogger logger)
        {
            BaseItem original = request.OriginalItem;
            if (original == null || libraryManager == null || userManager == null || userDataManager == null)
            {
                return true;
            }

            // 电影等没有剧集概念的内容不参与追剧过滤
            if (!string.Equals(request.Item?.Type, "Episode", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(request.Item?.Type, "Season", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            long seriesId = original.SeriesId;
            if (seriesId == 0)
            {
                return true;
            }

            BaseItem series = libraryManager.GetItemById(seriesId);
            if (series == null)
            {
                return true;
            }

            User[] users = userManager.GetUserList(new UserQuery());
            if (users == null)
            {
                return true;
            }

            bool watched = false;
            string watcher = null;

            foreach (User user in users)
            {
                // 1) 剧集本身被标记为已看
                UserItemData data = userDataManager.GetUserData(user, series);
                if (data != null && (data.Played || data.PlaybackPositionTicks > 0))
                {
                    watched = true;
                    watcher = user.Name;
                    break;
                }

                // 2) 看过任意一集（系列级的 Played 是聚合值，数据库里可能仍为 false）
                if (HasWatchedEpisode(series, user, libraryManager))
                {
                    watched = true;
                    watcher = user.Name;
                    break;
                }
            }

            if (logger != null)
            {
                logger.Info(
                    "[GotifyNotifier] 追剧判断: seriesId={0}, 用户数={1}, 已观看={2}{3}",
                    seriesId,
                    users.Length,
                    watched,
                    string.IsNullOrEmpty(watcher) ? string.Empty : "（" + watcher + "）");
            }

            return watched;
        }

        /// <summary>
        /// 该用户是否看过此剧的任意一集（已看完或看了一部分）
        /// </summary>
        private static bool HasWatchedEpisode(BaseItem series, User user, ILibraryManager libraryManager)
        {
            if (libraryManager == null || series == null || user == null)
            {
                return false;
            }

            BaseItem[] played = libraryManager.GetItemList(new InternalItemsQuery
            {
                AncestorIds = new[] { series.InternalId },
                IncludeItemTypes = new[] { "Episode" },
                Recursive = true,
                IsPlayed = true,
                User = user,
                Limit = 1
            });

            if (played != null && played.Length > 0)
            {
                return true;
            }

            BaseItem[] resumable = libraryManager.GetItemList(new InternalItemsQuery
            {
                AncestorIds = new[] { series.InternalId },
                IncludeItemTypes = new[] { "Episode" },
                Recursive = true,
                IsResumable = true,
                User = user,
                Limit = 1
            });

            return resumable != null && resumable.Length > 0;
        }

        /// <summary>
        /// 逗号分隔列表（同时支持中文逗号、分号）
        /// </summary>
        private static List<string> SplitList(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<string>();
            }

            return value
                .Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .ToList();
        }
    }
}
