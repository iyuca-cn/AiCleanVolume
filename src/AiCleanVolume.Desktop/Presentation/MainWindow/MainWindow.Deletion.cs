using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Domain.Sandbox;
using AiCleanVolume.Core.Domain.Settings;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Core.Application.CleanupPlanning;
using AiCleanVolume.Core.Application.Deletion;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Core.Kernel.Ports;
using AiCleanVolume.Desktop.Controls;
using AiCleanVolume.Desktop.Infrastructure.Ai;
using AiCleanVolume.Desktop.Infrastructure.Scanning;
using AiCleanVolume.Desktop.Infrastructure.Settings;
using AiCleanVolume.Desktop.Infrastructure.Windows;
using AiCleanVolume.Desktop.Presentation.Features.Suggestions;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.ViewModels;


namespace AiCleanVolume.Desktop
{
    public sealed partial class MainWindow : AntdUI.Window
    {
        private void DeleteSelectedSuggestions()
        {
            SaveSettingsFromUi();
            RefreshSuggestionSandboxFromCurrentSettings();
            if (suggestionRows == null || suggestionRows.Count == 0)
            {
                AntdUI.Modal.open(this, "提示", "当前没有可删除的建议项。", AntdUI.TType.Info);
                return;
            }

            List<CleanupSuggestionRow> selectedRows = new List<CleanupSuggestionRow>();
            int needConfirmation = 0;
            long totalBytes = 0;
            for (int i = 0; i < suggestionRows.Count; i++)
            {
                CleanupSuggestionRow row = suggestionRows[i];
                if (!row.Suggestion.Selected || row.Suggestion.Status == CleanupStatus.Deleted) continue;
                selectedRows.Add(row);
                totalBytes += row.Suggestion.Bytes;
                if (row.Suggestion.Sandbox != null && row.Suggestion.Sandbox.Action == SandboxAction.RequireConfirmation) needConfirmation++;
            }

            if (selectedRows.Count == 0)
            {
                AntdUI.Modal.open(this, "提示", "请先勾选至少一项。", AntdUI.TType.Info);
                return;
            }

            DeleteSuggestionRows(selectedRows);
        }

        private void DeleteSingleSuggestion(CleanupSuggestionRow row)
        {
            if (row == null || row.Suggestion == null) return;
            SaveSettingsFromUi();
            RefreshSuggestionSandboxFromCurrentSettings();
            if (row.Suggestion.Status == CleanupStatus.Deleted)
            {
                AntdUI.Modal.open(this, "提示", "该建议项已删除。", AntdUI.TType.Info);
                return;
            }

            DeleteSuggestionRows(new List<CleanupSuggestionRow> { row });
        }

