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
using AiCleanVolume.Desktop.Presentation.Features.Scan;
using AiCleanVolume.Desktop.Presentation.Features.Settings;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.ViewModels;


namespace AiCleanVolume.Desktop
{
    public sealed partial class MainWindow : AntdUI.Window
    {
        private void LoadDrives()
        {
            driveSelect.Items.Clear();
            if (suggestionDriveSelect != null) suggestionDriveSelect.Items.Clear();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed) continue;
                driveSelect.Items.Add(new AntdUI.SelectItem(drive.Name, drive.Name));
                if (suggestionDriveSelect != null) suggestionDriveSelect.Items.Add(new AntdUI.SelectItem(drive.Name, drive.Name));
            }

            string defaultDrive = ResolveDefaultDrive();
            driveSelect.SelectedValue = defaultDrive;
            if (suggestionDriveSelect != null) suggestionDriveSelect.SelectedValue = defaultDrive;
            pathInput.Text = defaultDrive;
        }

        private void DriveSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (loadingStartupUi) return;
            if (e.Value == null) return;
            pathInput.Text = e.Value.ToString();
            UpdateDriveSummaryForLocation(pathInput.Text);
            RefreshPromptForCurrentLocation();
        }

        private void SuggestionDriveSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (loadingStartupUi) return;
            RefreshPromptForCurrentLocation();
        }

        private void PathInput_TextChanged(object sender, EventArgs e)
        {
            if (loadingStartupUi) return;
            string location = pathInput.Text;
            if (string.IsNullOrWhiteSpace(location) && driveSelect != null && driveSelect.SelectedValue != null)
            {
                location = driveSelect.SelectedValue.ToString();
            }
            UpdateDriveSummaryForLocation(location);
            RefreshPromptForCurrentLocation();
        }

        private void SizeModeSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (loadingStartupUi) return;
            SaveSettingsFromUi();
            UpdateStorageSizeColumnTitle(ResolveSelectedSizeMode());
        }

        private void ScanCurrentLocation()
        {
            ScanCurrentLocation(null, null);
        }

        private void ScanCurrentLocation(Action onCompleted, string statusText)
        {
            SaveSettingsFromUi();
            string location = ResolveSelectedLocation();
            ScanRequest request = BuildScanRequest(location, 1);
            ScanLocation(request, onCompleted, statusText);
        }

        private void ScanSuggestionLocation(string location, Action onCompleted, string statusText)
        {
            SaveSettingsFromUi();
            ScanRequest request = BuildSuggestionScanRequest(location, 1);
            ScanLocation(request, onCompleted, statusText);
        }

        private void ScanLocation(ScanRequest request, Action onCompleted, string statusText)
        {
            StorageItem result = null;
            DateTime scanStartedAt = DateTime.UtcNow;
            ClearScanProviderCache();
            currentTreeVersion++;
            expandedStoragePaths.Clear();
            storageTreeDeleteDirty = false;
            string progressText = string.IsNullOrWhiteSpace(statusText) ? "正在扫描空间占用..." : statusText;
            string workerCaption = string.IsNullOrWhiteSpace(statusText) ? "正在扫描空间占用…" : statusText;
            SetStorageTreeState(StorageTreeState.Scanning);
            SetAiChatState(StorageTreeState.Scanning);
            StartScanProgress(progressText, scanStartedAt, ScanPageText.ResolveProgressInterval(request.Location));
            Log("扫描开始：" + ScanPageText.DescribeRequest(request));

            RunBackground(workerCaption, delegate
            {
                result = scanProvider.Scan(request);
            }, delegate
            {
                TimeSpan elapsed = DateTime.UtcNow - scanStartedAt;
                currentRoot = result;
                currentTreeRequest = CreateScanRequest(result.Path, 1, request);
                currentTreeRequest.SessionIdentity = result.SessionIdentity;
                currentTreeRequest.SessionNodeId = result.SessionNodeId;
                List<StorageEntryRow> rows = new List<StorageEntryRow> { new StorageEntryRow(result, true) };
                storageTable.DataSource = rows;
                UpdateDriveSummaryForLocation(result.Path);
                StopScanProgress();
                UpdateScanProgressState("扫描完成", 1F, false, AntdUI.TType.Success);
                UpdateScanElapsedState(elapsed);
                SetStorageTreeState(StorageTreeState.Done);
                SetAiChatState(StorageTreeState.Done);
                UpdateStorageStatsBar();
                RefreshStorageSelectionBar();
                StartAiScanReport();
                // 直接点「开始扫描」时自动生成推荐；带续接动作的扫描由其自身流程分析
                if (onCompleted == null) BeginInvoke((MethodInvoker)delegate { if (!IsDisposed && !busy) AnalyzeSuggestionsCore(true); });
                Log("扫描完成：" + result.Path + "，" + ScanPageText.DescribeSizeMode(request.SortMode) + " " + StorageFormatting.FormatBytes(result.Bytes) + "，耗时 " + elapsed.TotalSeconds.ToString("0.00") + " 秒，子项 " + (result.Children == null ? 0 : result.Children.Count) + "。");
                if (onCompleted != null) onCompleted();
            }, delegate
            {
                TimeSpan elapsed = DateTime.UtcNow - scanStartedAt;
                StopScanProgress();
                UpdateScanProgressState("扫描失败", 1F, false, AntdUI.TType.Error);
                UpdateScanElapsedState(elapsed);
                SetStorageTreeState(currentRoot == null ? StorageTreeState.Empty : StorageTreeState.Done);
                SetAiChatState(currentRoot == null ? StorageTreeState.Empty : StorageTreeState.Done);
            });
        }

        private void StorageTable_ExpandChanged(object sender, AntdUI.TableExpandEventArgs e)
        {
            StorageEntryRow row = e.Record as StorageEntryRow;
            if (row == null || row.Item == null) return;
            storageContextRow = row;
            SetPathInputFromStorageRow(row);
            TrackStorageExpandedPath(row, e.Expand);

            if (!e.Expand)
            {
                bool released = !storageTreeDeleteDirty && CanReloadStorageNode(row.Item) ? row.ReleaseLoadedChildren() : row.ReleaseChildRows();
                if (released) storageTable.Refresh();
                return;
            }

            if (!row.Item.IsDirectory || !row.Item.HasChildren) return;

            if (row.Item.ChildrenLoaded)
            {
                if (row.MaterializeLoadedChildren()) storageTable.Refresh();
                return;
            }

            if (currentTreeRequest == null) return;

            if (row.IsLoadingChildren) return;

            row.IsLoadingChildren = true;

            ScanRequest request = CreateScanRequest(row.Item.Path, 1, currentTreeRequest);
            request.SessionIdentity = row.Item.SessionIdentity;
            request.SessionNodeId = row.Item.SessionNodeId;
            int treeVersion = currentTreeVersion;
            backgroundWorker.Enqueue(delegate
            {
                StorageItem loaded = null;
                Exception error = null;

                try
                {
                    loaded = scanProvider.Scan(request);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                BeginInvoke((MethodInvoker)delegate
                {
                    if (IsDisposed || treeVersion != currentTreeVersion) return;

                    if (error != null)
                    {
                        row.ReloadChildren();
                        storageTable.Refresh();
                        Log("目录节点加载失败：" + error.Message);
                        ShowError("加载失败", error.Message);
                        return;
                    }

                    ApplyScannedNode(row.Item, loaded);
                    row.RefreshFromItem(true);
                    storageTable.Refresh();
                });
            });
        }

        private static void ApplyScannedNode(StorageItem target, StorageItem source)
        {
            if (target == null || source == null) return;

            target.Path = source.Path;
            target.Name = source.Name;
            target.Bytes = source.Bytes;
            target.IsDirectory = source.IsDirectory;
            target.HasChildren = source.HasChildren;
            target.ChildrenLoaded = source.ChildrenLoaded;
            target.DirectFileCount = source.DirectFileCount;
            target.TotalFileCount = source.TotalFileCount;
            target.TotalDirectoryCount = source.TotalDirectoryCount;
            target.SessionIdentity = source.SessionIdentity;
            target.SessionNodeId = source.SessionNodeId;
            target.ChildStart = source.ChildStart;
            target.ChildCount = source.ChildCount;
            target.LoadedChildCount = source.LoadedChildCount;
            target.TotalChildCount = source.TotalChildCount;
            target.Children.Clear();
            for (int i = 0; i < source.Children.Count; i++) target.Children.Add(source.Children[i]);
        }

        private void SetPathInputFromStorageRow(StorageEntryRow row)
        {
            if (row == null || row.Item == null || string.IsNullOrWhiteSpace(row.Item.Path) || pathInput == null) return;
            if (!string.Equals(pathInput.Text, row.Item.Path, StringComparison.OrdinalIgnoreCase))
            {
                pathInput.Text = row.Item.Path;
            }
            else
            {
                UpdateDriveSummaryForLocation(row.Item.Path);
            }
        }

        private ScanRequest BuildScanRequest(int loadDepth)
        {
            string location = ResolveSelectedLocation();
            return BuildScanRequest(location, loadDepth);
        }

        private string ResolveSelectedLocation()
        {
            string location = pathInput == null ? null : pathInput.Text;
            if (string.IsNullOrWhiteSpace(location) && driveSelect != null && driveSelect.SelectedValue != null)
            {
                location = driveSelect.SelectedValue.ToString();
            }

            if (string.IsNullOrWhiteSpace(location))
            {
                string defaultDrive = Environment.GetEnvironmentVariable("SystemDrive");
                if (string.IsNullOrWhiteSpace(defaultDrive)) defaultDrive = "C:";
                location = defaultDrive.TrimEnd('\\') + "\\";
            }

            return location.Trim();
        }

        private string ResolveSuggestionLocation()
        {
            // 推荐分析与左树共用同一扫描位置
            return ResolveSelectedLocation();
        }

        private void RefreshPromptForCurrentLocation()
        {
            if (settings == null || settings.Ai == null) return;

            AiSettingsPresetCatalog.AiPromptPresetOption preset = FindAiPromptPresetByPrompt(GetCurrentSystemPromptText());
            if (preset == null) return;

            pendingSystemPrompt = preset.BuildPrompt(GetPromptDriveRoot());
            settings.Ai.SystemPrompt = pendingSystemPrompt;
        }

        private string GetPromptDriveRoot()
        {
            string driveRoot = DrivePathText.GetDriveRoot(activePageId == PageSuggestions ? ResolveSuggestionLocation() : ResolveSelectedLocation());
            if (string.IsNullOrWhiteSpace(driveRoot) && currentRoot != null) driveRoot = DrivePathText.GetDriveRoot(currentRoot.Path);
            if (string.IsNullOrWhiteSpace(driveRoot)) driveRoot = "C:\\";
            return driveRoot;
        }

        private ScanRequest BuildScanRequest(string location, int loadDepth)
        {
            return new ScanRequest
            {
                Location = location,
                MinSizeBytes = ScanPageText.ParseMinSizeBytes(minSizeInput.Text, -1),
                PerLevelLimit = ParseInt(limitInput.Text, -1),
                SortMode = ResolveSelectedSizeMode(),
                LoadDepth = loadDepth
            };
        }

        private ScanRequest BuildSuggestionScanRequest(string location, int loadDepth)
        {
            return new ScanRequest
            {
                Location = location,
                MinSizeBytes = ScanPageText.ParseMinSizeBytes(suggestionMinSizeInput == null ? null : suggestionMinSizeInput.Text, 128),
                PerLevelLimit = ParseInt(suggestionLimitInput == null ? null : suggestionLimitInput.Text, -1),
                SortMode = ResolveSelectedSizeMode(),
                LoadDepth = loadDepth
            };
        }

        private static ScanRequest CreateScanRequest(string location, int loadDepth, ScanRequest template)
        {
            ScanRequest request = new ScanRequest();
            request.Location = location;
            request.SortMode = template.SortMode;
            request.MinSizeBytes = template.MinSizeBytes;
            request.PerLevelLimit = template.PerLevelLimit;
            request.LoadDepth = loadDepth;
            request.SessionIdentity = template.SessionIdentity;
            request.SessionNodeId = template.SessionNodeId;
            request.ChildStart = template.ChildStart;
            request.ChildCount = template.ChildCount;
            return request;
        }

        private ScanSortMode ResolveSelectedSizeMode()
        {
            return settings != null && settings.Scan != null ? settings.Scan.SortMode : ScanSortMode.Allocated;
        }

        private void UpdateStorageSizeColumnTitle(ScanSortMode mode)
        {
            if (storageSizeColumn == null) return;
            storageSizeColumn.Title = mode == ScanSortMode.Logical ? "实际大小" : "占用大小";
            if (storageTable != null) storageTable.Invalidate();
        }

        private static bool CanReloadStorageNode(StorageItem item)
        {
            return item != null &&
                !string.IsNullOrWhiteSpace(item.SessionIdentity) &&
                item.SessionNodeId >= 0;
        }

        private void InvalidateStorageTreeSession()
        {
            ClearScanProviderCache();
            if (currentTreeRequest == null) return;
            currentTreeRequest.SessionIdentity = null;
            currentTreeRequest.SessionNodeId = -1;
        }

        private void StorageTable_CellDoubleClick(object sender, AntdUI.TableClickEventArgs e)
        {
            StorageEntryRow row = e.Record as StorageEntryRow;
            if (row == null || row.Item == null) return;
            storageContextRow = row;
            if (row.Item.IsDirectory && row.Item.HasChildren)
            {
                bool expanded = IsStorageRowExpanded(row);
                storageTable.Expand(row, !expanded);
                return;
            }

            OpenStorageRow(row);
        }

        private void UpdateScanProgressState(string text, float value, bool loading, AntdUI.TType state)
        {
            UpdateScanProgressState(text, value, loading, state, true, true);
        }

        private void UpdateScanProgressState(string text, float value, bool loading, AntdUI.TType state, bool force)
        {
            UpdateScanProgressState(text, value, loading, state, force, false);
        }

        private void UpdateScanProgressState(string text, float value, bool loading, AntdUI.TType state, bool force, bool immediateValue)
        {
            if (value < 0F) value = 0F;
            if (value > 1F) value = 1F;
            AntdUI.TType progressState = state == AntdUI.TType.None && loading ? AntdUI.TType.Info : state;

            if (scanStatusLabel != null && (force || !string.Equals(scanState.LastScanProgressText, text, StringComparison.Ordinal)))
            {
                scanStatusLabel.Text = text;
                scanState.LastScanProgressText = text;
            }

            if (scanProgress == null) return;

            if (force || !scanState.HasLastScanProgressValue || Math.Abs(scanState.LastScanProgressValue - value) >= 0.001F)
            {
                SetScanProgressValue(value, immediateValue);
                scanState.LastScanProgressValue = value;
                scanState.HasLastScanProgressValue = true;
            }

            if (force || !scanState.HasLastScanProgressState || scanState.LastScanProgressState != progressState)
            {
                scanProgress.State = progressState;
                scanState.LastScanProgressState = progressState;
                scanState.HasLastScanProgressState = true;
            }

            if (force || !scanState.HasLastScanProgressLoading || scanState.LastScanProgressLoading != loading)
            {
                scanProgress.Loading = loading;
                scanState.LastScanProgressLoading = loading;
                scanState.HasLastScanProgressLoading = true;
            }
        }

        private void UpdateScanElapsedState(TimeSpan elapsed)
        {
            UpdateScanElapsedState(elapsed, true);
        }

        private void UpdateScanElapsedState(TimeSpan elapsed, bool force)
        {
            string text = "用时 " + ScanPageText.FormatElapsedSeconds(elapsed);
            if (!force && string.Equals(scanState.LastScanElapsedText, text, StringComparison.Ordinal)) return;
            if (scanElapsedLabel != null) scanElapsedLabel.Text = text;
            scanState.LastScanElapsedText = text;
            scanState.LastScanElapsedRenderedAt = DateTime.UtcNow;
        }

        private void StartScanProgress(string activeText, DateTime startedAt, int interval)
        {
            scanProgressStartedAt = startedAt;
            scanProgressActiveText = string.IsNullOrWhiteSpace(activeText) ? "正在扫描空间占用..." : activeText;
            ResetScanProgressRenderState();
            ResetScanProgressBarValue();

            if (scanProgressTimer == null)
            {
                scanProgressTimer = new Timer();
                scanProgressTimer.Tick += ScanProgressTimer_Tick;
            }

            scanProgressTimer.Interval = interval;
            RefreshActiveScanProgress(true);
            scanProgressTimer.Start();
        }

        private void StopScanProgress()
        {
            if (scanProgressTimer != null) scanProgressTimer.Stop();
        }

        private void ScanProgressTimer_Tick(object sender, EventArgs e)
        {
            RefreshActiveScanProgress(false);
        }

        private void RefreshActiveScanProgress(bool force)
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan elapsed = now - scanProgressStartedAt;
            float progressValue = ScanPageText.CalculateActiveProgress(elapsed);
            UpdateScanProgressState(scanProgressActiveText, progressValue, false, AntdUI.TType.Info, force);
            if (force || ShouldRefreshScanElapsed(now)) UpdateScanElapsedState(elapsed, force);
        }

        private bool ShouldRefreshScanElapsed(DateTime now)
        {
            if (scanState.LastScanElapsedRenderedAt == DateTime.MinValue) return true;
            return (now - scanState.LastScanElapsedRenderedAt).TotalMilliseconds >= ScanPageText.ElapsedTextIntervalMs;
        }

        private void ResetScanProgressRenderState()
        {
            scanState.LastScanProgressText = null;
            scanState.LastScanProgressValue = 0F;
            scanState.HasLastScanProgressValue = false;
            scanState.LastScanProgressLoading = false;
            scanState.HasLastScanProgressLoading = false;
            scanState.LastScanProgressState = AntdUI.TType.None;
            scanState.HasLastScanProgressState = false;
            scanState.LastScanElapsedText = null;
            scanState.LastScanElapsedRenderedAt = DateTime.MinValue;
        }

        private void ResetScanProgressBarValue()
        {
            SetScanProgressValue(0F, true);
        }

        private void SetScanProgressValue(float value, bool immediate)
        {
            if (scanProgress == null) return;
            if (!immediate)
            {
                scanProgress.Value = value;
                return;
            }

            int animation = scanProgress.Animation;
            scanProgress.Animation = 0;
            try
            {
                scanProgress.Value = value;
            }
            finally
            {
                scanProgress.Animation = animation;
            }
        }

        private void ClearScanProviderCache()
        {
            FolderSizeRankerScanProvider provider = scanProvider as FolderSizeRankerScanProvider;
            if (provider != null) provider.ClearCache();
        }
    }
}
