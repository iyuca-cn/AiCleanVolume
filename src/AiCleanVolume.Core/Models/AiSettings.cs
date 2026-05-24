using System;
using System.Collections.Generic;

namespace AiCleanVolume.Core.Models
{
    public sealed class AiSettings
    {
        private const string LegacySystemPrompt = "你是 Windows C 盘清理助手。只建议删除可再生成的缓存、临时文件、日志、崩溃转储、安装残留。不要建议删除系统目录、用户文档、应用程序主体或不确定的数据。输出严格 JSON。";
        private const string PreviousDefaultSystemPrompt = "你是 Windows C 盘清理助手。请你只建议删除可再生成的缓存、临时文件、日志、崩溃转储、安装残留。不要建议删除系统目录、用户文档、应用程序主体或不确定的数据。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。";
        public const string DefaultSystemPrompt = "你是 Windows 磁盘清理审核助手。只允许从候选清单中选择明确可再生成、删除后可自动恢复或可重新下载的缓存、临时文件、日志、崩溃转储和安装残留。禁止选择系统核心目录、应用程序主体、用户文档、桌面、下载目录中的个人文件、源码、配置、数据库、证书、密钥、同步盘和任何用途不确定的数据。风险不确定时不要选择。只输出严格 JSON 字符串数组，例如[\"path1\",\"path2\"]，数组元素必须完全等于候选 path，不要输出解释、Markdown 或额外字段。";
        public const string StandardApiAccessMode = "standard_api";
        public const string TwoApiAccessMode = "two_api";
        public const string DefaultModel = "gpt-5.4";

        public AiSettings()
        {
            Enabled = false;
            AccessMode = StandardApiAccessMode;
            Endpoint = "https://api.openai.com";
            Model = DefaultModel;
            MaxSuggestions = 30;
            SystemPrompt = DefaultSystemPrompt;
            ModelCookieMappings = new List<AiModelCookieMapping>();
            Profiles = new List<AiProfile>();
        }

        public bool Enabled { get; set; }
        public string AccessMode { get; set; }
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public string Model { get; set; }
        public int MaxSuggestions { get; set; }
        public string SystemPrompt { get; set; }
        public IList<AiModelCookieMapping> ModelCookieMappings { get; set; }
        public IList<AiProfile> Profiles { get; set; }

        public void EnsureDefaults()
        {
            AccessMode = NormalizeAccessMode(AccessMode);
            if (string.IsNullOrWhiteSpace(Endpoint)) Endpoint = "https://api.openai.com";
            if (string.IsNullOrWhiteSpace(Model)) Model = DefaultModel;
            if (MaxSuggestions <= 0) MaxSuggestions = 30;
            if (string.IsNullOrWhiteSpace(SystemPrompt) ||
                string.Equals(SystemPrompt, LegacySystemPrompt, StringComparison.Ordinal) ||
                string.Equals(SystemPrompt, PreviousDefaultSystemPrompt, StringComparison.Ordinal))
            {
                SystemPrompt = DefaultSystemPrompt;
            }
            ModelCookieMappings = NormalizeModelCookieMappings(ModelCookieMappings);
            Profiles = NormalizeProfiles(Profiles);
        }

        public static IList<AiProfile> NormalizeProfiles(IEnumerable<AiProfile> profiles)
        {
            List<AiProfile> result = new List<AiProfile>();
            if (profiles == null) return result;

            foreach (AiProfile profile in profiles)
            {
                if (profile == null) continue;
                AiProfile normalized = profile.Clone();
                normalized.Name = NormalizeValue(normalized.Name);
                normalized.AccessMode = NormalizeAccessMode(normalized.AccessMode);
                normalized.Endpoint = NormalizeValue(normalized.Endpoint);
                normalized.ApiKey = NormalizeValue(normalized.ApiKey);
                normalized.Model = NormalizeValue(normalized.Model);
                normalized.SystemPrompt = NormalizeValue(normalized.SystemPrompt);
                normalized.ModelCookieMappings = NormalizeModelCookieMappings(normalized.ModelCookieMappings);
                if (string.IsNullOrWhiteSpace(normalized.Name)) normalized.Name = BuildProfileAutoName(normalized.Model, normalized.SavedAt);
                if (string.IsNullOrWhiteSpace(normalized.Endpoint)) normalized.Endpoint = "https://api.openai.com";
                if (string.IsNullOrWhiteSpace(normalized.Model)) normalized.Model = DefaultModel;
                if (normalized.MaxSuggestions <= 0) normalized.MaxSuggestions = 30;
                if (string.IsNullOrWhiteSpace(normalized.SystemPrompt)) normalized.SystemPrompt = DefaultSystemPrompt;

                result.Add(normalized);
            }

            return result;
        }

        public static string BuildProfileAutoName(string model, DateTime savedAt)
        {
            string normalizedModel = NormalizeValue(model);
            if (string.IsNullOrWhiteSpace(normalizedModel)) normalizedModel = "未填写模型";
            return normalizedModel + " · " + savedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        public static string NormalizeAccessMode(string value)
        {
            string normalized = NormalizeValue(value);
            if (string.Equals(normalized, TwoApiAccessMode, StringComparison.OrdinalIgnoreCase)) return TwoApiAccessMode;
            return StandardApiAccessMode;
        }

        public static IList<AiModelCookieMapping> NormalizeModelCookieMappings(IEnumerable<AiModelCookieMapping> mappings)
        {
            List<AiModelCookieMapping> result = new List<AiModelCookieMapping>();
            Dictionary<string, int> indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (mappings == null) return result;

            foreach (AiModelCookieMapping mapping in mappings)
            {
                if (mapping == null) continue;
                string model = NormalizeValue(mapping.Model);
                string cookie = NormalizeValue(mapping.Cookie);
                if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(cookie)) continue;

                AiModelCookieMapping normalized = new AiModelCookieMapping
                {
                    Model = model,
                    Cookie = cookie
                };

                int existingIndex;
                if (indexes.TryGetValue(model, out existingIndex))
                {
                    result[existingIndex] = normalized;
                }
                else
                {
                    indexes.Add(model, result.Count);
                    result.Add(normalized);
                }
            }

            return result;
        }

        private static string NormalizeValue(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
