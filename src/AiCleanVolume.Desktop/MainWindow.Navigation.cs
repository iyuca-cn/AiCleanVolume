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
        private void SidebarResizeRail_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (sidebarCollapsed)
            {
                SetSidebarCollapsed(false);
                return;
            }

            sidebarResizing = true;
            sidebarResizeStartX = Cursor.Position.X;
            sidebarResizeStartWidth = sidebarWidth > 0 ? sidebarWidth : ResolveInitialSidebarWidth();
            if (sidebarResizeRail != null) sidebarResizeRail.Capture = true;
        }

        private void SidebarResizeRail_MouseMove(object sender, MouseEventArgs e)
        {
            if (!sidebarResizing) return;
            int targetWidth = sidebarResizeStartWidth + (Cursor.Position.X - sidebarResizeStartX);
            ApplySidebarWidth(targetWidth);
        }

        private void SidebarResizeRail_MouseUp(object sender, MouseEventArgs e)
        {
            FinishSidebarResize();
        }

        private void SidebarResizeRail_MouseCaptureChanged(object sender, EventArgs e)
        {
            FinishSidebarResize();
        }

        private void FinishSidebarResize()
        {
            if (!sidebarResizing) return;
            sidebarResizing = false;
            if (sidebarResizeRail != null) sidebarResizeRail.Capture = false;
            PersistSidebarWidth();
        }

        private void SettingsNavButton_Click(object sender, EventArgs e)
        {
            SetActivePage(PageSettings);
        }

        private void ApplySidebarWidth(int width)
        {
            sidebarWidth = ClampSidebarWidth(width);
            expandedSidebarWidth = sidebarWidth;
            if (!sidebarCollapsed) ApplySidebarVisualState();
            UpdateSettingsNavigationState();
        }

        private void ToggleSidebarCollapsed()
        {
            SetSidebarCollapsed(!sidebarCollapsed);
        }

        private void SetSidebarCollapsed(bool collapsed)
        {
            sidebarCollapsed = collapsed;
            if (!sidebarCollapsed && expandedSidebarWidth <= 0) expandedSidebarWidth = ResolveInitialSidebarWidth();
            ApplySidebarVisualState();
            UpdateSettingsNavigationState();
        }

        private void ApplySidebarVisualState()
        {
            int visualWidth = sidebarCollapsed ? SidebarCollapsedWidth : (expandedSidebarWidth > 0 ? expandedSidebarWidth : ResolveInitialSidebarWidth());
            if (!sidebarCollapsed) sidebarWidth = visualWidth;

            if (sidebarHost != null) sidebarHost.Width = visualWidth + SidebarRailWidth;
            if (sidebarResizeRail != null)
            {
                sidebarResizeRail.Visible = !sidebarCollapsed;
                sidebarResizeRail.Enabled = !busy && !sidebarCollapsed;
            }

            if (sidebarBrandPanel != null)
            {
                sidebarBrandPanel.Height = sidebarCollapsed ? 58 : 70;
                sidebarBrandPanel.Padding = sidebarCollapsed ? new Padding(8, 10, 8, 10) : new Padding(6, 10, 6, 14);
            }

            if (sidebarBrandIconLabel != null)
            {
                sidebarBrandIconLabel.Width = sidebarCollapsed ? 48 : 32;
                sidebarBrandIconLabel.TextAlign = sidebarCollapsed ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            }

            if (sidebarBrandTextLabel != null)
            {
                sidebarBrandTextLabel.Visible = !sidebarCollapsed;
            }

            if (sidebarPanel != null) sidebarPanel.Padding = sidebarCollapsed ? new Padding(8, 12, 8, 14) : new Padding(14, 12, 14, 14);

            if (navigationMenu != null)
            {
                navigationMenu.IconGap = sidebarCollapsed ? 0 : 10;
                navigationMenu.Padding = sidebarCollapsed ? new Padding(0, 6, 0, 6) : new Padding(2, 6, 2, 6);
                SetNavigationText(PageScan, sidebarCollapsed ? string.Empty : "扫描");
                SetNavigationText(PageSuggestions, sidebarCollapsed ? string.Empty : "清理建议");
                SetNavigationText(PageLog, sidebarCollapsed ? string.Empty : "日志管理");
            }

            if (settingsNavButton != null)
            {
                settingsNavButton.Dock = DockStyle.Fill;
                settingsNavButton.Text = null;
                settingsNavButton.DisplayStyle = AntdUI.TButtonDisplayStyle.Image;
            }

            if (settingsNavButton != null && settingsNavButton.Parent != null)
            {
                settingsNavButton.Parent.Padding = sidebarCollapsed ? new Padding(8, 10, 8, 10) : new Padding(10, 10, 10, 10);
                settingsNavButton.Left = 0;
                settingsNavButton.Top = 0;
            }

            if (sidebarCollapseButton != null && sidebarHost != null)
            {
                sidebarCollapseButton.IconSvg = sidebarCollapsed ? "ArrowRightOutlined" : "ArrowLeftOutlined";
                int sidebarContentRight = sidebarHost.Width - SidebarRailWidth;
                sidebarCollapseButton.Left = Math.Max(0, sidebarCollapsed ? sidebarContentRight - sidebarCollapseButton.Width / 2 : sidebarContentRight - sidebarCollapseButton.Width);
                sidebarCollapseButton.Top = 96;
                sidebarCollapseButton.BringToFront();
            }
        }

        private void SetNavigationText(string id, string text)
        {
            if (navigationMenu == null) return;
            AntdUI.MenuItem item = navigationMenu.FindID(id);
            if (item != null) item.Text = text;
        }

        private void NavigationMenu_SelectChanged(object sender, AntdUI.MenuSelectEventArgs e)
        {
            if (e.Value == null || string.IsNullOrWhiteSpace(e.Value.ID)) return;
            if (activePageId == e.Value.ID) return;
            SetActivePage(e.Value.ID);
        }

        private void SyncNavigationSelection(string pageId)
        {
            if (navigationMenu == null) return;
            if (pageId == PageSettings)
            {
                navigationMenu.USelect();
                return;
            }
            AntdUI.MenuItem item = navigationMenu.FindID(pageId);
            if (item == null)
            {
                navigationMenu.USelect();
                return;
            }
            if (navigationMenu.SelectItem == item) return;
            navigationMenu.Select(item, false);
        }

        private void UpdateSettingsNavigationState()
        {
            if (settingsNavButton == null) return;
            bool selected = activePageId == PageSettings;
            settingsNavButton.BackColor = selected ? PrimarySoftColor : SurfaceColor;
            settingsNavButton.DefaultBorderColor = selected ? Color.FromArgb(145, 202, 255) : BorderLightColor;
            settingsNavButton.ForeColor = selected ? PrimaryColor : TextSecondaryColor;
            settingsNavButton.IconSvg = "SettingOutlined";
        }

        private void SetActivePage(string pageId)
        {
            string previousPageId = activePageId;
            if (previousPageId == pageId) return;
            bool compactStorageTree = previousPageId == PageScan && pageId != PageScan;

            SuspendControlRedraw(this);
            SuspendPageSwitchLayout();
            try
            {
                activePageId = pageId;

                Control activePage = GetPageControl(pageId);
                if (activePage != null)
                {
                    activePage.Visible = true;
                    activePage.BringToFront();
                }

                scanPage.Visible = pageId == PageScan;
                suggestionsPage.Visible = pageId == PageSuggestions;
                logPage.Visible = pageId == PageLog;
                settingsPage.Visible = pageId == PageSettings;
                aiProfileCreatePage.Visible = pageId == PageAiProfileCreate;

                if (compactStorageTree) CompactStorageTreeRowsForNavigation();

                appBar.SubText = string.Empty;

                SyncNavigationSelection(pageId);
                UpdateSettingsNavigationState();
            }
            finally
            {
                ResumePageSwitchLayout();
                ResumeControlRedraw(this);
            }
        }

        private void CompactStorageTreeRowsForNavigation()
        {
            if (storageTable == null || currentRoot == null) return;

            expandedStoragePaths.Clear();
            RebindStorageTree();
        }

        private Control GetPageControl(string pageId)
        {
            switch (pageId)
            {
                case PageSuggestions:
                    return suggestionsPage;
                case PageLog:
                    return logPage;
                case PageSettings:
                    return settingsPage;
                case PageAiProfileCreate:
                    return aiProfileCreatePage;
                default:
                    return scanPage;
            }
        }

        private void SuspendPageSwitchLayout()
        {
            SuspendLayout();
            if (pageHost != null) pageHost.SuspendLayout();
        }

        private void ResumePageSwitchLayout()
        {
            if (pageHost != null) pageHost.ResumeLayout(true);
            ResumeLayout(true);
        }

        private static string GetPageTitle(string pageId)
        {
            switch (pageId)
            {
                case PageSuggestions:
                    return "清理建议";
                case PageLog:
                    return "日志管理";
                case PageSettings:
                    return "设置界面";
                case PageAiProfileCreate:
                    return "新增 AI 配置";
                default:
                    return "扫描界面";
            }
        }

        private static string GetPageDescription(string pageId)
        {
            switch (pageId)
            {
                case PageSuggestions:
                    return "查看常规路径、超级扫描和 AI 生成的清理建议，支持定位和批量删除。";
                case PageLog:
                    return "查看扫描、建议与删除流程的执行日志。";
                case PageSettings:
                    return "配置标准 API / 2API、建议数量、沙盒白名单和删除策略。";
                case PageAiProfileCreate:
                    return "创建一套新的 AI 接入配置，保存后回到设置页配置列表。";
                default:
                    return "选择磁盘或目录，扫描空间占用，并快速进入空间树分析。";
            }
        }

        private string GetActivePageDescription()
        {
            return GetPageDescription(string.IsNullOrWhiteSpace(activePageId) ? PageScan : activePageId);
        }

        private int ResolveInitialSidebarWidth()
        {
            if (settings != null && settings.Ui != null && settings.Ui.SidebarWidth > 0)
            {
                return ClampSidebarWidth(settings.Ui.SidebarWidth);
            }

            return CalculateAutoSidebarWidth();
        }

        private int CalculateAutoSidebarWidth()
        {
            int brandWidth = MeasureTextWidth(AppDisplayName, sidebarBrandTextLabel != null ? sidebarBrandTextLabel.Font : Font) + 36;
            int menuTextWidth = 0;
            string[] menuItems = { "扫描", "清理建议", "日志管理" };
            Font menuFont = navigationMenu != null ? navigationMenu.Font : Font;
            for (int i = 0; i < menuItems.Length; i++)
            {
                menuTextWidth = Math.Max(menuTextWidth, MeasureTextWidth(menuItems[i], menuFont));
            }

            int menuWidth = menuTextWidth + 96;
            return ClampSidebarWidth(Math.Max(brandWidth, menuWidth));
        }

        private void PersistSidebarWidth()
        {
            if (settings == null) return;
            settings.Ui.SidebarWidth = expandedSidebarWidth > 0 ? expandedSidebarWidth : sidebarWidth;
            settingsStore.Save(settings);
        }

        private static int ClampSidebarWidth(int width)
        {
            return Math.Max(SidebarMinWidth, Math.Min(SidebarMaxWidth, width));
        }
    }
}
