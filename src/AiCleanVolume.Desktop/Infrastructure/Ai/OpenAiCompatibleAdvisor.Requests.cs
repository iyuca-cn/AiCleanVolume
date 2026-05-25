using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Domain.Sandbox;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Core.Application.CleanupPlanning;
using AiCleanVolume.Core.Application.Deletion;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Core.Kernel.Ports;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#if !NET40
using RestSharp;
#endif


namespace AiCleanVolume.Desktop.Infrastructure.Ai
{
    public sealed partial class OpenAiCompatibleAdvisor : IAiCleanupAdvisor
    {
        private void WriteLog(string message)
        {
            if (log != null) log(message);
        }

        private static bool AddAuthHeaders(IDictionary<string, string> headers, AiSettings settings, string accessMode, out string message)
        {
            if (string.Equals(accessMode, AiSettings.TwoApiAccessMode, StringComparison.OrdinalIgnoreCase))
            {
                string providerCookie = ResolveProviderCookie(settings);
                if (string.IsNullOrWhiteSpace(providerCookie))
                {
                    message = "2API Cookie 为空或未匹配当前模型。model=" + settings.Model + " mappingCount=" + (settings.ModelCookieMappings == null ? 0 : settings.ModelCookieMappings.Count);
                    return false;
                }
                headers["X-Provider-Cookie"] = providerCookie;
                headers["Cookie"] = providerCookie;
                message = "2API Cookie 已添加：" + MaskSecret(providerCookie) + "，长度 " + providerCookie.Length + "。";
                return true;
            }

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                headers["Authorization"] = "Bearer " + settings.ApiKey;
                message = "标准 API Key 已添加：" + MaskSecret(settings.ApiKey) + "。";
                return true;
            }

            message = "标准 API 模式未填写 API Key，将直接请求接口。若服务要求鉴权可能失败。";
            return true;
        }

        private static AiHttpResponse ExecuteJsonPost(string endpoint, string path, IDictionary<string, string> headers, string body)
        {
#if NET40
            return ExecuteJsonPostWithHttpWebRequest(endpoint, path, headers, body);
#else
            return ExecuteJsonPostWithRestSharp(endpoint, path, headers, body);
#endif
        }

#if NET40
        private static AiHttpResponse ExecuteJsonPostWithHttpWebRequest(string endpoint, string path, IDictionary<string, string> headers, string body)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint + path);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.UserAgent = "AiCleanVolume";

            ApplyHeaders(request, headers);

            byte[] bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
            request.ContentLength = bytes.Length;
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(bytes, 0, bytes.Length);
            }

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    return ReadHttpWebResponse(response, null);
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse errorResponse = ex.Response as HttpWebResponse;
                if (errorResponse == null)
                {
                    return new AiHttpResponse
                    {
                        ResponseStatus = ex.Status.ToString(),
                        ErrorMessage = ex.Message,
                        IsCompleted = false
                    };
                }

                using (errorResponse)
                {
                    return ReadHttpWebResponse(errorResponse, ex.Message);
                }
            }
        }

        private static void ApplyHeaders(HttpWebRequest request, IDictionary<string, string> headers)
        {
            if (headers == null) return;

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key) || header.Value == null) continue;

                if (string.Equals(header.Key, "Accept", StringComparison.OrdinalIgnoreCase))
                {
                    request.Accept = header.Value;
                    continue;
                }

                if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    request.ContentType = header.Value;
                    continue;
                }

                if (string.Equals(header.Key, "User-Agent", StringComparison.OrdinalIgnoreCase))
                {
                    request.UserAgent = header.Value;
                    continue;
                }

                if (string.Equals(header.Key, "Cookie", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers[HttpRequestHeader.Cookie] = header.Value;
                    continue;
                }

                request.Headers[header.Key] = header.Value;
            }
        }

        private static AiHttpResponse ReadHttpWebResponse(HttpWebResponse response, string errorMessage)
        {
            string content = string.Empty;
            using (Stream responseStream = response.GetResponseStream())
            {
                if (responseStream != null)
                {
                    using (StreamReader reader = new StreamReader(responseStream, Encoding.UTF8))
                    {
                        content = reader.ReadToEnd();
                    }
                }
            }

            return new AiHttpResponse
            {
                StatusCode = (int)response.StatusCode,
                StatusDescription = response.StatusDescription,
                ResponseStatus = "Completed",
                Content = content,
                ErrorMessage = errorMessage,
                IsCompleted = true
            };
        }
