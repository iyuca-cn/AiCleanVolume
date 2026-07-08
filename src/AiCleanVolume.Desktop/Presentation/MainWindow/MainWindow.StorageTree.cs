using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AiCleanVolume.Core.Domain.Cleanup;
using AiCleanVolume.Core.Domain.Sandbox;
using AiCleanVolume.Core.Domain.Storage;
using AiCleanVolume.Core.Application.Deletion;
using AiCleanVolume.Core.Application.Scanning;
using AiCleanVolume.Desktop.Controls;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.Presentation.Shared.Antd;
using AiCleanVolume.Desktop.ViewModels;

namespace AiCleanVolume.Desktop
{
    // 左栏存储结构：空态 / 扫描中 / 完成 三态互斥，完成态含用量条 + 树表格 + 底部操作栏。
    public sealed partial class MainWindow : AntdUI.Window
    {
        private enum StorageTreeState
        {
            Empty,
            Scanning,
            Done
        }

        private AntdUI.Panel storageEmptyPanel;

        private AntdUI.Panel storageScanningPanel;

        private AntdUI.Panel storageDonePanel;

        private DualUsageBar driveUsageBar;

        private AntdUI.Label driveUsageLabel;

        private AntdUI.Label reclaimHintLabel;

        private AntdUI.Panel storageSelectionBar;

        private AntdUI.Label storageSelectionLabel;

        private AntdUI.Label storageSelectionSizeLabel;

        private AntdUI.Button askAiButton;

        private AntdUI.Button treeRecycleButton;

        private AntdUI.Button treePermDeleteButton;

        private AntdUI.Panel storageStatsBar;

        private AntdUI.Label storageStatsLabel;

        // 工具栏上的磁盘选择与扫描按钮（原扫描页控件迁移至此）
        private void EnsureScanToolbarControls()
        {
            driveSelect = new AntdUI.Select();
            driveSelect.Dock = DockStyle.Left;
            driveSelect.Width = 150;
            driveSelect.DropDownArrow = true;
            driveSelect.ListAutoWidth = true;
            driveSelect.DropDownRadius = 8;
            driveSelect.Radius = 8;
            driveSelect.BorderWidth = 1F;
            driveSelect.BorderColor = Palette.Border;
            driveSelect.BorderHover = Palette.Accent;
            driveSelect.BorderActive = Palette.Accent;
            driveSelect.BackColor = Palette.Surface;
            driveSelect.SelectedValueChanged += DriveSelect_SelectedValueChanged;

            scanButton = new AntdUI.Button();
            scanButton.Dock = DockStyle.Left;
            scanButton.AutoSizeMode = AntdUI.TAutoSize.Width;
            scanButton.Text = "开始扫描";
            scanButton.IconSvg = "SyncOutlined";
            scanButton.IconRatio = 0.75F;
            scanButton.Type = AntdUI.TTypeMini.Primary;
            scanButton.Radius = 8;
            scanButton.Margin = new Padding(10, 0, 0, 0);
            scanButton.Click += delegate { ScanCurrentLocation(); };

            // 位置 / 阈值 / 每层条目为隐藏状态承载控件：树导航与设置弹窗读写，不进布局
            pathInput = new AntdUI.Input();
            minSizeInput = new AntdUI.Input();
            minSizeInput.Text = "-1";
            limitInput = new AntdUI.Input();
            limitInput.Text = "512";
            pathInput.TextChanged += PathInput_TextChanged;
        }

        private void BuildStorageTreeColumn()
        {
            storageEmptyPanel = BuildStorageEmptyPanel();
            storageScanningPanel = BuildStorageScanningPanel();
            storageDonePanel = BuildStorageDonePanel();

            leftColumnPanel.Controls.Add(storageDonePanel);
            leftColumnPanel.Controls.Add(storageScanningPanel);
            leftColumnPanel.Controls.Add(storageEmptyPanel);
            SetStorageTreeState(StorageTreeState.Empty);
        }

