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
        private static string CombinePath(string parent, string name)
        {
            if (string.IsNullOrEmpty(parent)) return name ?? string.Empty;
            if (string.IsNullOrEmpty(name)) return parent;

            try
            {
                return Path.Combine(parent, name);
            }
            catch
            {
                return parent.TrimEnd('\\', '/') + "\\" + name;
            }
        }

        private static string NormalizeLocation(string location)
        {
            string value = Environment.ExpandEnvironmentVariables((location ?? string.Empty).Trim().Trim('"'));
            if (value.Length == 2 && value[1] == ':') return char.ToUpperInvariant(value[0]) + @":\";
            try
            {
                return Path.GetFullPath(value);
            }
            catch
            {
                return value;
            }
        }

        private static string NormalizePathKey(string path)
        {
            string value = (path ?? string.Empty).Trim().Trim('"');
            if (value.Length == 2 && value[1] == ':') return char.ToUpperInvariant(value[0]) + @":\";
            return value.TrimEnd('\\', '/');
        }

        private static bool IsSamePath(string left, string right)
        {
            return string.Equals(NormalizePathKey(left), NormalizePathKey(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildTreeTemplateKey(ScanRequest request)
        {
            return string.Join("|",
                request.SortMode.ToString(),
                request.MinSizeBytes.ToString(),
                request.PerLevelLimit.ToString());
        }

        private static bool IsCompatibleTreeSession(ScanSession session, string templateKey, ScanRequest request)
        {
            if (session == null) return false;
            if (!string.Equals(session.TemplateKey, templateKey, StringComparison.Ordinal)) return false;
            if (!string.IsNullOrWhiteSpace(request.SessionIdentity))
            {
                return string.Equals(session.SessionIdentity, request.SessionIdentity, StringComparison.Ordinal);
            }

            return IsSamePath(session.RootPath, request.Location);
        }
    }
}