#else
        private static AiHttpResponse ExecuteJsonPostWithRestSharp(string endpoint, string path, IDictionary<string, string> headers, string body)
        {
            RestClient client = new RestClient(endpoint);
            RestRequest request = new RestRequest(path, Method.POST);
            request.AddHeader("Content-Type", "application/json");
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    if (string.IsNullOrWhiteSpace(header.Key) || header.Value == null) continue;
                    request.AddHeader(header.Key, header.Value);
                }
            }

            request.AddParameter("application/json", body, ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);
            if (response == null) return null;

            return new AiHttpResponse
            {
                StatusCode = (int)response.StatusCode,
                StatusDescription = response.StatusDescription,
                ResponseStatus = response.ResponseStatus.ToString(),
                Content = response.Content,
                ErrorMessage = response.ErrorMessage,
                IsCompleted = response.ResponseStatus == ResponseStatus.Completed
            };
        }
#endif

        private static string BuildResponseSummary(AiHttpResponse response, TimeSpan elapsed)
        {
            if (response == null) return " elapsed=" + elapsed.TotalMilliseconds.ToString("0") + "ms";
            string error = string.IsNullOrWhiteSpace(response.ErrorMessage) ? string.Empty : " error=" + response.ErrorMessage;
            return " status=" + response.StatusCode + " " + response.StatusDescription + " responseStatus=" + response.ResponseStatus + " elapsed=" + elapsed.TotalMilliseconds.ToString("0") + "ms contentChars=" + (response.Content == null ? 0 : response.Content.Length) + error + " contentPreview=" + Preview(response.Content, 500);
        }

        private static string MaskSecret(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "<empty>";
            string trimmed = value.Trim();
            if (trimmed.Length <= 8) return "***" + trimmed.Length + " chars";
            return trimmed.Substring(0, 4) + "..." + trimmed.Substring(trimmed.Length - 4) + " (" + trimmed.Length + " chars)";
        }

        private static string Preview(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string normalized = value.Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length <= maxLength) return normalized;
            return normalized.Substring(0, maxLength) + "...";
        }

        private static string BuildPrompt(StorageItem root, IList<CleanupCandidate> candidates, int maxSuggestions)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("请从候选清单中选择可以清理的文件或文件夹。");
            builder.AppendLine("规则：只返回候选清单里的 path；不要建议删除系统核心、用户文档、应用主体；风险高就不要选。");
            builder.AppendLine("输出严格 JSON 数组，格式示例：[\"C:\\\\path1\",\"C:\\\\path2\"]。");
            builder.AppendLine("最多返回 " + (maxSuggestions <= 0 ? 30 : maxSuggestions) + " 项。");
            if (root != null) builder.AppendLine("扫描根：" + root.Path + "，总大小：" + StorageFormatting.FormatBytes(root.Bytes));
            builder.AppendLine("候选：");
            for (int i = 0; i < candidates.Count; i++)
            {
                CleanupCandidate c = candidates[i];
                builder.Append(i + 1).Append(". ");
                builder.Append(c.IsDirectory ? "DIR" : "FILE").Append(" | ");
                builder.Append(StorageFormatting.FormatBytes(c.Bytes)).Append(" | ");
                builder.Append(c.Path).Append(" | ");
                builder.Append(c.ReasonHint).AppendLine();
            }
            return builder.ToString();
        }

        private static string ResolveChatCompletionsPath(string endpoint)
        {
            return endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? "/chat/completions" : "/v1/chat/completions";
        }

        private static string NormalizeEndpoint(string endpoint)
        {
            string normalized = (endpoint ?? string.Empty).Trim().TrimEnd('/');
            const string ChatCompletionsSuffix = "/chat/completions";
            if (normalized.EndsWith(ChatCompletionsSuffix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - ChatCompletionsSuffix.Length).TrimEnd('/');
            }

            return normalized;
        }
    }
}