        private AntdUI.Panel BuildStorageEmptyPanel()
        {
            AntdUI.Panel panel = CreateFlatPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Palette.Surface;

            AntdUI.Panel centerHost = CreateFlatPanel();
            centerHost.Anchor = AnchorStyles.None;
            centerHost.Size = new Size(360, 240);

            AntdUI.Label iconLabel = new AntdUI.Label();
            iconLabel.Dock = DockStyle.Top;
            iconLabel.Height = 76;
            iconLabel.PrefixSvg = "FolderOpenOutlined";
            iconLabel.ForeColor = Palette.TextFaint;
            iconLabel.TextAlign = ContentAlignment.MiddleCenter;

            AntdUI.Label titleLabel = new AntdUI.Label();
            titleLabel.Dock = DockStyle.Top;
            titleLabel.Height = 34;
            titleLabel.Text = "还没有扫描";
            titleLabel.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
            titleLabel.ForeColor = Palette.TextPrimary;
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;

            AntdUI.Label descLabel = new AntdUI.Label();
            descLabel.Dock = DockStyle.Top;
            descLabel.Height = 66;
            descLabel.Text = "在顶部选择磁盘，点击「开始扫描」。\r\n扫描完成后这里会按占用大小列出每个文件夹，\r\n并标出 AI 判定可清理的部分。";
            descLabel.Font = new Font(Font.FontFamily, 9F);
            descLabel.ForeColor = Palette.TextMuted;
            descLabel.TextAlign = ContentAlignment.TopCenter;

            AntdUI.Button startButton = new AntdUI.Button();
            startButton.AutoSizeMode = AntdUI.TAutoSize.None;
            startButton.Text = "开始扫描";
            startButton.Type = AntdUI.TTypeMini.Primary;
            startButton.Radius = 8;
            startButton.Size = new Size(140, 40);
            startButton.Click += delegate { ScanCurrentLocation(); };

            AntdUI.Panel buttonHost = CreateFlatPanel();
            buttonHost.Dock = DockStyle.Top;
            buttonHost.Height = 52;
            startButton.Left = (360 - startButton.Width) / 2;
            startButton.Top = 10;
            buttonHost.Controls.Add(startButton);

            centerHost.Controls.Add(buttonHost);
            centerHost.Controls.Add(descLabel);
            centerHost.Controls.Add(titleLabel);
            centerHost.Controls.Add(iconLabel);
            buttonHost.BringToFront();

            panel.Controls.Add(centerHost);
            panel.Resize += delegate
            {
                centerHost.Left = (panel.ClientSize.Width - centerHost.Width) / 2;
                centerHost.Top = (panel.ClientSize.Height - centerHost.Height) / 2 - 30;
            };
            return panel;
        }

        private AntdUI.Panel BuildStorageScanningPanel()
        {
            AntdUI.Panel panel = CreateFlatPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Palette.Surface;
            panel.Padding = new Padding(18, 16, 18, 16);
            panel.Visible = false;

            AntdUI.Label titleLabel = new AntdUI.Label();
            titleLabel.Dock = DockStyle.Top;
            titleLabel.Height = 30;
            titleLabel.Text = "正在扫描";
            titleLabel.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold);
            titleLabel.ForeColor = Palette.TextPrimary;

            scanProgress = new AntdUI.Progress();
            scanProgress.Dock = DockStyle.Top;
            scanProgress.Height = 22;
            scanProgress.Shape = AntdUI.TShapeProgress.Round;
            scanProgress.Radius = 8;
            scanProgress.Value = 0F;
            scanProgress.Fill = Palette.Accent;
            scanProgress.Back = Palette.BarTrack;
            scanProgress.ValueRatio = 0.55F;
            scanProgress.UseSystemText = false;
            scanProgress.Animation = Presentation.Features.Scan.ScanPageText.ActiveProgressAnimationMs;

