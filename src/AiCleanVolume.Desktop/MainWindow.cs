using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
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
        private readonly CandidatePlanner candidatePlanner;
        private readonly IAiCleanupAdvisor aiAdvisor;
        private readonly IDeletionSandbox deletionSandbox;
        private readonly IDeletionService deletionService;
        private readonly IExplorerService explorerService;
        private readonly IPrivilegeService privilegeService;

        private ApplicationSettings settings;
        private StorageItem currentRoot;
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

        private AntdUI.Switch aiEnabledSwitch;
        private AntdUI.Switch recycleSwitch;
        private AntdUI.Switch privilegedSwitch;
        private AntdUI.Input endpointInput;
        private AntdUI.Input apiKeyInput;
        private AntdUI.Input modelInput;
        private AntdUI.Input maxSuggestionsInput;
        private AntdUI.Input allowRootsInput;
        private AntdUI.Input logInput;

        private Label rootValueLabel;
        private Label scanValueLabel;
        private Label suggestionValueLabel;

        private readonly string defaultDescription = "选择磁盘或目录，扫描空间占用，生成可确认的安全清理建议";
        private FormWindowState lastWindowState;
        private bool applyingNormalBounds;
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

            scanButton = CreateHeaderButton("重新扫描", AntdUI.TTypeMini.Primary);
            scanButton.Click += delegate { ScanCurrentLocation(); };

            titleBar.Controls.Add(saveSettingsButton);
            titleBar.Controls.Add(deleteButton);
            titleBar.Controls.Add(analyzeButton);
            titleBar.Controls.Add(scanButton);

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
            toolbarHost.Height = 156;
            toolbarHost.Padding = new Padding(0, 0, 0, 12);

            AntdUI.Panel toolbarCard = CreateCardPanel(16);
            toolbarCard.Dock = DockStyle.Fill;
            toolbarCard.Shadow = 3;
            toolbarCard.ShadowOpacity = 0.04F;

            TableLayoutPanel toolbarLayout = new TableLayoutPanel();
            toolbarLayout.Dock = DockStyle.Fill;
            toolbarLayout.BackColor = Color.Transparent;
            toolbarLayout.ColumnCount = 10;
            toolbarLayout.RowCount = 2;
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132F));
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108F));
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108F));
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132F));
            toolbarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            toolbarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            driveSelect = new AntdUI.Select();
            driveSelect.Dock = DockStyle.Fill;
            driveSelect.DropDownArrow = true;
            driveSelect.ListAutoWidth = true;
            driveSelect.Font = Font;
            driveSelect.SelectedValueChanged += DriveSelect_SelectedValueChanged;

            pathInput = CreateInput("输入盘符或目录，例如 C:\\");
            minSizeInput = CreateInput("128，负数=不限");
            limitInput = CreateInput("80，负数=不限");

            sortSelect = new AntdUI.Select();
            sortSelect.Dock = DockStyle.Fill;
            sortSelect.DropDownArrow = true;
            sortSelect.ListAutoWidth = true;
            sortSelect.Font = Font;
            sortSelect.Items.Add(new AntdUI.SelectItem("分配大小", ScanSortMode.Allocated));
            sortSelect.Items.Add(new AntdUI.SelectItem("逻辑大小", ScanSortMode.Logical));

            toolbarLayout.Controls.Add(CreateCaption("盘符"), 0, 0);
            toolbarLayout.Controls.Add(driveSelect, 1, 0);
            toolbarLayout.Controls.Add(CreateCaption("位置"), 2, 0);
            toolbarLayout.Controls.Add(pathInput, 3, 0);
            toolbarLayout.Controls.Add(CreateCaption("最小 MB"), 4, 0);
            toolbarLayout.Controls.Add(minSizeInput, 5, 0);
            toolbarLayout.Controls.Add(CreateCaption("每层限制"), 6, 0);
            toolbarLayout.Controls.Add(limitInput, 7, 0);
            toolbarLayout.Controls.Add(CreateCaption("排序"), 8, 0);
            toolbarLayout.Controls.Add(sortSelect, 9, 0);

            Control rootCard = CreateInfoCard("当前根路径", out rootValueLabel);
            Control scanCard = CreateInfoCard("最近扫描", out scanValueLabel);
            Control suggestionCard = CreateInfoCard("建议数量", out suggestionValueLabel);

            toolbarLayout.Controls.Add(rootCard, 0, 1);
            toolbarLayout.SetColumnSpan(rootCard, 4);
            toolbarLayout.Controls.Add(scanCard, 4, 1);
            toolbarLayout.SetColumnSpan(scanCard, 3);
            toolbarLayout.Controls.Add(suggestionCard, 7, 1);
            toolbarLayout.SetColumnSpan(suggestionCard, 3);

            toolbarCard.Controls.Add(toolbarLayout);
            toolbarHost.Controls.Add(toolbarCard);
            return toolbarHost;
        }

        private Control CreateStoragePanel()
        {
            AntdUI.Panel panel = CreateCardPanel(20);
            panel.Dock = DockStyle.Fill;

            Label heading = CreateSectionTitle("空间树视图");

            Label desc = CreateSectionDescription("双击文件或文件夹即可在资源管理器中定位查看。");

            storageTable = new AntdUI.Table();
            storageTable.Dock = DockStyle.Fill;
            ConfigureTableSurface(storageTable);
            storageTable.FixedHeader = true;
            storageTable.ScrollBarAvoidHeader = true;
            storageTable.CellDoubleClick += StorageTable_CellDoubleClick;

            panel.Controls.Add(storageTable);
            panel.Controls.Add(desc);
            panel.Controls.Add(heading);
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
            settings.Scan.MinSizeMb = ParseInt(minSizeInput.Text, 128);
            settings.Scan.PerLevelLimit = ParseInt(limitInput.Text, 80);
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
            rootValueLabel.Text = defaultDrive;
        }

        private void DriveSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (e.Value == null) return;
            pathInput.Text = e.Value.ToString();
        }

        private void ScanCurrentLocation()
        {
            SaveSettingsFromUi();
            ScanRequest request = BuildScanRequest();
            StorageItem result = null;

            RunBackground("正在扫描空间占用…", delegate
            {
                result = scanProvider.Scan(request);
            }, delegate
            {
                currentRoot = result;
                List<StorageEntryRow> rows = new List<StorageEntryRow> { new StorageEntryRow(result) };
                storageTable.DataSource = rows;
                rootValueLabel.Text = result.Path;
                scanValueLabel.Text = DateTime.Now.ToString("HH:mm:ss");
                Log("扫描完成：" + result.Path + "，大小 " + StorageFormatting.FormatBytes(result.Bytes));
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
            IList<CleanupCandidate> candidates = candidatePlanner.BuildCandidates(currentRoot, Math.Max(67108864L, settings.Scan.MinSizeMb * 1024L * 1024L / 2L), settings.Ai.MaxSuggestions * 4);

            RunBackground("正在生成 AI 清理建议…", delegate
            {
                suggestions = aiAdvisor.Analyze(currentRoot, candidates, settings);
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
            suggestionValueLabel.Text = suggestionRows.Count + " 项";
        }

        private ScanRequest BuildScanRequest()
        {
            string location = string.IsNullOrWhiteSpace(pathInput.Text) ? "C:\\" : pathInput.Text.Trim();
            return new ScanRequest
            {
                Location = location,
                MinSizeBytes = ParseInt(minSizeInput.Text, 128) * 1024L * 1024L,
                PerLevelLimit = ParseInt(limitInput.Text, 80),
                SortMode = sortSelect.SelectedValue is ScanSortMode ? (ScanSortMode)sortSelect.SelectedValue : ScanSortMode.Allocated
            };
        }

        private void StorageTable_CellDoubleClick(object sender, AntdUI.TableClickEventArgs e)
        {
            StorageEntryRow row = e.Record as StorageEntryRow;
            if (row == null) return;
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

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplySidebarWidth(ResolveInitialSidebarWidth());
            ApplyNormalWindowBounds(true);
            lastWindowState = WindowState;
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
            SetBusy(true, caption);
            Exception error = null;
            ThreadPool.QueueUserWorkItem(delegate
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
            UseWaitCursor = busy;
            if (navigationMenu != null) navigationMenu.Enabled = !busy;
            if (settingsNavButton != null) settingsNavButton.Enabled = !busy;
            if (sidebarResizeRail != null) sidebarResizeRail.Enabled = !busy;
            scanButton.Enabled = !busy;
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
