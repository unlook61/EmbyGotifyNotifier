using System;

namespace Emby.Plugin.GotifyNotifications
{
    /// <summary>
    /// Token 掩码与回填处理。
    /// 配置页永远不返回明文 Token：GET 只返回掩码（如 <c>A1b2****wxyz</c>），
    /// 前端把掩码原样提交回来时，服务端保留已保存的真实 Token。
    /// </summary>
    public static class TokenHelper
    {
        /// <summary>掩码字符</summary>
        public const char MaskChar = '*';

        /// <summary>
        /// 生成掩码：保留前 4 位与后 4 位，中间统一用 * 填充。
        /// 过短的 Token 全部打码，避免泄露。
        /// </summary>
        public static string Mask(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            string value = token.Trim();

            if (value.Length <= 12)
            {
                return new string(MaskChar, Math.Max(6, value.Length));
            }

            string head = value.Substring(0, 4);
            string tail = value.Substring(value.Length - 4);
            return head + new string(MaskChar, 8) + tail;
        }

        /// <summary>
        /// 是否为掩码值（前端未修改 Token 时会把掩码原样提交）
        /// </summary>
        public static bool IsMasked(string token)
        {
            return !string.IsNullOrEmpty(token) && token.IndexOf(MaskChar) >= 0;
        }

        /// <summary>
        /// 解析前端提交的 Token：掩码或空值时沿用已保存的 Token。
        /// </summary>
        public static string Resolve(string submittedToken, string savedToken)
        {
            if (string.IsNullOrWhiteSpace(submittedToken) || IsMasked(submittedToken))
            {
                return savedToken ?? string.Empty;
            }

            return submittedToken.Trim();
        }
    }
}