            scanStatusLabel = new AntdUI.Label();
            scanStatusLabel.Dock = DockStyle.Top;
            scanStatusLabel.Height = 26;
            scanStatusLabel.Font = AntdControlFactory.MonoFont(8.5F);
            scanStatusLabel.ForeColor = Palette.TextFaint;
            scanStatusLabel.AutoEllipsis = true;
            scanStatusLabel.Text = "等待开始扫描";
            scanStatusLabel.Padding = new Padding(0, 6, 0, 0);

            scanElapsedLabel = new AntdUI.Label();
            scanElapsedLabel.Dock = DockStyle.Top;
            scanElapsedLabel.Height = 24;
            scanElapsedLabel.Font = new Font(Font.FontFamily, 9F);
            scanElapsedLabel.ForeColor = Palette.TextMuted;
            scanElapsedLabel.Text = "用时 0.0 秒";

            panel.Controls.Add(scanElapsedLabel);
            panel.Controls.Add(scanStatusLabel);
            panel.Controls.Add(scanProgress);
            panel.Controls.Add(titleLabel);
            scanElapsedLabel.BringToFront();
            scanStatusLabel.BringToFront();
            return panel;
        }

        private AntdUI.Panel BuildStorageDonePanel()
        {
            AntdUI.Panel panel = CreateFlatPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Palette.Surface;
            panel.Visible = false;

            // 头部：标题 + 用量双色条 + 图例
            AntdUI.Panel header = CreateFlatPanel();
            header.Dock = DockStyle.Top;
            header.Height = 96;
            header.Padding = new Padding(18, 12, 18, 8);

            AntdUI.Panel titleRow = CreateFlatPanel();
            titleRow.Dock = DockStyle.Top;
            titleRow.Height = 26;

            AntdUI.Label titleLabel = new AntdUI.Label();
            titleLabel.Dock = DockStyle.Left;
            titleLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            titleLabel.Text = "存储结构";
            titleLabel.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            titleLabel.ForeColor = Palette.TextPrimary;

            AntdUI.Label sortHintLabel = new AntdUI.Label();
            sortHintLabel.Dock = DockStyle.Right;
            sortHintLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            sortHintLabel.Text = "按占用排序";
            sortHintLabel.Font = new Font(Font.FontFamily, 8.5F);
            sortHintLabel.ForeColor = Palette.TextMuted;

            titleRow.Controls.Add(titleLabel);
            titleRow.Controls.Add(sortHintLabel);

            AntdUI.Panel usageRow = CreateFlatPanel();
            usageRow.Dock = DockStyle.Top;
            usageRow.Height = 22;

            driveUsageLabel = new AntdUI.Label();
            driveUsageLabel.Dock = DockStyle.Left;
            driveUsageLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            driveUsageLabel.Font = AntdControlFactory.MonoFont(8.5F);
            driveUsageLabel.ForeColor = Palette.TextSecondary;
            driveUsageLabel.Text = "-";

            reclaimHintLabel = new AntdUI.Label();
            reclaimHintLabel.Dock = DockStyle.Right;
            reclaimHintLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            reclaimHintLabel.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold);
            reclaimHintLabel.ForeColor = Palette.Accent;
            reclaimHintLabel.Text = string.Empty;

            usageRow.Controls.Add(driveUsageLabel);
            usageRow.Controls.Add(reclaimHintLabel);

            driveUsageBar = new DualUsageBar();
            driveUsageBar.Dock = DockStyle.Top;
            driveUsageBar.Height = 8;

            AntdUI.Panel legendRow = CreateFlatPanel();
            legendRow.Dock = DockStyle.Top;
            legendRow.Height = 20;
            legendRow.Padding = new Padding(0, 4, 0, 0);

