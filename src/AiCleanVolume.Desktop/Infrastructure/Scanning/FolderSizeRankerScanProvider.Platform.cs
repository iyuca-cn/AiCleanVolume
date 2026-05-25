using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Domain.Sandbox;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Core.Application.CleanupPlanning;
using AiCleanVolume.Core.Application.Deletion;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Core.Kernel.Ports;
using Newtonsoft.Json;


namespace AiCleanVolume.Desktop.Infrastructure.Scanning
{
    public sealed partial class FolderSizeRankerScanProvider : IScanProvider
    {
        private static StorageItem TryScanWithPlatformApi(ScanRequest request, string cliError)
        {
            if (!Directory.Exists(request.Location)) return null;
            try
            {
                int depth = request.LoadDepth < 0 ? int.MaxValue : request.LoadDepth;
                DirectoryInfo root = new DirectoryInfo(request.Location);
                return ScanDirectory(root, request, true, depth);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("folder-size-ranker-cli 执行失败：" + cliError + Environment.NewLine + "平台 API 降级扫描也失败：" + ex.Message, ex);
            }
        }

        private static StorageItem ScanDirectory(DirectoryInfo directory, ScanRequest request, bool isRoot, int remainingDepth)
        {
            StorageItem item = new StorageItem();
            item.Path = directory.FullName;
            item.Name = isRoot ? directory.FullName : StorageFormatting.GetDisplayName(directory.FullName, true);
            item.IsDirectory = true;

            List<StorageItem> directFiles = remainingDepth > 0 ? new List<StorageItem>() : null;
            List<StorageItem> childDirectories = new List<StorageItem>();
            long totalBytes = 0;
            int directFileCount = 0;

            FileInfo[] files = SafeGetFiles(directory);
            for (int i = 0; i < files.Length; i++)
            {
                FileInfo file = files[i];
                if (IsReparsePoint(file)) continue;
                long fileBytes = SafeGetLength(file);
                totalBytes += fileBytes;
                if (fileBytes < request.MinSizeBytes) continue;
                directFileCount++;

                if (directFiles == null) continue;

                StorageItem child = new StorageItem();
                child.Path = file.FullName;
                child.Name = StorageFormatting.GetDisplayName(file.FullName, false);
                child.Bytes = fileBytes;
                child.IsDirectory = false;
                child.HasChildren = false;
                child.ChildrenLoaded = true;
                child.DirectFileCount = 0;
                child.TotalFileCount = 1;
                child.TotalDirectoryCount = 0;
                directFiles.Add(child);
            }

            DirectoryInfo[] directories = SafeGetDirectories(directory);
            for (int i = 0; i < directories.Length; i++)
            {
                DirectoryInfo childDirectory = directories[i];
                if (IsReparsePoint(childDirectory)) continue;
                StorageItem child = ScanDirectory(childDirectory, request, false, remainingDepth > 0 ? remainingDepth - 1 : 0);
                totalBytes += child.Bytes;
                if (child.Bytes >= request.MinSizeBytes) childDirectories.Add(child);
            }

            if (directFiles != null) directFiles.Sort(CompareByBytesDescending);
            childDirectories.Sort(CompareByBytesDescending);

            int limit = request.PerLevelLimit < 0 ? int.MaxValue : request.PerLevelLimit;
            int limitedFileCount = Math.Min(directFileCount, limit);
            int limitedDirectoryCount = Math.Min(childDirectories.Count, limit);

            if (directFiles != null) AddLimited(item.Children, directFiles, limit);
            if (remainingDepth > 0) AddLimited(item.Children, childDirectories, limit);

            item.Bytes = totalBytes;
            item.DirectFileCount = limitedFileCount;

            int totalFiles = limitedFileCount;
            int totalDirs = 0;
            for (int i = 0; i < limitedDirectoryCount; i++)
            {
                StorageItem child = childDirectories[i];
                totalFiles += child.TotalFileCount;
                totalDirs += 1 + child.TotalDirectoryCount;
            }

            item.TotalFileCount = totalFiles;
            item.TotalDirectoryCount = totalDirs;
            item.HasChildren = item.DirectFileCount > 0 || item.TotalDirectoryCount > 0;
            item.ChildrenLoaded = remainingDepth > 0;
            return item;
        }

        private static FileInfo[] SafeGetFiles(DirectoryInfo directory)
        {
            try { return directory.GetFiles(); }
            catch { return new FileInfo[0]; }
        }

        private static DirectoryInfo[] SafeGetDirectories(DirectoryInfo directory)
        {
            try { return directory.GetDirectories(); }
            catch { return new DirectoryInfo[0]; }
        }

        private static long SafeGetLength(FileInfo file)
        {
            try { return file.Length; }
            catch { return 0; }
        }

        private static bool IsReparsePoint(FileSystemInfo info)
        {
            try { return (info.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint; }
            catch { return true; }
        }

        private static int CompareByBytesDescending(StorageItem left, StorageItem right)
        {
            return right.Bytes.CompareTo(left.Bytes);
        }

        private static void AddLimited(IList<StorageItem> target, IList<StorageItem> source, int limit)
        {
            int count = Math.Min(source.Count, limit);
            for (int i = 0; i < count; i++) target.Add(source[i]);
        }
    }
}
