using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugin.GotifyNotifications
{
    /// <summary>
    /// Gotify 消息发送客户端。供 <see cref="Notifier"/>（入库通知）
    /// 与 <see cref="GotifyNotifierService"/>（测试通知）共用。
    /// </summary>
    public static class GotifyClient
    {
        /// <summary>最多尝试次数（首次 + 1 次重试）</summary>
        private const int MaxAttempts = 2;

        /// <summary>重试间隔</summary>
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 调用 Gotify REST API 发送消息（失败时按需重试一次）
        /// POST {server}/message?token={token}
        /// </summary>
        public static async Task<GotifySendResult> SendAsync(
            IHttpClient httpClient,
            IJsonSerializer jsonSerializer,
            ILogger logger,
            PluginOptions options,
            string title,
            string message,
            string imageUrl,
            string clickUrl,
            CancellationToken cancellationToken)
        {
            if (options == null
                || string.IsNullOrWhiteSpace(options.ServerUrl)
                || string.IsNullOrWhiteSpace(options.Token))
            {
                logger.Warn("[GotifyNotifier] 未配置 Gotify 服务器地址或 Token，跳过发送。");
                return GotifySendResult.Fail(0, "未配置 Gotify 服务器地址或 Token。");
            }

            GotifySendResult result = null;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                result = await SendOnceAsync(
                    httpClient, jsonSerializer, logger, options, title, message, imageUrl, clickUrl, cancellationToken)
                    .ConfigureAwait(false);

                if (result.Success)
                {
                    if (attempt > 1)
                    {
                        logger.Info("[GotifyNotifier] 第 {0} 次重试后发送成功", attempt);
                    }

                    return result;
                }

                // 不可重试的错误（Token 无效、地址错误等）或已是最后一次，直接返回
                if (!result.Retryable || attempt >= MaxAttempts)
                {
                    return result;
                }

                logger.Warn(
                    "[GotifyNotifier] 第 {0} 次发送失败（{1}），{2} 秒后重试一次",
                    attempt,
                    result.Error,
                    (int)RetryDelay.TotalSeconds);

                try
                {
                    await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return result;
                }
            }

            return result;
        }

        /// <summary>
        /// 单次发送
        /// </summary>
        private static async Task<GotifySendResult> SendOnceAsync(
            IHttpClient httpClient,
            IJsonSerializer jsonSerializer,
            ILogger logger,
            PluginOptions options,
            string title,
            string message,
            string imageUrl,
            string clickUrl,
            CancellationToken cancellationToken)
        {
            string url = options.ServerUrl.TrimEnd('/') + "/message?token=" + Uri.EscapeDataString(options.Token.Trim());
            string json = BuildPayloadJson(jsonSerializer, options, title, message, imageUrl, clickUrl);

            // 注意：绝不能在日志中输出完整 URL，其中包含 Token
            logger.Debug("[GotifyNotifier] 正在发送到 {0}", MaskUrl(options.ServerUrl));

            try
            {
                if (options.DisableTlsValidation)
                {
                    // 用户勾选了跳过证书校验，使用自定义 HttpClient
                    return await PostWithCustomClientAsync(url, json, cancellationToken).ConfigureAwait(false);
                }

                HttpRequestOptions requestOptions = new HttpRequestOptions
                {
                    Url = url,
                    RequestContent = json.AsMemory(),
                    RequestContentType = "application/json",
                    CancellationToken = cancellationToken,
                    TimeoutMs = 15000,
                    // 关闭请求/响应日志，避免 Token 泄露到日志文件
                    LogRequest = false,
                    LogResponse = false,
                    LogErrors = false,
                    LogErrorResponseBody = false
                };

                using (HttpResponseInfo response = await httpClient.Post(requestOptions).ConfigureAwait(false))
                {
                    int statusCode = (int)response.StatusCode;

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        string body = ReadResponseSnippet(response);
                        logger.Error(
                            "[GotifyNotifier] Gotify 返回状态码 {0}（{1}）：{2}",
                            statusCode,
                            DescribeStatusCode(statusCode),
                            body);

                        return GotifySendResult.Fail(
                            statusCode,
                            "Gotify 返回 HTTP " + statusCode + "（" + DescribeStatusCode(statusCode) + "）"
                                + (string.IsNullOrEmpty(body) ? string.Empty : "：" + body),
                            IsRetryableStatus(statusCode));
                    }

                    return GotifySendResult.Ok(statusCode);
                }
            }
            catch (MediaBrowser.Model.Net.HttpException httpEx)
            {
                // Emby 的 HttpClient 在非 2xx 时会抛 HttpException（而不是返回 HttpResponseInfo）
                if (httpEx.IsTimedOut)
                {
                    logger.Error("[GotifyNotifier] 请求 Gotify 超时: {0}", MaskUrl(options.ServerUrl));
                    return GotifySendResult.Fail(0, "连接 Gotify 超时（15 秒），请检查服务器地址与网络。");
                }

                if (httpEx.StatusCode.HasValue)
                {
                    int statusCode = (int)httpEx.StatusCode.Value;
                    logger.Error(
                        "[GotifyNotifier] Gotify 返回状态码 {0}（{1}）",
                        statusCode,
                        DescribeStatusCode(statusCode));

                    return GotifySendResult.Fail(
                        statusCode,
                        "Gotify 返回 HTTP " + statusCode + "（" + DescribeStatusCode(statusCode) + "）",
                        IsRetryableStatus(statusCode));
                }

                logger.ErrorException("[GotifyNotifier] HTTP 请求异常: " + MaskUrl(options.ServerUrl), httpEx);
                return GotifySendResult.Fail(0, DescribeException(httpEx), IsRetryableException(httpEx));
            }
            catch (TaskCanceledException)
            {
                // 超时不重试：Emby 的 HttpClient 对同一地址有 30 秒冷却，
                // 立刻重试只会命中冷却错误，反而掩盖真实原因
                logger.Error("[GotifyNotifier] 请求 Gotify 超时: {0}", MaskUrl(options.ServerUrl));
                return GotifySendResult.Fail(0, "连接 Gotify 超时（15 秒），请检查服务器地址与网络。");
            }
            catch (Exception ex)
            {
                logger.ErrorException("[GotifyNotifier] HTTP 请求异常: " + MaskUrl(options.ServerUrl), ex);
                return GotifySendResult.Fail(0, DescribeException(ex), IsRetryableException(ex));
            }
        }

        /// <summary>
        /// 5xx / 429 / 408 属于可重试的服务端错误
        /// </summary>
        private static bool IsRetryableStatus(int statusCode)
        {
            return statusCode >= 500 || statusCode == 429 || statusCode == 408;
        }

        /// <summary>
        /// 证书错误、冷却期错误重试没有意义；连接类错误可以再试一次
        /// </summary>
        private static bool IsRetryableException(Exception ex)
        {
            string message = ex == null ? string.Empty : (ex.Message ?? string.Empty);

            if (message.IndexOf("previous timeout", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (message.IndexOf("certificate", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("SSL", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 把底层异常翻译成用户能看懂的说明
        /// </summary>
        private static string DescribeException(Exception ex)
        {
            string message = ex == null ? string.Empty : (ex.Message ?? string.Empty);

            // Emby 的 HttpClient 对同一地址有 30 秒冷却：上一次超时后，
            // 30 秒内的请求会直接抛 "Cancelling connection ... due to a previous timeout."
            if (message.IndexOf("previous timeout", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Emby 对同一地址有 30 秒冷却（上一次请求超时），请等待约 30 秒后重试；"
                    + "若反复出现，请确认 Emby 服务器能访问该地址。";
            }

            if (message.IndexOf("Connection refused", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("actively refused", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "无法连接 Gotify：目标端口拒绝连接。请确认地址与端口正确、Gotify 正在运行。";
            }

            if (message.IndexOf("Name or service not known", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("No such host", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "无法解析 Gotify 域名，请检查地址拼写与 Emby 服务器的 DNS。";
            }

            if (message.IndexOf("SSL", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("certificate", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "HTTPS 证书校验失败。若 Gotify 使用自签名证书，请勾选「跳过 HTTPS 证书校验」。";
            }

            return "无法连接 Gotify：" + message;
        }

        /// <summary>
        /// 读取响应体片段（用于把 Gotify 的错误说明带回配置页）
        /// </summary>
        private static string ReadResponseSnippet(HttpResponseInfo response)
        {
            try
            {
                if (response == null || response.Content == null)
                {
                    return string.Empty;
                }

                using (System.IO.Stream stream = response.Content)
                using (System.IO.StreamReader reader = new System.IO.StreamReader(stream, Encoding.UTF8))
                {
                    char[] buffer = new char[300];
                    int read = reader.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        return string.Empty;
                    }

                    string text = new string(buffer, 0, read).Trim();
                    return text.Replace("\r", " ").Replace("\n", " ");
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 常见状态码的中文说明
        /// </summary>
        private static string DescribeStatusCode(int statusCode)
        {
            switch (statusCode)
            {
                case 400:
                    return "请求格式错误";
                case 401:
                    return "Token 无效或已失效";
                case 403:
                    return "Token 无权发送消息";
                case 404:
                    return "接口不存在，请检查服务器地址是否正确";
                case 413:
                    return "消息内容过大";
                default:
                    return statusCode >= 500 ? "Gotify 服务器内部错误" : "未知错误";
            }
        }

        /// <summary>
        /// 使用自定义 HttpClient 发送（用于跳过 HTTPS 证书校验的场景）
        /// </summary>
        private static async Task<GotifySendResult> PostWithCustomClientAsync(string url, string json, CancellationToken cancellationToken)
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = delegate { return true; }
            };

            try
            {
                using (HttpClient client = new HttpClient(handler, true))
                using (HttpContent content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    using (HttpResponseMessage response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false))
                    {
                        int statusCode = (int)response.StatusCode;
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            return GotifySendResult.Ok(statusCode);
                        }

                        string body = string.Empty;
                        try
                        {
                            body = (await response.Content.ReadAsStringAsync().ConfigureAwait(false) ?? string.Empty).Trim();
                            if (body.Length > 300)
                            {
                                body = body.Substring(0, 300);
                            }

                            body = body.Replace("\r", " ").Replace("\n", " ");
                        }
                        catch
                        {
                        }

                        return GotifySendResult.Fail(
                            statusCode,
                            "Gotify 返回 HTTP " + statusCode + "（" + DescribeStatusCode(statusCode) + "）"
                                + (string.IsNullOrEmpty(body) ? string.Empty : "：" + body),
                            IsRetryableStatus(statusCode));
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return GotifySendResult.Fail(0, "连接 Gotify 超时（15 秒），请检查服务器地址与网络。");
            }
            catch (Exception ex)
            {
                return GotifySendResult.Fail(0, DescribeException(ex), IsRetryableException(ex));
            }
        }

        /// <summary>
        /// 构造 Gotify 消息 JSON
        /// </summary>
        private static string BuildPayloadJson(
            IJsonSerializer jsonSerializer,
            PluginOptions options,
            string title,
            string message,
            string imageUrl,
            string clickUrl)
        {
            int priority = options.Priority;
            if (priority < 0)
            {
                priority = 0;
            }
            else if (priority > 10)
            {
                priority = 10;
            }

            GotifyMessage payload = new GotifyMessage
            {
                Message = message,
                Title = title,
                Priority = priority
            };

            // Android 客户端扩展：点击跳转 + 大图
            Dictionary<string, object> notification = null;
            if (!string.IsNullOrEmpty(clickUrl))
            {
                if (notification == null)
                {
                    notification = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                }

                notification["click"] = new Dictionary<string, string> { ["url"] = clickUrl };
            }

            if (!string.IsNullOrEmpty(imageUrl))
            {
                if (notification == null)
                {
                    notification = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                }

                notification["bigImageUrl"] = imageUrl;
            }

            if (notification != null)
            {
                payload.Extras = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["client::notification"] = notification
                };
            }

            // Markdown 排版：声明内容类型，Gotify 客户端在应用内查看时按 Markdown 渲染。
            // 注意：手机系统通知栏始终显示纯文本，不会渲染 Markdown。
            if (options.UseMarkdown)
            {
                if (payload.Extras == null)
                {
                    payload.Extras = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                }

                payload.Extras["client::display"] = new Dictionary<string, string>
                {
                    ["contentType"] = "text/markdown"
                };
            }

            return jsonSerializer.SerializeToString(payload);
        }

        /// <summary>
        /// 日志脱敏：仅保留协议+主机，去掉路径与查询串
        /// </summary>
        public static string MaskUrl(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                return "(未配置)";
            }

            try
            {
                Uri uri = new Uri(serverUrl);
                return uri.GetLeftPart(UriPartial.Authority);
            }
            catch
            {
                return serverUrl;
            }
        }

        /// <summary>
        /// Gotify /message 接口的请求体
        /// </summary>
        private class GotifyMessage
        {
            public string Message { get; set; }

            public string Title { get; set; }

            public int Priority { get; set; }

            public Dictionary<string, object> Extras { get; set; }
        }
    }
}
