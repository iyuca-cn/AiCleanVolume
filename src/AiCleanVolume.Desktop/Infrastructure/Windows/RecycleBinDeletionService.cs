using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Domain.Sandbox;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Core.Application.CleanupPlanning;
using AiCleanVolume.Core.Application.Deletion;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Core.Kernel.Ports;

namespace AiCleanVolume.Desktop.Infrastructure.Windows
{
    public sealed class RecycleBinDeletionService : IDeletionService
    {
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x400;
        private const uint INVALID_FILE_ATTRIBUTES = 0xFFFFFFFF;

        private const int ERROR_FILE_NOT_FOUND = 2;
        private const int ERROR_PATH_NOT_FOUND = 3;
        private const int ERROR_ACCESS_DENIED = 5;
        private const int ERROR_NO_MORE_FILES = 18;
        private const int ERROR_SHARING_VIOLATION = 32;
        private const int ERROR_LOCK_VIOLATION = 33;
        private const int ERROR_DIR_NOT_EMPTY = 145;

        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        public CleanupResult Delete(CleanupSuggestion suggestion, bool useRecycleBin, DeletionProgressState progress)
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

                string path = NormalizeInputPath(suggestion.Path);
                UpdateProgress(progress, path);

                if (suggestion.IsDirectory)
                {
                    DeletionTally tally = new DeletionTally();
                    DeleteDirectoryByWinApi(path, tally, progress);
                    if (tally.FailedCount == 0)
                    {
                        result.Success = true;
                        result.Message = "已删除文件夹（文件 " + tally.DeletedFiles + " 个，子目录 " + tally.DeletedDirectories + " 个）。";
                    }
                    else
                    {
                        result.Success = false;
                        int done = tally.DeletedFiles + tally.DeletedDirectories;
                        result.Message = "部分删除：已删除 " + done + " 项，" + tally.FailedCount + " 项无法删除（其余能删的已删除）。"
                            + Environment.NewLine + tally.FirstFailureMessage;
                    }
                    return result;
                }

                DeleteFileByWinApi(path);
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

        // 尽力删除（best-effort）：遇到删不掉的子项时记录并继续删除其余项，而不是中断整个删除。
        private static void DeleteDirectoryByWinApi(string path, DeletionTally tally, DeletionProgressState progress)
        {
            Stack<DirectoryDeleteFrame> pending = new Stack<DirectoryDeleteFrame>();
            pending.Push(new DirectoryDeleteFrame(path, false));

            while (pending.Count > 0)
            {
                DirectoryDeleteFrame frame = pending.Pop();
                UpdateProgress(progress, frame.Path);

                if (frame.RemoveSelf)
                {
                    try { RemoveDirectoryByWinApi(frame.Path); tally.DeletedDirectories++; }
                    catch (Exception ex)
                    {
                        // 若已有子项删除失败，则本目录非空属必然结果，残留项已计入，不再重复计数；
                        // 否则说明是目录自身被占用，记录该错误。
                        if (tally.FailedCount == 0) tally.Record(ex.Message);
                    }
                    continue;
                }

                string currentPath = frame.Path;
                string extended = ToExtendedPath(currentPath);
                uint attributes = GetFileAttributesW(extended);
                if (attributes == INVALID_FILE_ATTRIBUTES)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND) continue;
                    tally.Record(FriendlyMessage(true, currentPath, error));
                    continue;
                }

                // 重解析点（目录联接 / 符号链接）：只删除链接本身，绝不进入目标，避免误删链接指向的真实数据。
                if ((attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
                {
                    try { RemoveDirectoryByWinApi(currentPath); tally.DeletedDirectories++; }
                    catch (Exception ex) { tally.Record(ex.Message); }
                    continue;
                }

                if ((attributes & FILE_ATTRIBUTE_DIRECTORY) == 0)
                {
                    try { DeleteFileByWinApi(currentPath); tally.DeletedFiles++; }
                    catch (Exception ex) { tally.Record(ex.Message); }
                    continue;
                }

                SetFileAttributesW(extended, FILE_ATTRIBUTE_NORMAL);

                WIN32_FIND_DATA find;
                IntPtr handle = FindFirstFileW(extended + "\\*", out find);
                List<string> childDirectories = new List<string>();
                bool canRemoveSelf = true;
                if (handle == InvalidHandle)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != ERROR_FILE_NOT_FOUND && error != ERROR_NO_MORE_FILES && error != ERROR_PATH_NOT_FOUND)
                    {
                        tally.Record(FriendlyMessage(true, currentPath, error));
                        canRemoveSelf = false;
                    }
                }
                else
                {
                    try
                    {
                        do
                        {
                            string name = find.cFileName;
                            if (name == "." || name == "..") continue;

                            string childPath = currentPath + "\\" + name;
                            if ((find.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
                            {
                                childDirectories.Add(childPath);
                            }
                            else
                            {
                                UpdateProgress(progress, childPath);
                                try { DeleteFileByWinApi(childPath); tally.DeletedFiles++; }
                                catch (Exception ex) { tally.Record(ex.Message); }
                            }
                        }
                        while (FindNextFileW(handle, out find));
                    }
                    finally
                    {
                        FindClose(handle);
                    }
                }

                if (!canRemoveSelf) continue;

                pending.Push(new DirectoryDeleteFrame(currentPath, true));
                for (int i = 0; i < childDirectories.Count; i++)
                {
                    pending.Push(new DirectoryDeleteFrame(childDirectories[i], false));
                }
            }
        }

