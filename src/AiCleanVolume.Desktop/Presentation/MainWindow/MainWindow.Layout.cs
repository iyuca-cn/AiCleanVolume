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
        private AntdUI.Panel CreatePageContainer()
        {
            AntdUI.Panel page = CreateFlatPanel();
            page.Dock = DockStyle.Fill;
            page.BackColor = PageBackground;
            return page;
        }

        private static AntdUI.PageHeader CreatePageHeader(string title, string description)
        {
            AntdUI.PageHeader header = new AntdUI.PageHeader();
            header.Dock = DockStyle.Top;
            header.Height = 72;
            header.Text = title;
            header.Description = description;
            header.UseTitleFont = true;
            header.ShowButton = false;
            header.ShowIcon = false;
            header.DividerShow = true;
            header.DividerMargin = 2;
            header.DividerColor = Color.FromArgb(230, 230, 230);
            header.BackColor = Color.Transparent;
            header.Padding = new Padding(8, 4, 8, 10);
            return header;
        }

        private static AntdUI.Divider CreateSectionDivider(string text)
        {
            AntdUI.Divider divider = new AntdUI.Divider();
            divider.Dock = DockStyle.Top;
            divider.Height = 28;
            divider.Text = text;
            divider.Font = new Font("Segoe UI", 10F);
            divider.Orientation = AntdUI.TOrientation.Left;
            divider.Margin = new Padding(0, 4, 0, 4);
            return divider;
        }

        private AntdUI.Panel CreateSidebarHost()
        {
            AntdUI.Panel host = CreateFlatPanel();
            host.Dock = DockStyle.Left;
            host.Width = SidebarMinWidth + SidebarRailWidth;
            host.BackColor = SurfaceColor;

            sidebarResizeRail = CreateFlatPanel();
            sidebarResizeRail.Dock = DockStyle.Right;
            sidebarResizeRail.Width = SidebarRailWidth;
            sidebarResizeRail.BackColor = PageBackground;
            sidebarResizeRail.Cursor = Cursors.VSplit;
            sidebarResizeRail.MouseDown += SidebarResizeRail_MouseDown;
            sidebarResizeRail.MouseMove += SidebarResizeRail_MouseMove;
            sidebarResizeRail.MouseUp += SidebarResizeRail_MouseUp;
            sidebarResizeRail.MouseCaptureChanged += SidebarResizeRail_MouseCaptureChanged;

            sidebarPanel = new AntdUI.Panel();
            sidebarPanel.Dock = DockStyle.Fill;
            sidebarPanel.Back = Color.White;
            sidebarPanel.BorderWidth = 0F;
            sidebarPanel.Radius = 0;
            sidebarPanel.Shadow = 0;
            sidebarPanel.Padding = new Padding(14, 12, 14, 14);
            sidebarPanel.ColorScheme = AntdUI.TAMode.Light;

            sidebarCollapseButton = CreateSidebarCollapseButton();

            AntdUI.Panel footerPanel = CreateSidebarFooterPanel();
            AntdUI.Panel dividerPanel = CreateFlatPanel();
            dividerPanel.Dock = DockStyle.Bottom;
            dividerPanel.Height = 1;
            dividerPanel.BackColor = Palette.Divider;

            navigationMenu = CreateSidebarMenu();
            sidebarBrandPanel = CreateSidebarBrandPanel();

            sidebarPanel.Controls.Add(navigationMenu);
            sidebarPanel.Controls.Add(dividerPanel);
            sidebarPanel.Controls.Add(footerPanel);
            sidebarPanel.Controls.Add(sidebarBrandPanel);

            host.Controls.Add(sidebarPanel);
            host.Controls.Add(sidebarResizeRail);
            host.Controls.Add(sidebarCollapseButton);
            sidebarCollapseButton.BringToFront();
            return host;
        }

        private AntdUI.Panel CreateSidebarBrandPanel()
        {
            AntdUI.Panel brandPanel = CreateFlatPanel();
            brandPanel.Dock = DockStyle.Top;
            brandPanel.Height = 76;
            brandPanel.Padding = new Padding(10, 14, 10, 14);
            brandPanel.BackColor = Color.Transparent;

            AntdUI.Panel iconContainer = new AntdUI.Panel();
            iconContainer.Dock = DockStyle.Left;
            iconContainer.Width = 42;
            iconContainer.Height = 42;
            iconContainer.Radius = 10;
            iconContainer.Back = Color.FromArgb(22, 119, 255);
            iconContainer.BorderWidth = 0F;
            iconContainer.Shadow = 6;
            iconContainer.ShadowOpacity = 0.2F;
            iconContainer.ShadowColor = Color.FromArgb(22, 119, 255);

            sidebarBrandIconLabel = new AntdUI.Label();
            sidebarBrandIconLabel.Dock = DockStyle.Fill;
            sidebarBrandIconLabel.Text = "AI";
            sidebarBrandIconLabel.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            sidebarBrandIconLabel.ForeColor = Color.White;
            sidebarBrandIconLabel.BackColor = Color.Transparent;
            sidebarBrandIconLabel.TextAlign = ContentAlignment.MiddleCenter;

            sidebarBrandTextLabel = new AntdUI.Label();
            sidebarBrandTextLabel.Dock = DockStyle.Fill;
            sidebarBrandTextLabel.Width = 156;
            sidebarBrandTextLabel.Text = AppDisplayName;
            sidebarBrandTextLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            sidebarBrandTextLabel.ForeColor = Color.FromArgb(30, 30, 30);
            sidebarBrandTextLabel.BackColor = Color.Transparent;
            sidebarBrandTextLabel.AutoEllipsis = true;
            sidebarBrandTextLabel.TextAlign = ContentAlignment.MiddleLeft;
            sidebarBrandTextLabel.Padding = new Padding(12, 0, 0, 0);

            iconContainer.Controls.Add(sidebarBrandIconLabel);
            brandPanel.Controls.Add(sidebarBrandTextLabel);
            brandPanel.Controls.Add(iconContainer);
            return brandPanel;
        }

        private AntdUI.Button CreateSidebarCollapseButton()
        {
            AntdUI.Button button = new AntdUI.Button();
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.DisplayStyle = AntdUI.TButtonDisplayStyle.Image;
            button.Shape = AntdUI.TShape.Round;
            button.Radius = 10;
            button.IconSvg = "ArrowLeftOutlined";
            button.Type = AntdUI.TTypeMini.Default;
            button.Ghost = false;
            button.BorderWidth = 1F;
            button.DefaultBorderColor = BorderLightColor;
            button.BackColor = SurfaceColor;
            button.ForeColor = TextSecondaryColor;
            button.Width = 22;
            button.Height = 32;
            button.IconRatio = 0.58F;
            button.WaveSize = 1;
            button.Click += delegate { ToggleSidebarCollapsed(); };
            return button;
        }

        private AntdUI.Menu CreateSidebarMenu()
        {
            AntdUI.Menu menu = new AntdUI.Menu();
            menu.Dock = DockStyle.Fill;
            menu.Mode = AntdUI.TMenuMode.Inline;
            menu.Unique = true;
            menu.Radius = 8;
            menu.Indent = false;
            menu.Gap = 4;
            menu.IconGap = 10;
            menu.itemMargin = 4;
            menu.IconRatio = 1.1F;
            menu.Padding = new Padding(4, 8, 4, 8);
            menu.ForeColor = Palette.TextSecondary;
            menu.BackColor = Color.Transparent;
            menu.BackHover = Palette.RowHover;
            menu.BackActive = Palette.Accent;
            menu.ForeActive = Color.White;
            menu.ColorScheme = AntdUI.TAMode.Light;
            menu.ScrollBarBlock = true;
            menu.SelectChanged += NavigationMenu_SelectChanged;

            AntdUI.MenuItem scanItem = CreateNavigationItem(PageScan, "扫描", "FolderOpenOutlined");
            scanItem.Select = true;
            menu.Items.Add(scanItem);
            menu.Items.Add(CreateNavigationItem(PageSuggestions, "清理建议", "RobotFilled"));
            menu.Items.Add(new AntdUI.MenuDividerItem());
            menu.Items.Add(CreateNavigationItem(PageLog, "日志管理", "FileTextOutlined"));
            return menu;
        }

        private AntdUI.Panel CreateSidebarFooterPanel()
        {
            AntdUI.Panel footerPanel = CreateFlatPanel();
            footerPanel.Dock = DockStyle.Bottom;
            footerPanel.Height = 68;
            footerPanel.Padding = new Padding(10, 10, 10, 10);
            footerPanel.BackColor = Color.Transparent;

            settingsNavButton = new AntdUI.Button();
            settingsNavButton.Dock = DockStyle.Fill;
            settingsNavButton.Width = 44;
            settingsNavButton.Height = 44;
            settingsNavButton.IconSvg = "SettingOutlined";
            settingsNavButton.Text = null;
            settingsNavButton.DisplayStyle = AntdUI.TButtonDisplayStyle.Image;
            settingsNavButton.IconRatio = 0.88F;
            settingsNavButton.Radius = 8;
            settingsNavButton.Type = AntdUI.TTypeMini.Default;
            settingsNavButton.BorderWidth = 0F;
            settingsNavButton.Ghost = true;
            settingsNavButton.WaveSize = 2;
            settingsNavButton.ForeColor = Color.FromArgb(100, 100, 100);
            settingsNavButton.BackColor = Color.Transparent;
            settingsNavButton.DefaultBorderColor = Color.Transparent;
            settingsNavButton.Click += SettingsNavButton_Click;

            footerPanel.Controls.Add(settingsNavButton);
            return footerPanel;
        }

        private void RefreshSettingsPageLayout(bool resetScroll)
        {
            if (settingsScrollHost == null) return;

            if (settingsPage != null) settingsPage.SuspendLayout();
            if (settingsContentLayout != null)
            {
                settingsContentLayout.Width = Math.Max(720, settingsScrollHost.ClientSize.Width - 12);
                settingsContentLayout.Height = 944;
                settingsContentLayout.PerformLayout();
            }

            ResizeAiProfileCards();

            if (resetScroll && settingsScrollHost.ScrollBar != null && settingsScrollHost.ScrollBar.ValueY != 0)
            {
                settingsScrollHost.ScrollBar.ValueY = 0;
            }

            settingsScrollHost.PerformLayout();
            if (settingsPage != null) settingsPage.PerformLayout();
            if (settingsPage != null) settingsPage.ResumeLayout(true);
            InvalidateSettingsPageControls(settingsPage);
            if (settingsContentLayout != null) settingsContentLayout.Invalidate(true);
            settingsScrollHost.Invalidate(true);
            if (settingsPage != null) settingsPage.Update();
        }

        private static void InvalidateSettingsPageControls(Control control)
        {
            if (control == null) return;
            control.Invalidate(true);
            foreach (Control child in control.Controls)
            {
                InvalidateSettingsPageControls(child);
            }
        }

        private static AntdUI.MenuItem CreateNavigationItem(string id, string text, string iconSvg)
        {
            AntdUI.MenuItem item = new AntdUI.MenuItem(text);
            item.ID = id;
            item.IconSvg = iconSvg;
            return item;
        }
    }
}
