using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Emby.Plugin.GotifyNotifications
{
    /// <summary>
    /// 按用户当前的标题/正文模板生成一条"示意通知"，
    /// 用于配置页的「发送测试通知」——手机上看到的排版与真实入库通知一致。
    /// </summary>
    public static class SampleNotification
    {
        private const string DefaultTitle = "\U0001F3AC 新片入库";

        /// <summary>
        /// 生成示例标题与正文
        /// </summary>
        public static void Build(PluginOptions options, string serverName, out string title, out string message)
        {
            options = options ?? new PluginOptions();

            Dictionary<string, string> values = BuildSampleValues(serverName);

            string titleTemplate = string.IsNullOrWhiteSpace(options.TitleTemplate)
                ? DefaultTitle
                : options.TitleTemplate;

            string messageTemplate = string.IsNullOrWhiteSpace(options.MessageTemplate)
                ? "{TitleLine}\n\n{Overview}"
                : options.MessageTemplate;

            title = ReplaceTokens(titleTemplate, values).Trim();
            message = CleanMessage(ReplaceTokens(messageTemplate, values));

            const string footer = "—— 测试消息（按当前模板生成）";
            message = string.IsNullOrEmpty(message) ? footer : message + "\n\n" + footer;
        }

        /// <summary>
        /// 示例数据：尽量贴近真实入库通知
        /// </summary>
        private static Dictionary<string, string> BuildSampleValues(string serverName)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["{TitleLine}"] = "《星际穿越》(2014)",
                ["{Name}"] = "星际穿越",
                ["{SeriesName}"] = "示例剧集",
                ["{Season}"] = "1",
                ["{Episode}"] = "2",
                ["{Year}"] = "2014",
                ["{Type}"] = "Movie",
                ["{TypeName}"] = "电影",
                ["{Overview}"] = "示例简介：这里会显示影片的剧情简介，用来预览通知正文的排版效果。",
                ["{Title}"] = "星际穿越 已添加到 " + (serverName ?? "Emby"),
                ["{ServerName}"] = serverName ?? string.Empty,
                ["{Url}"] = "https://emby.example.com/web/index.html#!/item?id=12345",
                ["{Event}"] = "library.new",
                ["{Date}"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                ["{Count}"] = "1"
            };
        }

        private static string ReplaceTokens(string template, Dictionary<string, string> values)
        {
            return TemplateRenderer.ReplaceTokens(template, values);
        }

        /// <summary>
        /// 去掉首尾空行，避免手机通知出现大片留白（与真实发送保持一致）
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
    }
}
