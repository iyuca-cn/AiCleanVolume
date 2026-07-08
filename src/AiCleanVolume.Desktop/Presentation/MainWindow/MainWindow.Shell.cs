using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using AiCleanVolume.Desktop.Infrastructure.Windows;
using AiCleanVolume.Desktop.Presentation.Shared;

namespace AiCleanVolume.Desktop
{
    // 单屏外壳：深色标题栏 + 工具栏 + 提升横幅 + 三栏主体 + 深色状态栏。
    // 设置 / 新增 AI 配置 / 日志暂以整页覆盖三栏区域，后续迁往 Modal / Drawer。
    public sealed partial class MainWindow : AntdUI.Window
    {
        private AntdUI.Panel toolbarPanel;

        private AntdUI.Panel elevationBanner;

        private AntdUI.Panel statusBarPanel;

        private AntdUI.Panel columnsHost;

        private AntdUI.Panel leftColumnPanel;

        private AntdUI.Panel centerColumnPanel;

        private AntdUI.Panel rightColumnPanel;

        private AntdUI.Switch toolbarPrivilegeSwitch;

        private AntdUI.Tag privilegeWarningTag;

        private AntdUI.Tag aiStatusTag;

        private AntdUI.Button settingsToolbarButton;

        private AntdUI.Label statusStateLabel;

        private AntdUI.Label statusDriveLabel;

        private void BuildAppBar()
        {
            appBar = new AntdUI.PageHeader();
            appBar.Dock = DockStyle.Top;
            appBar.ColorScheme = AntdUI.TAMode.Dark;
            appBar.BackExtend = "#10202C, #10202C";
            appBar.BackColor = Palette.TitleBar;
            appBar.ForeColor = Palette.TitleBarText;
            appBar.UseSystemStyleColor = true;
            appBar.UseForeColorDrawIcons = true;
            appBar.ShowButton = true;
            appBar.ShowIcon = true;
            appBar.IconSvg = "RobotFilled";
            appBar.IconRatio = 0.6F;
            appBar.UseTitleFont = false;
            appBar.DividerShow = false;
            appBar.Padding = new Padding(14, 0, 0, 0);
            appBar.Text = AppDisplayName;
            appBar.SubText = "v" + typeof(MainWindow).Assembly.GetName().Version.ToString(3);
            appBar.Height = 42;
        }

