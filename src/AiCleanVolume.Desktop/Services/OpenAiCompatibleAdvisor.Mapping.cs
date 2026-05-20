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
    public sealed partial class OpenAiCompatibleAdvisor : IAiCleanupAdvisor
    {
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
    }
}
