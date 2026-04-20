using System;
using System.Collections.Generic;

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
        public AiSettings()
        {
            Enabled = false;
            Endpoint = "https://api.openai.com";
            Model = "gpt-4o-mini";
            MaxSuggestions = 30;
            SystemPrompt = "你是 Windows C 盘清理助手。只建议删除可再生成的缓存、临时文件、日志、崩溃转储、安装残留。不要建议删除系统目录、用户文档、应用程序主体或不确定的数据。输出严格 JSON。";
        }

        public bool Enabled { get; set; }
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public string Model { get; set; }
        public int MaxSuggestions { get; set; }
        public string SystemPrompt { get; set; }

        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(Endpoint)) Endpoint = "https://api.openai.com";
            if (string.IsNullOrWhiteSpace(Model)) Model = "gpt-4o-mini";
            if (MaxSuggestions <= 0) MaxSuggestions = 30;
            if (string.IsNullOrWhiteSpace(SystemPrompt))
            {
                SystemPrompt = "你是 Windows C 盘清理助手。只建议删除可再生成的缓存、临时文件、日志、崩溃转储、安装残留。不要建议删除系统目录、用户文档、应用程序主体或不确定的数据。输出严格 JSON。";
            }
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
            AddIfMissing(Environment.GetEnvironmentVariable("TEMP"));
            AddIfMissing(Environment.GetEnvironmentVariable("TMP"));
            AddIfMissing(CombineSpecial(Environment.SpecialFolder.LocalApplicationData, "Temp"));
            AddIfMissing(CombineSpecial(Environment.SpecialFolder.LocalApplicationData, "Microsoft\\Windows\\INetCache"));
            AddIfMissing(CombineWindows("Temp"));
            AddIfMissing(CombineWindows("SoftwareDistribution\\Download"));
            AddIfMissing(CombineWindows("Prefetch"));
        }

        private void AddIfMissing(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            for (int i = 0; i < AllowedRoots.Count; i++)
            {
                if (string.Equals(AllowedRoots[i], value, StringComparison.OrdinalIgnoreCase)) return;
            }
            AllowedRoots.Add(value);
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
            MinSizeMb = 128;
            PerLevelLimit = 80;
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