        private AntdUI.Panel BuildToolbar()
        {
            toolbarPanel = CreateFlatPanel();
            toolbarPanel.Dock = DockStyle.Top;
            toolbarPanel.Height = 60;
            toolbarPanel.BackColor = Palette.Surface;
            toolbarPanel.Padding = new Padding(16, 12, 16, 12);

            AntdUI.Panel bottomLine = CreateFlatPanel();
            bottomLine.Dock = DockStyle.Bottom;
            bottomLine.Height = 1;
            bottomLine.BackColor = Palette.Border;

            AntdUI.Label privilegeLabel = new AntdUI.Label();
            privilegeLabel.Dock = DockStyle.Left;
            privilegeLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            privilegeLabel.Text = "完全权限模式";
            privilegeLabel.ForeColor = Palette.TextSecondary;
            privilegeLabel.Font = new Font(Font.FontFamily, 9.5F);
            privilegeLabel.Padding = new Padding(8, 0, 0, 0);

            toolbarPrivilegeSwitch = new AntdUI.Switch();
            toolbarPrivilegeSwitch.Dock = DockStyle.Left;
            toolbarPrivilegeSwitch.Width = 44;
            toolbarPrivilegeSwitch.CheckedChanged += ToolbarPrivilegeSwitch_CheckedChanged;

            privilegeWarningTag = new AntdUI.Tag();
            privilegeWarningTag.Dock = DockStyle.Left;
            privilegeWarningTag.AutoSizeMode = AntdUI.TAutoSize.Width;
            privilegeWarningTag.Text = "已启用 — 可处理受保护的系统文件，请谨慎操作";
            privilegeWarningTag.Font = new Font(Font.FontFamily, 8.5F);
            privilegeWarningTag.BackColor = Palette.WarningSoft;
            privilegeWarningTag.ForeColor = Palette.Warning;
            privilegeWarningTag.BorderWidth = 1F;
            privilegeWarningTag.Radius = 10;
            privilegeWarningTag.Margin = new Padding(8, 4, 0, 4);
            privilegeWarningTag.Visible = false;

            settingsToolbarButton = new AntdUI.Button();
            settingsToolbarButton.Dock = DockStyle.Right;
            settingsToolbarButton.AutoSizeMode = AntdUI.TAutoSize.Width;
            settingsToolbarButton.Text = "设置";
            settingsToolbarButton.IconSvg = "SettingOutlined";
            settingsToolbarButton.IconRatio = 0.8F;
            settingsToolbarButton.Ghost = true;
            settingsToolbarButton.BorderWidth = 0F;
            settingsToolbarButton.ForeColor = Palette.TextSecondary;
            settingsToolbarButton.Radius = 8;
            settingsToolbarButton.Click += SettingsToolbarButton_Click;

            aiStatusTag = new AntdUI.Tag();
            aiStatusTag.Dock = DockStyle.Right;
            aiStatusTag.AutoSizeMode = AntdUI.TAutoSize.Width;
            aiStatusTag.Font = new Font(Font.FontFamily, 9F);
            aiStatusTag.Radius = 12;
            aiStatusTag.BorderWidth = 1F;
            aiStatusTag.Margin = new Padding(0, 4, 10, 4);

            toolbarPanel.Controls.Add(privilegeLabel);
            toolbarPanel.Controls.Add(toolbarPrivilegeSwitch);
            toolbarPanel.Controls.Add(privilegeWarningTag);
            toolbarPanel.Controls.Add(aiStatusTag);
            toolbarPanel.Controls.Add(settingsToolbarButton);
            toolbarPanel.Controls.Add(bottomLine);

            // Dock 顺序调整：switch 在 label 左侧
            toolbarPrivilegeSwitch.BringToFront();
            UpdateAiStatusChip();
            return toolbarPanel;
        }

        private AntdUI.Panel BuildElevationBanner()
        {
            elevationBanner = CreateFlatPanel();
            elevationBanner.Dock = DockStyle.Top;
            elevationBanner.Height = 34;
            elevationBanner.BackColor = Palette.WarningSoft;
            elevationBanner.Padding = new Padding(16, 6, 10, 6);
            elevationBanner.Visible = !new WindowsPrivilegeService().IsProcessElevated();

            AntdUI.Panel bottomLine = CreateFlatPanel();
            bottomLine.Dock = DockStyle.Bottom;
            bottomLine.Height = 1;
            bottomLine.BackColor = Palette.WarningBorder;

            AntdUI.Label messageLabel = new AntdUI.Label();
            messageLabel.Dock = DockStyle.Fill;
            messageLabel.Text = "当前未以管理员身份运行，NTFS 完整扫描与部分系统文件的删除可能受限。";
            messageLabel.ForeColor = Palette.Warning;
            messageLabel.Font = new Font(Font.FontFamily, 9F);

            AntdUI.Button dismissButton = new AntdUI.Button();
            dismissButton.Dock = DockStyle.Right;
            dismissButton.AutoSizeMode = AntdUI.TAutoSize.None;
            dismissButton.Width = 26;
            dismissButton.IconSvg = "CloseOutlined";
            dismissButton.IconRatio = 0.55F;
            dismissButton.Ghost = true;
            dismissButton.BorderWidth = 0F;
            dismissButton.ForeColor = Palette.Warning;
            dismissButton.WaveSize = 0;
            dismissButton.Click += delegate { elevationBanner.Visible = false; };

            AntdUI.Button elevateButton = new AntdUI.Button();
            elevateButton.Dock = DockStyle.Right;
            elevateButton.AutoSizeMode = AntdUI.TAutoSize.Width;
            elevateButton.Text = "以管理员身份重新启动";
            elevateButton.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
            elevateButton.Ghost = true;
            elevateButton.BorderWidth = 0F;
            elevateButton.ForeColor = Palette.Warning;
            elevateButton.Click += delegate { RestartElevated(); };

            elevationBanner.Controls.Add(messageLabel);
            elevationBanner.Controls.Add(elevateButton);
            elevationBanner.Controls.Add(dismissButton);
            elevationBanner.Controls.Add(bottomLine);
            return elevationBanner;
        }

