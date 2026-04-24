using System;
using System.Collections.Generic;
using System.IO;

namespace AiCleanVolume.Core.Models
{
    public sealed class ApplicationSettings
    {
        public ApplicationSettings()
        {
            Ai = new AiSettings();
            Sandbox = new SandboxSettings();
            Scan = new ScanSettings();
            Ui = new UiSettings();
        }

        public AiSettings Ai { get; set; }
        public SandboxSettings Sandbox { get; set; }
        public ScanSettings Scan { get; set; }
        public UiSettings Ui { get; set; }

        public void EnsureDefaults()
        {
            if (Ai == null) Ai = new AiSettings();
            if (Sandbox == null) Sandbox = new SandboxSettings();
            if (Scan == null) Scan = new ScanSettings();
            if (Ui == null) Ui = new UiSettings();
            Ai.EnsureDefaults();
            Sandbox.EnsureDefaults();
            Scan.EnsureDefaults();
            Ui.EnsureDefaults();
        }
    }

    public sealed class AiSettings
    {
        private const string LegacySystemPrompt = "你是 Windows C 盘清理助手。只建议删除可再生成的缓存、临时文件、日志、崩溃转储、安装残留。不要建议删除系统目录、用户文档、应用程序主体或不确定的数据。输出严格 JSON。";
        public const string DefaultSystemPrompt = "你是 Windows C 盘清理助手。请你只建议删除可再生成的缓存、临时文件、日志、崩溃转储、安装残留。不要建议删除系统目录、用户文档、应用程序主体或不确定的数据。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。";
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
            if (string.IsNullOrWhiteSpace(SystemPrompt) || string.Equals(SystemPrompt, LegacySystemPrompt, StringComparison.Ordinal))
            {
                SystemPrompt = DefaultSystemPrompt;
            }
            ModelCookieMappings = NormalizeModelCookieMappings(ModelCookieMappings);
            Profiles = NormalizeProfiles(Profiles);
        }

