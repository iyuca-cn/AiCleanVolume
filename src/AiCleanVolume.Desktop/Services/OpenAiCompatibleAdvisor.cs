using System;
using System.Collections.Generic;
using System.Text;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;
using Newtonsoft.Json;
using RestSharp;

namespace AiCleanVolume.Desktop.Services
{
    public sealed class OpenAiCompatibleAdvisor : IAiCleanupAdvisor
    {
        private readonly IAiCleanupAdvisor fallback;

        public OpenAiCompatibleAdvisor(IAiCleanupAdvisor fallback)
        {
            this.fallback = fallback;
        }

        public IList<CleanupSuggestion> Analyze(StorageItem root, IList<CleanupCandidate> candidates, ApplicationSettings settings)
        {
            if (settings == null || settings.Ai == null || !settings.Ai.Enabled || string.IsNullOrWhiteSpace(settings.Ai.Endpoint) || string.IsNullOrWhiteSpace(settings.Ai.Model))
            {
                return fallback.Analyze(root, candidates, settings);
            }

            try
            {
                string prompt = BuildPrompt(root, candidates, settings.Ai.MaxSuggestions);
                RestClient client = new RestClient(settings.Ai.Endpoint.TrimEnd('/'));
                RestRequest request = new RestRequest("/v1/chat/completions", Method.POST);
                if (!string.IsNullOrWhiteSpace(settings.Ai.ApiKey)) request.AddHeader("Authorization", "Bearer " + settings.Ai.ApiKey);
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/json", JsonConvert.SerializeObject(new
                {
                    model = settings.Ai.Model,
                    temperature = 0.1,
                    messages = new object[]
                    {
                        new { role = "system", content = settings.Ai.SystemPrompt },
                        new { role = "user", content = prompt }
                    }
                }), ParameterType.RequestBody);

                IRestResponse response = client.Execute(request);
                if (response == null || response.ResponseStatus != ResponseStatus.Completed || (int)response.StatusCode >= 400)
                {
                    return fallback.Analyze(root, candidates, settings);
                }

                ChatCompletionResponse chat = JsonConvert.DeserializeObject<ChatCompletionResponse>(response.Content);
                if (chat == null || chat.choices == null || chat.choices.Count == 0 || chat.choices[0].message == null)
                {
                    return fallback.Analyze(root, candidates, settings);
                }

                string content = ExtractJson(chat.choices[0].message.content);
                AiSuggestionEnvelope envelope = JsonConvert.DeserializeObject<AiSuggestionEnvelope>(content);
                IList<CleanupSuggestion> mapped = MapSuggestions(envelope, candidates);
                return mapped.Count == 0 ? fallback.Analyze(root, candidates, settings) : mapped;
            }
            catch
            {
                return fallback.Analyze(root, candidates, settings);
            }
        }

        private static string BuildPrompt(StorageItem root, IList<CleanupCandidate> candidates, int maxSuggestions)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("请从候选清单中选择可以清理的文件或文件夹。");
            builder.AppendLine("规则：只返回候选清单里的 path；不要建议删除系统核心、用户文档、应用主体；风险高就不要选。");
            builder.AppendLine("输出 JSON 格式：{\"candidates\":[{\"path\":\"...\",\"risk\":\"Low|Medium|High\",\"score\":0.0,\"reason\":\"中文原因\"}]}");
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

        private static string ExtractJson(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return "{}";
            int start = content.IndexOf('{');
            int end = content.LastIndexOf('}');
            if (start >= 0 && end > start) return content.Substring(start, end - start + 1);
            return content;
        }

        private static IList<CleanupSuggestion> MapSuggestions(AiSuggestionEnvelope envelope, IList<CleanupCandidate> candidates)
        {
            List<CleanupSuggestion> result = new List<CleanupSuggestion>();
            if (envelope == null || envelope.candidates == null) return result;

            Dictionary<string, CleanupCandidate> map = new Dictionary<string, CleanupCandidate>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < candidates.Count; i++)
            {
                string key = Normalize(candidates[i].Path);
                if (!map.ContainsKey(key)) map.Add(key, candidates[i]);
            }

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

        private static CleanupRisk ParseRisk(string value, CleanupRisk fallbackRisk)
        {
            if (string.Equals(value, "Low", StringComparison.OrdinalIgnoreCase)) return CleanupRisk.Low;
            if (string.Equals(value, "Medium", StringComparison.OrdinalIgnoreCase)) return CleanupRisk.Medium;
            if (string.Equals(value, "High", StringComparison.OrdinalIgnoreCase)) return CleanupRisk.High;
            return fallbackRisk;
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
