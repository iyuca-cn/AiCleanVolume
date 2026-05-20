using System;
using System.Collections.Generic;
using System.IO;

namespace AiCleanVolume.Core.Models
{
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
}
