using System;
using System.Collections.Generic;
using System.Globalization;

namespace Emby.Plugin.GotifyNotifications
{
    /// <summary>
    /// 最近通知尝试记录（内存环形缓冲，最多 30 条）。
    /// 用于配置页诊断：能直接看到"Emby 到底有没有把事件交给插件""发送成功还是失败"。
    /// </summary>
    public static class NotificationHistory
    {
        private const int MaxEntries = 30;

        private static readonly Queue<NotificationHistoryEntry> Entries =
            new Queue<NotificationHistoryEntry>();

        private static readonly object SyncLock = new object();

        /// <summary>记录一次通知处理</summary>
        public static void Add(string eventId, string title, bool success, string status, string detail)
        {
            NotificationHistoryEntry entry = new NotificationHistoryEntry
            {
                Time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                EventId = eventId ?? string.Empty,
                Title = title ?? string.Empty,
                Success = success,
                Status = status ?? string.Empty,
                Detail = detail ?? string.Empty
            };

            lock (SyncLock)
            {
                Entries.Enqueue(entry);
                while (Entries.Count > MaxEntries)
                {
                    Entries.Dequeue();
                }
            }
        }

        /// <summary>取出最近的记录（新的在前）</summary>
        public static List<NotificationHistoryEntry> Snapshot(int max)
        {
            lock (SyncLock)
            {
                List<NotificationHistoryEntry> list = new List<NotificationHistoryEntry>(Entries);
                list.Reverse();

                if (max > 0 && list.Count > max)
                {
                    list = list.GetRange(0, max);
                }

                return list;
            }
        }
    }

    /// <summary>
    /// 一条通知处理记录
    /// </summary>
    public class NotificationHistoryEntry
    {
        /// <summary>处理时间 HH:mm:ss</summary>
        public string Time { get; set; }

        /// <summary>事件 ID，如 library.new</summary>
        public string EventId { get; set; }

        /// <summary>通知标题</summary>
        public string Title { get; set; }

        /// <summary>是否成功（queued 也算已受理）</summary>
        public bool Success { get; set; }

        /// <summary>sent / failed / queued / skipped</summary>
        public string Status { get; set; }

        /// <summary>说明：成功/失败原因/被跳过</summary>
        public string Detail { get; set; }
    }
}
