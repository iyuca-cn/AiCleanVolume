using System;
using System.Collections.Generic;
using System.IO;
using AiCleanVolume.Core.Models;

namespace AiCleanVolume.Core.Services
{
    public sealed class DeletionSandbox : IDeletionSandbox
    {
        public SandboxEvaluation Evaluate(string path, SandboxSettings settings, bool processIsElevated)
        {
            if (settings == null) settings = new SandboxSettings();
            settings.EnsureDefaults();

            if (settings.FullyPrivilegedMode && processIsElevated)
            {
                return new SandboxEvaluation
                {
                    Action = SandboxAction.Bypass,
                    Message = "完全权限模式已启用，且当前进程已管理员运行。",
                    MatchedRoot = null
                };
            }

            string normalizedPath = Normalize(path);
            IList<string> roots = settings.AllowedRoots ?? new List<string>();
            for (int i = 0; i < roots.Count; i++)
            {
                string root = Normalize(roots[i]);
                if (string.IsNullOrWhiteSpace(root)) continue;
                if (IsSameOrChild(normalizedPath, root))
                {
                    return new SandboxEvaluation
                    {
                        Action = SandboxAction.Allow,
                        Message = "命中沙盒允许位置，可直接放行。",
                        MatchedRoot = root
                    };
                }
            }

            return new SandboxEvaluation
            {
                Action = SandboxAction.RequireConfirmation,
                Message = "未命中允许位置，删除前需要用户确认。",
                MatchedRoot = null
            };
        }

        private static bool IsSameOrChild(string path, string root)
        {
            if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) return true;
            if (!root.EndsWith("\\", StringComparison.Ordinal)) root += "\\";
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try
            {
                string full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
                return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }
    }
}
