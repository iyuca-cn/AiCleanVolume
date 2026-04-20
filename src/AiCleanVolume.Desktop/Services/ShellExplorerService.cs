using System;
using System.Diagnostics;
using System.IO;
using AiCleanVolume.Core.Services;

namespace AiCleanVolume.Desktop.Services
{
    public sealed class ShellExplorerService : IExplorerService
    {
        public void OpenPath(string path, bool selectItem)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            if (selectItem && File.Exists(path))
            {
                Process.Start("explorer.exe", "/select,\"" + path + "\"");
                return;
            }

            if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", "\"" + path + "\"");
                return;
            }

            string parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                Process.Start("explorer.exe", "\"" + parent + "\"");
            }
        }
    }
}
