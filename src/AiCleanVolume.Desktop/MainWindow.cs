using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AiCleanVolume.Core.Models;
using AiCleanVolume.Core.Services;
using AiCleanVolume.Desktop.Services;
using AiCleanVolume.Desktop.ViewModels;

namespace AiCleanVolume.Desktop
{
    public sealed class MainWindow : AntdUI.Window
    {
        private static readonly Color PageBackground = Color.FromArgb(245, 245, 245);
        private static readonly Color SurfaceColor = Color.White;
        private static readonly Color FillSecondary = Color.FromArgb(250, 250, 250);
        private static readonly Color BorderDefaultColor = Color.FromArgb(217, 217, 217);
        private static readonly Color BorderLightColor = Color.FromArgb(240, 240, 240);
        private static readonly Color PrimaryColor = Color.FromArgb(22, 119, 255);
        private static readonly Color TextPrimaryColor = Color.FromArgb(31, 31, 31);
        private static readonly Color TextSecondaryColor = Color.FromArgb(89, 89, 89);
        private static readonly Color TextTertiaryColor = Color.FromArgb(140, 140, 140);
        private const string PageScan = "scan";
        private const string PageSuggestions = "suggestions";
        private const string PageLog = "log";
        private const string PageSettings = "settings";
        private const string AppDisplayName = "AI智能清盘";
        private const int WmGetMinMaxInfo = 0x0024;
        private static readonly Size DefaultClientArea = new Size(1540, 920);
        private static readonly Size BaseMinimumWindowSize = new Size(1120, 720);
        private const int SidebarMinWidth = 180;
        private const int SidebarMaxWidth = 320;
        private const int SidebarRailWidth = 10;

        private readonly SettingsStore settingsStore;
        private readonly IScanProvider scanProvider;
        private readonly ReusableBackgroundWorker backgroundWorker;
        private readonly CandidatePlanner candidatePlanner;
        private readonly IAiCleanupAdvisor aiAdvisor;
        private readonly IDeletionSandbox deletionSandbox;
        private readonly IDeletionService deletionService;
        private readonly IExplorerService explorerService;
        private readonly IPrivilegeService privilegeService;

        private ApplicationSettings settings;
        private StorageItem currentRoot;
        private ScanRequest currentTreeRequest;
        private int currentTreeVersion;
        private List<CleanupSuggestionRow> suggestionRows;

        private AntdUI.PageHeader appBar;
        private AntdUI.PageHeader titleBar;
        private AntdUI.Menu navigationMenu;
        private Panel sidebarHost;
        private AntdUI.Panel sidebarPanel;
        private Panel sidebarBrandPanel;
        private Label sidebarBrandTextLabel;
        private Panel sidebarResizeRail;
        private AntdUI.Button settingsNavButton;
        private Panel scanPage;
        private Panel suggestionsPage;
        private Panel logPage;
        private Panel settingsPage;
        private AntdUI.Button scanButton;
        private AntdUI.Button analyzeButton;
        private AntdUI.Button deleteButton;
        private AntdUI.Button saveSettingsButton;
        private string activePageId;

        private AntdUI.Select driveSelect;
        private AntdUI.Select sortSelect;
        private AntdUI.Input pathInput;
        private AntdUI.Input minSizeInput;
        private AntdUI.Input limitInput;

        private AntdUI.Table storageTable;
        private AntdUI.Table suggestionTable;
        private ContextMenuStrip storageContextMenu;
        private ToolStripMenuItem deleteStorageMenuItem;
        private StorageEntryRow storageContextRow;

        private AntdUI.Switch aiEnabledSwitch;
        private AntdUI.Switch recycleSwitch;
        private AntdUI.Switch privilegedSwitch;
        private AntdUI.Input endpointInput;
        private AntdUI.Input apiKeyInput;
        private AntdUI.Input modelInput;
        private AntdUI.Input maxSuggestionsInput;
        private AntdUI.Input allowRootsInput;
        private AntdUI.Input logInput;

        private Label selectedDriveValueLabel;
        private Label totalSpaceValueLabel;
        private Label usedSpaceValueLabel;
        private Label availableSpaceValueLabel;
        private Label reservedSpaceValueLabel;
        private Label scanStatusLabel;
        private Panel scanProgressTrack;
        private Panel scanProgressFill;
        private float scanProgressValue;

        private readonly string defaultDescription = "选择磁盘或目录，扫描空间占用，生成可确认的安全清理建议";
        private FormWindowState lastWindowState;
        private bool applyingNormalBounds;
        private bool busy;
        private bool sidebarResizing;
        private int sidebarWidth;
        private int sidebarResizeStartX;
        private int sidebarResizeStartWidth;

        public MainWindow()
        {
            settingsStore = new SettingsStore();
            settings = settingsStore.Load();
            candidatePlanner = new CandidatePlanner();
            deletionSandbox = new DeletionSandbox();
            privilegeService = new WindowsPrivilegeService();
            scanProvider = new FolderSizeRankerScanProvider();
            backgroundWorker = new ReusableBackgroundWorker("AiCleanVolume.UiWorker");
            aiAdvisor = new OpenAiCompatibleAdvisor(new HeuristicCleanupAdvisor());
            deletionService = new RecycleBinDeletionService();
            explorerService = new ShellExplorerService();
            suggestionRows = new List<CleanupSuggestionRow>();
            lastWindowState = FormWindowState.Normal;
            sidebarWidth = 0;

            InitializeComponent();
            ConfigureTables();
            LoadSettingsToUi();
            LoadDrives();
            Log("应用已启动。若 AI 未启用，将自动回退到本地启发式规则。");
        }

        private void InitializeComponent()
        {
            Font = new Font("Microsoft YaHei UI", 10.5F);
            BackColor = PageBackground;
            ForeColor = TextPrimaryColor;
            StartPosition = FormStartPosition.CenterScreen;
            Text = AppDisplayName;
            ClientSize = DefaultClientArea;
            MinimumSize = BaseMinimumWindowSize;
            KeyPreview = true;

            appBar = new AntdUI.PageHeader();
            appBar.Dock = DockStyle.Top;
            appBar.BackColor = SurfaceColor;
            appBar.ShowButton = true;
            appBar.ShowIcon = false;
            appBar.UseTitleFont = false;
            appBar.Padding = new Padding(12, 0, 0, 0);
            appBar.Text = AppDisplayName;
            appBar.Description = string.Empty;
            appBar.Height = 40;

            titleBar = new AntdUI.PageHeader();
            titleBar.Dock = DockStyle.Top;
            titleBar.BackColor = SurfaceColor;
            titleBar.ShowButton = false;
            titleBar.ShowIcon = false;
            titleBar.UseTitleFont = true;
            titleBar.Padding = new Padding(12, 0, 0, 8);
            titleBar.Text = AppDisplayName;
            titleBar.Description = defaultDescription;
            titleBar.Height = 72;

            saveSettingsButton = CreateHeaderButton("保存配置", AntdUI.TTypeMini.Default);
            saveSettingsButton.Click += delegate { SaveSettings(); };

            deleteButton = CreateHeaderButton("删除勾选", AntdUI.TTypeMini.Error);
            deleteButton.Click += delegate { DeleteSelectedSuggestions(); };

            analyzeButton = CreateHeaderButton("AI 识别", AntdUI.TTypeMini.Success);
            analyzeButton.Click += delegate { AnalyzeSuggestions(); };

            scanButton = CreateToolbarActionButton("扫描", AntdUI.TTypeMini.Primary);
            scanButton.Click += delegate { ScanCurrentLocation(); };

            titleBar.Controls.Add(saveSettingsButton);
            titleBar.Controls.Add(deleteButton);
            titleBar.Controls.Add(analyzeButton);

            Panel shell = new Panel();
            shell.Dock = DockStyle.Fill;
            shell.BackColor = SurfaceColor;
            shell.Padding = Padding.Empty;

            Panel contentHost = new Panel();
            contentHost.Dock = DockStyle.Fill;
            contentHost.BackColor = SurfaceColor;
            contentHost.Padding = Padding.Empty;

            Panel pageHost = new Panel();
            pageHost.Dock = DockStyle.Fill;
            pageHost.BackColor = PageBackground;
            pageHost.Padding = new Padding(24, 16, 24, 24);

            scanPage = CreatePageContainer();
            scanPage.Controls.Add(CreateStoragePanel());
            scanPage.Controls.Add(CreateScanToolbarPanel());

            suggestionsPage = CreatePageContainer();
            suggestionsPage.Controls.Add(CreateSuggestionPanel());

            logPage = CreatePageContainer();
            logPage.Controls.Add(CreateLogPanel());

            settingsPage = CreatePageContainer();
            settingsPage.Controls.Add(CreateSettingsPanel());

            pageHost.Controls.Add(settingsPage);
            pageHost.Controls.Add(logPage);
            pageHost.Controls.Add(suggestionsPage);
            pageHost.Controls.Add(scanPage);

            contentHost.Controls.Add(pageHost);
            contentHost.Controls.Add(titleBar);

            sidebarHost = CreateSidebarHost();

            shell.Controls.Add(contentHost);
            shell.Controls.Add(sidebarHost);

            Controls.Add(shell);
            Controls.Add(appBar);
            SetActivePage(PageScan);
            ApplySidebarWidth(ResolveInitialSidebarWidth());
        }

