namespace Emby.Plugin.GotifyNotifications
{
    /// <summary>
    /// Gotify 发送结果。除了成功/失败，还带回 HTTP 状态码与错误详情，
    /// 便于配置页把真实原因展示给用户，而不是笼统的“发送失败”。
    /// </summary>
    public class GotifySendResult
    {
        /// <summary>是否成功送达（Gotify 返回 200）</summary>
        public bool Success { get; set; }

        /// <summary>HTTP 状态码；未发出请求时为 0</summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 失败原因（中文简述，可直接展示给用户）
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// 是否值得再试一次（5xx/429、连接类错误为 true；Token 无效、证书错误为 false）
        /// </summary>
        public bool Retryable { get; set; }

        public static GotifySendResult Ok(int statusCode)
        {
            return new GotifySendResult { Success = true, StatusCode = statusCode };
        }

        public static GotifySendResult Fail(int statusCode, string error, bool retryable = false)
        {
            return new GotifySendResult
            {
                Success = false,
                StatusCode = statusCode,
                Error = error,
                Retryable = retryable
            };
        }
    }
}
