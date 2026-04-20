using System;
using System.Collections.Generic;
using System.IO;
using AiCleanVolume.Core.Models;

namespace AiCleanVolume.Core.Services
{
    public sealed class CandidatePlanner
    {
        public IList<CleanupCandidate> BuildCandidates(StorageItem root, long minBytes, int maxCount)
        {
            List<CleanupCandidate> candidates = new List<CleanupCandidate>();
            if (root == null) return candidates;

            List<StorageItem> flattened = new List<StorageItem>();
            Flatten(root, flattened, true);
            flattened.Sort((left, right) => right.Bytes.CompareTo(left.Bytes));

            for (int i = 0; i < flattened.Count; i++)
            {
                StorageItem item = flattened[i];
                if (item.Bytes < minBytes) continue;
                if (IsCriticalPath(item.Path)) continue;

                string hint;
                CleanupRisk risk;
                if (!TryClassify(item, out hint, out risk)) continue;

                candidates.Add(new CleanupCandidate
                {
                    Path = item.Path,
                    Name = StorageFormatting.GetDisplayName(item.Path, item.IsDirectory),
                    Bytes = item.Bytes,
                    IsDirectory = item.IsDirectory,
                    ReasonHint = hint,
                    Risk = risk,
                    Source = "规则候选"
                });

                if (candidates.Count >= maxCount) break;
            }

            return candidates;
        }

        private static void Flatten(StorageItem item, IList<StorageItem> output, bool skipCurrent)
        {
            if (!skipCurrent) output.Add(item);
            for (int i = 0; i < item.Children.Count; i++) Flatten(item.Children[i], output, false);
        }

        private static bool TryClassify(StorageItem item, out string reason, out CleanupRisk risk)
        {
            string lower = (item.Path ?? string.Empty).ToLowerInvariant();
            string extension = item.IsDirectory ? string.Empty : Path.GetExtension(lower);

            if (ContainsAny(lower, "\\temp", "\\tmp", "\\cache", "\\caches", "\\logs", "\\log\\", "\\crash", "\\dump", "\\dumps", "\\shadercache", "\\webcache"))
            {
                reason = "路径包含缓存、临时、日志或转储特征，通常可再生成。";
                risk = CleanupRisk.Low;
                return true;
            }

            if (lower.Contains("\\softwaredistribution\\download") || lower.Contains("\\prefetch") || lower.Contains("$recycle.bin"))
            {
                reason = "属于常见 Windows 下载缓存、预读取缓存或回收站位置。";
                risk = CleanupRisk.Low;
                return true;
            }

            if (!item.IsDirectory && ContainsAny(extension, ".tmp", ".temp", ".dmp", ".dump", ".log", ".etl", ".old", ".bak"))
            {
                reason = "文件扩展名表现为临时文件、日志、转储或备份残留。";
                risk = CleanupRisk.Low;
                return true;
            }

            if (lower.Contains("\\downloads\\") || lower.Contains("\\desktop\\"))
            {
                reason = "位于用户可见目录，可能是大文件；需要用户确认用途后再删除。";
                risk = CleanupRisk.Medium;
                return true;
            }

            if (lower.Contains("\\appdata\\local\\") && ContainsAny(lower, "\\packages\\", "\\nuget\\", "\\npm-cache", "\\pip\\cache", "\\gradle\\caches"))
            {
                reason = "位于开发工具或应用本地缓存目录，通常可重新下载或生成。";
                risk = CleanupRisk.Medium;
                return true;
            }

            reason = null;
            risk = CleanupRisk.High;
            return false;
        }

        private static bool ContainsAny(string source, params string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (source.Contains(tokens[i])) return true;
            }
            return false;
        }

        private static bool IsCriticalPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return true;
            string lower = Normalize(path).ToLowerInvariant();
            string windows = Normalize(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).ToLowerInvariant();
            string programFiles = Normalize(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)).ToLowerInvariant();
            string programFilesX86 = Normalize(Environment.GetEnvironmentVariable("ProgramFiles(x86)")).ToLowerInvariant();
            string systemDrive = Normalize(Environment.GetEnvironmentVariable("SystemDrive") + "\\").ToLowerInvariant();

            if (lower == systemDrive) return true;
            if (lower == windows || lower == programFiles || lower == programFilesX86) return true;
            if (lower.Contains("\\windows\\system32") || lower.Contains("\\windows\\winsxs")) return true;
            if (lower.Contains("\\system volume information") || lower.Contains("\\recovery") || lower.EndsWith("\\boot", StringComparison.Ordinal)) return true;
            return false;
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try { return Path.GetFullPath(path).TrimEnd('\\', '/'); }
            catch { return path.Trim().TrimEnd('\\', '/'); }
        }
    }
}
