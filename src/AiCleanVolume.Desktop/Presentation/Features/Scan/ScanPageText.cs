using System;
using System.IO;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Desktop.Presentation.Shared;

namespace AiCleanVolume.Desktop.Presentation.Features.Scan
{
    public static class ScanPageText
    {
        private const int NtfsScanProgressIntervalMs = 20;

        private const int DefaultScanProgressIntervalMs = 200;

        public static string DescribeRequest(ScanRequest request)
        {
            if (request == null) return "<null>";
            return "location=" + request.Location +
                "，minSize=" + (request.MinSizeBytes < 0 ? "不限" : StorageFormatting.FormatBytes(request.MinSizeBytes)) +
                "，limit=" + request.PerLevelLimit +
                "，mode=" + DescribeSizeMode(request.SortMode) +
                "，loadDepth=" + request.LoadDepth +
                "，session=" + request.SessionIdentity + "/" + request.SessionNodeId;
        }

        public static string DescribeSizeMode(ScanSortMode mode)
        {
            return mode == ScanSortMode.Logical ? "实际大小" : "占用大小";
        }

        public static float CalculateActiveProgress(TimeSpan elapsed)
        {
            double seconds = Math.Max(0D, elapsed.TotalSeconds);
            double value = 0.05D + 0.90D * (1D - Math.Exp(-seconds / 8D));
            if (value < 0.05D) value = 0.05D;
            if (value > 0.95D) value = 0.95D;
            return (float)value;
        }

        public static string FormatElapsedSeconds(TimeSpan elapsed)
        {
            return elapsed.TotalSeconds.ToString("0.0") + " 秒";
        }

        public static int ResolveProgressInterval(string location)
        {
            DriveInfo drive = TryResolveDriveInfo(location);
            if (drive == null) return DefaultScanProgressIntervalMs;

            try
            {
                if (drive.IsReady && string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
                {
                    return NtfsScanProgressIntervalMs;
                }
            }
            catch
            {
            }

            return DefaultScanProgressIntervalMs;
        }

        public static string FormatBytesWithPercent(long bytes, long totalBytes)
        {
            if (totalBytes <= 0) return StorageFormatting.FormatBytes(bytes);
            double percent = (double)bytes / totalBytes * 100D;
            return StorageFormatting.FormatBytes(bytes) + " (" + percent.ToString("0.0") + "%)";
        }

        public static string BuildDriveDisplayText(DriveInfo drive, string location)
        {
            string root = drive != null ? drive.Name : DrivePathText.GetDriveRoot(location);
            if (string.IsNullOrWhiteSpace(root)) return "-";

            string volumeLabel = "本地磁盘";
            if (drive != null)
            {
                try
                {
                    if (drive.IsReady && !string.IsNullOrWhiteSpace(drive.VolumeLabel)) volumeLabel = drive.VolumeLabel;
                }
                catch
                {
                    volumeLabel = "本地磁盘";
                }
            }

            return "[" + root.TrimEnd('\\') + "] " + volumeLabel;
        }

        public static DriveInfo TryResolveDriveInfo(string location)
        {
            string root = DrivePathText.GetDriveRoot(location);
            if (string.IsNullOrWhiteSpace(root)) return null;

            DriveInfo[] drives;
            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch
            {
                return null;
            }

            for (int i = 0; i < drives.Length; i++)
            {
                if (string.Equals(drives[i].Name, root, StringComparison.OrdinalIgnoreCase)) return drives[i];
            }
            return null;
        }

        public static long ParseMinSizeBytes(string text, int fallbackMb)
        {
            int sizeMb = ParseInt(text, fallbackMb);
            return sizeMb < 0 ? -1L : sizeMb * 1024L * 1024L;
        }

        private static int ParseInt(string text, int fallback)
        {
            int parsed;
            return int.TryParse(text, out parsed) ? parsed : fallback;
        }
    }
}
