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
            dividerPanel.BackColor = Color.FromArgb(240, 240, 240);

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
            menu.ForeColor = Color.FromArgb(80, 80, 80);
            menu.BackColor = Color.Transparent;
            menu.BackHover = Color.FromArgb(245, 247, 250);
            menu.BackActive = Color.FromArgb(22, 119, 255);
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

        private Control CreateScanToolbarPanel()
        {
            AntdUI.Panel toolbarHost = CreateFlatPanel();
            toolbarHost.Dock = DockStyle.Top;
            toolbarHost.BackColor = PageBackground;
            toolbarHost.Height = 148;
            toolbarHost.Padding = new Padding(0, 0, 0, 8);

            AntdUI.Panel toolbarCard = CreateCompactSurfacePanel(12);
            toolbarCard.Dock = DockStyle.Fill;

            AntdUI.GridPanel toolbarLayout = CreateGridPanel("fill 1 420");
            toolbarLayout.Dock = DockStyle.Fill;
            toolbarLayout.BackColor = Color.Transparent;

            Control filtersPanel = CreateScanFiltersPanel();
            AntdUI.Divider divider = new AntdUI.Divider();
            divider.Dock = DockStyle.Fill;
            divider.Vertical = true;
            divider.ColorSplit = BorderLightColor;
            divider.Margin = new Padding(16, 4, 16, 8);

            Control statusPanel = CreateScanStatusPanel();
            Control summaryPanel = CreateDriveSummaryPanel();
            AntdUI.GridPanel leftLayout = CreateGridPanel("84:fill;32:fill");
            leftLayout.Dock = DockStyle.Fill;
            AddGridControl(leftLayout, filtersPanel, 0);
            AddGridControl(leftLayout, statusPanel, 1);

            AddGridControl(toolbarLayout, leftLayout, 0);
            AddGridControl(toolbarLayout, divider, 1);
            AddGridControl(toolbarLayout, summaryPanel, 2);

            toolbarCard.Controls.Add(toolbarLayout);
            toolbarHost.Controls.Add(toolbarCard);
            return toolbarHost;
        }

        private Control CreateScanFiltersPanel()
        {
            AntdUI.GridPanel host = CreateGridPanel("42:42 192 86 42 112 44 88 44 88 fill;42:42 fill");
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.Transparent;

            driveSelect = new AntdUI.Select();
            driveSelect.Dock = DockStyle.Fill;
            driveSelect.DropDownArrow = true;
            driveSelect.ListAutoWidth = true;
            driveSelect.Font = Font;
            driveSelect.SelectedValueChanged += DriveSelect_SelectedValueChanged;

            scanButton.Dock = DockStyle.Fill;
            scanButton.Margin = new Padding(10, 0, 0, 0);

            pathInput = CreateInput("C:\\ 或目录路径");
            pathInput.PrefixSvg = "FolderOpenOutlined";
            pathInput.TextChanged += PathInput_TextChanged;

            sortSelect = new AntdUI.Select();
            sortSelect.Dock = DockStyle.Fill;
            sortSelect.DropDownArrow = true;
            sortSelect.ListAutoWidth = true;
            sortSelect.Font = Font;
            string[] sortOptionTexts = { "占用大小", "实际大小" };
            sortSelect.Items.Add(new AntdUI.SelectItem(sortOptionTexts[0], ScanSortMode.Allocated));
            sortSelect.Items.Add(new AntdUI.SelectItem(sortOptionTexts[1], ScanSortMode.Logical));
            sortSelect.SelectedValueChanged += SizeModeSelect_SelectedValueChanged;
            int sortSelectWidth = MeasureSelectWidth(sortSelect.Font, sortOptionTexts);
            sortSelect.Width = sortSelectWidth;

            minSizeInput = CreateInput("-1 表示不限");
            limitInput = CreateInput("-1 表示不限");

            AddGridControl(host, CreateToolbarCaption("选择:"), 0);
            AddGridControl(host, driveSelect, 1);
            AddGridControl(host, scanButton, 2);
            AddGridControl(host, CreateToolbarCaption("模式:"), 3);
            AddGridControl(host, sortSelect, 4);
            AddGridControl(host, CreateToolbarCaption("最小:"), 5);
            AddGridControl(host, minSizeInput, 6);
            AddGridControl(host, CreateToolbarCaption("限制:"), 7);
            AddGridControl(host, limitInput, 8);
            AddGridControl(host, CreateGridSpacer(), 9);
            AddGridControl(host, CreateToolbarCaption("位置:"), 10);
            AddGridControl(host, pathInput, 11);
            return host;
        }

        private Control CreateDriveSummaryPanel()
        {
            AntdUI.GridPanel layout = CreateGridPanel("fill 190");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;
            layout.Padding = new Padding(0, 0, 0, 0);

            AntdUI.GridPanel leftLayout = CreateGridPanel("24:48 fill;24:48 fill;24:48 fill");
            leftLayout.Dock = DockStyle.Fill;
            leftLayout.BackColor = Color.Transparent;

            AntdUI.GridPanel rightLayout = CreateGridPanel("24:64 fill;24:64 fill;fill");
            rightLayout.Dock = DockStyle.Fill;
            rightLayout.BackColor = Color.Transparent;

            selectedDriveValueLabel = CreateSummaryValueLabel(true);
            selectedDriveValueLabel.AutoEllipsis = true;
            totalSpaceValueLabel = CreateSummaryValueLabel(true);
            usedSpaceValueLabel = CreateSummaryValueLabel(true);
            availableSpaceValueLabel = CreateSummaryValueLabel(true);
            reservedSpaceValueLabel = CreateSummaryValueLabel(true);
            totalSpaceValueLabel.AutoEllipsis = true;
            usedSpaceValueLabel.AutoEllipsis = true;
            availableSpaceValueLabel.AutoEllipsis = true;
            reservedSpaceValueLabel.AutoEllipsis = true;

            AddGridControl(leftLayout, CreateSummaryCaption("选择:"), 0);
            AddGridControl(leftLayout, selectedDriveValueLabel, 1);
            AddGridControl(leftLayout, CreateSummaryCaption("已用:"), 2);
            AddGridControl(leftLayout, usedSpaceValueLabel, 3);
            AddGridControl(leftLayout, CreateSummaryCaption("可用:"), 4);
            AddGridControl(leftLayout, availableSpaceValueLabel, 5);

            AddGridControl(rightLayout, CreateSummaryCaption("总空间:"), 0);
            AddGridControl(rightLayout, totalSpaceValueLabel, 1);
            AddGridControl(rightLayout, CreateSummaryCaption("预留:"), 2);
            AddGridControl(rightLayout, reservedSpaceValueLabel, 3);

            AddGridControl(layout, leftLayout, 0);
            AddGridControl(layout, rightLayout, 1);
            return layout;
        }

        private Control CreateScanStatusPanel()
        {
            AntdUI.GridPanel panel = CreateGridPanel("126 112 fill");
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.Transparent;
            panel.Padding = new Padding(0, 8, 0, 0);

            scanStatusLabel = new AntdUI.Label();
            scanStatusLabel.Dock = DockStyle.Fill;
            scanStatusLabel.Font = new Font("Microsoft YaHei UI", 9F);
            scanStatusLabel.ForeColor = TextSecondaryColor;
            scanStatusLabel.BackColor = Color.Transparent;
            scanStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            scanStatusLabel.Text = "等待开始扫描";

            scanElapsedLabel = new AntdUI.Label();
            scanElapsedLabel.Dock = DockStyle.Fill;
            scanElapsedLabel.Font = new Font("Microsoft YaHei UI", 9F);
            scanElapsedLabel.ForeColor = TextSecondaryColor;
            scanElapsedLabel.BackColor = Color.Transparent;
            scanElapsedLabel.TextAlign = ContentAlignment.MiddleLeft;
            scanElapsedLabel.Text = "用时 0.0 秒";
            scanElapsedLabel.Margin = new Padding(8, 0, 0, 0);

            scanProgress = new AntdUI.Progress();
            scanProgress.Dock = DockStyle.Fill;
            scanProgress.Margin = new Padding(0, 7, 4, 9);
            scanProgress.Shape = AntdUI.TShapeProgress.Round;
            scanProgress.Radius = 8;
            scanProgress.Value = 0F;
            scanProgress.State = AntdUI.TType.Success;
            scanProgress.UseSystemText = false;

            AddGridControl(panel, scanStatusLabel, 0);
            AddGridControl(panel, scanElapsedLabel, 1);
            AddGridControl(panel, scanProgress, 2);
            return panel;
        }

        private Control CreateStoragePanel()
        {
            AntdUI.Panel panel = CreateCompactSurfacePanel(0);
            panel.Dock = DockStyle.Fill;

            storageTable = new AntdUI.Table();
            storageTable.Dock = DockStyle.Fill;
            storageTable.TabStop = true;
            ConfigureTableSurface(storageTable);
            storageTable.FixedHeader = true;
            storageTable.ScrollBarAvoidHeader = true;
            storageTable.ExpandChanged += StorageTable_ExpandChanged;
            storageTable.CellClick += StorageTable_CellClick;
            storageTable.CellDoubleClick += StorageTable_CellDoubleClick;
            storageTable.KeyDown += StorageTable_KeyDown;

            panel.Controls.Add(storageTable);
            return panel;
        }

        private Control CreateSuggestionPanel()
        {
            AntdUI.Panel panel = CreateCompactSurfacePanel(0);
            panel.Dock = DockStyle.Fill;

            AntdUI.Panel toolbarHost = CreateFlatPanel();
            toolbarHost.Dock = DockStyle.Top;
            toolbarHost.Height = 94;
            toolbarHost.Padding = new Padding(8, 8, 8, 6);
            toolbarHost.BackColor = Color.Transparent;

            invertSuggestionsButton = CreateSuggestionActionButton("反选", AntdUI.TTypeMini.Default);
            invertSuggestionsButton.Click += delegate { InvertSuggestionSelection(); };

            clearAllSuggestionsButton = CreateSuggestionActionButton("全不选", AntdUI.TTypeMini.Default);
            clearAllSuggestionsButton.Click += delegate { SetSuggestionSelection(false); };

            selectAllSuggestionsButton = CreateSuggestionActionButton("全选", AntdUI.TTypeMini.Primary);
            selectAllSuggestionsButton.Click += delegate { SetSuggestionSelection(true); };

            suggestionPromptButton = CreateToolbarActionButton("提示词", AntdUI.TTypeMini.Default);
            suggestionPromptButton.IconSvg = "EditOutlined";
            suggestionPromptButton.Click += delegate { ShowSuggestionPromptEditor(); };

            privilegedQuickCheckbox = CreateCheckbox("完全权限模式（仅管理员运行时生效）");
            privilegedQuickCheckbox.CheckedChanged += PrivilegedCheckbox_CheckedChanged;

            AntdUI.GridPanel topRow = CreateGridPanel("42 160 120 78 76 78 fill");
            topRow.Dock = DockStyle.Fill;
            topRow.BackColor = Color.Transparent;

            suggestionDriveSelect = CreateSelect();
            suggestionDriveSelect.ListAutoWidth = true;
            suggestionDriveSelect.SelectedValueChanged += SuggestionDriveSelect_SelectedValueChanged;
            suggestionMinSizeInput = CreateInput("最小值（单位MB）");
            suggestionMinSizeInput.Text = "128";
            suggestionLimitInput = CreateInput("数量限制，-1 不限");
            suggestionLimitInput.Text = "-1";

            AddGridControl(topRow, CreateToolbarCaption("盘符:"), 0);
            AddGridControl(topRow, suggestionDriveSelect, 1);
            AddGridControl(topRow, CreateToolbarCaption("最小 MB:"), 2);
            AddGridControl(topRow, suggestionMinSizeInput, 3);
            AddGridControl(topRow, CreateToolbarCaption("数量:"), 4);
            AddGridControl(topRow, suggestionLimitInput, 5);
            AddGridControl(topRow, privilegedQuickCheckbox, 6);

            AntdUI.GridPanel actionRow = CreateGridPanel("104 104 104 104 112 fill 74 74 74");
            actionRow.Dock = DockStyle.Fill;
            actionRow.BackColor = Color.Transparent;
            regularCleanButton.Margin = new Padding(0, 2, 8, 2);
            superCleanButton.Margin = new Padding(0, 2, 8, 2);
            analyzeButton.Margin = new Padding(0, 2, 8, 2);
            suggestionPromptButton.Margin = new Padding(0, 2, 8, 2);
            deleteButton.Margin = new Padding(0, 2, 8, 2);
            selectAllSuggestionsButton.Margin = new Padding(0, 4, 8, 4);
            clearAllSuggestionsButton.Margin = new Padding(0, 4, 8, 4);
            invertSuggestionsButton.Margin = new Padding(0, 4, 0, 4);
            AddGridControl(actionRow, regularCleanButton, 0);
            AddGridControl(actionRow, superCleanButton, 1);
            AddGridControl(actionRow, analyzeButton, 2);
            AddGridControl(actionRow, suggestionPromptButton, 3);
            AddGridControl(actionRow, deleteButton, 4);
            AddGridControl(actionRow, CreateGridSpacer(), 5);
            AddGridControl(actionRow, selectAllSuggestionsButton, 6);
            AddGridControl(actionRow, clearAllSuggestionsButton, 7);
            AddGridControl(actionRow, invertSuggestionsButton, 8);

            AntdUI.GridPanel toolbarLayout = CreateGridPanel("40:fill;40:fill");
            toolbarLayout.Dock = DockStyle.Fill;
            toolbarLayout.BackColor = Color.Transparent;
            AddGridControl(toolbarLayout, topRow, 0);
            AddGridControl(toolbarLayout, actionRow, 1);
            toolbarHost.Controls.Add(toolbarLayout);

            suggestionTable = new AntdUI.Table();
            suggestionTable.Dock = DockStyle.Fill;
            ConfigureCleanupListSurface(suggestionTable);
            suggestionTable.FixedHeader = true;
            suggestionTable.ScrollBarAvoidHeader = true;
            suggestionTable.CellDoubleClick += SuggestionTable_CellDoubleClick;
            suggestionTable.CellButtonClick += SuggestionTable_CellButtonClick;

            panel.Controls.Add(suggestionTable);
            panel.Controls.Add(toolbarHost);
            return panel;
        }

        private Control CreateSettingsPanel()
        {
            AntdUI.Panel panel = CreateFlatPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = PageBackground;
            panel.Padding = new Padding(0);

            aiEnabledSwitch = CreateSettingsSwitch();
            testAiSettingsButton = CreateSettingsActionButton("测试 AI", AntdUI.TTypeMini.Default);
            testAiSettingsButton.IconSvg = "SearchOutlined";
            testAiSettingsButton.Click += delegate { TestAiSettings(); };
            recycleSwitch = CreateSettingsSwitch();
            privilegedCheckbox = CreateCheckbox("启用完全权限（管理员）");
            privilegedCheckbox.CheckedChanged += PrivilegedCheckbox_CheckedChanged;
            aiAccessModeSelect = CreateSettingsSelect();
            PopulateAiAccessModes();
            aiAccessModeSelect.SelectedValueChanged += AiAccessModeSelect_SelectedValueChanged;
            endpointInput = CreateInput("https://api.openai.com");
            apiKeyInput = CreateInput("sk-...");
            modelInput = CreateInput(AiSettings.DefaultModel);
            maxSuggestionsInput = CreateInput("30");
            applyAiProfileButton = CreateSettingsActionButton("应用选中", AntdUI.TTypeMini.Primary);
            applyAiProfileButton.IconSvg = "CheckOutlined";
            applyAiProfileButton.Click += delegate { ApplySelectedAiProfile(); };
            addAiProfileButton = CreateAddAiProfileButton();
            addAiProfileButton.Click += delegate { OpenAiProfileCreatePage(); };
            aiProviderPresetSelect = CreateSettingsSelect();
            PopulateAiProviderPresets();
            aiProviderPresetSelect.SelectedValueChanged += AiProviderPresetSelect_SelectedValueChanged;
            endpointInput.TextChanged += AiEndpointOrModelInput_TextChanged;
            modelInput.TextChanged += AiEndpointOrModelInput_TextChanged;
            modelCookieMappingsInput = CreateInput("直接粘贴当前模型的一整行 Cookie；也兼容 model=Cookie");
            modelCookieMappingsInput.Multiline = false;
            modelCookieMappingsInput.AutoScroll = false;
            allowRootsInput = CreateInput("每行一个允许位置");
            allowRootsInput.Multiline = true;
            allowRootsInput.AutoScroll = true;

            settingsScrollHost = CreateVerticalScrollPanel();
            AntdUI.StackPanel scrollHost = settingsScrollHost;
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.AutoScroll = true;
            scrollHost.BackColor = PageBackground;
            scrollHost.Padding = new Padding(0, 0, 4, 8);

            settingsContentLayout = CreateGridPanel("62:fill;96:fill;520:fill;266:fill");
            AntdUI.GridPanel layout = settingsContentLayout;
            layout.Dock = DockStyle.Top;
            layout.Height = 944;
            layout.BackColor = PageBackground;
            layout.Width = Math.Max(720, scrollHost.ClientSize.Width - 8);
            scrollHost.Resize += delegate
            {
                layout.Width = Math.Max(720, scrollHost.ClientSize.Width - 8);
                ResizeAiProfileCards();
            };

            Control overviewSection = CreateSettingsOverviewSection();
            Control profilesSection = CreateAiProfileSection();
            Control sandboxSection = CreateSandboxSection();
            Control actionBar = CreateSettingsActionBar();
            actionBar.Margin = new Padding(0, 6, 0, 8);
            overviewSection.Margin = new Padding(0, 6, 0, 12);
            profilesSection.Margin = new Padding(0, 6, 0, 12);
            sandboxSection.Margin = new Padding(0);

            AddGridControl(layout, actionBar, 0);
            AddGridControl(layout, overviewSection, 1);
            AddGridControl(layout, profilesSection, 2);
            AddGridControl(layout, sandboxSection, 3);

            scrollHost.Controls.Add(layout);

            panel.Controls.Add(scrollHost);
            return panel;
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

        private Control CreateSettingsActionBar()
        {
            AntdUI.Panel panel = CreateCompactSurfacePanel(6);
            panel.Dock = DockStyle.Fill;

            AntdUI.GridPanel layout = CreateGridPanel("fill 112");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            AntdUI.Label hint = CreateSmallMutedLabel("AI 接入、清理策略和沙盒范围");
            saveSettingsButton.Margin = new Padding(0, 0, 0, 0);
            saveSettingsButton.Dock = DockStyle.Fill;

            AddGridControl(layout, hint, 0);
            AddGridControl(layout, saveSettingsButton, 1);
            panel.Controls.Add(layout);
            return panel;
        }

        private Control CreateSettingsOverviewSection()
        {
            AntdUI.Panel section = CreateSettingsSurfacePanel(12);

            AntdUI.GridPanel layout = CreateGridPanel("48 78 84 78 86 104 fill");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            maxSuggestionsInput.Margin = new Padding(0, 8, 0, 8);

            AddGridControl(layout, CreateCaption("AI"), 0);
            AddGridControl(layout, aiEnabledSwitch, 1);
            AddGridControl(layout, CreateCaption("回收站"), 2);
            AddGridControl(layout, recycleSwitch, 3);
            AddGridControl(layout, CreateCaption("建议条数"), 4);
            AddGridControl(layout, maxSuggestionsInput, 5);
            AddGridControl(layout, CreateGridSpacer(), 6);

            section.Controls.Add(layout);
            return section;
        }

        private Control CreateAiProfileSection()
        {
            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("AI 配置", "最近保存的接口、模型和访问方式会显示在这里。", out body);

            AntdUI.GridPanel layout = CreateGridPanel("fill 230 108 116 46;fill-44 fill");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            AntdUI.Label hint = CreateSmallMutedLabel("点击卡片选择配置，右侧按钮可直接应用到当前设置。");
            privilegedCheckbox.Margin = new Padding(0, 4, 8, 4);
            AddGridControl(layout, hint, 0);
            AddGridControl(layout, privilegedCheckbox, 1);
            AddGridControl(layout, testAiSettingsButton, 2);
            AddGridControl(layout, applyAiProfileButton, 3);
            AddGridControl(layout, addAiProfileButton, 4);

            aiProfileListPanel = CreateVerticalScrollPanel();
            aiProfileListPanel.Dock = DockStyle.Fill;
            aiProfileListPanel.BackColor = SurfaceColor;
            aiProfileListPanel.AutoScroll = true;
            aiProfileListPanel.Padding = new Padding(0);
            aiProfileListPanel.Margin = new Padding(0);
            aiProfileListPanel.Resize += delegate { ResizeAiProfileCards(); };

            AddGridControl(layout, aiProfileListPanel, 5);
            body.Controls.Add(layout);
            return section;
        }

        private Control CreateSandboxSection()
        {
            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("沙盒范围", "允许位置内的路径可直接执行删除，其他位置会继续确认。", out body);

            AntdUI.GridPanel layout = CreateGridPanel("fill;fill-26 fill");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            AddGridControl(layout, CreateSmallMutedLabel("允许位置"), 0);
            AddGridControl(layout, allowRootsInput, 1);

            body.Controls.Add(layout);
            return section;
        }

        private Control CreateLogPanel()
        {
            AntdUI.Panel panel = CreateCompactSurfacePanel(8);
            panel.Dock = DockStyle.Fill;

            logInput = CreateInput(string.Empty);
            logInput.Dock = DockStyle.Fill;
            logInput.Multiline = true;
            logInput.ReadOnly = true;
            logInput.AutoScroll = true;
            logInput.MaxLength = int.MaxValue;

            panel.Controls.Add(logInput);
            return panel;
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