        public static IList<AiProfile> NormalizeProfiles(IEnumerable<AiProfile> profiles)
        {
            List<AiProfile> result = new List<AiProfile>();
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

                string fingerprint = normalized.BuildFingerprint();
                if (keys.Add(fingerprint)) result.Add(normalized);
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

    public sealed class AiModelCookieMapping
    {
        public string Model { get; set; }
        public string Cookie { get; set; }
    }

    public sealed class AiProfile
    {
        public AiProfile()
        {
            SavedAt = DateTime.Now;
            ModelCookieMappings = new List<AiModelCookieMapping>();
        }

        public string Name { get; set; }
        public DateTime SavedAt { get; set; }
        public string AccessMode { get; set; }
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public string Model { get; set; }
        public int MaxSuggestions { get; set; }
        public string SystemPrompt { get; set; }
        public IList<AiModelCookieMapping> ModelCookieMappings { get; set; }

        public AiProfile Clone()
        {
            AiProfile clone = new AiProfile
            {
                Name = Name,
                SavedAt = SavedAt,
                AccessMode = AccessMode,
                Endpoint = Endpoint,
                ApiKey = ApiKey,
                Model = Model,
                MaxSuggestions = MaxSuggestions,
                SystemPrompt = SystemPrompt,
                ModelCookieMappings = new List<AiModelCookieMapping>()
            };

            IList<AiModelCookieMapping> mappings = AiSettings.NormalizeModelCookieMappings(ModelCookieMappings);
            for (int i = 0; i < mappings.Count; i++)
            {
                clone.ModelCookieMappings.Add(new AiModelCookieMapping
                {
                    Model = mappings[i].Model,
                    Cookie = mappings[i].Cookie
                });
            }

            return clone;
        }

        public string BuildFingerprint()
        {
            List<string> parts = new List<string>();
            parts.Add(AiSettings.NormalizeAccessMode(AccessMode));
            parts.Add(NormalizeValue(Endpoint));
            parts.Add(NormalizeValue(ApiKey));
            parts.Add(NormalizeValue(Model));
            parts.Add(MaxSuggestions.ToString());
            parts.Add(NormalizeValue(SystemPrompt));

            IList<AiModelCookieMapping> mappings = AiSettings.NormalizeModelCookieMappings(ModelCookieMappings);
            for (int i = 0; i < mappings.Count; i++)
            {
                parts.Add(NormalizeValue(mappings[i].Model) + "=" + NormalizeValue(mappings[i].Cookie));
            }

            return string.Join("\n", parts.ToArray());
        }

        private static string NormalizeValue(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }

    public sealed class SandboxSettings
    {
        public SandboxSettings()
        {
            FullyPrivilegedMode = false;
            UseRecycleBin = true;
            AllowedRoots = new List<string>();
            EnsureDefaults();
        }

        public bool FullyPrivilegedMode { get; set; }
        public bool UseRecycleBin { get; set; }
        public IList<string> AllowedRoots { get; set; }

        public void EnsureDefaults()
        {
            if (AllowedRoots == null) AllowedRoots = new List<string>();
            AllowedRoots = NormalizeAllowedRoots(AllowedRoots);
            AddIfMissing(Environment.GetEnvironmentVariable("TEMP"));
            AddIfMissing(Environment.GetEnvironmentVariable("TMP"));
            AddIfMissing(CombineSpecial(Environment.SpecialFolder.LocalApplicationData, "Temp"));
            AddIfMissing(CombineSpecial(Environment.SpecialFolder.LocalApplicationData, "Microsoft\\Windows\\INetCache"));
            AddIfMissing(CombineWindows("Temp"));
            AddIfMissing(CombineWindows("SoftwareDistribution\\Download"));
            AddIfMissing(CombineWindows("Prefetch"));
            AllowedRoots = NormalizeAllowedRoots(AllowedRoots);
        }

        private void AddIfMissing(string value)
        {
            value = NormalizeAllowedRoot(value);
            if (string.IsNullOrWhiteSpace(value)) return;
            string key = NormalizeAllowedRootKey(value);
            for (int i = 0; i < AllowedRoots.Count; i++)
            {
                if (string.Equals(NormalizeAllowedRootKey(AllowedRoots[i]), key, StringComparison.OrdinalIgnoreCase)) return;
            }
            AllowedRoots.Add(value);
        }

        public static IList<string> NormalizeAllowedRoots(IEnumerable<string> values)
        {
            List<string> result = new List<string>();
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (values == null) return result;

            foreach (string item in values)
            {
                string normalized = NormalizeAllowedRoot(item);
                if (string.IsNullOrWhiteSpace(normalized)) continue;

                string key = NormalizeAllowedRootKey(normalized);
                if (keys.Add(key)) result.Add(normalized);
            }

            return result;
        }

        private static string NormalizeAllowedRoot(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            string normalized = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
            try
            {
                normalized = Path.GetFullPath(normalized);
            }
            catch
            {
            }

            normalized = normalized.Trim();
            if (normalized.Length > 3) normalized = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (normalized.Length == 2 && normalized[1] == ':') normalized = char.ToUpperInvariant(normalized[0]) + @":\";
            return normalized;
        }

        private static string NormalizeAllowedRootKey(string value)
        {
            string normalized = NormalizeAllowedRoot(value);
            if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;
            return normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string CombineSpecial(Environment.SpecialFolder folder, string relative)
        {
            string root = Environment.GetFolderPath(folder);
            return string.IsNullOrWhiteSpace(root) ? null : System.IO.Path.Combine(root, relative);
        }

        private static string CombineWindows(string relative)
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            return string.IsNullOrWhiteSpace(root) ? null : System.IO.Path.Combine(root, relative);
        }
    }

    public sealed class ScanSettings
    {
        public ScanSettings()
        {
            MinSizeMb = -1;
            PerLevelLimit = -1;
            SortMode = ScanSortMode.Allocated;
        }

        public int MinSizeMb { get; set; }
        public int PerLevelLimit { get; set; }
        public ScanSortMode SortMode { get; set; }

        public void EnsureDefaults()
        {
            if (!Enum.IsDefined(typeof(ScanSortMode), SortMode)) SortMode = ScanSortMode.Allocated;
        }
    }

    public sealed class UiSettings
    {
        public int SidebarWidth { get; set; }

        public void EnsureDefaults()
        {
            if (SidebarWidth < 0) SidebarWidth = 0;
        }
    }
}
