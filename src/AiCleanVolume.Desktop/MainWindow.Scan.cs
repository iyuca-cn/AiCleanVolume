using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;
using AiCleanVolume.Desktop.Controls;
using AiCleanVolume.Desktop.Services;
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
            StartScanProgress(progressText, scanStartedAt, ResolveScanProgressInterval(request.Location));
            Log("扫描开始：" + DescribeScanRequest(request));

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
                Log("扫描完成：" + result.Path + "，" + DescribeSizeMode(request.SortMode) + " " + StorageFormatting.FormatBytes(result.Bytes) + "，耗时 " + elapsed.TotalSeconds.ToString("0.00") + " 秒，子项 " + (result.Children == null ? 0 : result.Children.Count) + "。");
                if (onCompleted != null) onCompleted();
            }, delegate
            {
                TimeSpan elapsed = DateTime.UtcNow - scanStartedAt;
                StopScanProgress();
                UpdateScanProgressState("扫描失败", 1F, false, AntdUI.TType.Error);
                UpdateScanElapsedState(elapsed);
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
            if (suggestionDriveSelect != null && suggestionDriveSelect.SelectedValue != null)
            {
                string selected = suggestionDriveSelect.SelectedValue.ToString();
                if (!string.IsNullOrWhiteSpace(selected)) return selected.Trim();
            }

            return ResolveSelectedLocation();
        }

        private void RefreshPromptForCurrentLocation()
        {
            if (settings == null || settings.Ai == null) return;

            AiPromptPreset preset = FindAiPromptPresetByPrompt(GetCurrentSystemPromptText());
            if (preset == null) return;

            pendingSystemPrompt = preset.BuildPrompt(GetPromptDriveRoot());
            settings.Ai.SystemPrompt = pendingSystemPrompt;
        }

        private string GetPromptDriveRoot()
        {
            string driveRoot = TryGetDriveRoot(activePageId == PageSuggestions ? ResolveSuggestionLocation() : ResolveSelectedLocation());
            if (string.IsNullOrWhiteSpace(driveRoot) && currentRoot != null) driveRoot = TryGetDriveRoot(currentRoot.Path);
            if (string.IsNullOrWhiteSpace(driveRoot)) driveRoot = "C:\\";
            return driveRoot;
        }

        private ScanRequest BuildScanRequest(string location, int loadDepth)
        {
            return new ScanRequest
            {
                Location = location,
                MinSizeBytes = ParseMinSizeBytes(minSizeInput.Text, -1),
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
                MinSizeBytes = ParseMinSizeBytes(suggestionMinSizeInput == null ? null : suggestionMinSizeInput.Text, 128),
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
            return request;
        }

        private static string DescribeScanRequest(ScanRequest request)
        {
            if (request == null) return "<null>";
            return "location=" + request.Location + "，minSize=" + (request.MinSizeBytes < 0 ? "不限" : StorageFormatting.FormatBytes(request.MinSizeBytes)) + "，limit=" + request.PerLevelLimit + "，mode=" + DescribeSizeMode(request.SortMode) + "，loadDepth=" + request.LoadDepth + "，session=" + request.SessionIdentity + "/" + request.SessionNodeId;
        }

        private ScanSortMode ResolveSelectedSizeMode()
        {
            return sortSelect != null && sortSelect.SelectedValue is ScanSortMode ? (ScanSortMode)sortSelect.SelectedValue : ScanSortMode.Allocated;
        }

        private static string DescribeSizeMode(ScanSortMode mode)
        {
            return mode == ScanSortMode.Logical ? "实际大小" : "占用大小";
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

        private void UpdateDriveSummaryForLocation(string location)
        {
            if (selectedDriveValueLabel == null) return;

            DriveInfo drive = TryResolveDriveInfo(location);
            selectedDriveValueLabel.Text = BuildDriveDisplayText(drive, location);

            if (drive == null)
            {
                SetDriveSummaryValue(totalSpaceValueLabel, "-");
                SetDriveSummaryValue(usedSpaceValueLabel, "-");
                SetDriveSummaryValue(availableSpaceValueLabel, "-");
                SetDriveSummaryValue(reservedSpaceValueLabel, "-");
                return;
            }

            try
            {
                if (!drive.IsReady)
                {
                    SetDriveSummaryValue(totalSpaceValueLabel, "-");
                    SetDriveSummaryValue(usedSpaceValueLabel, "-");
                    SetDriveSummaryValue(availableSpaceValueLabel, "-");
                    SetDriveSummaryValue(reservedSpaceValueLabel, "-");
                    return;
                }

                long totalBytes = drive.TotalSize;
                long availableBytes = drive.AvailableFreeSpace;
                long reservedBytes = Math.Max(0L, drive.TotalFreeSpace - availableBytes);
                long usedBytes = Math.Max(0L, totalBytes - drive.TotalFreeSpace);

                SetDriveSummaryValue(totalSpaceValueLabel, StorageFormatting.FormatBytes(totalBytes));
                SetDriveSummaryValue(usedSpaceValueLabel, FormatBytesWithPercent(usedBytes, totalBytes));
                SetDriveSummaryValue(availableSpaceValueLabel, FormatBytesWithPercent(availableBytes, totalBytes));
                SetDriveSummaryValue(reservedSpaceValueLabel, StorageFormatting.FormatBytes(reservedBytes));
            }
            catch
            {
                SetDriveSummaryValue(totalSpaceValueLabel, "-");
                SetDriveSummaryValue(usedSpaceValueLabel, "-");
                SetDriveSummaryValue(availableSpaceValueLabel, "-");
                SetDriveSummaryValue(reservedSpaceValueLabel, "-");
            }
        }

        private void UpdateScanProgressState(string text, float value, bool loading, AntdUI.TType state)
        {
            if (scanStatusLabel != null) scanStatusLabel.Text = text;
            if (value < 0F) value = 0F;
            if (value > 1F) value = 1F;
            if (scanProgress == null) return;
            scanProgress.Value = value;
            scanProgress.State = state == AntdUI.TType.None && loading ? AntdUI.TType.Info : state;
            scanProgress.Loading = loading;
        }

        private void UpdateScanElapsedState(TimeSpan elapsed)
        {
            if (scanElapsedLabel != null) scanElapsedLabel.Text = "用时 " + FormatElapsedSeconds(elapsed);
        }

        private void StartScanProgress(string activeText, DateTime startedAt, int interval)
        {
            scanProgressStartedAt = startedAt;
            scanProgressActiveText = string.IsNullOrWhiteSpace(activeText) ? "正在扫描空间占用..." : activeText;

            if (scanProgressTimer == null)
            {
                scanProgressTimer = new Timer();
                scanProgressTimer.Tick += ScanProgressTimer_Tick;
            }

            scanProgressTimer.Interval = interval;
            RefreshActiveScanProgress();
            scanProgressTimer.Start();
        }

        private void StopScanProgress()
        {
            if (scanProgressTimer != null) scanProgressTimer.Stop();
        }

        private void ScanProgressTimer_Tick(object sender, EventArgs e)
        {
            RefreshActiveScanProgress();
        }

        private void RefreshActiveScanProgress()
        {
            TimeSpan elapsed = DateTime.UtcNow - scanProgressStartedAt;
            float progressValue = CalculateActiveScanProgress(elapsed);
            UpdateScanProgressState(scanProgressActiveText, progressValue, true, AntdUI.TType.None);
            UpdateScanElapsedState(elapsed);
        }

        private static float CalculateActiveScanProgress(TimeSpan elapsed)
        {
            double seconds = Math.Max(0D, elapsed.TotalSeconds);
            double value = 0.05D + 0.90D * (1D - Math.Exp(-seconds / 8D));
            if (value < 0.05D) value = 0.05D;
            if (value > 0.95D) value = 0.95D;
            return (float)value;
        }

        private static string FormatElapsedSeconds(TimeSpan elapsed)
        {
            return elapsed.TotalSeconds.ToString("0.0") + " 秒";
        }

        private static int ResolveScanProgressInterval(string location)
        {
            DriveInfo drive = TryResolveDriveInfo(location);
            if (drive == null) return DefaultScanProgressIntervalMs;

            try
            {
                if (drive.IsReady && string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
                {
                    return NtfsScanProgressIntervalMs;
                }
            }
            catch
            {
            }

            return DefaultScanProgressIntervalMs;
        }

        private static string FormatBytesWithPercent(long bytes, long totalBytes)
        {
            if (totalBytes <= 0) return StorageFormatting.FormatBytes(bytes);
            double percent = (double)bytes / totalBytes * 100D;
            return StorageFormatting.FormatBytes(bytes) + " (" + percent.ToString("0.0") + "%)";
        }

        private static void SetDriveSummaryValue(AntdUI.Label label, string text)
        {
            if (label != null) label.Text = text;
        }

        private static string BuildDriveDisplayText(DriveInfo drive, string location)
        {
            string root = drive != null ? drive.Name : TryGetDriveRoot(location);
            if (string.IsNullOrWhiteSpace(root)) return "-";

            string volumeLabel = "本地磁盘";
            if (drive != null)
            {
                try
                {
                    if (drive.IsReady && !string.IsNullOrWhiteSpace(drive.VolumeLabel)) volumeLabel = drive.VolumeLabel;
                }
                catch
                {
                    volumeLabel = "本地磁盘";
                }
            }

            return "[" + root.TrimEnd('\\') + "] " + volumeLabel;
        }

        private static DriveInfo TryResolveDriveInfo(string location)
        {
            string root = TryGetDriveRoot(location);
            if (string.IsNullOrWhiteSpace(root)) return null;

            DriveInfo[] drives;
            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch
            {
                return null;
            }

            for (int i = 0; i < drives.Length; i++)
            {
                if (string.Equals(drives[i].Name, root, StringComparison.OrdinalIgnoreCase)) return drives[i];
            }
            return null;
        }

        private static string TryGetDriveRoot(string location)
        {
            if (string.IsNullOrWhiteSpace(location)) return null;
            string value = location.Trim();

            try
            {
                string root = Path.GetPathRoot(value);
                if (!string.IsNullOrWhiteSpace(root)) return root;
            }
            catch
            {
            }

            if (value.Length >= 2 && value[1] == ':')
            {
                return char.ToUpperInvariant(value[0]) + ":\\"; 
            }
            return null;
        }

        private static long ParseMinSizeBytes(string text, int fallbackMb)
        {
            int sizeMb = ParseInt(text, fallbackMb);
            return sizeMb < 0 ? -1L : sizeMb * 1024L * 1024L;
        }

        private void ClearScanProviderCache()
        {
            FolderSizeRankerScanProvider provider = scanProvider as FolderSizeRankerScanProvider;
            if (provider != null) provider.ClearCache();
        }
    }
}
