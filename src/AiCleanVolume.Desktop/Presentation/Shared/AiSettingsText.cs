using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using AiCleanVolume.Core.Domain.Settings;

namespace AiCleanVolume.Desktop.Presentation.Shared
{
    public static class AiSettingsText
    {
        public static string NormalizeValue(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        public static string NormalizeEndpoint(string endpoint)
        {
            string normalized = NormalizeValue(endpoint);
            if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;
            return normalized.TrimEnd('/');
        }

        public static int ParsePositiveInt(string text, int fallback)
        {
            int parsed;
            return int.TryParse(text, out parsed) && parsed > 0 ? parsed : fallback;
        }

        public static IList<string> ParseLines(string text)
        {
            List<string> result = new List<string>();
            string[] parts = (text ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string value = parts[i].Trim();
                if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
            }
            return result;
        }

        public static IList<AiModelCookieMapping> ParseModelCookieMappings(string text, string currentModel)
        {
            List<AiModelCookieMapping> mappings = new List<AiModelCookieMapping>();
            string[] parts = (text ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string line = parts[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                int separatorIndex = line.IndexOf('=');
                string model;
                string cookie;
                if (separatorIndex > 0 && separatorIndex < line.Length - 1 && LooksLikeModelCookieMapping(line, separatorIndex))
                {
                    model = line.Substring(0, separatorIndex).Trim();
                    cookie = line.Substring(separatorIndex + 1).Trim();
                }
                else
                {
                    model = currentModel;
                    cookie = line;
                }
                if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(cookie)) continue;

                mappings.Add(new AiModelCookieMapping
                {
                    Model = model,
                    Cookie = cookie
                });
            }

            return AiSettings.NormalizeModelCookieMappings(mappings);
        }

        public static string FormatModelCookieMappings(IEnumerable<AiModelCookieMapping> mappings, string currentModel)
        {
            IList<AiModelCookieMapping> normalized = AiSettings.NormalizeModelCookieMappings(mappings);
            string model = NormalizeValue(currentModel);
            if (!string.IsNullOrWhiteSpace(model))
            {
                for (int i = normalized.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(normalized[i].Model, model, StringComparison.OrdinalIgnoreCase)) return normalized[i].Cookie;
                }
            }

            List<string> lines = new List<string>();
            for (int i = 0; i < normalized.Count; i++)
            {
                lines.Add(normalized[i].Model + "=" + normalized[i].Cookie);
            }

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        public static bool IsConfigured(AiSettings ai)
        {
            return ai != null && !string.IsNullOrWhiteSpace(ai.Endpoint) && !string.IsNullOrWhiteSpace(ai.Model);
        }

        public static bool IsConfigured(AiProfile profile)
        {
            return profile != null && !string.IsNullOrWhiteSpace(profile.Endpoint) && !string.IsNullOrWhiteSpace(profile.Model);
        }

        public static string BuildProfileDisplayName(AiProfile profile)
        {
            if (profile == null) return string.Empty;
            string name = NormalizeValue(profile.Name);
            string endpoint = NormalizeEndpoint(profile.Endpoint);
            if (string.IsNullOrWhiteSpace(endpoint)) return name;

            Uri uri;
            string host = Uri.TryCreate(endpoint, UriKind.Absolute, out uri) ? uri.Host : endpoint;
            return string.IsNullOrWhiteSpace(host) ? name : name + " · " + host;
        }

        public static string FormatAccessModeLabel(string accessMode)
        {
            return string.Equals(AiSettings.NormalizeAccessMode(accessMode), AiSettings.TwoApiAccessMode, StringComparison.OrdinalIgnoreCase) ? "2API" : "标准 API";
        }

        public static string BuildProfileMeta(AiProfile profile)
        {
            if (profile == null) return string.Empty;
            string model = string.IsNullOrWhiteSpace(profile.Model) ? "未填写模型" : profile.Model.Trim();
            int maxSuggestions = profile.MaxSuggestions <= 0 ? 30 : profile.MaxSuggestions;
            return "模型：" + model + "    建议：" + maxSuggestions.ToString() + " 条    保存：" + profile.SavedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        public static string BuildProfileAvatarText(AiProfile profile)
        {
            string source = profile == null ? null : (string.IsNullOrWhiteSpace(profile.Name) ? profile.Model : profile.Name);
            source = NormalizeValue(source);
            if (string.IsNullOrWhiteSpace(source)) return "AI";

            string[] parts = Regex.Split(source, "[^A-Za-z0-9]+");
            string result = string.Empty;
            for (int i = 0; i < parts.Length && result.Length < 2; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) continue;
                result += char.ToUpperInvariant(parts[i][0]).ToString();
            }

            if (result.Length == 0)
            {
                result = source.Substring(0, Math.Min(2, source.Length)).ToUpperInvariant();
            }
            else if (result.Length == 1 && source.Length > 1)
            {
                result += char.ToUpperInvariant(source[1]).ToString();
            }

            return result.Length > 2 ? result.Substring(0, 2) : result;
        }

        private static bool LooksLikeModelCookieMapping(string line, int separatorIndex)
        {
            string left = line.Substring(0, separatorIndex).Trim();
            if (string.IsNullOrWhiteSpace(left)) return false;
            if (left.IndexOf(';') >= 0 || left.IndexOf(' ') >= 0 || left.IndexOf('\t') >= 0) return false;
            return left.IndexOf('/') >= 0 || left.IndexOf(':') >= 0 || left.IndexOf('.') >= 0 || left.StartsWith("gpt", StringComparison.OrdinalIgnoreCase) || left.StartsWith("claude", StringComparison.OrdinalIgnoreCase);
        }
    }
}