        private AntdUI.Panel BuildStatusBar()
        {
            statusBarPanel = CreateFlatPanel();
            statusBarPanel.Dock = DockStyle.Bottom;
            statusBarPanel.Height = 30;
            statusBarPanel.BackColor = Palette.TitleBar;
            statusBarPanel.Padding = new Padding(16, 5, 16, 5);

            statusStateLabel = new AntdUI.Label();
            statusStateLabel.Dock = DockStyle.Left;
            statusStateLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            statusStateLabel.Text = "● 空闲";
            statusStateLabel.ForeColor = Palette.TitleBarMuted;
            statusStateLabel.Font = new Font("Consolas", 8.5F);
            statusStateLabel.Cursor = Cursors.Hand;
            statusStateLabel.Click += StatusStateLabel_Click;

            statusDriveLabel = new AntdUI.Label();
            statusDriveLabel.Dock = DockStyle.Left;
            statusDriveLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            statusDriveLabel.Text = string.Empty;
            statusDriveLabel.ForeColor = Palette.TitleBarMuted;
            statusDriveLabel.Font = new Font("Consolas", 8.5F);
            statusDriveLabel.Padding = new Padding(18, 0, 0, 0);

            AntdUI.Label licenseLabel = new AntdUI.Label();
            licenseLabel.Dock = DockStyle.Right;
            licenseLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            licenseLabel.Text = "MIT License";
            licenseLabel.ForeColor = Palette.TitleBarMuted;
            licenseLabel.Font = new Font("Consolas", 8.5F);

            AntdUI.Label apiLabel = new AntdUI.Label();
            apiLabel.Dock = DockStyle.Right;
            apiLabel.AutoSizeMode = AntdUI.TAutoSize.Width;
            apiLabel.Text = "OpenAI 兼容接口 · /v1/chat/completions";
            apiLabel.ForeColor = Palette.TitleBarMuted;
            apiLabel.Font = new Font("Consolas", 8.5F);
            apiLabel.Padding = new Padding(0, 0, 18, 0);

            statusBarPanel.Controls.Add(statusStateLabel);
            statusBarPanel.Controls.Add(statusDriveLabel);
            statusBarPanel.Controls.Add(apiLabel);
            statusBarPanel.Controls.Add(licenseLabel);
            statusDriveLabel.BringToFront();
            return statusBarPanel;
        }

        private AntdUI.Panel BuildColumnsHost()
        {
            columnsHost = CreateFlatPanel();
            columnsHost.Dock = DockStyle.Fill;
            columnsHost.BackColor = Palette.Page;

            leftColumnPanel = CreateFlatPanel();
            leftColumnPanel.Dock = DockStyle.Left;
            leftColumnPanel.Width = 470;
            leftColumnPanel.BackColor = Palette.Surface;

            AntdUI.Panel leftBorder = CreateFlatPanel();
            leftBorder.Dock = DockStyle.Left;
            leftBorder.Width = 1;
            leftBorder.BackColor = Palette.Border;

            rightColumnPanel = CreateFlatPanel();
            rightColumnPanel.Dock = DockStyle.Right;
            rightColumnPanel.Width = 350;
            rightColumnPanel.BackColor = Palette.Surface;

            AntdUI.Panel rightBorder = CreateFlatPanel();
            rightBorder.Dock = DockStyle.Right;
            rightBorder.Width = 1;
            rightBorder.BackColor = Palette.Border;

            centerColumnPanel = CreateFlatPanel();
            centerColumnPanel.Dock = DockStyle.Fill;
            centerColumnPanel.BackColor = Palette.Page;

            columnsHost.Controls.Add(centerColumnPanel);
            columnsHost.Controls.Add(rightBorder);
            columnsHost.Controls.Add(rightColumnPanel);
            columnsHost.Controls.Add(leftBorder);
            columnsHost.Controls.Add(leftColumnPanel);
            return columnsHost;
        }

