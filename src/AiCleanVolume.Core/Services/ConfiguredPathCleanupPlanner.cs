using System;
using System.Collections.Generic;
using System.IO;
using AiCleanVolume.Core.Models;

namespace AiCleanVolume.Core.Services
{
    public sealed class ConfiguredPathCleanupPlanner
    {
        public IList<CleanupSuggestion> BuildSuggestions(ApplicationSettings settings, int maxCount)
        {
            List<CleanupSuggestion> suggestions = new List<CleanupSuggestion>();
            if (settings == null || settings.Sandbox == null) return suggestions;

            settings.Sandbox.EnsureDefaults();
            IList<string> roots = settings.Sandbox.AllowedRoots ?? new List<string>();
            HashSet<string> rootSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < roots.Count; i++)
            {
                string path = NormalizePath(roots[i]);
                if (string.IsNullOrWhiteSpace(path)) continue;
                if (!rootSeen.Add(path)) continue;

                bool isDirectory = Directory.Exists(path);
                bool isFile = File.Exists(path);
                if (!isDirectory && !isFile) continue;

                if (isFile)
                {
                    AddSuggestion(suggestions, seen, path, false, "来自内置或已配置的常规清理文件，可按配置直接清理。");
                    continue;
                }

                AddDirectoryContents(suggestions, seen, path);
            }

            suggestions.Sort((left, right) => right.Bytes.CompareTo(left.Bytes));
            if (maxCount > 0 && suggestions.Count > maxCount) suggestions.RemoveRange(maxCount, suggestions.Count - maxCount);
            return suggestions;
        }

        private static void AddDirectoryContents(List<CleanupSuggestion> suggestions, HashSet<string> seen, string root)
        {
            string[] files = SafeGetFiles(root);
            for (int i = 0; i < files.Length; i++)
            {
                AddSuggestion(suggestions, seen, files[i], false, "来自内置或已配置的常规清理路径下的文件。");
            }

            string[] directories = SafeGetDirectories(root);
            for (int i = 0; i < directories.Length; i++)
            {
                AddSuggestion(suggestions, seen, directories[i], true, "来自内置或已配置的常规清理路径下的文件夹。");
            }
        }

        private static void AddSuggestion(List<CleanupSuggestion> suggestions, HashSet<string> seen, string path, bool isDirectory, string reason)
        {
            path = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(path) || !seen.Add(path)) return;

            suggestions.Add(new CleanupSuggestion
            {
                Path = path,
                Name = StorageFormatting.GetDisplayName(path, isDirectory),
                Bytes = isDirectory ? GetDirectoryBytes(path) : GetFileBytes(path),
                IsDirectory = isDirectory,
                Risk = CleanupRisk.Low,
                Score = 0.9,
                Reason = reason,
                Source = "常规路径配置",
                Selected = true
            });
        }

        private static long GetFileBytes(string path)
        {
            try
            {
                return new FileInfo(path).Length;
            }
            catch
            {
                return 0;
            }
        }

        private static long GetDirectoryBytes(string path)
        {
            long total = 0;
            Stack<string> pending = new Stack<string>();
            pending.Push(path);

            while (pending.Count > 0)
            {
                string current = pending.Pop();

                string[] files = SafeGetFiles(current);
                for (int i = 0; i < files.Length; i++) total += GetFileBytes(files[i]);

                string[] directories = SafeGetDirectories(current);
                for (int i = 0; i < directories.Length; i++) pending.Push(directories[i]);
            }

            return total;
        }

        private static string[] SafeGetFiles(string path)
        {
            try
            {
                return Directory.GetFiles(path);
            }
            catch
            {
                return new string[0];
            }
        }

        private static string[] SafeGetDirectories(string path)
        {
            try
            {
                return Directory.GetDirectories(path);
            }
            catch
            {
                return new string[0];
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'))).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim().Trim('"').TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }
    }
}
