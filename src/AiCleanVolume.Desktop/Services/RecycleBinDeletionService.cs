using System;
using System.IO;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;
using Microsoft.VisualBasic.FileIO;

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
                    if (!Directory.Exists(suggestion.Path))
                    {
                        result.Success = false;
                        result.Message = "目录不存在。";
                        return result;
                    }

                    if (useRecycleBin)
                    {
                        FileSystem.DeleteDirectory(suggestion.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    }
                    else
                    {
                        Directory.Delete(suggestion.Path, true);
                    }
                }
                else
                {
                    if (!File.Exists(suggestion.Path))
                    {
                        result.Success = false;
                        result.Message = "文件不存在。";
                        return result;
                    }

                    if (useRecycleBin)
                    {
                        FileSystem.DeleteFile(suggestion.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    }
                    else
                    {
                        File.Delete(suggestion.Path);
                    }
                }

                result.Success = true;
                result.Message = useRecycleBin ? "已移入回收站。" : "已永久删除。";
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
    }
}