        private Panel CreatePageContainer()
        {
            Panel page = new Panel();
            page.Dock = DockStyle.Fill;
            page.BackColor = PageBackground;
            return page;
        }

        private Panel CreateSidebarHost()
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Left;
            host.Width = SidebarMinWidth + SidebarRailWidth;
            host.BackColor = SurfaceColor;

            sidebarResizeRail = new Panel();
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
            sidebarPanel.Back = SurfaceColor;
            sidebarPanel.BorderWidth = 1F;
            sidebarPanel.BorderColor = BorderLightColor;
            sidebarPanel.Radius = 0;
            sidebarPanel.Shadow = 0;
            sidebarPanel.Padding = new Padding(12, 10, 12, 12);

            Panel footerPanel = CreateSidebarFooterPanel();
            Panel dividerPanel = new Panel();
            dividerPanel.Dock = DockStyle.Bottom;
            dividerPanel.Height = 1;
            dividerPanel.BackColor = BorderLightColor;

            navigationMenu = CreateSidebarMenu();
            sidebarBrandPanel = CreateSidebarBrandPanel();

            sidebarPanel.Controls.Add(navigationMenu);
            sidebarPanel.Controls.Add(dividerPanel);
            sidebarPanel.Controls.Add(footerPanel);
            sidebarPanel.Controls.Add(sidebarBrandPanel);

            host.Controls.Add(sidebarPanel);
            host.Controls.Add(sidebarResizeRail);
            return host;
        }

        private Panel CreateSidebarBrandPanel()
        {
            Panel brandPanel = new Panel();
            brandPanel.Dock = DockStyle.Top;
            brandPanel.Height = 70;
            brandPanel.Padding = new Padding(6, 10, 6, 14);
            brandPanel.BackColor = Color.Transparent;

            sidebarBrandTextLabel = new Label();
            sidebarBrandTextLabel.Dock = DockStyle.Fill;
            sidebarBrandTextLabel.Text = AppDisplayName;
            sidebarBrandTextLabel.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
            sidebarBrandTextLabel.ForeColor = TextPrimaryColor;
            sidebarBrandTextLabel.BackColor = Color.Transparent;
            sidebarBrandTextLabel.AutoEllipsis = true;
            sidebarBrandTextLabel.TextAlign = ContentAlignment.MiddleLeft;

            brandPanel.Controls.Add(sidebarBrandTextLabel);
            return brandPanel;
        }

        private AntdUI.Menu CreateSidebarMenu()
        {
            AntdUI.Menu menu = new AntdUI.Menu();
            menu.Dock = DockStyle.Fill;
            menu.Mode = AntdUI.TMenuMode.Inline;
            menu.Unique = true;
            menu.Radius = 12;
            menu.Indent = false;
            menu.Gap = 12;
            menu.IconGap = 10;
            menu.itemMargin = 5;
            menu.IconRatio = 1.08F;
            menu.Padding = new Padding(2, 6, 2, 6);
            menu.BackHover = Color.FromArgb(242, 247, 255);
            menu.BackActive = Color.FromArgb(230, 240, 255);
            menu.ForeActive = PrimaryColor;
            menu.ScrollBarBlock = true;
            menu.SelectChanged += NavigationMenu_SelectChanged;

            AntdUI.MenuItem scanItem = CreateNavigationItem(PageScan, "扫描", "FolderOpenOutlined");
            scanItem.Select = true;
            menu.Items.Add(scanItem);
            menu.Items.Add(CreateNavigationItem(PageSuggestions, "AI 建议", "RobotFilled"));
            menu.Items.Add(new AntdUI.MenuDividerItem());
            menu.Items.Add(CreateNavigationItem(PageLog, "日志管理", "FileTextOutlined"));
            return menu;
        }

        private Panel CreateSidebarFooterPanel()
        {
            Panel footerPanel = new Panel();
            footerPanel.Dock = DockStyle.Bottom;
            footerPanel.Height = 64;
            footerPanel.Padding = new Padding(10, 10, 10, 10);
            footerPanel.BackColor = Color.Transparent;

            settingsNavButton = new AntdUI.Button();
            settingsNavButton.Dock = DockStyle.Left;
            settingsNavButton.Width = 42;
            settingsNavButton.Height = 42;
            settingsNavButton.IconSvg = "SettingOutlined";
            settingsNavButton.Text = null;
            settingsNavButton.Radius = 12;
            settingsNavButton.Type = AntdUI.TTypeMini.Default;
            settingsNavButton.BorderWidth = 1F;
            settingsNavButton.DefaultBorderColor = BorderLightColor;
            settingsNavButton.Click += SettingsNavButton_Click;

            footerPanel.Controls.Add(settingsNavButton);
            return footerPanel;
        }

