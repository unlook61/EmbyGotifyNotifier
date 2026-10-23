using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Emby.Plugin.GotifyNotifications
{
    /// <summary>
    /// 模板变量替换。统一了通知器与示意通知的行为：
    /// 变量名大小写不敏感（{Name} / {name} / {NAME} 等价），未知变量原样保留。
    /// </summary>
    public static class TemplateRenderer
    {
        private static readonly Regex TokenPattern = new Regex(@"\{[A-Za-z]+\}", RegexOptions.Compiled);

        /// <summary>
        /// 替换模板中的 {Token}
        /// </summary>
        public static string ReplaceTokens(string template, Dictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            if (values == null || values.Count == 0)
            {
                return template;
            }

            return TokenPattern.Replace(template, delegate(Match match)
            {
                string value;
                return values.TryGetValue(match.Value, out value) ? (value ?? string.Empty) : match.Value;
            });
        }
    }
}
