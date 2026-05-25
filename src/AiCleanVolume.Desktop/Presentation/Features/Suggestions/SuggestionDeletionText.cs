using System;
using System.Collections.Generic;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Core.Domain.Sandbox;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Desktop.ViewModels;

namespace AiCleanVolume.Desktop.Presentation.Features.Suggestions
{
    public static class SuggestionDeletionText
    {
        public static string BuildBatchConfirmMessage(IList<CleanupSuggestionRow> rows)
        {
            int count = rows == null ? 0 : rows.Count;
            int needConfirmation = CountConfirmationRequired(rows);
            long totalBytes = SumBytes(rows);

            string message = "即将删除 " + count + " 项。" +
                Environment.NewLine + Environment.NewLine +
                "总大小：" + StorageFormatting.FormatBytes(totalBytes);
            if (needConfirmation > 0)
            {
                message += Environment.NewLine + Environment.NewLine + "其中 " + needConfirmation + " 项未命中白名单，需要你承担确认责任。";
            }

            return message + Environment.NewLine + Environment.NewLine + "当前使用 WinAPI 直接删除，不经过回收站，无法从回收站恢复。";
        }

        public static int CountConfirmationRequired(IList<CleanupSuggestionRow> rows)
        {
            if (rows == null) return 0;

            int needConfirmation = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                CleanupSuggestionRow row = rows[i];
                if (row != null && row.Suggestion != null && row.Suggestion.Sandbox != null && row.Suggestion.Sandbox.Action == SandboxAction.RequireConfirmation)
                {
                    needConfirmation++;
                }
            }

            return needConfirmation;
        }

        public static long SumBytes(IList<CleanupSuggestionRow> rows)
        {
            if (rows == null) return 0L;

            long totalBytes = 0L;
            for (int i = 0; i < rows.Count; i++)
            {
                CleanupSuggestionRow row = rows[i];
                if (row != null && row.Suggestion != null) totalBytes += row.Suggestion.Bytes;
            }

            return totalBytes;
        }

        public static string BuildStorageRowConfirmMessage(StorageEntryRow row, SandboxEvaluation sandbox, bool useRecycleBin)
        {
            string message = "确认要删除此文件（夹）吗？" +
                Environment.NewLine + Environment.NewLine +
                "路径：" + row.Item.Path +
                Environment.NewLine + Environment.NewLine +
                "大小：" + StorageFormatting.FormatBytes(row.Item.Bytes);

            if (sandbox != null && sandbox.Action == SandboxAction.RequireConfirmation)
            {
                message += Environment.NewLine + Environment.NewLine + "注意：该路径未命中沙盒允许位置，请确认确实要删除。";
            }

            if (!useRecycleBin)
            {
                message += Environment.NewLine + Environment.NewLine + "当前配置为永久删除，无法从回收站恢复。";
            }

            return message;
        }
    }
}