        private Control CreateScanToolbarPanel()
        {
            Panel toolbarHost = new Panel();
            toolbarHost.Dock = DockStyle.Top;
            toolbarHost.BackColor = PageBackground;
            toolbarHost.Height = 188;
            toolbarHost.Padding = new Padding(0, 0, 0, 12);

            AntdUI.Panel toolbarCard = CreateCardPanel(16);
            toolbarCard.Dock = DockStyle.Fill;
            toolbarCard.Shadow = 3;
            toolbarCard.ShadowOpacity = 0.04F;

            TableLayoutPanel toolbarLayout = new TableLayoutPanel();
            toolbarLayout.Dock = DockStyle.Fill;
            toolbarLayout.BackColor = Color.Transparent;
            toolbarLayout.ColumnCount = 3;
            toolbarLayout.RowCount = 3;
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1F));
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 336F));
            toolbarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            toolbarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            toolbarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Control filtersPanel = CreateScanFiltersPanel();
            Panel divider = new Panel();
            divider.Dock = DockStyle.Fill;
            divider.BackColor = BorderLightColor;
            divider.Margin = new Padding(18, 4, 18, 8);

            Control summaryPanel = CreateDriveSummaryPanel();
            Control statusPanel = CreateScanStatusPanel();

            toolbarLayout.Controls.Add(filtersPanel, 0, 0);
            toolbarLayout.SetRowSpan(filtersPanel, 2);
            toolbarLayout.Controls.Add(divider, 1, 0);
            toolbarLayout.SetRowSpan(divider, 3);
            toolbarLayout.Controls.Add(summaryPanel, 2, 0);
            toolbarLayout.SetRowSpan(summaryPanel, 3);
            toolbarLayout.Controls.Add(statusPanel, 0, 2);

            toolbarCard.Controls.Add(toolbarLayout);
            toolbarHost.Controls.Add(toolbarCard);
            return toolbarHost;
        }

        private Control CreateScanFiltersPanel()
        {
            TableLayoutPanel host = new TableLayoutPanel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.Transparent;
            host.RowCount = 2;
            host.ColumnCount = 1;
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

            TableLayoutPanel topRow = new TableLayoutPanel();
            topRow.Dock = DockStyle.Fill;
            topRow.BackColor = Color.Transparent;
            topRow.ColumnCount = 5;
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 216F));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            driveSelect = new AntdUI.Select();
            driveSelect.Dock = DockStyle.Fill;
            driveSelect.DropDownArrow = true;
            driveSelect.ListAutoWidth = true;
            driveSelect.Font = Font;
            driveSelect.SelectedValueChanged += DriveSelect_SelectedValueChanged;

            scanButton.Dock = DockStyle.Fill;
            scanButton.Margin = new Padding(10, 0, 0, 0);

            pathInput = CreateInput("C:\\ 或目录路径");
            pathInput.TextChanged += PathInput_TextChanged;

            topRow.Controls.Add(CreateToolbarCaption("选择:"), 0, 0);
            topRow.Controls.Add(driveSelect, 1, 0);
            topRow.Controls.Add(scanButton, 2, 0);
            topRow.Controls.Add(CreateToolbarCaption("位置:"), 3, 0);
            topRow.Controls.Add(pathInput, 4, 0);

            TableLayoutPanel bottomRow = new TableLayoutPanel();
            bottomRow.Dock = DockStyle.Fill;
            bottomRow.BackColor = Color.Transparent;
            bottomRow.ColumnCount = 7;
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56F));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            minSizeInput = CreateInput("-1 表示不限");
            limitInput = CreateInput("-1 表示不限");

            sortSelect = new AntdUI.Select();
            sortSelect.Dock = DockStyle.Fill;
            sortSelect.DropDownArrow = true;
            sortSelect.ListAutoWidth = true;
            sortSelect.Font = Font;
            string[] sortOptionTexts = { "分配大小", "逻辑大小" };
            sortSelect.Items.Add(new AntdUI.SelectItem(sortOptionTexts[0], ScanSortMode.Allocated));
            sortSelect.Items.Add(new AntdUI.SelectItem(sortOptionTexts[1], ScanSortMode.Logical));
            int sortSelectWidth = MeasureSelectWidth(sortSelect.Font, sortOptionTexts);
            bottomRow.ColumnStyles[5].Width = sortSelectWidth;
            sortSelect.Width = sortSelectWidth;

            bottomRow.Controls.Add(CreateToolbarCaption("最小:"), 0, 0);
            bottomRow.Controls.Add(minSizeInput, 1, 0);
            bottomRow.Controls.Add(CreateToolbarCaption("限制:"), 2, 0);
            bottomRow.Controls.Add(limitInput, 3, 0);
            bottomRow.Controls.Add(CreateToolbarCaption("排序:"), 4, 0);
            bottomRow.Controls.Add(sortSelect, 5, 0);

            host.Controls.Add(topRow, 0, 0);
            host.Controls.Add(bottomRow, 0, 1);
            return host;
        }

        private Control CreateDriveSummaryPanel()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;
            layout.ColumnCount = 4;
            layout.RowCount = 4;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            layout.Padding = new Padding(0, 2, 0, 0);

            selectedDriveValueLabel = CreateSummaryValueLabel(true);
            selectedDriveValueLabel.AutoEllipsis = true;
            totalSpaceValueLabel = CreateSummaryValueLabel(true);
            usedSpaceValueLabel = CreateSummaryValueLabel(true);
            availableSpaceValueLabel = CreateSummaryValueLabel(true);
            reservedSpaceValueLabel = CreateSummaryValueLabel(true);

            layout.Controls.Add(CreateSummaryCaption("选择:"), 0, 0);
            layout.Controls.Add(selectedDriveValueLabel, 1, 0);
            layout.SetColumnSpan(selectedDriveValueLabel, 3);

            layout.Controls.Add(CreateSummaryCaption("总空间:"), 0, 1);
            layout.Controls.Add(totalSpaceValueLabel, 1, 1);
            layout.Controls.Add(CreateSummaryCaption("预留:"), 2, 1);
            layout.Controls.Add(reservedSpaceValueLabel, 3, 1);

            layout.Controls.Add(CreateSummaryCaption("已用:"), 0, 2);
            layout.Controls.Add(usedSpaceValueLabel, 1, 2);
            layout.SetColumnSpan(usedSpaceValueLabel, 3);

            layout.Controls.Add(CreateSummaryCaption("可用:"), 0, 3);
            layout.Controls.Add(availableSpaceValueLabel, 1, 3);
            layout.SetColumnSpan(availableSpaceValueLabel, 3);
            return layout;
        }

        private Control CreateScanStatusPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.Transparent;
            panel.Padding = new Padding(0, 6, 0, 0);

            scanStatusLabel = new Label();
            scanStatusLabel.Dock = DockStyle.Top;
            scanStatusLabel.Height = 20;
            scanStatusLabel.Font = new Font("Microsoft YaHei UI", 9F);
            scanStatusLabel.ForeColor = TextSecondaryColor;
            scanStatusLabel.BackColor = Color.Transparent;
            scanStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            scanStatusLabel.Text = "等待开始扫描";

            scanProgressTrack = new Panel();
            scanProgressTrack.Dock = DockStyle.Top;
            scanProgressTrack.Height = 14;
            scanProgressTrack.Padding = new Padding(1);
            scanProgressTrack.BackColor = Color.FromArgb(232, 242, 225);
            scanProgressTrack.Resize += ScanProgressTrack_Resize;

            scanProgressFill = new Panel();
            scanProgressFill.Dock = DockStyle.Left;
            scanProgressFill.Width = 0;
            scanProgressFill.BackColor = Color.FromArgb(82, 196, 26);

            scanProgressTrack.Controls.Add(scanProgressFill);

            panel.Controls.Add(scanProgressTrack);
            panel.Controls.Add(scanStatusLabel);
            return panel;
        }

        private Control CreateStoragePanel()
        {
            AntdUI.Panel panel = CreateCardPanel(20);
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

            storageContextMenu = new ContextMenuStrip();
            deleteStorageMenuItem = new ToolStripMenuItem("删除");
            deleteStorageMenuItem.Click += DeleteStorageMenuItem_Click;
            storageContextMenu.Items.Add(deleteStorageMenuItem);

            panel.Controls.Add(storageTable);
            return panel;
        }

        private Control CreateSuggestionAndLogPage()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.RowCount = 2;
            layout.ColumnCount = 1;
            layout.BackColor = PageBackground;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 64F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 36F));

            Control suggestionPanel = CreateSuggestionPanel();
            Control logPanel = CreateLogPanel();
            suggestionPanel.Margin = new Padding(0, 0, 0, 12);
            logPanel.Margin = Padding.Empty;

            layout.Controls.Add(suggestionPanel, 0, 0);
            layout.Controls.Add(logPanel, 0, 1);
            return layout;
        }

        private Control CreateSuggestionPanel()
        {
            AntdUI.Panel panel = CreateCardPanel(20);
            panel.Dock = DockStyle.Fill;

            Label heading = CreateSectionTitle("AI 清理建议");

            Label desc = CreateSectionDescription("默认勾选候选项；双击或点击“查看”可打开对应位置。");

            suggestionTable = new AntdUI.Table();
            suggestionTable.Dock = DockStyle.Fill;
            ConfigureTableSurface(suggestionTable);
            suggestionTable.FixedHeader = true;
            suggestionTable.ScrollBarAvoidHeader = true;
            suggestionTable.CellDoubleClick += SuggestionTable_CellDoubleClick;
            suggestionTable.CellButtonClick += SuggestionTable_CellButtonClick;

            panel.Controls.Add(suggestionTable);
            panel.Controls.Add(desc);
            panel.Controls.Add(heading);
            return panel;
        }

        private Control CreateSettingsPanel()
        {
            AntdUI.Panel panel = CreateCardPanel(20);
            panel.Dock = DockStyle.Fill;

            Label heading = CreateSectionTitle("AI 与沙盒配置");

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;
            layout.ColumnCount = 4;
            layout.RowCount = 6;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            for (int i = 0; i < 5; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            aiEnabledSwitch = new AntdUI.Switch();
            recycleSwitch = new AntdUI.Switch();
            privilegedSwitch = new AntdUI.Switch();
            endpointInput = CreateInput("https://api.openai.com");
            apiKeyInput = CreateInput("sk-...");
            modelInput = CreateInput("gpt-4o-mini");
            maxSuggestionsInput = CreateInput("30");
            allowRootsInput = CreateInput("每行一个允许位置");
            allowRootsInput.Multiline = true;
            allowRootsInput.AutoScroll = true;

            layout.Controls.Add(CreateCaption("AI"), 0, 0);
            layout.Controls.Add(aiEnabledSwitch, 1, 0);
            layout.Controls.Add(CreateCaption("回收站"), 2, 0);
            layout.Controls.Add(recycleSwitch, 3, 0);

            layout.Controls.Add(CreateCaption("完全权限"), 0, 1);
            layout.Controls.Add(privilegedSwitch, 1, 1);
            layout.Controls.Add(CreateCaption("建议条数"), 2, 1);
            layout.Controls.Add(maxSuggestionsInput, 3, 1);

            layout.Controls.Add(CreateCaption("接口地址"), 0, 2);
            layout.Controls.Add(endpointInput, 1, 2);
            layout.SetColumnSpan(endpointInput, 3);

            layout.Controls.Add(CreateCaption("模型"), 0, 3);
            layout.Controls.Add(modelInput, 1, 3);
            layout.Controls.Add(CreateCaption("API Key"), 2, 3);
            layout.Controls.Add(apiKeyInput, 3, 3);

            layout.Controls.Add(CreateCaption("允许位置"), 0, 4);
            layout.Controls.Add(allowRootsInput, 1, 4);
            layout.SetColumnSpan(allowRootsInput, 3);
            layout.SetRowSpan(allowRootsInput, 2);

            panel.Controls.Add(layout);
            panel.Controls.Add(heading);
            return panel;
        }

        private Control CreateLogPanel()
        {
            AntdUI.Panel panel = CreateCardPanel(20);
            panel.Dock = DockStyle.Fill;

            Label heading = CreateSectionTitle("执行日志");

            logInput = CreateInput(string.Empty);
            logInput.Dock = DockStyle.Fill;
            logInput.Multiline = true;
            logInput.ReadOnly = true;
            logInput.AutoScroll = true;
            logInput.MaxLength = int.MaxValue;

            panel.Controls.Add(logInput);
            panel.Controls.Add(heading);
            return panel;
        }

        private static AntdUI.MenuItem CreateNavigationItem(string id, string text, string iconSvg)
        {
            AntdUI.MenuItem item = new AntdUI.MenuItem(text);
            item.ID = id;
            item.IconSvg = iconSvg;
            return item;
        }

        private void SidebarResizeRail_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
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
            if (sidebarHost != null) sidebarHost.Width = sidebarWidth + SidebarRailWidth;
            if (sidebarBrandPanel != null) sidebarBrandPanel.Height = 70;
            if (sidebarBrandPanel != null) sidebarBrandPanel.Padding = new Padding(6, 10, 6, 14);
            if (sidebarPanel != null) sidebarPanel.Padding = new Padding(12, 10, 12, 12);
            if (settingsNavButton != null) settingsNavButton.Width = 42;
            if (settingsNavButton != null) settingsNavButton.Dock = DockStyle.Left;
            if (settingsNavButton != null && settingsNavButton.Parent != null)
            {
                settingsNavButton.Parent.Padding = new Padding(10, 10, 10, 10);
                settingsNavButton.Left = 0;
                settingsNavButton.Top = 0;
            }
            UpdateSettingsNavigationState();
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
            settingsNavButton.BackColor = selected ? Color.FromArgb(230, 240, 255) : SurfaceColor;
            settingsNavButton.DefaultBorderColor = selected ? Color.FromArgb(145, 202, 255) : BorderLightColor;
            settingsNavButton.ForeColor = selected ? PrimaryColor : TextSecondaryColor;
        }

        private void SetActivePage(string pageId)
        {
            activePageId = pageId;
            scanPage.Visible = pageId == PageScan;
            suggestionsPage.Visible = pageId == PageSuggestions;
            logPage.Visible = pageId == PageLog;
            settingsPage.Visible = pageId == PageSettings;

            if (scanPage.Visible) scanPage.BringToFront();
            if (suggestionsPage.Visible) suggestionsPage.BringToFront();
            if (logPage.Visible) logPage.BringToFront();
            if (settingsPage.Visible) settingsPage.BringToFront();

            titleBar.Text = GetPageTitle(pageId);
            titleBar.Description = GetPageDescription(pageId);

            scanButton.Visible = pageId == PageScan;
            analyzeButton.Visible = pageId == PageSuggestions;
            deleteButton.Visible = pageId == PageSuggestions;
            saveSettingsButton.Visible = pageId == PageSettings;
            SyncNavigationSelection(pageId);
            UpdateSettingsNavigationState();
        }

        private static string GetPageTitle(string pageId)
        {
            switch (pageId)
            {
                case PageSuggestions:
                    return AppDisplayName + " · AI 建议与查看";
                case PageLog:
                    return AppDisplayName + " · 日志管理";
                case PageSettings:
                    return AppDisplayName + " · 设置界面";
                default:
                    return AppDisplayName + " · 扫描界面";
            }
        }

        private static string GetPageDescription(string pageId)
        {
            switch (pageId)
            {
                case PageSuggestions:
                    return "查看 AI / 本地规则生成的清理建议，支持定位和批量删除。";
                case PageLog:
                    return "查看扫描、建议与删除流程的执行日志。";
                case PageSettings:
                    return "配置 AI 接口、建议数量、沙盒白名单和删除策略。";
                default:
                    return "选择磁盘或目录，扫描空间占用，并快速进入空间树分析。";
            }
        }

        private string GetActivePageDescription()
        {
            return GetPageDescription(string.IsNullOrWhiteSpace(activePageId) ? PageScan : activePageId);
        }

        private void ConfigureTables()
        {
            storageTable.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("name", "名称").SetTree("Children").SetWidth("auto"),
                new AntdUI.Column("size", "大小", AntdUI.ColumnAlign.Right).SetWidth("112"),
                new AntdUI.Column("kind", "类型", AntdUI.ColumnAlign.Center).SetWidth("86"),
                new AntdUI.Column("files", "文件数", AntdUI.ColumnAlign.Right).SetWidth("90"),
                new AntdUI.Column("dirs", "子目录", AntdUI.ColumnAlign.Right).SetWidth("90"),
                new AntdUI.Column("path", "完整路径").SetWidth("auto")
            };

            suggestionTable.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.ColumnCheck("selected", "选中").SetWidth("64"),
                new AntdUI.Column("name", "名称").SetWidth("160"),
                new AntdUI.Column("size", "大小", AntdUI.ColumnAlign.Right).SetWidth("104"),
                new AntdUI.Column("risk", "风险", AntdUI.ColumnAlign.Center).SetWidth("88"),
                new AntdUI.Column("sandbox", "沙盒", AntdUI.ColumnAlign.Center).SetWidth("96"),
                new AntdUI.Column("status", "状态", AntdUI.ColumnAlign.Center).SetWidth("86"),
                new AntdUI.Column("source", "来源").SetWidth("90"),
                new AntdUI.Column("reason", "原因").SetWidth("auto"),
                new AntdUI.Column("actions", "操作").SetWidth("86")
            };
        }

        private void LoadSettingsToUi()
        {
            settings.EnsureDefaults();
            aiEnabledSwitch.Checked = settings.Ai.Enabled;
            recycleSwitch.Checked = settings.Sandbox.UseRecycleBin;
            privilegedSwitch.Checked = settings.Sandbox.FullyPrivilegedMode;
            endpointInput.Text = settings.Ai.Endpoint;
            apiKeyInput.Text = settings.Ai.ApiKey;
            modelInput.Text = settings.Ai.Model;
            maxSuggestionsInput.Text = settings.Ai.MaxSuggestions.ToString();
            minSizeInput.Text = settings.Scan.MinSizeMb.ToString();
            limitInput.Text = settings.Scan.PerLevelLimit.ToString();
            sortSelect.SelectedValue = settings.Scan.SortMode;
            allowRootsInput.Text = string.Join(Environment.NewLine, new List<string>(settings.Sandbox.AllowedRoots).ToArray());
        }

        private void SaveSettings()
        {
            try
            {
                SaveSettingsFromUi();
                settingsStore.Save(settings);
                Log("配置已保存。");
                MessageBox.Show(this, "配置已保存。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("保存配置失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveSettingsFromUi()
        {
            settings.Ai.Enabled = aiEnabledSwitch.Checked;
            settings.Ai.Endpoint = endpointInput.Text.Trim();
            settings.Ai.ApiKey = apiKeyInput.Text.Trim();
            settings.Ai.Model = modelInput.Text.Trim();
            settings.Ai.MaxSuggestions = ParsePositiveInt(maxSuggestionsInput.Text, 30);
            settings.Sandbox.UseRecycleBin = recycleSwitch.Checked;
            settings.Sandbox.FullyPrivilegedMode = privilegedSwitch.Checked;
            settings.Sandbox.AllowedRoots = ParseLines(allowRootsInput.Text);
            settings.Scan.MinSizeMb = ParseInt(minSizeInput.Text, -1);
            settings.Scan.PerLevelLimit = ParseInt(limitInput.Text, -1);
            if (sortSelect.SelectedValue is ScanSortMode) settings.Scan.SortMode = (ScanSortMode)sortSelect.SelectedValue;
            settings.EnsureDefaults();
        }

        private void LoadDrives()
        {
            driveSelect.Items.Clear();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed) continue;
                driveSelect.Items.Add(new AntdUI.SelectItem(drive.Name, drive.Name));
            }

            string defaultDrive = Environment.GetEnvironmentVariable("SystemDrive");
            if (string.IsNullOrWhiteSpace(defaultDrive)) defaultDrive = "C:";
            defaultDrive = defaultDrive.TrimEnd('\\') + "\\";
            driveSelect.SelectedValue = defaultDrive;
            pathInput.Text = defaultDrive;
            UpdateDriveSummaryForLocation(defaultDrive);
        }

        private void DriveSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (e.Value == null) return;
            pathInput.Text = e.Value.ToString();
            UpdateDriveSummaryForLocation(pathInput.Text);
        }

        private void PathInput_TextChanged(object sender, EventArgs e)
        {
            string location = pathInput.Text;
            if (string.IsNullOrWhiteSpace(location) && driveSelect != null && driveSelect.SelectedValue != null)
            {
                location = driveSelect.SelectedValue.ToString();
            }
            UpdateDriveSummaryForLocation(location);
        }

        private void ScanCurrentLocation()
        {
            SaveSettingsFromUi();
            ScanRequest request = BuildScanRequest(1);
            StorageItem result = null;
            DateTime scanStartedAt = DateTime.UtcNow;
            ClearScanProviderCache();
            currentTreeVersion++;
            UpdateScanProgressState("正在扫描空间占用...", 0.56F, true, AntdUI.TType.None);

            RunBackground("正在扫描空间占用…", delegate
            {
                result = scanProvider.Scan(request);
            }, delegate
            {
                TimeSpan elapsed = DateTime.UtcNow - scanStartedAt;
                currentRoot = result;
                currentTreeRequest = CreateScanRequest(result.Path, 1, request);
                List<StorageEntryRow> rows = new List<StorageEntryRow> { new StorageEntryRow(result) };
                storageTable.DataSource = rows;
                UpdateDriveSummaryForLocation(result.Path);
                UpdateScanProgressState("扫描完成 " + elapsed.TotalSeconds.ToString("0.00") + " 秒", 1F, false, AntdUI.TType.Success);
                Log("扫描完成：" + result.Path + "，大小 " + StorageFormatting.FormatBytes(result.Bytes));
            }, delegate
            {
                UpdateScanProgressState("扫描失败", 1F, false, AntdUI.TType.Error);
            });
        }

        private void AnalyzeSuggestions()
        {
            if (currentRoot == null)
            {
                MessageBox.Show(this, "请先完成一次扫描。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveSettingsFromUi();
            IList<CleanupSuggestion> suggestions = null;
            StorageItem analysisRoot = null;
            ScanRequest request = BuildScanRequest(currentRoot.Path, -1);

            RunBackground("正在生成 AI 清理建议…", delegate
            {
                analysisRoot = scanProvider.Scan(request);
                IList<CleanupCandidate> candidates = candidatePlanner.BuildCandidates(analysisRoot, Math.Max(67108864L, settings.Scan.MinSizeMb * 1024L * 1024L / 2L), settings.Ai.MaxSuggestions * 4);
                suggestions = aiAdvisor.Analyze(analysisRoot, candidates, settings);
                EvaluateSandbox(suggestions);
            }, delegate
            {
                BindSuggestions(suggestions);
                Log((settings.Ai.Enabled ? "AI" : "本地规则") + " 建议生成完成，共 " + suggestionRows.Count + " 项。");
            });
        }

        private void DeleteSelectedSuggestions()
        {
            if (suggestionRows == null || suggestionRows.Count == 0)
            {
                MessageBox.Show(this, "当前没有可删除的建议项。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<CleanupSuggestionRow> selectedRows = new List<CleanupSuggestionRow>();
            int needConfirmation = 0;
            for (int i = 0; i < suggestionRows.Count; i++)
            {
                CleanupSuggestionRow row = suggestionRows[i];
                if (!row.Suggestion.Selected || row.Suggestion.Status == CleanupStatus.Deleted) continue;
                selectedRows.Add(row);
                if (row.Suggestion.Sandbox != null && row.Suggestion.Sandbox.Action == SandboxAction.RequireConfirmation) needConfirmation++;
            }

            if (selectedRows.Count == 0)
            {
                MessageBox.Show(this, "请先勾选至少一项。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult confirm = MessageBox.Show(this,
                "即将删除 " + selectedRows.Count + " 项。" + (needConfirmation > 0 ? "其中 " + needConfirmation + " 项未命中白名单，需要你承担确认责任。" : string.Empty),
                "确认删除",
                MessageBoxButtons.OKCancel,
                needConfirmation > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;

            List<DeletionOutcome> outcomes = new List<DeletionOutcome>();
            RunBackground("正在执行删除…", delegate
            {
                for (int i = 0; i < selectedRows.Count; i++)
                {
                    CleanupSuggestionRow row = selectedRows[i];
                    CleanupResult result = deletionService.Delete(row.Suggestion, settings.Sandbox.UseRecycleBin);
                    outcomes.Add(new DeletionOutcome { Row = row, Result = result });
                }
            }, delegate
            {
                for (int i = 0; i < outcomes.Count; i++)
                {
                    DeletionOutcome outcome = outcomes[i];
                    if (outcome.Result.Success) outcome.Row.SetStatus(CleanupStatus.Deleted, outcome.Result.Message);
                    else outcome.Row.SetStatus(CleanupStatus.Failed, outcome.Result.Message);
                }
                suggestionTable.Refresh();
                Log("删除流程执行完成。");
            });
        }

        private void EvaluateSandbox(IList<CleanupSuggestion> suggestions)
        {
            if (suggestions == null) return;
            bool elevated = privilegeService.IsProcessElevated();
            for (int i = 0; i < suggestions.Count; i++)
            {
                suggestions[i].Sandbox = deletionSandbox.Evaluate(suggestions[i].Path, settings.Sandbox, elevated);
            }
        }

        private void BindSuggestions(IList<CleanupSuggestion> suggestions)
        {
            suggestionRows = new List<CleanupSuggestionRow>();
            if (suggestions != null)
            {
                for (int i = 0; i < suggestions.Count; i++) suggestionRows.Add(new CleanupSuggestionRow(suggestions[i]));
            }
            suggestionTable.DataSource = suggestionRows;
        }

        private void StorageTable_ExpandChanged(object sender, AntdUI.TableExpandEventArgs e)
        {
            if (!e.Expand) return;

            StorageEntryRow row = e.Record as StorageEntryRow;
            if (row == null || row.Item == null) return;
            SetPathInputFromStorageRow(row);
            if (!row.Item.IsDirectory || row.Item.ChildrenLoaded || !row.Item.HasChildren) return;
            if (currentTreeRequest == null) return;

            if (row.IsLoadingChildren) return;

            row.IsLoadingChildren = true;

            ScanRequest request = CreateScanRequest(row.Item.Path, 1, currentTreeRequest);
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
                        MessageBox.Show(this, error.Message, "加载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    ApplyScannedNode(row.Item, loaded);
                    row.RefreshFromItem();
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

        private void StorageTable_CellClick(object sender, AntdUI.TableClickEventArgs eventArgs)
        {
            if (storageTable != null && storageTable.CanFocus) storageTable.Focus();
            if (eventArgs.Button != MouseButtons.Right) return;

            StorageEntryRow row = eventArgs.Record as StorageEntryRow;
            if (row == null || row.Item == null) return;

            storageContextRow = row;
            storageTable.SetSelected(row);
            deleteStorageMenuItem.Enabled = CanOfferStorageDelete(row);
            deleteStorageMenuItem.Text = "删除" + (row.Item.IsDirectory ? "文件夹" : "文件");
            storageContextMenu.Show(storageTable, new Point(eventArgs.X, eventArgs.Y));
        }

        private void DeleteStorageMenuItem_Click(object sender, EventArgs eventArgs)
        {
            DeleteStorageRow(storageContextRow);
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
            if (busy || activePageId != PageScan || IsEditingTextInput()) return false;

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
            return selectedIndex > 0 ? GetStorageRowAtIndex(selectedIndex - 1) : null;
        }

        private bool IsEditingTextInput()
        {
            return ControlHasFocus(pathInput) ||
                ControlHasFocus(minSizeInput) ||
                ControlHasFocus(limitInput) ||
                ControlHasFocus(endpointInput) ||
                ControlHasFocus(apiKeyInput) ||
                ControlHasFocus(modelInput) ||
                ControlHasFocus(maxSuggestionsInput) ||
                ControlHasFocus(allowRootsInput) ||
                ControlHasFocus(logInput);
        }

        private static bool ControlHasFocus(Control control)
        {
            return control != null && control.ContainsFocus;
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
            SandboxEvaluation sandbox = deletionSandbox.Evaluate(row.Item.Path, settings.Sandbox, privilegeService.IsProcessElevated());
            if (!ConfirmStorageDelete(row, sandbox)) return;

            CleanupSuggestion suggestion = CreateManualStorageSuggestion(row, sandbox);
            CleanupResult deleteResult = null;

            RunBackground("正在删除文件树项目…", delegate
            {
                deleteResult = deletionService.Delete(suggestion, settings.Sandbox.UseRecycleBin);
            }, delegate
            {
                if (deleteResult != null && deleteResult.Success)
                {
                    RemoveDeletedStorageRow(row);
                    Log("文件树删除完成：" + suggestion.Path + "，" + deleteResult.Message);
                    return;
                }

                string message = deleteResult == null ? "删除失败。" : deleteResult.Message;
                Log("文件树删除失败：" + suggestion.Path + "，" + message);
                MessageBox.Show(this, message, "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            });
        }

        private bool ValidateStorageDeleteTarget(StorageEntryRow row)
        {
            if (row == null || row.Item == null || string.IsNullOrWhiteSpace(row.Item.Path))
            {
                MessageBox.Show(this, "删除目标为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (IsProtectedStorageDeleteTarget(row.Item.Path))
            {
                MessageBox.Show(this, "为避免误删，不支持直接删除当前扫描根或磁盘根目录。请展开到具体子项后再删除。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private bool ConfirmStorageDelete(StorageEntryRow row, SandboxEvaluation sandbox)
        {
            string targetType = row.Item.IsDirectory ? "文件夹" : "文件";
            string actionText = settings.Sandbox.UseRecycleBin ? "将此" + targetType + "移入回收站" : "永久删除此" + targetType;
            string message = "确定要" + actionText + "吗？" +
                Environment.NewLine + Environment.NewLine +
                row.Item.Path +
                Environment.NewLine + Environment.NewLine +
                "大小：" + StorageFormatting.FormatBytes(row.Item.Bytes);

            if (sandbox != null && sandbox.Action == SandboxAction.RequireConfirmation)
            {
                message += Environment.NewLine + Environment.NewLine + "注意：该路径未命中沙盒允许位置，请确认确实要删除。";
            }

            if (!settings.Sandbox.UseRecycleBin)
            {
                message += Environment.NewLine + Environment.NewLine + "当前配置为永久删除，无法从回收站恢复。";
            }

            MessageBoxIcon icon = !settings.Sandbox.UseRecycleBin || (sandbox != null && sandbox.Action == SandboxAction.RequireConfirmation)
                ? MessageBoxIcon.Warning
                : MessageBoxIcon.Question;
            DialogResult confirm = MessageBox.Show(this, message, "确认删除", MessageBoxButtons.OKCancel, icon);
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

            List<StorageItem> ancestors = new List<StorageItem>();
            if (!TryRemoveStorageItem(currentRoot, row.Item, ancestors))
            {
                storageTable.Refresh();
                return;
            }

            AdjustAncestorStats(ancestors, row.Item);
            UpdatePathAfterStorageDelete(row, ancestors);
            RebindStorageTree();
            currentTreeVersion++;
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

            string driveRoot = TryGetDriveRoot(path);
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

        private ScanRequest BuildScanRequest(int loadDepth)
        {
            string location = string.IsNullOrWhiteSpace(pathInput.Text) ? "C:\\" : pathInput.Text.Trim();
            return BuildScanRequest(location, loadDepth);
        }

        private ScanRequest BuildScanRequest(string location, int loadDepth)
        {
            return new ScanRequest
            {
                Location = location,
                MinSizeBytes = ParseMinSizeBytes(minSizeInput.Text, -1),
                PerLevelLimit = ParseInt(limitInput.Text, -1),
                SortMode = sortSelect.SelectedValue is ScanSortMode ? (ScanSortMode)sortSelect.SelectedValue : ScanSortMode.Allocated,
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
            return request;
        }

        private void StorageTable_CellDoubleClick(object sender, AntdUI.TableClickEventArgs e)
        {
            StorageEntryRow row = e.Record as StorageEntryRow;
            if (row == null || row.Item == null) return;
            explorerService.OpenPath(row.path, !row.Item.IsDirectory);
        }

        private void SuggestionTable_CellDoubleClick(object sender, AntdUI.TableClickEventArgs e)
        {
            CleanupSuggestionRow row = e.Record as CleanupSuggestionRow;
            if (row == null) return;
            explorerService.OpenPath(row.path, !row.Suggestion.IsDirectory);
        }

        private void SuggestionTable_CellButtonClick(object sender, AntdUI.TableButtonEventArgs e)
        {
            CleanupSuggestionRow row = e.Record as CleanupSuggestionRow;
            if (row == null) return;
            explorerService.OpenPath(row.path, !row.Suggestion.IsDirectory);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Delete && TryHandleStorageDeleteShortcut()) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplySidebarWidth(ResolveInitialSidebarWidth());
            ApplyNormalWindowBounds(true);
            lastWindowState = WindowState;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && backgroundWorker != null) backgroundWorker.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            FormWindowState previousState = lastWindowState;
            base.OnSizeChanged(e);
            if (!applyingNormalBounds && WindowState == FormWindowState.Normal && previousState != FormWindowState.Normal && IsHandleCreated)
            {
                BeginInvoke((MethodInvoker)delegate { ApplyNormalWindowBounds(false); });
            }
            lastWindowState = WindowState;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmGetMinMaxInfo)
            {
                UpdateMaximizedBounds(m.LParam);
                m.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref m);
        }

        private void UpdateMaximizedBounds(IntPtr lParam)
        {
            if (lParam == IntPtr.Zero) return;

            MinMaxInfo info = (MinMaxInfo)Marshal.PtrToStructure(lParam, typeof(MinMaxInfo));
            Screen screen = Screen.FromHandle(Handle);
            Rectangle monitorArea = screen.Bounds;
            Rectangle workArea = screen.WorkingArea;

            info.MaxPosition.X = workArea.Left - monitorArea.Left;
            info.MaxPosition.Y = workArea.Top - monitorArea.Top;
            info.MaxSize.X = workArea.Width;
            info.MaxSize.Y = workArea.Height;
            Size minimumSize = GetConstrainedMinimumSize(workArea);
            info.MinTrackSize.X = minimumSize.Width;
            info.MinTrackSize.Y = minimumSize.Height;

            Marshal.StructureToPtr(info, lParam, false);
        }

        private void ApplyNormalWindowBounds(bool centerWhenShrunk)
        {
            if (!IsHandleCreated || WindowState != FormWindowState.Normal) return;

            Rectangle currentBounds = Bounds;
            Screen screen = Screen.FromRectangle(currentBounds);
            Rectangle workArea = screen.WorkingArea;
            Size constrainedMinimum = GetConstrainedMinimumSize(workArea);
            if (!MinimumSize.Equals(constrainedMinimum)) MinimumSize = constrainedMinimum;

            int width = Clamp(currentBounds.Width, constrainedMinimum.Width, workArea.Width);
            int height = Clamp(currentBounds.Height, constrainedMinimum.Height, workArea.Height);

            int left;
            int top;
            bool shrunk = width != currentBounds.Width || height != currentBounds.Height;
            if (centerWhenShrunk && shrunk)
            {
                left = workArea.Left + Math.Max(0, (workArea.Width - width) / 2);
                top = workArea.Top + Math.Max(0, (workArea.Height - height) / 2);
            }
            else
            {
                left = Clamp(currentBounds.Left, workArea.Left, workArea.Right - width);
                top = Clamp(currentBounds.Top, workArea.Top, workArea.Bottom - height);
            }

            Rectangle normalizedBounds = new Rectangle(left, top, width, height);
            if (normalizedBounds.Equals(currentBounds)) return;

            applyingNormalBounds = true;
            try
            {
                Bounds = normalizedBounds;
            }
            finally
            {
                applyingNormalBounds = false;
            }
        }

        private static Size GetConstrainedMinimumSize(Rectangle workArea)
        {
            int width = Math.Min(BaseMinimumWindowSize.Width, Math.Max(1, workArea.Width));
            int height = Math.Min(BaseMinimumWindowSize.Height, Math.Max(1, workArea.Height));
            return new Size(width, height);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min) return min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void RunBackground(string caption, Action action, Action onSuccess)
        {
            RunBackground(caption, action, onSuccess, null);
        }

        private void RunBackground(string caption, Action action, Action onSuccess, Action onError)
        {
            SetBusy(true, caption);
            Exception error = null;
            backgroundWorker.Enqueue(delegate
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                BeginInvoke((MethodInvoker)delegate
                {
                    SetBusy(false, GetActivePageDescription());
                    if (error != null)
                    {
                        if (onError != null) onError();
                        Log(caption + "失败：" + error.Message);
                        MessageBox.Show(this, error.Message, "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (onSuccess != null) onSuccess();
                });
            });
        }

        private void SetBusy(bool busy, string description)
        {
            this.busy = busy;
            UseWaitCursor = busy;
            if (navigationMenu != null) navigationMenu.Enabled = !busy;
            if (settingsNavButton != null) settingsNavButton.Enabled = !busy;
            if (sidebarResizeRail != null) sidebarResizeRail.Enabled = !busy;
            scanButton.Enabled = !busy;
            if (driveSelect != null) driveSelect.Enabled = !busy;
            if (pathInput != null) pathInput.Enabled = !busy;
            if (minSizeInput != null) minSizeInput.Enabled = !busy;
            if (limitInput != null) limitInput.Enabled = !busy;
            if (sortSelect != null) sortSelect.Enabled = !busy;
            analyzeButton.Enabled = !busy;
            deleteButton.Enabled = !busy;
            saveSettingsButton.Enabled = !busy;
            titleBar.Description = description;
        }

        private void Log(string message)
        {
            if (logInput == null) return;
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message;
            if (string.IsNullOrWhiteSpace(logInput.Text)) logInput.Text = line;
            else logInput.Text += Environment.NewLine + line;
        }

        private void LogBackground(string message)
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
            scanProgressValue = value;

            if (scanProgressTrack == null || scanProgressFill == null) return;

            if (state == AntdUI.TType.Error)
            {
                scanProgressTrack.BackColor = Color.FromArgb(255, 232, 232);
                scanProgressFill.BackColor = Color.FromArgb(255, 77, 79);
            }
            else if (loading)
            {
                scanProgressTrack.BackColor = Color.FromArgb(232, 243, 255);
                scanProgressFill.BackColor = Color.FromArgb(22, 119, 255);
            }
            else
            {
                scanProgressTrack.BackColor = Color.FromArgb(232, 242, 225);
                scanProgressFill.BackColor = Color.FromArgb(82, 196, 26);
            }

            RefreshScanProgressFill();
        }

        private void ScanProgressTrack_Resize(object sender, EventArgs e)
        {
            RefreshScanProgressFill();
        }

        private void RefreshScanProgressFill()
        {
            if (scanProgressTrack == null || scanProgressFill == null) return;
            int innerWidth = Math.Max(0, scanProgressTrack.ClientSize.Width - scanProgressTrack.Padding.Horizontal);
            int innerHeight = Math.Max(0, scanProgressTrack.ClientSize.Height - scanProgressTrack.Padding.Vertical);
            int fillWidth = (int)Math.Round(innerWidth * scanProgressValue);
            if (fillWidth < 0) fillWidth = 0;
            if (fillWidth > innerWidth) fillWidth = innerWidth;
            scanProgressFill.Width = fillWidth;
            scanProgressFill.Height = innerHeight;
        }

        private static string FormatBytesWithPercent(long bytes, long totalBytes)
        {
            if (totalBytes <= 0) return StorageFormatting.FormatBytes(bytes);
            double percent = (double)bytes / totalBytes * 100D;
            return StorageFormatting.FormatBytes(bytes) + " (" + percent.ToString("0.0") + "%)";
        }

        private static void SetDriveSummaryValue(Label label, string text)
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

        private static IList<string> ParseLines(string text)
        {
            List<string> result = new List<string>();
            string[] parts = (text ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string value = parts[i].Trim();
                if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
            }
            return result;
        }

        private static int ParsePositiveInt(string text, int fallback)
        {
            int parsed;
            return int.TryParse(text, out parsed) && parsed > 0 ? parsed : fallback;
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
            string[] menuItems = { "扫描", "AI 建议", "日志管理" };
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
            settings.Ui.SidebarWidth = sidebarWidth;
            settingsStore.Save(settings);
        }

        private static int ClampSidebarWidth(int width)
        {
            return Math.Max(SidebarMinWidth, Math.Min(SidebarMaxWidth, width));
        }

        private static int MeasureTextWidth(string text, Font font)
        {
            return TextRenderer.MeasureText(text ?? string.Empty, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
        }

        private static int MeasureSelectWidth(Font font, params string[] options)
        {
            int maxTextWidth = 0;
            for (int i = 0; i < options.Length; i++)
            {
                maxTextWidth = Math.Max(maxTextWidth, MeasureTextWidth(options[i], font));
            }

            return maxTextWidth + 40;
        }

        private static Label CreateToolbarCaption(string text)
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Text = text;
            label.Font = new Font("Microsoft YaHei UI", 9.5F);
            label.ForeColor = TextSecondaryColor;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static Label CreateSummaryCaption(string text)
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Text = text;
            label.Font = new Font("Microsoft YaHei UI", 9F);
            label.ForeColor = TextSecondaryColor;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static Label CreateSummaryValueLabel(bool bold)
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = new Font("Microsoft YaHei UI", bold ? 10F : 9.5F, bold ? FontStyle.Bold : FontStyle.Regular);
            label.ForeColor = TextPrimaryColor;
            label.BackColor = Color.Transparent;
            label.Text = "-";
            return label;
        }

        private static int ParseInt(string text, int fallback)
        {
            int parsed;
            return int.TryParse(text, out parsed) ? parsed : fallback;
        }

        private static AntdUI.Panel CreateCardPanel(int padding)
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Padding = new Padding(padding);
            panel.Radius = 12;
            panel.Back = SurfaceColor;
            panel.BorderWidth = 1F;
            panel.BorderColor = BorderLightColor;
            panel.Shadow = 6;
            panel.ShadowOpacity = 0.06F;
            panel.ShadowOffsetY = 2;
            return panel;
        }

        private static Label CreateSectionTitle(string text)
        {
            Label heading = new Label();
            heading.Dock = DockStyle.Top;
            heading.Height = 34;
            heading.Text = text;
            heading.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            heading.ForeColor = TextPrimaryColor;
            heading.BackColor = Color.Transparent;
            return heading;
        }

        private static Label CreateSectionDescription(string text)
        {
            Label desc = new Label();
            desc.Dock = DockStyle.Top;
            desc.Height = 26;
            desc.Text = text;
            desc.ForeColor = TextSecondaryColor;
            desc.BackColor = Color.Transparent;
            return desc;
        }

        private static void ConfigureTableSurface(AntdUI.Table table)
        {
            table.Bordered = true;
            table.Radius = 8;
            table.BorderWidth = 1F;
            table.BorderCellWidth = 1F;
            table.BorderColor = BorderLightColor;
            table.ColumnBack = FillSecondary;
            table.ColumnFore = TextSecondaryColor;
            table.RowHoverBg = Color.FromArgb(230, 244, 255);
            table.RowSelectedBg = Color.FromArgb(240, 247, 255);
        }

        private AntdUI.Button CreateHeaderButton(string text, AntdUI.TTypeMini type)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.Dock = DockStyle.Right;
            button.AutoSizeMode = AntdUI.TAutoSize.Width;
            button.Text = text;
            button.Type = type;
            button.Width = 120;
            button.Height = 36;
            button.Radius = 6;
            button.BorderWidth = 1F;
            button.Margin = new Padding(8, 12, 0, 12);
            return button;
        }

        private static AntdUI.Button CreateToolbarActionButton(string text, AntdUI.TTypeMini type)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.Text = text;
            button.Type = type;
            button.Width = 92;
            button.Height = 36;
            button.Radius = 6;
            button.BorderWidth = 1F;
            button.Margin = Padding.Empty;
            return button;
        }

        private static AntdUI.Input CreateInput(string placeholder)
        {
            AntdUI.Input input = new AntdUI.Input();
            input.Dock = DockStyle.Fill;
            input.PlaceholderText = placeholder;
            input.Font = new Font("Microsoft YaHei UI", 10.5F);
            input.Radius = 6;
            input.BorderWidth = 1F;
            input.BorderColor = BorderDefaultColor;
            input.BorderHover = PrimaryColor;
            input.BorderActive = PrimaryColor;
            input.BackColor = SurfaceColor;
            return input;
        }

        private static Label CreateCaption(string text)
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Text = text;
            label.Font = new Font("Microsoft YaHei UI", 9.5F);
            label.ForeColor = TextSecondaryColor;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static Control CreateInfoCard(string title, out Label valueLabel)
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Dock = DockStyle.Fill;
            panel.Radius = 8;
            panel.Padding = new Padding(12, 8, 12, 8);
            panel.Back = FillSecondary;
            panel.BorderWidth = 1F;
            panel.BorderColor = BorderLightColor;

            Label titleLabel = new Label();
            titleLabel.Dock = DockStyle.Top;
            titleLabel.Height = 18;
            titleLabel.Text = title;
            titleLabel.Font = new Font("Microsoft YaHei UI", 9F);
            titleLabel.ForeColor = TextTertiaryColor;
            titleLabel.BackColor = Color.Transparent;

            valueLabel = new Label();
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            valueLabel.ForeColor = TextPrimaryColor;
            valueLabel.BackColor = Color.Transparent;
            valueLabel.TextAlign = ContentAlignment.MiddleLeft;
            valueLabel.Text = "-";

            panel.Controls.Add(valueLabel);
            panel.Controls.Add(titleLabel);
            return panel;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MinMaxInfo
        {
            public NativePoint Reserved;
            public NativePoint MaxSize;
            public NativePoint MaxPosition;
            public NativePoint MinTrackSize;
            public NativePoint MaxTrackSize;
        }

        private sealed class DeletionOutcome
        {
            public CleanupSuggestionRow Row { get; set; }
            public CleanupResult Result { get; set; }
        }
    }
}