            AntdUI.Label legendLabel = new AntdUI.Label();
            legendLabel.Dock = DockStyle.Fill;
            legendLabel.Font = new Font(Font.FontFamily, 8F);
            legendLabel.ForeColor = Palette.TextMuted;
            legendLabel.Text = "■ 正常占用    ■ AI 判定可清理";
            legendRow.Controls.Add(legendLabel);

            header.Controls.Add(legendRow);
            header.Controls.Add(driveUsageBar);
            header.Controls.Add(usageRow);
            header.Controls.Add(titleRow);
            legendRow.BringToFront();

            AntdUI.Panel headerLine = CreateFlatPanel();
            headerLine.Dock = DockStyle.Top;
            headerLine.Height = 1;
            headerLine.BackColor = Palette.Divider;

            // 表格
            storageTable = new AntdUI.Table();
            storageTable.Dock = DockStyle.Fill;
            storageTable.TabStop = true;
            AntdControlFactory.ConfigureTableSurface(storageTable);
            storageTable.FixedHeader = true;
            storageTable.ScrollBarAvoidHeader = true;
            storageTable.ExpandChanged += StorageTable_ExpandChanged;
            storageTable.CellClick += StorageTable_CellClick;
            storageTable.CellDoubleClick += StorageTable_CellDoubleClick;
            storageTable.KeyDown += StorageTable_KeyDown;
            storageTable.CheckedChanged += StorageTable_CheckedChanged;

            // 底部：无选中时统计，有选中时操作栏
            storageStatsBar = CreateFlatPanel();
            storageStatsBar.Dock = DockStyle.Bottom;
            storageStatsBar.Height = 34;
            storageStatsBar.BackColor = Palette.SurfaceFaint;
            storageStatsBar.Padding = new Padding(18, 6, 18, 6);

            storageStatsLabel = new AntdUI.Label();
            storageStatsLabel.Dock = DockStyle.Fill;
            storageStatsLabel.Font = new Font(Font.FontFamily, 8.5F);
            storageStatsLabel.ForeColor = Palette.TextMuted;
            storageStatsLabel.Text = string.Empty;
            storageStatsBar.Controls.Add(storageStatsLabel);

            storageSelectionBar = CreateFlatPanel();
            storageSelectionBar.Dock = DockStyle.Bottom;
            storageSelectionBar.Height = 86;
            storageSelectionBar.BackColor = Palette.SurfaceFaint;
            storageSelectionBar.Padding = new Padding(18, 8, 18, 10);
            storageSelectionBar.Visible = false;

            AntdUI.Panel selectionInfoRow = CreateFlatPanel();
            selectionInfoRow.Dock = DockStyle.Top;
            selectionInfoRow.Height = 26;

            storageSelectionLabel = new AntdUI.Label();
            storageSelectionLabel.Dock = DockStyle.Left;
            storageSelectionLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            storageSelectionLabel.Font = new Font(Font.FontFamily, 9F);
            storageSelectionLabel.ForeColor = Palette.TextSecondary;
            storageSelectionLabel.Text = "已选 0 项";

            storageSelectionSizeLabel = new AntdUI.Label();
            storageSelectionSizeLabel.Dock = DockStyle.Right;
            storageSelectionSizeLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            storageSelectionSizeLabel.Font = AntdControlFactory.MonoFontBold(10F);
            storageSelectionSizeLabel.ForeColor = Palette.Accent;
            storageSelectionSizeLabel.Text = string.Empty;

            selectionInfoRow.Controls.Add(storageSelectionLabel);
            selectionInfoRow.Controls.Add(storageSelectionSizeLabel);

            AntdUI.Panel selectionButtonRow = CreateFlatPanel();
            selectionButtonRow.Dock = DockStyle.Bottom;
            selectionButtonRow.Height = 38;

