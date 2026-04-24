using System;
using System.Collections.Generic;
using System.Text;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace AiCleanVolume.Desktop.Services
{
    public sealed class OpenAiCompatibleAdvisor : IAiCleanupAdvisor
    {
        private readonly IAiCleanupAdvisor fallback;
        private readonly Action<string> log;

        public OpenAiCompatibleAdvisor(IAiCleanupAdvisor fallback)
            : this(fallback, null)
        {
        }

        public OpenAiCompatibleAdvisor(IAiCleanupAdvisor fallback, Action<string> log)
        {
            this.fallback = fallback;
            this.log = log;
        }

        public IList<CleanupSuggestion> Analyze(StorageItem root, IList<CleanupCandidate> candidates, ApplicationSettings settings)
        {
            if (settings == null || settings.Ai == null || !settings.Ai.Enabled || string.IsNullOrWhiteSpace(settings.Ai.Endpoint) || string.IsNullOrWhiteSpace(settings.Ai.Model))
            {
                WriteLog("AI 未启用或配置不完整，使用本地规则。Enabled=" + (settings != null && settings.Ai != null && settings.Ai.Enabled) + " EndpointEmpty=" + (settings == null || settings.Ai == null || string.IsNullOrWhiteSpace(settings.Ai.Endpoint)) + " ModelEmpty=" + (settings == null || settings.Ai == null || string.IsNullOrWhiteSpace(settings.Ai.Model)));
                return fallback.Analyze(root, candidates, settings);
            }

            try
            {
                string prompt = BuildPrompt(root, candidates, settings.Ai.MaxSuggestions);
                string endpoint = NormalizeEndpoint(settings.Ai.Endpoint);
                string accessMode = AiSettings.NormalizeAccessMode(settings.Ai.AccessMode);
                string path = ResolveChatCompletionsPath(endpoint);
                WriteLog("AI 请求准备：mode=" + accessMode + " endpoint=" + endpoint + " path=" + path + " model=" + settings.Ai.Model + " candidates=" + (candidates == null ? 0 : candidates.Count) + " promptChars=" + prompt.Length + " maxSuggestions=" + settings.Ai.MaxSuggestions);
                RestClient client = new RestClient(endpoint);
                RestRequest request = new RestRequest(path, Method.POST);
                request.AddHeader("Content-Type", "application/json");
                if (string.Equals(accessMode, AiSettings.TwoApiAccessMode, StringComparison.OrdinalIgnoreCase))
                {
                    string providerCookie = ResolveProviderCookie(settings.Ai);
                    if (string.IsNullOrWhiteSpace(providerCookie))
                    {
                        WriteLog("2API Cookie 为空或未匹配当前模型，使用本地规则。model=" + settings.Ai.Model + " mappingCount=" + (settings.Ai.ModelCookieMappings == null ? 0 : settings.Ai.ModelCookieMappings.Count));
                        return fallback.Analyze(root, candidates, settings);
                    }
                    request.AddHeader("X-Provider-Cookie", providerCookie);
                    request.AddHeader("Cookie", providerCookie);
                    WriteLog("2API Cookie 已添加：" + MaskSecret(providerCookie) + "，长度 " + providerCookie.Length + "。");
                }
                else if (!string.IsNullOrWhiteSpace(settings.Ai.ApiKey))
                {
                    request.AddHeader("Authorization", "Bearer " + settings.Ai.ApiKey);
                    WriteLog("标准 API Key 已添加：" + MaskSecret(settings.Ai.ApiKey) + "。");
                }
                else
                {
                    WriteLog("标准 API 模式未填写 API Key，将直接请求接口。若服务要求鉴权可能失败。");
                }
                string body = JsonConvert.SerializeObject(new
                {
                    model = settings.Ai.Model,
                    temperature = 0.1,
                    messages = new object[]
                    {
                        new { role = "system", content = settings.Ai.SystemPrompt },
                        new { role = "user", content = prompt }
                    }
                });
                request.AddParameter("application/json", body, ParameterType.RequestBody);
                WriteLog("AI 请求发送：POST " + endpoint + path + " bodyChars=" + body.Length + "。");

                DateTime startedAt = DateTime.UtcNow;
                IRestResponse response = client.Execute(request);
                TimeSpan elapsed = DateTime.UtcNow - startedAt;
                if (response == null || response.ResponseStatus != ResponseStatus.Completed || (int)response.StatusCode >= 400)
                {
                    WriteLog("AI 请求失败，使用本地规则。responseNull=" + (response == null) + BuildResponseSummary(response, elapsed));
                    return fallback.Analyze(root, candidates, settings);
                }
                WriteLog("AI 请求成功：" + BuildResponseSummary(response, elapsed));

                ChatCompletionResponse chat = JsonConvert.DeserializeObject<ChatCompletionResponse>(response.Content);
                if (chat == null || chat.choices == null || chat.choices.Count == 0 || chat.choices[0].message == null)
                {
                    WriteLog("AI 响应结构无效，使用本地规则。contentPreview=" + Preview(response.Content, 500));
                    return fallback.Analyze(root, candidates, settings);
                }

                string content = ExtractJson(chat.choices[0].message.content);
                IList<CleanupSuggestion> mapped = MapSuggestions(content, candidates);
                WriteLog("AI 响应解析完成：contentChars=" + (chat.choices[0].message.content == null ? 0 : chat.choices[0].message.content.Length) + " jsonChars=" + content.Length + " mapped=" + mapped.Count + "。");
                if (mapped.Count == 0)
                {
                    WriteLog("AI 没有映射到候选路径，使用本地规则。jsonPreview=" + Preview(content, 500));
                    return fallback.Analyze(root, candidates, settings);
                }
                return mapped;
            }
            catch (Exception ex)
            {
                WriteLog("AI 调用异常，使用本地规则：" + ex.GetType().Name + " " + ex.Message);
                return fallback.Analyze(root, candidates, settings);
            }
        }

        private void WriteLog(string message)
        {
            if (log != null) log(message);
        }

        private static string BuildResponseSummary(IRestResponse response, TimeSpan elapsed)
        {
            if (response == null) return " elapsed=" + elapsed.TotalMilliseconds.ToString("0") + "ms";
            string error = string.IsNullOrWhiteSpace(response.ErrorMessage) ? string.Empty : " error=" + response.ErrorMessage;
            return " status=" + (int)response.StatusCode + " " + response.StatusDescription + " responseStatus=" + response.ResponseStatus + " elapsed=" + elapsed.TotalMilliseconds.ToString("0") + "ms contentChars=" + (response.Content == null ? 0 : response.Content.Length) + error + " contentPreview=" + Preview(response.Content, 500);
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

        private static string ExtractJson(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return "[]";
            content = content.Trim();
            if (content.StartsWith("```", StringComparison.Ordinal))
            {
                int firstNewLine = content.IndexOf('\n');
                if (firstNewLine >= 0) content = content.Substring(firstNewLine + 1).Trim();
                if (content.EndsWith("```", StringComparison.Ordinal)) content = content.Substring(0, content.Length - 3).Trim();
            }

            int arrayStart = content.IndexOf('[');
            int arrayEnd = content.LastIndexOf(']');
            int start = content.IndexOf('{');
            int end = content.LastIndexOf('}');
            if (start >= 0 && end > start && (arrayStart < 0 || start < arrayStart)) return content.Substring(start, end - start + 1);
            if (arrayStart >= 0 && arrayEnd > arrayStart) return content.Substring(arrayStart, arrayEnd - arrayStart + 1);
            if (start >= 0 && end > start) return content.Substring(start, end - start + 1);
            return content;
        }

        private static IList<CleanupSuggestion> MapSuggestions(string content, IList<CleanupCandidate> candidates)
        {
            List<CleanupSuggestion> result = new List<CleanupSuggestion>();
            if (string.IsNullOrWhiteSpace(content) || candidates == null) return result;

            Dictionary<string, CleanupCandidate> map = new Dictionary<string, CleanupCandidate>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < candidates.Count; i++)
            {
                string key = Normalize(candidates[i].Path);
                if (!map.ContainsKey(key)) map.Add(key, candidates[i]);
            }

            JToken token = JToken.Parse(content);
            if (token.Type == JTokenType.Array)
            {
                return MapSuggestions((JArray)token, map);
            }

            AiSuggestionEnvelope envelope = token.Type == JTokenType.Object ? token.ToObject<AiSuggestionEnvelope>() : null;
            if (envelope == null || envelope.candidates == null) return result;

            for (int i = 0; i < envelope.candidates.Count; i++)
            {
                AiSuggestionDto dto = envelope.candidates[i];
                CleanupCandidate candidate;
                if (dto == null || !map.TryGetValue(Normalize(dto.path), out candidate)) continue;
                CleanupRisk risk = ParseRisk(dto.risk, candidate.Risk);
                if (risk == CleanupRisk.High) continue;

                result.Add(new CleanupSuggestion
                {
                    Path = candidate.Path,
                    Name = candidate.Name,
                    Bytes = candidate.Bytes,
                    IsDirectory = candidate.IsDirectory,
                    Risk = risk,
                    Score = dto.score,
                    Reason = string.IsNullOrWhiteSpace(dto.reason) ? candidate.ReasonHint : dto.reason,
                    Source = "AI 判断",
                    Selected = true
                });
            }

            return result;
        }

        private static IList<CleanupSuggestion> MapSuggestions(JArray paths, IDictionary<string, CleanupCandidate> map)
        {
            List<CleanupSuggestion> result = new List<CleanupSuggestion>();
            if (paths == null || map == null) return result;

            for (int i = 0; i < paths.Count; i++)
            {
                JToken token = paths[i];
                if (token == null || token.Type != JTokenType.String) continue;

                CleanupCandidate candidate;
                if (!map.TryGetValue(Normalize(token.ToString()), out candidate)) continue;
                if (candidate.Risk == CleanupRisk.High) continue;

                result.Add(new CleanupSuggestion
                {
                    Path = candidate.Path,
                    Name = candidate.Name,
                    Bytes = candidate.Bytes,
                    IsDirectory = candidate.IsDirectory,
                    Risk = candidate.Risk,
                    Score = candidate.Risk == CleanupRisk.Low ? 0.9 : 0.65,
                    Reason = candidate.ReasonHint,
                    Source = "AI 判断",
                    Selected = true
                });
            }

            return result;
        }

        private static string ResolveProviderCookie(AiSettings settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.Model) || settings.ModelCookieMappings == null) return null;

            string currentModel = NormalizeValue(settings.Model);
            for (int i = 0; i < settings.ModelCookieMappings.Count; i++)
            {
                AiModelCookieMapping mapping = settings.ModelCookieMappings[i];
                if (mapping == null) continue;
                if (!string.Equals(NormalizeValue(mapping.Model), currentModel, StringComparison.OrdinalIgnoreCase)) continue;

                string cookie = NormalizeValue(mapping.Cookie);
                return string.IsNullOrWhiteSpace(cookie) ? null : cookie;
            }

            return null;
        }

        private static CleanupRisk ParseRisk(string value, CleanupRisk fallbackRisk)
        {
            if (string.Equals(value, "Low", StringComparison.OrdinalIgnoreCase)) return CleanupRisk.Low;
            if (string.Equals(value, "Medium", StringComparison.OrdinalIgnoreCase)) return CleanupRisk.Medium;
            if (string.Equals(value, "High", StringComparison.OrdinalIgnoreCase)) return CleanupRisk.High;
            return fallbackRisk;
        }

        private static string NormalizeValue(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Trim().TrimEnd('\\', '/');
        }

        private sealed class ChatCompletionResponse
        {
            public List<ChatChoice> choices { get; set; }
        }

        private sealed class ChatChoice
        {
            public ChatMessage message { get; set; }
        }

        private sealed class ChatMessage
        {
            public string content { get; set; }
        }

        private sealed class AiSuggestionEnvelope
        {
            public List<AiSuggestionDto> candidates { get; set; }
        }

        private sealed class AiSuggestionDto
        {
            public string path { get; set; }
            public string risk { get; set; }
            public double score { get; set; }
            public string reason { get; set; }
        }
    }
}
