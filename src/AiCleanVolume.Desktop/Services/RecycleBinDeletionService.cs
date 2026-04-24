using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;

namespace AiCleanVolume.Desktop.Services
{
    public sealed class RecycleBinDeletionService : IDeletionService
    {
        public CleanupResult Delete(CleanupSuggestion suggestion, bool useRecycleBin)
        {
            CleanupResult result = new CleanupResult();
            result.Path = suggestion == null ? null : suggestion.Path;

            try
            {
                if (suggestion == null || string.IsNullOrWhiteSpace(suggestion.Path))
                {
                    result.Success = false;
                    result.Message = "删除目标为空。";
                    return result;
                }

                if (suggestion.IsDirectory)
                {
                    DeleteDirectoryByWinApi(suggestion.Path);
                    result.Success = true;
                    result.Message = "已通过 WinAPI 删除目录。";
                    return result;
                }

                DeleteFileByWinApi(suggestion.Path);
                result.Success = true;
                result.Message = "已通过 WinAPI 删除文件。";
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                result.Exception = ex;
                return result;
            }
        }

        private static void DeleteDirectoryByWinApi(string path)
        {
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException("目录不存在。" + path);

            File.SetAttributes(path, FileAttributes.Normal);
            string[] files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                DeleteFileByWinApi(files[i]);
            }

            string[] directories = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < directories.Length; i++)
            {
                DeleteDirectoryByWinApi(directories[i]);
            }

            File.SetAttributes(path, FileAttributes.Normal);
            if (!RemoveDirectory(path)) ThrowLastWin32Error("删除目录失败", path);
        }

        private static void DeleteFileByWinApi(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("文件不存在。", path);

            File.SetAttributes(path, FileAttributes.Normal);
            if (!DeleteFile(path)) ThrowLastWin32Error("删除文件失败", path);
        }

        private static void ThrowLastWin32Error(string action, string path)
        {
            int error = Marshal.GetLastWin32Error();
            throw new IOException(action + "：" + path + "。" + new Win32Exception(error).Message, error);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFile(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool RemoveDirectory(string lpPathName);
    }
}