        private void ToolbarPrivilegeSwitch_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            // 与建议页快捷复选框走同一套同步与确认逻辑
            if (privilegedQuickCheckbox != null && privilegedQuickCheckbox.Checked != e.Value)
            {
                privilegedQuickCheckbox.Checked = e.Value;
            }
        }

        private void SettingsToolbarButton_Click(object sender, EventArgs e)
        {
            SetActivePage(activePageId == PageSettings ? PageScan : PageSettings);
        }

        private void StatusStateLabel_Click(object sender, EventArgs e)
        {
            SetActivePage(activePageId == PageLog ? PageScan : PageLog);
        }

        internal void UpdateAiStatusChip()
        {
            if (aiStatusTag == null) return;

            bool configured = settings != null && settings.Ai != null && settings.Ai.Enabled;
            string model = configured ? settings.Ai.Model : null;
            aiStatusTag.Text = configured
                ? "● AI 已连接  " + (string.IsNullOrWhiteSpace(model) ? "(未填模型)" : model.Trim())
                : "○ AI 未启用";
            aiStatusTag.BackColor = configured ? Palette.AccentSoft : Palette.CardFill;
            aiStatusTag.ForeColor = configured ? Palette.AccentText : Palette.TextMuted;
        }

        internal void UpdateStatusBar(string stateText, Color stateColor, string driveText)
        {
            if (statusStateLabel != null)
            {
                statusStateLabel.Text = "● " + stateText;
                statusStateLabel.ForeColor = stateColor;
            }

            if (statusDriveLabel != null && driveText != null) statusDriveLabel.Text = driveText;
        }

        private void RestartElevated()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(Application.ExecutablePath);
                startInfo.Verb = "runas";
                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
                Application.Exit();
            }
            catch (Exception)
            {
                // 用户取消 UAC 时维持现状
            }
        }

        private AntdUI.Panel CreatePageContainer()
        {
            AntdUI.Panel page = CreateFlatPanel();
            page.Dock = DockStyle.Fill;
            page.BackColor = PageBackground;
            return page;
        }

        private void SetActivePage(string pageId)
        {
            if (activePageId == pageId) return;

            SuspendLayout();
            if (pageHost != null) pageHost.SuspendLayout();
            try
            {
                activePageId = pageId;

                Control overlay = GetOverlayControl(pageId);
                if (columnsHost != null) columnsHost.Visible = overlay == null;
                if (logPage != null) logPage.Visible = pageId == PageLog;
                if (settingsPage != null) settingsPage.Visible = pageId == PageSettings;
                if (aiProfileCreatePage != null) aiProfileCreatePage.Visible = pageId == PageAiProfileCreate;
                if (overlay != null) overlay.BringToFront();
                else if (columnsHost != null) columnsHost.BringToFront();

                if (settingsToolbarButton != null)
                {
                    settingsToolbarButton.Text = pageId == PageSettings || pageId == PageAiProfileCreate ? "返回" : "设置";
                    settingsToolbarButton.IconSvg = pageId == PageSettings || pageId == PageAiProfileCreate ? "ArrowLeftOutlined" : "SettingOutlined";
                }

                appBar.SubText = GetPageTitle(pageId);
            }
            finally
            {
                if (pageHost != null) pageHost.ResumeLayout(true);
                ResumeLayout(true);
                if (pageId == PageSettings) RefreshSettingsPageLayout(true);
                Invalidate(true);
            }
        }

        private Control GetOverlayControl(string pageId)
        {
            switch (pageId)
            {
                case PageLog:
                    return logPage;
                case PageSettings:
                    return settingsPage;
                case PageAiProfileCreate:
                    return aiProfileCreatePage;
                default:
                    return null;
            }
        }

        private static string GetPageTitle(string pageId)
        {
            switch (pageId)
            {
                case PageLog:
                    return "日志";
                case PageSettings:
                    return "设置";
                case PageAiProfileCreate:
                    return "新增 AI 配置";
                default:
                    return "v" + typeof(MainWindow).Assembly.GetName().Version.ToString(3);
            }
        }

        private string GetActivePageDescription()
        {
            return "扫描磁盘占用，结合 AI 解析给出清理建议。";
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
    }
}