            askAiButton = new AntdUI.Button();
            askAiButton.Dock = DockStyle.Left;
            askAiButton.Width = 120;
            askAiButton.Text = "询问 AI";
            askAiButton.Radius = 8;
            askAiButton.BorderWidth = 1F;
            askAiButton.DefaultBorderColor = Palette.AccentSoftBorder;
            askAiButton.DefaultBack = Palette.AccentSoft;
            askAiButton.ForeColor = Palette.Accent;
            askAiButton.Click += AskAiButton_Click;

            treeRecycleButton = new AntdUI.Button();
            treeRecycleButton.Dock = DockStyle.Left;
            treeRecycleButton.Width = 130;
            treeRecycleButton.Text = "移到回收站";
            treeRecycleButton.Type = AntdUI.TTypeMini.Primary;
            treeRecycleButton.Radius = 8;
            treeRecycleButton.Margin = new Padding(8, 0, 0, 0);
            treeRecycleButton.Click += delegate { DeleteCheckedStorageRows(true); };

            treePermDeleteButton = new AntdUI.Button();
            treePermDeleteButton.Dock = DockStyle.Left;
            treePermDeleteButton.Width = 110;
            treePermDeleteButton.Text = "永久删除";
            treePermDeleteButton.Radius = 8;
            treePermDeleteButton.BorderWidth = 1F;
            treePermDeleteButton.DefaultBorderColor = Palette.DangerBorder;
            treePermDeleteButton.ForeColor = Palette.Danger;
            treePermDeleteButton.Margin = new Padding(8, 0, 0, 0);
            treePermDeleteButton.Click += delegate { DeleteCheckedStorageRows(false); };

            selectionButtonRow.Controls.Add(treePermDeleteButton);
            selectionButtonRow.Controls.Add(treeRecycleButton);
            selectionButtonRow.Controls.Add(askAiButton);
            askAiButton.BringToFront();

            storageSelectionBar.Controls.Add(selectionInfoRow);
            storageSelectionBar.Controls.Add(selectionButtonRow);