        private void DeleteSuggestionRows(List<CleanupSuggestionRow> rows)
        {
            if (rows == null || rows.Count == 0) return;

            List<DeleteConfirmItem> items = new List<DeleteConfirmItem>();
            for (int i = 0; i < rows.Count; i++)
            {
                CleanupSuggestionRow row = rows[i];
                if (row == null || row.Suggestion == null) continue;
                items.Add(new DeleteConfirmItem
                {
                    Name = row.Suggestion.Name,
                    Path = row.Suggestion.Path,
                    Bytes = row.Suggestion.Bytes,
                    Sandbox = row.Suggestion.Sandbox
                });
            }

            if (!ShowDeleteConfirmModal(items, settings.Sandbox.UseRecycleBin)) return;

            List<DeletionOutcome> outcomes = new List<DeletionOutcome>();
            DeletionProgressState progress = new DeletionProgressState();
            DateTime deleteStartedAt = DateTime.UtcNow;
            StartDeletionProgressDisplay(progress);
            RunBackground("正在执行删除…", delegate
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    CleanupSuggestionRow row = rows[i];
                    if (row == null || row.Suggestion == null) continue;
                    progress.Update("正在删除", row.Suggestion.Path);
                    CleanupDeletionWorkflowResult result = deletionWorkflow.Delete(row.Suggestion, settings.Sandbox, progress);
                    outcomes.Add(new DeletionOutcome { Row = row, Result = result.Result });
                }
            }, delegate
            {
                StopDeletionProgressDisplay();
                int successCount = 0;
                int failedCount = 0;
                for (int i = 0; i < outcomes.Count; i++)
                {
                    DeletionOutcome outcome = outcomes[i];
                    if (outcome.Result.Success)
                    {
                        outcome.Row.SetStatus(CleanupStatus.Deleted, outcome.Result.Message);
                        successCount++;
                    }
                    else
                    {
                        outcome.Row.SetStatus(CleanupStatus.Failed, outcome.Result.Message);
                        failedCount++;
                    }
                }
                suggestionTable.Refresh();
                RebuildSuggestionCards();
                TimeSpan elapsed = DateTime.UtcNow - deleteStartedAt;
                Log("删除流程执行完成：成功 " + successCount + " 项，失败 " + failedCount + " 项，耗时 " + elapsed.TotalSeconds.ToString("0.00") + " 秒。");
            }, delegate
            {
                StopDeletionProgressDisplay();
            });
        }

        private void StorageTable_CellClick(object sender, AntdUI.TableClickEventArgs eventArgs)
        {
            if (storageTable != null && storageTable.CanFocus) storageTable.Focus();
            StorageEntryRow row = eventArgs.Record as StorageEntryRow;
            if (row == null || row.Item == null) return;

            storageContextRow = row;
            storageTable.SetSelected(row);
            if (eventArgs.Button != MouseButtons.Right) return;

            ShowStorageContextMenu(row, eventArgs.X, eventArgs.Y);
        }

        private void OpenStorageRow(StorageEntryRow row)
        {
            if (row == null || row.Item == null || string.IsNullOrWhiteSpace(row.Item.Path)) return;
            explorerService.OpenPath(row.Item.Path, !row.Item.IsDirectory);
        }

        private void ShowStorageContextMenu(StorageEntryRow row, int x, int y)
        {
            if (row == null || row.Item == null) return;

            bool canOpen = !string.IsNullOrWhiteSpace(row.Item.Path);
            bool canDelete = CanOfferStorageDelete(row);
            AntdUI.IContextMenuStripItem[] items =
            {
                new AntdUI.ContextMenuStripItem("附加到 AI 提问")
                {
                    ID = StorageContextAskAiId,
                    IconSvg = "RobotOutlined",
                    Enabled = canOpen
                },
                new AntdUI.ContextMenuStripItem("在文件资源管理器打开")
                {
                    ID = StorageContextOpenId,
                    IconSvg = "FolderOpenOutlined",
                    Enabled = canOpen
                },
                new AntdUI.ContextMenuStripItemDivider(),
                new AntdUI.ContextMenuStripItem("删除" + (row.Item.IsDirectory ? "文件夹" : "文件"))
                {
                    ID = StorageContextDeleteId,
                    IconSvg = "DeleteOutlined",
                    Fore = AntdUI.Style.Db.Error,
                    Enabled = canDelete
                }
            };

            Point menuPoint = storageTable == null ? Cursor.Position : storageTable.PointToScreen(new Point(x, y));
            AntdUI.ContextMenuStrip.Config config = new AntdUI.ContextMenuStrip.Config(storageTable ?? (Control)this, StorageContextMenu_Click, items);
            config.Location = menuPoint;
            config.Align = AntdUI.TAlign.BR;
            AntdUI.ContextMenuStrip.open(config);
        }

        private void StorageContextMenu_Click(AntdUI.IContextMenuStrip item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ID)) return;
            if (string.Equals(item.ID, StorageContextAskAiId, StringComparison.OrdinalIgnoreCase))
            {
                if (storageContextRow != null && storageContextRow.Item != null)
                {
                    AddChatAttachment(storageContextRow.Item.Path, storageContextRow.Item.Bytes);
                    if (chatInput != null) chatInput.Focus();
                }
                return;
            }

            if (string.Equals(item.ID, StorageContextOpenId, StringComparison.OrdinalIgnoreCase))
            {
                OpenStorageRow(storageContextRow);
                return;
            }

            if (string.Equals(item.ID, StorageContextDeleteId, StringComparison.OrdinalIgnoreCase))
            {
                DeleteStorageRow(storageContextRow);
            }
        }

        private void StorageTable_KeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.KeyCode != Keys.Delete) return;
            if (!TryHandleStorageDeleteShortcut()) return;

            eventArgs.Handled = true;
            eventArgs.SuppressKeyPress = true;
        }

        private bool TryHandleStorageDeleteShortcut()
        {
            if (busy || activePageId != PageScan) return false;

            StorageEntryRow row = ResolveActiveStorageRow();
            if (row == null) return false;

            DeleteStorageRow(row);
            return true;
        }

        private StorageEntryRow ResolveActiveStorageRow()
        {
            if (storageTable == null) return null;

            StorageEntryRow focusedRow = storageTable.FocusedRow as StorageEntryRow;
            if (focusedRow != null) return focusedRow;

            object[] selectedRows = storageTable.SelectedsReal();
            for (int index = 0; index < selectedRows.Length; index++)
            {
                StorageEntryRow selectedRow = selectedRows[index] as StorageEntryRow;
                if (selectedRow != null) return selectedRow;
            }

            int selectedIndex = storageTable.SelectedIndex;
            StorageEntryRow indexedRow = GetStorageRowAtIndex(selectedIndex);
            if (indexedRow != null) return indexedRow;
            if (selectedIndex > 0)
            {
                StorageEntryRow indexedRowFallback = GetStorageRowAtIndex(selectedIndex - 1);
                if (indexedRowFallback != null) return indexedRowFallback;
            }

            return storageContextRow;
        }

        private StorageEntryRow GetStorageRowAtIndex(int index)
        {
            if (storageTable == null || index < 0) return null;

            AntdUI.Table.IRow tableRow = storageTable.GetRow(index);
            return tableRow == null ? null : tableRow.record as StorageEntryRow;
        }

        private bool CanOfferStorageDelete(StorageEntryRow row)
        {
            return row != null &&
                row.Item != null &&
                !string.IsNullOrWhiteSpace(row.Item.Path) &&
                !IsProtectedStorageDeleteTarget(row.Item.Path);
        }

        private void DeleteStorageRow(StorageEntryRow row)
        {
            if (!ValidateStorageDeleteTarget(row)) return;

            SaveSettingsFromUi();
            SandboxEvaluation sandbox = deletionWorkflow.Evaluate(row.Item.Path, settings.Sandbox);
            if (!ConfirmStorageDelete(row, sandbox)) return;

            CleanupSuggestion suggestion = CreateManualStorageSuggestion(row, sandbox);
            CleanupResult deleteResult = null;
            DeletionProgressState progress = new DeletionProgressState();

            StartDeletionProgressDisplay(progress);
            RunBackground("正在删除文件树项目…", delegate
            {
                progress.Update("正在删除", suggestion.Path);
                deleteResult = deletionWorkflow.Delete(suggestion, settings.Sandbox, progress).Result;
            }, delegate
            {
                StopDeletionProgressDisplay();
                if (deleteResult != null && deleteResult.Success)
                {
                    RemoveDeletedStorageRow(row);
                    Log("文件树删除完成：" + suggestion.Path + "，" + deleteResult.Message);
                    return;
                }

                string message = deleteResult == null ? "删除失败。" : deleteResult.Message;
                Log("文件树删除失败：" + suggestion.Path + "，" + message);
                ShowError("删除失败", message);
            }, delegate
            {
                StopDeletionProgressDisplay();
            });
        }

        private bool ValidateStorageDeleteTarget(StorageEntryRow row)
        {
            if (row == null || row.Item == null || string.IsNullOrWhiteSpace(row.Item.Path))
            {
                ShowInfo("提示", "删除目标为空。");
                return false;
            }

            if (IsProtectedStorageDeleteTarget(row.Item.Path))
            {
                ShowWarning("提示", "为避免误删，不支持直接删除当前扫描根或磁盘根目录。请展开到具体子项后再删除。");
                return false;
            }

            return true;
        }

        private bool ConfirmStorageDelete(StorageEntryRow row, SandboxEvaluation sandbox)
        {
            string message = SuggestionDeletionText.BuildStorageRowConfirmMessage(row, sandbox, settings.Sandbox.UseRecycleBin);

            AntdUI.TType icon = !settings.Sandbox.UseRecycleBin || (sandbox != null && sandbox.Action == SandboxAction.RequireConfirmation)
                ? AntdUI.TType.Warn
                : AntdUI.TType.Info;
            AntdUI.Modal.Config config = AntdUI.Modal.config(this, "确认删除", message, icon);
            config.OkText = "确认删除";
            config.CancelText = "取消";
            config.OkType = AntdUI.TTypeMini.Error;
            config.MaskClosable = false;
            DialogResult confirm = AntdUI.Modal.open(config);
            return confirm == DialogResult.OK;
        }

        private static CleanupSuggestion CreateManualStorageSuggestion(StorageEntryRow row, SandboxEvaluation sandbox)
        {
            return new CleanupSuggestion
            {
                Path = row.Item.Path,
                Name = row.Item.Name,
                Bytes = row.Item.Bytes,
                IsDirectory = row.Item.IsDirectory,
                Risk = CleanupRisk.High,
                Score = 1,
                Selected = true,
                Reason = "用户从文件树手动删除。",
                Source = "文件树",
                Status = CleanupStatus.Pending,
                Sandbox = sandbox
            };
        }

        private void RemoveDeletedStorageRow(StorageEntryRow row)
        {
            if (currentRoot == null || row == null || row.Item == null)
            {
                storageTable.Refresh();
                return;
            }

            StorageItem removedItem = row.Item;
            List<StorageItem> ancestors = new List<StorageItem>();
            if (!TryRemoveStorageItem(currentRoot, removedItem, ancestors))
            {
                storageTable.Refresh();
                return;
            }

            AdjustAncestorStats(ancestors, removedItem);
            UpdatePathAfterStorageDelete(row, ancestors);
            RemoveStorageRowFromParent(row);
            RefreshStorageAncestorRows(row.Parent);
            RemoveExpandedStoragePathsFor(removedItem.Path);
            currentTreeVersion++;
            storageTreeDeleteDirty = true;
            if (row.Parent != null) storageTable.SetSelected(row.Parent);
            storageTable.Refresh();
        }

        private static void RemoveStorageRowFromParent(StorageEntryRow row)
        {
            if (row == null || row.Parent == null || row.Parent.Children == null) return;
            row.Parent.Children.Remove(row);
        }

        private static void RefreshStorageAncestorRows(StorageEntryRow row)
        {
            StorageEntryRow current = row;
            while (current != null)
            {
                current.RefreshDisplayValues();
                current = current.Parent;
            }
        }

        private void RemoveExpandedStoragePathsFor(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || expandedStoragePaths.Count == 0) return;

            List<string> removeKeys = new List<string>();
            foreach (string expandedPath in expandedStoragePaths)
            {
                if (IsSameOrChildPath(expandedPath, path)) removeKeys.Add(expandedPath);
            }

            for (int index = 0; index < removeKeys.Count; index++) expandedStoragePaths.Remove(removeKeys[index]);
        }

        private void TrackStorageExpandedPath(StorageEntryRow row, bool expanded)
        {
            if (row == null || row.Item == null || string.IsNullOrWhiteSpace(row.Item.Path)) return;

            string key = NormalizePathForComparison(row.Item.Path);
            if (string.IsNullOrWhiteSpace(key)) return;

            if (expanded) expandedStoragePaths.Add(key);
            else expandedStoragePaths.Remove(key);
        }

        private bool IsStorageRowExpanded(StorageEntryRow row)
        {
            if (row == null || row.Item == null || string.IsNullOrWhiteSpace(row.Item.Path)) return false;
            return expandedStoragePaths.Contains(NormalizePathForComparison(row.Item.Path));
        }

        private static bool TryRemoveStorageItem(StorageItem parent, StorageItem target, IList<StorageItem> ancestors)
        {
            if (parent == null || target == null || parent.Children == null) return false;

            for (int index = 0; index < parent.Children.Count; index++)
            {
                StorageItem child = parent.Children[index];
                if (ReferenceEquals(child, target) || IsSamePath(child.Path, target.Path))
                {
                    parent.Children.RemoveAt(index);
                    if (parent.ChildrenLoaded && parent.Children.Count == 0) parent.HasChildren = false;
                    ancestors.Add(parent);
                    return true;
                }

                ancestors.Add(parent);
                if (TryRemoveStorageItem(child, target, ancestors)) return true;
                ancestors.RemoveAt(ancestors.Count - 1);
            }

            return false;
        }

        private static void AdjustAncestorStats(IList<StorageItem> ancestors, StorageItem removedItem)
        {
            if (ancestors == null || removedItem == null) return;

            int fileDelta = removedItem.IsDirectory ? Math.Max(0, removedItem.TotalFileCount) : 1;
            int directoryDelta = removedItem.IsDirectory ? Math.Max(0, removedItem.TotalDirectoryCount) + 1 : 0;

            for (int index = 0; index < ancestors.Count; index++)
            {
                StorageItem ancestor = ancestors[index];
                if (ancestor == null) continue;

                ancestor.Bytes = Math.Max(0L, ancestor.Bytes - Math.Max(0L, removedItem.Bytes));
                ancestor.TotalFileCount = Math.Max(0, ancestor.TotalFileCount - fileDelta);
                ancestor.TotalDirectoryCount = Math.Max(0, ancestor.TotalDirectoryCount - directoryDelta);
            }

            if (!removedItem.IsDirectory && ancestors.Count > 0)
            {
                StorageItem directParent = ancestors[ancestors.Count - 1];
                directParent.DirectFileCount = Math.Max(0, directParent.DirectFileCount - 1);
            }
        }

        private void UpdatePathAfterStorageDelete(StorageEntryRow row, IList<StorageItem> ancestors)
        {
            if (pathInput == null || row == null || row.Item == null) return;
            if (!IsSameOrChildPath(pathInput.Text, row.Item.Path)) return;

            StorageItem parent = ancestors != null && ancestors.Count > 0 ? ancestors[ancestors.Count - 1] : currentRoot;
            if (parent != null && !string.IsNullOrWhiteSpace(parent.Path)) pathInput.Text = parent.Path;
            else if (currentRoot != null && !string.IsNullOrWhiteSpace(currentRoot.Path)) pathInput.Text = currentRoot.Path;
        }

        private void RebindStorageTree()
        {
            if (storageTable == null || currentRoot == null) return;

            storageTable.DataSource = new List<StorageEntryRow> { new StorageEntryRow(currentRoot) };
            storageTable.Refresh();
        }

        private bool IsProtectedStorageDeleteTarget(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return true;
            if (currentRoot != null && IsSamePath(path, currentRoot.Path)) return true;

            string driveRoot = DrivePathText.GetDriveRoot(path);
            return !string.IsNullOrWhiteSpace(driveRoot) && IsSamePath(path, driveRoot);
        }

        private static bool IsSamePath(string left, string right)
        {
            return string.Equals(NormalizePathForComparison(left), NormalizePathForComparison(right), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameOrChildPath(string path, string parent)
        {
            string normalizedPath = NormalizePathForComparison(path);
            string normalizedParent = NormalizePathForComparison(parent);
            if (string.IsNullOrWhiteSpace(normalizedPath) || string.IsNullOrWhiteSpace(normalizedParent)) return false;
            if (string.Equals(normalizedPath, normalizedParent, StringComparison.OrdinalIgnoreCase)) return true;

            string prefix = normalizedParent.EndsWith(":", StringComparison.Ordinal) ? normalizedParent + "\\" : normalizedParent + "\\";
            return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePathForComparison(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        private sealed class DeleteConfirmItem
        {
            public string Name;
            public string Path;
            public long Bytes;
            public SandboxEvaluation Sandbox;
        }

        // 统一删除确认弹窗：逐项列出沙盒结论与大小，永久删除时加红色警示条。
        private bool ShowDeleteConfirmModal(List<DeleteConfirmItem> items, bool useRecycleBin)
        {
            if (items == null || items.Count == 0) return false;

            const int contentWidth = 540;
            long totalBytes = 0;
            for (int i = 0; i < items.Count; i++) totalBytes += items[i].Bytes;

            AntdUI.Panel content = CreateFlatPanel();
            content.Width = contentWidth;
            content.BackColor = Color.Transparent;

            AntdUI.Label summary = new AntdUI.Label();
            summary.Dock = DockStyle.Top;
            summary.Height = 28;
            summary.Text = "即将删除 " + items.Count + " 项 · 共 " + StorageFormatting.FormatBytes(totalBytes);
            summary.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            summary.ForeColor = Palette.TextPrimary;
            summary.BackColor = Color.Transparent;

            AntdUI.StackPanel list = CreateVerticalScrollPanel();
            list.Dock = DockStyle.Fill;
            list.AutoScroll = true;
            list.BackColor = Color.Transparent;
            int rowWidth = contentWidth - 6;
            for (int i = 0; i < items.Count; i++)
            {
                list.Controls.Add(BuildDeleteConfirmRow(items[i], rowWidth));
            }

            int visibleRows = Math.Min(items.Count, 8);
            int listHeight = visibleRows * 44 + 4;
            int warnHeight = useRecycleBin ? 0 : 48;

            if (!useRecycleBin)
            {
                AntdUI.Panel warn = CreateFlatPanel();
                warn.Dock = DockStyle.Bottom;
                warn.Height = warnHeight;
                warn.Back = Palette.DangerSoft;
                warn.Radius = 8;
                warn.Padding = new Padding(12, 0, 12, 0);
                warn.Margin = new Padding(0, 8, 0, 0);
                AntdUI.Label warnLabel = new AntdUI.Label();
                warnLabel.Dock = DockStyle.Fill;
                warnLabel.Text = "永久删除不经过回收站，无法恢复。";
                warnLabel.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
                warnLabel.ForeColor = Palette.Danger;
                warnLabel.BackColor = Color.Transparent;
                warnLabel.TextAlign = ContentAlignment.MiddleLeft;
                warn.Controls.Add(warnLabel);
                content.Controls.Add(warn);
            }

            content.Controls.Add(list);
            content.Controls.Add(summary);
            list.BringToFront();
            content.Height = 28 + listHeight + warnHeight + 6;

            AntdUI.Modal.Config config = AntdUI.Modal.config(this, "确认删除", content, useRecycleBin ? AntdUI.TType.Warn : AntdUI.TType.Error);
            config.OkText = useRecycleBin ? "移到回收站" : "永久删除";
            config.CancelText = "取消";
            config.OkType = useRecycleBin ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Error;
            config.Width = contentWidth + 100;
            config.MaskClosable = false;
            return AntdUI.Modal.open(config) == DialogResult.OK;
        }

        private static Control BuildDeleteConfirmRow(DeleteConfirmItem item, int width)
        {
            SandboxAction action = item.Sandbox == null ? SandboxAction.RequireConfirmation : item.Sandbox.Action;
            Color dotColor;
            string verdict;
            if (action == SandboxAction.Allow) { dotColor = Palette.Success; verdict = "允许"; }
            else if (action == SandboxAction.Bypass) { dotColor = Palette.Danger; verdict = "越权删除"; }
            else { dotColor = Palette.Warning; verdict = "需确认"; }
            if (item.Sandbox != null && !string.IsNullOrWhiteSpace(item.Sandbox.Message)) verdict = item.Sandbox.Message;

            AntdUI.Panel row = CreateFlatPanel();
            row.Width = width;
            row.Height = 38;
            row.Margin = new Padding(0, 0, 0, 6);
            row.Back = Palette.CardFill;
            row.Radius = 8;
            row.Padding = new Padding(10, 0, 12, 0);

            AntdUI.Label dot = new AntdUI.Label();
            dot.Dock = DockStyle.Left;
            dot.Width = 18;
            dot.Text = "●";
            dot.ForeColor = dotColor;
            dot.BackColor = Color.Transparent;
            dot.TextAlign = ContentAlignment.MiddleCenter;

            AntdUI.Label size = new AntdUI.Label();
            size.Dock = DockStyle.Right;
            size.Width = 104;
            size.Text = StorageFormatting.FormatBytes(item.Bytes);
            size.Font = new Font("Consolas", 9.5F);
            size.ForeColor = Palette.TextSecondary;
            size.BackColor = Color.Transparent;
            size.TextAlign = ContentAlignment.MiddleRight;

            AntdUI.Label sandbox = new AntdUI.Label();
            sandbox.Dock = DockStyle.Right;
            sandbox.Width = 128;
            sandbox.Text = verdict;
            sandbox.Font = new Font("Microsoft YaHei UI", 8.5F);
            sandbox.ForeColor = dotColor;
            sandbox.BackColor = Color.Transparent;
            sandbox.AutoEllipsis = true;
            sandbox.TextAlign = ContentAlignment.MiddleLeft;

            AntdUI.Label name = new AntdUI.Label();
            name.Dock = DockStyle.Fill;
            name.Text = string.IsNullOrWhiteSpace(item.Name) ? item.Path : item.Name;
            name.Font = new Font("Microsoft YaHei UI", 9.5F);
            name.ForeColor = Palette.TextPrimary;
            name.BackColor = Color.Transparent;
            name.AutoEllipsis = true;
            name.TextAlign = ContentAlignment.MiddleLeft;

            row.Controls.Add(dot);
            row.Controls.Add(size);
            row.Controls.Add(sandbox);
            row.Controls.Add(name);
            name.BringToFront();
            return row;
        }

        private sealed class DeletionOutcome
        {
            public CleanupSuggestionRow Row { get; set; }
            public CleanupResult Result { get; set; }
        }
    }
}
