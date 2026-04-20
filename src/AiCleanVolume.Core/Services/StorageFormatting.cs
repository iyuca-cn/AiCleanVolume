using System;
using System.IO;

namespace AiCleanVolume.Core.Services
{
    public static class StorageFormatting
    {
        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return unit == 0 ? bytes + " B" : value.ToString("0.##") + " " + units[unit];
        }

        public static string GetDisplayName(string path, bool isDirectory)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (trimmed.Length <= 3 && trimmed.EndsWith(":", StringComparison.Ordinal)) return trimmed + "\\";
            string name = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name) ? path : name;
        }
    }
}