            panel.Controls.Add(storageTable);
            panel.Controls.Add(headerLine);
            panel.Controls.Add(header);
            panel.Controls.Add(storageStatsBar);
            panel.Controls.Add(storageSelectionBar);
            storageTable.BringToFront();
            return panel;
        }

        private void SetStorageTreeState(StorageTreeState state)
        {
            if (storageEmptyPanel == null) return;
            storageEmptyPanel.Visible = state == StorageTreeState.Empty;
            storageScanningPanel.Visible = state == StorageTreeState.Scanning;
            storageDonePanel.Visible = state == StorageTreeState.Done;
        }

        private void StorageTable_CheckedChanged(object sender, AntdUI.TableCheckEventArgs e)
        {
            RefreshStorageSelectionBar();
        }

        private void RefreshStorageSelectionBar()
        {
            List<StorageEntryRow> selectedRows = CollectCheckedStorageRows();
            bool hasSelection = selectedRows.Count > 0;
            if (storageSelectionBar != null) storageSelectionBar.Visible = hasSelection;
            if (storageStatsBar != null) storageStatsBar.Visible = !hasSelection;
            if (!hasSelection) return;

            long totalBytes = 0;
            for (int i = 0; i < selectedRows.Count; i++) totalBytes += selectedRows[i].Item.Bytes;
            storageSelectionLabel.Text = "已选 " + selectedRows.Count + " 项";
            storageSelectionSizeLabel.Text = StorageFormatting.FormatBytes(totalBytes);
        }

        // 收集勾选行；父目录已勾选时吞并其下勾选的子项，避免重复计量/重复删除
        private List<StorageEntryRow> CollectCheckedStorageRows()
        {
            List<StorageEntryRow> checkedRows = new List<StorageEntryRow>();
            List<StorageEntryRow> rootRows = storageTable == null ? null : storageTable.DataSource as List<StorageEntryRow>;
            if (rootRows != null)
            {
                for (int i = 0; i < rootRows.Count; i++) CollectCheckedStorageRows(rootRows[i], checkedRows);
            }

            checkedRows.Sort(delegate (StorageEntryRow left, StorageEntryRow right)
            {
                return string.Compare(left.Item.Path, right.Item.Path, StringComparison.OrdinalIgnoreCase);
            });

            List<StorageEntryRow> merged = new List<StorageEntryRow>();
            string lastDirPrefix = null;
            for (int i = 0; i < checkedRows.Count; i++)
            {
                string path = checkedRows[i].Item.Path;
                if (lastDirPrefix != null && path.StartsWith(lastDirPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                merged.Add(checkedRows[i]);
                if (checkedRows[i].Item.IsDirectory) lastDirPrefix = path.TrimEnd('\\') + "\\";
            }

            return merged;
        }

        private static void CollectCheckedStorageRows(StorageEntryRow row, List<StorageEntryRow> result)
        {
            if (row == null || row.Item == null) return;
            if (row.selected && !string.IsNullOrWhiteSpace(row.Item.Path)) result.Add(row);
            for (int i = 0; i < row.Children.Count; i++)
            {
                StorageEntryRow child = row.Children[i] as StorageEntryRow;
                if (child != null) CollectCheckedStorageRows(child, result);
            }
        }

        private void AskAiButton_Click(object sender, EventArgs e)
        {
            List<StorageEntryRow> rows = CollectCheckedStorageRows();
            if (rows.Count == 0)
            {
                ShowInfo("提示", "请先勾选要询问的项目。");
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                AddChatAttachment(rows[i].Item.Path, rows[i].Item.Bytes);
            }

            if (chatInput != null) chatInput.Focus();
        }

        private void DeleteCheckedStorageRows(bool useRecycleBin)
        {
            if (busy) return;
            List<StorageEntryRow> rows = CollectCheckedStorageRows();
            if (rows.Count == 0)
            {
                ShowInfo("提示", "请先勾选要删除的项目。");
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (IsProtectedStorageDeleteTarget(rows[i].Item.Path))
                {
                    ShowWarning("提示", "选中项包含扫描根或磁盘根目录，已阻止删除：" + rows[i].Item.Path);
                    return;
                }
            }

            long totalBytes = 0;
            for (int i = 0; i < rows.Count; i++) totalBytes += rows[i].Item.Bytes;

            string modeText = useRecycleBin ? "移到回收站" : "永久删除（不可恢复）";
            AntdUI.Modal.Config config = AntdUI.Modal.config(
                this,
                "确认删除",
                "将对选中的 " + rows.Count + " 项（共 " + StorageFormatting.FormatBytes(totalBytes) + "）执行：" + modeText + "。\r\n每项删除前会先做沙盒安全评估。",
                useRecycleBin ? AntdUI.TType.Info : AntdUI.TType.Warn);
            config.OkText = "确认删除";
            config.CancelText = "取消";
            config.OkType = AntdUI.TTypeMini.Error;
            config.MaskClosable = false;
            if (AntdUI.Modal.open(config) != DialogResult.OK) return;

            SaveSettingsFromUi();
            bool originalRecycle = settings.Sandbox.UseRecycleBin;
            settings.Sandbox.UseRecycleBin = useRecycleBin;

            DeletionProgressState progress = new DeletionProgressState();
            List<StorageEntryRow> deletedRows = new List<StorageEntryRow>();
            List<string> failures = new List<string>();

            StartDeletionProgressDisplay(progress);
            RunBackground("正在删除选中项…", delegate
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    StorageEntryRow row = rows[i];
                    progress.Update("正在删除", row.Item.Path);
                    SandboxEvaluation sandbox = deletionWorkflow.Evaluate(row.Item.Path, settings.Sandbox);
                    CleanupSuggestion suggestion = CreateManualStorageSuggestion(row, sandbox);
                    CleanupResult deleteResult = deletionWorkflow.Delete(suggestion, settings.Sandbox, progress).Result;
                    if (deleteResult != null && deleteResult.Success)
                    {
                        lock (deletedRows) deletedRows.Add(row);
                    }
                    else
                    {
                        lock (failures) failures.Add(row.Item.Path + "：" + (deleteResult == null ? "删除失败" : deleteResult.Message));
                    }
                }
            }, delegate
            {
                settings.Sandbox.UseRecycleBin = originalRecycle;
                StopDeletionProgressDisplay();
                for (int i = 0; i < deletedRows.Count; i++) RemoveDeletedStorageRow(deletedRows[i]);
                RefreshStorageSelectionBar();
                UpdateStorageStatsBar();
                Log("批量删除完成：成功 " + deletedRows.Count + " 项，失败 " + failures.Count + " 项。");
                if (failures.Count > 0)
                {
                    ShowError("部分删除失败", string.Join(Environment.NewLine, failures.ToArray()));
                }
            }, delegate
            {
                settings.Sandbox.UseRecycleBin = originalRecycle;
                StopDeletionProgressDisplay();
            });
        }

        private void UpdateStorageStatsBar()
        {
            if (storageStatsLabel == null) return;
            if (currentRoot == null)
            {
                storageStatsLabel.Text = string.Empty;
                return;
            }

            storageStatsLabel.Text = "共 " + currentRoot.TotalFileCount.ToString("N0") + " 个文件 · " +
                currentRoot.TotalDirectoryCount.ToString("N0") + " 个文件夹";
        }

        // 头部用量双色条 + 状态栏当前盘
        private void UpdateDriveSummaryForLocation(string location)
        {
            DriveInfo drive = Presentation.Features.Scan.ScanPageText.TryResolveDriveInfo(location);
            string driveText = null;

            if (drive != null)
            {
                try
                {
                    if (drive.IsReady)
                    {
                        long totalBytes = drive.TotalSize;
                        long usedBytes = Math.Max(0L, totalBytes - drive.TotalFreeSpace);
                        float usedRatio = totalBytes > 0 ? (float)((double)usedBytes / totalBytes) : 0F;
                        if (driveUsageBar != null)
                        {
                            driveUsageBar.UsedRatio = usedRatio;
                        }

                        if (driveUsageLabel != null)
                        {
                            driveUsageLabel.Text = drive.Name.TrimEnd('\\') + "  已用 " +
                                StorageFormatting.FormatBytes(usedBytes) + " / " + StorageFormatting.FormatBytes(totalBytes);
                        }

                        driveText = drive.Name.TrimEnd('\\') + " " + drive.DriveFormat + " " +
                            StorageFormatting.FormatBytes(usedBytes) + "/" + StorageFormatting.FormatBytes(totalBytes);
                    }
                }
                catch
                {
                    // 磁盘信息读取失败时保持现状
                }
            }

            UpdateStatusBar(busy ? "扫描中" : "空闲", busy ? Palette.AccentHover : Palette.TitleBarMuted, driveText);
        }

        /// <summary>AI 报告回写：可清理字节数 → 双色条第二段与提示文字。</summary>
        internal void UpdateReclaimEstimate(long reclaimBytes)
        {
            DriveInfo drive = Presentation.Features.Scan.ScanPageText.TryResolveDriveInfo(currentRoot == null ? null : currentRoot.Path);
            if (reclaimHintLabel != null)
            {
                reclaimHintLabel.Text = reclaimBytes > 0 ? "可释放约 " + StorageFormatting.FormatBytes(reclaimBytes) : string.Empty;
            }

            if (driveUsageBar != null && drive != null && drive.IsReady && drive.TotalSize > 0 && reclaimBytes > 0)
            {
                float reclaimRatio = (float)((double)reclaimBytes / drive.TotalSize);
                driveUsageBar.UsedRatio = Math.Max(0F, driveUsageBar.UsedRatio - reclaimRatio);
                driveUsageBar.ReclaimRatio = reclaimRatio;
            }
        }
    }
}
