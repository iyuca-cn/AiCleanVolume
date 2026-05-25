using System;
using System.IO;

namespace AiCleanVolume.Desktop.Presentation.Shared
{
    public static class DrivePathText
    {
        public static string GetDriveRoot(string location)
        {
            if (string.IsNullOrWhiteSpace(location)) return null;
            string value = location.Trim();

            try
            {
                string root = Path.GetPathRoot(value);
                if (!string.IsNullOrWhiteSpace(root)) return root;
            }
            catch
            {
            }

            if (value.Length >= 2 && value[1] == ':')
            {
                return char.ToUpperInvariant(value[0]) + ":\\";
            }

            return null;
        }

        public static string FormatDriveLabel(string driveRoot)
        {
            string root = GetDriveRoot(driveRoot);
            if (string.IsNullOrWhiteSpace(root) || root.Length < 2) return "当前磁盘";
            return char.ToUpperInvariant(root[0]) + "盘";
        }

        public static string NormalizeDriveRootText(string driveRoot)
        {
            string root = GetDriveRoot(driveRoot);
            return string.IsNullOrWhiteSpace(root) ? "当前所选位置" : root;
        }
    }
}
