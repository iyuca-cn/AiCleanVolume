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
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.ViewModels;


namespace AiCleanVolume.Desktop
{
    public sealed partial class MainWindow : AntdUI.Window
    {
        private void RunBackground(string caption, Action action, Action onSuccess)
        {
            backgroundOperationRunner.Run(caption, action, onSuccess);
        }

        private void RunBackground(string caption, Action action, Action onSuccess, Action onError)
        {
            backgroundOperationRunner.Run(caption, action, onSuccess, onError);
        }

        public void ShowInfo(string title, string message)
        {
            ShowNotice(title, message, AntdUI.TType.Info);
        }

        public void ShowWarning(string title, string message)
        {
            ShowNotice(title, message, AntdUI.TType.Warn);
        }

        public void ShowError(string title, string message)
        {
            ShowNotice(title, message, AntdUI.TType.Error);
        }

        private void ShowNotice(string title, string message, AntdUI.TType icon)
        {
            AntdUI.Modal.open(this, title, message ?? string.Empty, icon);
        }

        public void SetBusy(bool busy, string description)
        {
            this.busy = busy;
            UseWaitCursor = busy;
            if (appBar != null) appBar.Loading = busy;
            if (settingsToolbarButton != null) settingsToolbarButton.Enabled = !busy;
            if (toolbarPrivilegeSwitch != null) toolbarPrivilegeSwitch.Enabled = !busy;
            scanButton.Enabled = !busy;
            scanButton.Loading = busy && activePageId == PageScan;
            if (driveSelect != null) driveSelect.Enabled = !busy;
            if (pathInput != null) pathInput.Enabled = !busy;
            if (minSizeInput != null) minSizeInput.Enabled = !busy;
            if (limitInput != null) limitInput.Enabled = !busy;
            if (suggestionDriveSelect != null) suggestionDriveSelect.Enabled = !busy;
            if (suggestionMinSizeInput != null) suggestionMinSizeInput.Enabled = !busy;
            if (suggestionLimitInput != null) suggestionLimitInput.Enabled = !busy;
            analyzeButton.Enabled = !busy;
            regularCleanButton.Enabled = !busy;
            superCleanButton.Enabled = !busy;
            analyzeButton.Loading = busy && activePageId == PageSuggestions;
            regularCleanButton.Loading = busy && activePageId == PageSuggestions;
            superCleanButton.Loading = busy && activePageId == PageSuggestions;
            if (suggestionPromptButton != null) suggestionPromptButton.Enabled = !busy;
            deleteButton.Enabled = !busy;
            saveSettingsButton.Enabled = !busy;
            if (testAiSettingsButton != null) testAiSettingsButton.Enabled = !busy;
            if (applyAiProfileButton != null) applyAiProfileButton.Enabled = !busy;
            if (addAiProfileButton != null) addAiProfileButton.Enabled = !busy;
            if (saveAiProfilePageButton != null) saveAiProfilePageButton.Enabled = !busy;
            if (cancelAiProfilePageButton != null) cancelAiProfilePageButton.Enabled = !busy;
            if (backAiProfilePageButton != null) backAiProfilePageButton.Enabled = !busy;
            if (aiProfileListPanel != null) aiProfileListPanel.Enabled = !busy;
            if (selectAllSuggestionsButton != null) selectAllSuggestionsButton.Enabled = !busy;
            if (clearAllSuggestionsButton != null) clearAllSuggestionsButton.Enabled = !busy;
            if (invertSuggestionsButton != null) invertSuggestionsButton.Enabled = !busy;
            if (privilegedCheckbox != null) privilegedCheckbox.Enabled = !busy;
            if (privilegedQuickCheckbox != null) privilegedQuickCheckbox.Enabled = !busy;
        }

        private void StartDeletionProgressDisplay(DeletionProgressState progress)
        {
            if (progress == null) return;

            activeDeletionProgress = progress;
            activeDeletionProgress.Reset();
            lastDeletionProgressVersion = -1L;
            lastDeletionProgressText = null;

            if (deletionProgressTimer == null)
            {
                deletionProgressTimer = new Timer();
                deletionProgressTimer.Tick += DeletionProgressTimer_Tick;
            }

            deletionProgressTimer.Interval = DeletionProgressTimerIntervalMs;
            SetDeletionProgressSubText("正在删除...");
            deletionProgressTimer.Start();
        }

        private void StopDeletionProgressDisplay()
        {
            if (deletionProgressTimer != null) deletionProgressTimer.Stop();
            activeDeletionProgress = null;
            lastDeletionProgressVersion = 0L;
            lastDeletionProgressText = null;

            if (appBar != null)
            {
                string pageId = string.IsNullOrWhiteSpace(activePageId) ? PageScan : activePageId;
                appBar.SubText = GetPageTitle(pageId);
            }
        }

        private void DeletionProgressTimer_Tick(object sender, EventArgs e)
        {
            DeletionProgressState progress = activeDeletionProgress;
            if (progress == null) return;

            DeletionProgressSnapshot snapshot = progress.Read();
            if (snapshot == null || snapshot.Version == lastDeletionProgressVersion) return;

            lastDeletionProgressVersion = snapshot.Version;
            SetDeletionProgressSubText(BuildDeletionProgressSubText(snapshot));
        }

        private void SetDeletionProgressSubText(string text)
        {
            if (appBar == null) return;
            if (string.Equals(lastDeletionProgressText, text, StringComparison.Ordinal)) return;

            lastDeletionProgressText = text;
            appBar.SubText = text;
        }

        private static string BuildDeletionProgressSubText(DeletionProgressSnapshot snapshot)
        {
            string stage = snapshot == null || string.IsNullOrWhiteSpace(snapshot.Stage) ? "正在删除" : snapshot.Stage;
            string path = snapshot == null ? string.Empty : CompactDeletionPath(snapshot.Path, 132);
            if (string.IsNullOrWhiteSpace(path)) return stage + "...";
            return stage + "：" + path;
        }

        private static string CompactDeletionPath(string path, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length <= maxLength) return path ?? string.Empty;

            int headLength = Math.Min(48, Math.Max(12, maxLength / 3));
            int tailLength = maxLength - headLength - 5;
            if (tailLength < 24)
            {
                tailLength = Math.Max(12, maxLength / 2);
                headLength = Math.Max(8, maxLength - tailLength - 5);
            }

            return path.Substring(0, headLength) + " ... " + path.Substring(path.Length - tailLength);
        }

        public void Log(string message)
        {
            if (logInput == null) return;
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message;
            if (logPageFeature != null) logPageFeature.Append(line);
            else if (string.IsNullOrWhiteSpace(logInput.Text)) logInput.Text = line;
            else logInput.Text += Environment.NewLine + line;
        }

        public void LogBackground(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || IsDisposed) return;

            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!IsDisposed) Log(message);
                });
                return;
            }

            Log(message);
        }
    }
}