        private static void UpdateProgress(DeletionProgressState progress, string path)
        {
            if (progress != null) progress.Update("正在删除", path);
        }

        private sealed class DirectoryDeleteFrame
        {
            public DirectoryDeleteFrame(string path, bool removeSelf)
            {
                Path = path;
                RemoveSelf = removeSelf;
            }

            public string Path { get; private set; }

            public bool RemoveSelf { get; private set; }
        }

        // 单次尝试删除：被占用等原因失败时直接抛出，由上层记录并跳过该项，绝不重试，确保一次删除完成。
        private static void DeleteFileByWinApi(string path)
        {
            string extended = ToExtendedPath(path);
            SetFileAttributesW(extended, FILE_ATTRIBUTE_NORMAL);

            if (DeleteFileW(extended)) return;

            int lastError = Marshal.GetLastWin32Error();
            if (lastError == ERROR_FILE_NOT_FOUND || lastError == ERROR_PATH_NOT_FOUND) return;

            ThrowFriendly(false, path, lastError);
        }

        // 单次尝试删除目录自身：同样不重试，失败即跳过。
        private static void RemoveDirectoryByWinApi(string path)
        {
            string extended = ToExtendedPath(path);
            SetFileAttributesW(extended, FILE_ATTRIBUTE_NORMAL);

            if (RemoveDirectoryW(extended)) return;

            int lastError = Marshal.GetLastWin32Error();
            if (lastError == ERROR_FILE_NOT_FOUND || lastError == ERROR_PATH_NOT_FOUND) return;

            ThrowFriendly(true, path, lastError);
        }

        private static string FriendlyMessage(bool isDirectory, string path, int error)
        {
            string target = isDirectory ? "文件夹" : "文件";
            string reason;
            switch (error)
            {
                case ERROR_SHARING_VIOLATION:
                case ERROR_LOCK_VIOLATION:
                    reason = target + "被其他程序占用，已跳过。可关闭杀毒软件 / Windows 搜索索引 / 网盘同步等可能占用它的程序后重试。";
                    break;
                case ERROR_ACCESS_DENIED:
                    reason = "没有删除该" + target + "的权限。请以管理员身份运行，或在设置中开启“完全权限模式”后重试。";
                    break;
                case ERROR_DIR_NOT_EMPTY:
                    reason = target + "非空：其中可能有文件正被占用而未能删除。请关闭占用程序后重试。";
                    break;
                default:
                    reason = "删除" + target + "失败：" + new Win32Exception(error).Message;
                    break;
            }
            return reason + Environment.NewLine + "路径：" + path;
        }

        private static void ThrowFriendly(bool isDirectory, string path, int error)
        {
            throw new IOException(FriendlyMessage(isDirectory, path, error), error);
        }

        private static string NormalizeInputPath(string path)
        {
            string normalized = path.Trim().Trim('"').Replace('/', '\\');
            // 去掉尾部分隔符，但保留盘符根（如 "C:\"）。
            if (normalized.Length > 3) normalized = normalized.TrimEnd('\\');
            return normalized;
        }

        private static string ToExtendedPath(string path)
        {
            if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) return path;
            if (path.StartsWith(@"\\", StringComparison.Ordinal)) return @"\\?\UNC\" + path.Substring(2);
            return @"\\?\" + path;
        }

        private sealed class DeletionTally
        {
            public int DeletedFiles;
            public int DeletedDirectories;
            public int FailedCount;
            public string FirstFailureMessage;

            public void Record(string message)
            {
                FailedCount++;
                if (FirstFailureMessage == null) FirstFailureMessage = message;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindFirstFileW(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool FindNextFileW(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindClose(IntPtr hFindFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFileAttributesW(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetFileAttributesW(string lpFileName, uint dwFileAttributes);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFileW(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool RemoveDirectoryW(string lpPathName);
    }
}
