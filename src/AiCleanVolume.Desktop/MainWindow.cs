using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
        private const string CustomAiPromptPresetKey = "__custom__";
        private const string CustomAiProviderPresetKey = "__custom__";
        private const string DefaultAiSystemPrompt = "你是 Windows 磁盘清理助手。请你只建议删除可再生成的缓存、临时文件、日志、崩溃转储、安装残留。不要建议删除系统目录、用户文档、应用程序主体或不确定的数据。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。";
        private static readonly AiPromptPreset[] AiPromptPresets =
        {
            new AiPromptPreset("standard", "标准清理", DefaultAiSystemPrompt),
            new AiPromptPreset("conservative", "保守清理", "你是谨慎的 Windows 磁盘清理助手。只选择明确可再生成、低风险且常见的缓存、临时文件、浏览器缓存、下载缓存和崩溃转储。任何不确定、用户生成、业务数据、源码、项目文件、应用主体和系统核心路径都不要建议删除。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("cache_aggressive", "激进缓存", "你是偏激进但仍安全的 Windows 缓存清理助手。优先建议大型可再生成缓存、构建缓存、包管理缓存、浏览器缓存、临时下载和安装残留。不要选择用户文档、媒体、源码、应用程序主体、数据库或系统核心文件。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("developer", "开发环境", "你是面向开发者电脑的 Windows 清理助手。优先识别可重建的 node_modules 缓存、NuGet 缓存、Gradle 缓存、Maven 缓存、pip 缓存、npm/yarn/pnpm 缓存、构建输出、测试临时文件和 IDE 缓存。不要删除源码、配置、数据库、密钥、用户文档或项目根目录。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("system_temp", "仅系统临时", "你是 Windows 系统临时文件清理助手。只建议删除 Windows Temp、用户 Temp、INetCache、SoftwareDistribution 下载缓存、崩溃转储和明确的临时文件。不要建议删除 Program Files、Windows 核心目录、用户文档、桌面、下载目录中的个人文件。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("logs_first", "日志优先", "你是 Windows 日志清理助手。优先选择大型日志、轮转日志、旧崩溃转储、诊断报告和应用运行临时日志。不要删除当前应用主体、配置、数据库、用户文档或无法判断用途的文件。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("installer_leftovers", "安装残留", "你是 Windows 安装残留清理助手。优先识别安装包缓存、安装临时目录、升级残留、解压残留和失败安装产生的临时文件。不要删除已安装程序主体、用户数据、许可证文件或系统核心组件。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("browser_cache", "浏览器缓存", "你是浏览器缓存清理助手。优先选择浏览器缓存、GPUCache、Code Cache、Service Worker Cache、崩溃报告和临时网络缓存。不要删除书签、历史数据库、扩展数据、密码、用户配置或下载的个人文件。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("media_safe", "媒体保护", "你是保护用户媒体资料的 Windows 清理助手。可以建议删除临时文件、缓存、日志和崩溃转储，但不要删除图片、视频、音频、文档、压缩包、设计素材、工程文件和下载目录中无法确定用途的文件。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("large_files_review", "大文件审查", "你是大文件审查助手。只从候选清单中挑选明显可再生成或无业务价值的大型缓存、临时文件、日志和残留文件；对下载、文档、桌面、项目目录、虚拟机镜像、数据库和媒体文件保持高风险并避免建议删除。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("recycle_bin_safe", "回收站友好", "你是回收站删除模式下的 Windows 清理助手。优先选择放入回收站后不影响系统运行的缓存、日志、临时文件和安装残留。不要依赖回收站作为安全理由去选择不确定或用户重要数据。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。"),
            new AiPromptPreset("enterprise_safe", "办公电脑", "你是办公电脑清理助手。只建议删除缓存、临时文件、日志、崩溃转储和安装残留。不要删除企业应用数据、邮件数据、同步盘、桌面、文档、下载、项目资料、数据库、证书、密钥和配置文件。输出严格 JSON，为那种[path1,path2]，这些表示可以删除的。")
        };
        private static readonly AiProviderPreset[] AiProviderPresets =
        {
            new AiProviderPreset("chatgpt", "ChatGPT / OpenAI", "https://api.openai.com", AiSettings.DefaultModel),
            new AiProviderPreset("deepseek", "DeepSeek", "https://api.deepseek.com", "deepseek-chat")
        };

        private readonly SettingsStore settingsStore;
        private readonly IScanProvider scanProvider;
        private readonly ReusableBackgroundWorker backgroundWorker;
        private readonly CandidatePlanner candidatePlanner;
        private readonly IAiCleanupAdvisor localAdvisor;
        private readonly OpenAiCompatibleAdvisor aiAdvisor;
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
        private AntdUI.Button regularCleanButton;
        private AntdUI.Button deleteButton;
        private AntdUI.Button saveSettingsButton;
        private AntdUI.Button testAiSettingsButton;
        private AntdUI.Button selectAllSuggestionsButton;
        private AntdUI.Button clearAllSuggestionsButton;
        private AntdUI.Button invertSuggestionsButton;
        private string activePageId;

        private AntdUI.Select driveSelect;
        private AntdUI.Select suggestionDriveSelect;
        private AntdUI.Select sortSelect;
        private AntdUI.Input pathInput;
        private AntdUI.Input minSizeInput;
        private AntdUI.Input limitInput;
        private AntdUI.Input suggestionMinSizeInput;
        private AntdUI.Input suggestionLimitInput;

        private AntdUI.Table storageTable;
        private AntdUI.Table suggestionTable;
        private ContextMenuStrip storageContextMenu;
        private ToolStripMenuItem openStorageMenuItem;
        private ToolStripMenuItem deleteStorageMenuItem;
        private StorageEntryRow storageContextRow;

        private AntdUI.Switch aiEnabledSwitch;
        private AntdUI.Switch recycleSwitch;
        private AntdUI.Checkbox privilegedCheckbox;
        private AntdUI.Checkbox privilegedQuickCheckbox;
        private AntdUI.Select aiAccessModeSelect;
        private AntdUI.Input endpointInput;
        private AntdUI.Input apiKeyInput;
        private AntdUI.Input modelInput;
        private AntdUI.Input maxSuggestionsInput;
        private AntdUI.Select aiProfileSelect;
        private AntdUI.Button applyAiProfileButton;
        private AntdUI.Button saveAiProfileButton;
        private AntdUI.Select aiProviderPresetSelect;
        private AntdUI.Select aiPromptPresetSelect;
        private AntdUI.Input systemPromptInput;
        private AntdUI.Input modelCookieMappingsInput;
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
        private bool syncingAiPromptPreset;
        private bool syncingAiProviderPreset;
        private bool syncingPrivilegeCheckboxes;
        private bool storageTreeDeleteDirty;
        private int sidebarWidth;
        private int sidebarResizeStartX;
        private int sidebarResizeStartWidth;
        private readonly HashSet<string> expandedStoragePaths;

        public MainWindow()
        {
            settingsStore = new SettingsStore();
            settings = settingsStore.Load();
            candidatePlanner = new CandidatePlanner();
            deletionSandbox = new DeletionSandbox();
            privilegeService = new WindowsPrivilegeService();
            scanProvider = new FolderSizeRankerScanProvider();
            backgroundWorker = new ReusableBackgroundWorker("AiCleanVolume.UiWorker");
            localAdvisor = new HeuristicCleanupAdvisor();
            aiAdvisor = new OpenAiCompatibleAdvisor(localAdvisor, LogBackground);
            deletionService = new RecycleBinDeletionService();
            explorerService = new ShellExplorerService();
            suggestionRows = new List<CleanupSuggestionRow>();
            expandedStoragePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            regularCleanButton = CreateHeaderButton("常规清理", AntdUI.TTypeMini.Primary);
            regularCleanButton.Click += delegate { AnalyzeRegularSuggestions(); };

            scanButton = CreateToolbarActionButton("扫描", AntdUI.TTypeMini.Primary);
            scanButton.Click += delegate { ScanCurrentLocation(); };

            titleBar.Controls.Add(saveSettingsButton);
            titleBar.Controls.Add(deleteButton);
            titleBar.Controls.Add(analyzeButton);
            titleBar.Controls.Add(regularCleanButton);

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
            menu.Items.Add(CreateNavigationItem(PageSuggestions, "清理建议", "RobotFilled"));
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
            openStorageMenuItem = new ToolStripMenuItem("在文件资源管理器打开");
            openStorageMenuItem.Click += OpenStorageMenuItem_Click;
            deleteStorageMenuItem = new ToolStripMenuItem("删除");
            deleteStorageMenuItem.Click += DeleteStorageMenuItem_Click;
            storageContextMenu.Items.Add(openStorageMenuItem);
            storageContextMenu.Items.Add(new ToolStripSeparator());
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

            Label heading = CreateSectionTitle("清理建议");

            Label desc = CreateSectionDescription("支持“常规清理”和“AI 识别”；列表按清理软件风格展示，默认勾选可安全处理项。");

            Panel optionsBar = new Panel();
            optionsBar.Dock = DockStyle.Top;
            optionsBar.Height = 34;
            optionsBar.Padding = new Padding(0, 0, 0, 6);
            optionsBar.BackColor = Color.Transparent;

            invertSuggestionsButton = CreateSuggestionActionButton("反选", AntdUI.TTypeMini.Default);
            invertSuggestionsButton.Click += delegate { InvertSuggestionSelection(); };
            invertSuggestionsButton.Dock = DockStyle.Right;

            clearAllSuggestionsButton = CreateSuggestionActionButton("全不选", AntdUI.TTypeMini.Default);
            clearAllSuggestionsButton.Click += delegate { SetSuggestionSelection(false); };
            clearAllSuggestionsButton.Dock = DockStyle.Right;

            selectAllSuggestionsButton = CreateSuggestionActionButton("全选", AntdUI.TTypeMini.Primary);
            selectAllSuggestionsButton.Click += delegate { SetSuggestionSelection(true); };
            selectAllSuggestionsButton.Dock = DockStyle.Right;

            privilegedQuickCheckbox = CreateCheckbox("完全权限模式（仅管理员运行时生效）");
            privilegedQuickCheckbox.Dock = DockStyle.Left;
            privilegedQuickCheckbox.Width = 280;
            privilegedQuickCheckbox.CheckedChanged += PrivilegedCheckbox_CheckedChanged;
            optionsBar.Controls.Add(privilegedQuickCheckbox);
            optionsBar.Controls.Add(invertSuggestionsButton);
            optionsBar.Controls.Add(clearAllSuggestionsButton);
            optionsBar.Controls.Add(selectAllSuggestionsButton);

            TableLayoutPanel scanOptionsBar = new TableLayoutPanel();
            scanOptionsBar.Dock = DockStyle.Top;
            scanOptionsBar.Height = 42;
            scanOptionsBar.Padding = new Padding(0, 0, 0, 8);
            scanOptionsBar.BackColor = Color.Transparent;
            scanOptionsBar.ColumnCount = 7;
            scanOptionsBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));
            scanOptionsBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            scanOptionsBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            scanOptionsBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78F));
            scanOptionsBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            scanOptionsBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78F));
            scanOptionsBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            suggestionDriveSelect = CreateSelect();
            suggestionDriveSelect.ListAutoWidth = true;
            suggestionDriveSelect.SelectedValueChanged += SuggestionDriveSelect_SelectedValueChanged;
            suggestionMinSizeInput = CreateInput("最小值（单位MB）");
            suggestionMinSizeInput.Text = "128";
            suggestionLimitInput = CreateInput("数量限制，-1 不限");
            suggestionLimitInput.Text = "-1";

            scanOptionsBar.Controls.Add(CreateToolbarCaption("盘符:"), 0, 0);
            scanOptionsBar.Controls.Add(suggestionDriveSelect, 1, 0);
            scanOptionsBar.Controls.Add(CreateToolbarCaption("最小值（MB）:"), 2, 0);
            scanOptionsBar.Controls.Add(suggestionMinSizeInput, 3, 0);
            scanOptionsBar.Controls.Add(CreateToolbarCaption("数量限制:"), 4, 0);
            scanOptionsBar.Controls.Add(suggestionLimitInput, 5, 0);

            suggestionTable = new AntdUI.Table();
            suggestionTable.Dock = DockStyle.Fill;
            ConfigureCleanupListSurface(suggestionTable);
            suggestionTable.FixedHeader = true;
            suggestionTable.ScrollBarAvoidHeader = true;
            suggestionTable.CellDoubleClick += SuggestionTable_CellDoubleClick;
            suggestionTable.CellButtonClick += SuggestionTable_CellButtonClick;

            panel.Controls.Add(suggestionTable);
            panel.Controls.Add(scanOptionsBar);
            panel.Controls.Add(optionsBar);
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
            layout.RowCount = 11;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            for (int i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 122F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            aiEnabledSwitch = new AntdUI.Switch();
            testAiSettingsButton = CreateSettingsActionButton("测试 AI", AntdUI.TTypeMini.Primary);
            testAiSettingsButton.Click += delegate { TestAiSettings(); };
            recycleSwitch = new AntdUI.Switch();
            privilegedCheckbox = CreateCheckbox("启用完全权限（管理员）");
            privilegedCheckbox.CheckedChanged += PrivilegedCheckbox_CheckedChanged;
            aiAccessModeSelect = CreateSelect();
            PopulateAiAccessModes();
            aiAccessModeSelect.SelectedValueChanged += AiAccessModeSelect_SelectedValueChanged;
            endpointInput = CreateInput("https://api.openai.com");
            apiKeyInput = CreateInput("sk-...");
            modelInput = CreateInput(AiSettings.DefaultModel);
            maxSuggestionsInput = CreateInput("30");
            aiProfileSelect = CreateSelect();
            applyAiProfileButton = CreateSettingsActionButton("应用", AntdUI.TTypeMini.Primary);
            applyAiProfileButton.Click += delegate { ApplySelectedAiProfile(); };
            saveAiProfileButton = CreateSettingsActionButton("保存为配置", AntdUI.TTypeMini.Default);
            saveAiProfileButton.Click += delegate { SaveCurrentAiProfileWithPrompt(); };
            aiProviderPresetSelect = CreateSelect();
            PopulateAiProviderPresets();
            aiProviderPresetSelect.SelectedValueChanged += AiProviderPresetSelect_SelectedValueChanged;
            endpointInput.TextChanged += AiEndpointOrModelInput_TextChanged;
            modelInput.TextChanged += AiEndpointOrModelInput_TextChanged;
            aiPromptPresetSelect = CreateSelect();
            PopulateAiPromptPresets();
            aiPromptPresetSelect.SelectedValueChanged += AiPromptPresetSelect_SelectedValueChanged;
            systemPromptInput = CreateInput("系统提示词");
            systemPromptInput.Multiline = true;
            systemPromptInput.AutoScroll = true;
            systemPromptInput.TextChanged += SystemPromptInput_TextChanged;
            modelCookieMappingsInput = CreateInput("直接粘贴当前模型的一整行 Cookie；也兼容 model=Cookie");
            modelCookieMappingsInput.Multiline = false;
            modelCookieMappingsInput.AutoScroll = false;
            allowRootsInput = CreateInput("每行一个允许位置");
            allowRootsInput.Multiline = true;
            allowRootsInput.AutoScroll = true;

            layout.Controls.Add(CreateCaption("AI"), 0, 0);
            layout.Controls.Add(CreateAiSettingsHeaderControls(), 1, 0);
            layout.Controls.Add(CreateCaption("回收站"), 2, 0);
            layout.Controls.Add(recycleSwitch, 3, 0);

            layout.Controls.Add(CreateCaption("完全权限"), 0, 1);
            layout.Controls.Add(privilegedCheckbox, 1, 1);
            layout.Controls.Add(CreateCaption("建议条数"), 2, 1);
            layout.Controls.Add(maxSuggestionsInput, 3, 1);

            layout.Controls.Add(CreateCaption("接入类型"), 0, 2);
            layout.Controls.Add(aiAccessModeSelect, 1, 2);
            layout.SetColumnSpan(aiAccessModeSelect, 3);

            layout.Controls.Add(CreateCaption("历史配置"), 0, 3);
            layout.Controls.Add(aiProfileSelect, 1, 3);
            layout.Controls.Add(applyAiProfileButton, 2, 3);
            layout.Controls.Add(saveAiProfileButton, 3, 3);

            layout.Controls.Add(CreateCaption("接口地址"), 0, 4);
            layout.Controls.Add(endpointInput, 1, 4);
            layout.SetColumnSpan(endpointInput, 3);

            layout.Controls.Add(CreateCaption("模型"), 0, 5);
            layout.Controls.Add(modelInput, 1, 5);
            layout.Controls.Add(CreateCaption("API Key"), 2, 5);
            layout.Controls.Add(apiKeyInput, 3, 5);

            layout.Controls.Add(CreateCaption("接口预设"), 0, 6);
            layout.Controls.Add(aiProviderPresetSelect, 1, 6);
            layout.SetColumnSpan(aiProviderPresetSelect, 3);

            layout.Controls.Add(CreateCaption("模型 Cookie"), 0, 7);
            layout.Controls.Add(modelCookieMappingsInput, 1, 7);
            layout.SetColumnSpan(modelCookieMappingsInput, 3);

            layout.Controls.Add(CreateCaption("AI 预设"), 0, 8);
            layout.Controls.Add(aiPromptPresetSelect, 1, 8);
            layout.SetColumnSpan(aiPromptPresetSelect, 3);

            layout.Controls.Add(CreateCaption("系统提示"), 0, 9);
            layout.Controls.Add(systemPromptInput, 1, 9);
            layout.SetColumnSpan(systemPromptInput, 3);

            layout.Controls.Add(CreateCaption("允许位置"), 0, 10);
            layout.Controls.Add(allowRootsInput, 1, 10);
            layout.SetColumnSpan(allowRootsInput, 3);

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
            regularCleanButton.Visible = pageId == PageSuggestions;
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
                    return AppDisplayName + " · 常规清理与 AI 建议";
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
                    return "查看常规清理、AI 和本地规则生成的清理建议，支持定位和批量删除。";
                case PageLog:
                    return "查看扫描、建议与删除流程的执行日志。";
                case PageSettings:
                    return "配置标准 API / 2API、建议数量、沙盒白名单和删除策略。";
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
                new AntdUI.ColumnCheck("selected", "选中").SetWidth("60"),
                new AntdUI.Column("name", "清理项").SetWidth("160"),
                new AntdUI.Column("category", "类别", AntdUI.ColumnAlign.Center).SetWidth("108"),
                new AntdUI.Column("size", "大小", AntdUI.ColumnAlign.Right).SetWidth("104"),
                new AntdUI.Column("risk", "风险", AntdUI.ColumnAlign.Center).SetWidth("88"),
                new AntdUI.Column("sandbox", "沙盒", AntdUI.ColumnAlign.Center).SetWidth("96"),
                new AntdUI.Column("source", "来源", AntdUI.ColumnAlign.Center).SetWidth("108"),
                new AntdUI.Column("status", "状态", AntdUI.ColumnAlign.Center).SetWidth("86"),
                new AntdUI.Column("details", "路径与说明").SetWidth("auto").SetLineBreak(),
                new AntdUI.Column("actions", "操作").SetWidth("132")
            };
        }

        private void LoadSettingsToUi()
        {
            settings.EnsureDefaults();
            aiEnabledSwitch.Checked = settings.Ai.Enabled;
            recycleSwitch.Checked = settings.Sandbox.UseRecycleBin;
            ApplyPrivilegedCheckboxState(settings.Sandbox.FullyPrivilegedMode);
            aiAccessModeSelect.SelectedValue = settings.Ai.AccessMode;
            endpointInput.Text = settings.Ai.Endpoint;
            apiKeyInput.Text = settings.Ai.ApiKey;
            modelInput.Text = settings.Ai.Model;
            maxSuggestionsInput.Text = settings.Ai.MaxSuggestions.ToString();
            systemPromptInput.Text = settings.Ai.SystemPrompt;
            modelCookieMappingsInput.Text = FormatModelCookieMappings(settings.Ai.ModelCookieMappings, settings.Ai.Model);
            UpdateAiAccessModeUi();
            PopulateAiProfiles();
            SelectAiProviderPresetForSettings(settings.Ai.Endpoint, settings.Ai.Model);
            SelectAiPromptPresetForPrompt(settings.Ai.SystemPrompt);
            minSizeInput.Text = settings.Scan.MinSizeMb.ToString();
            limitInput.Text = settings.Scan.PerLevelLimit.ToString();
            if (suggestionMinSizeInput != null) suggestionMinSizeInput.Text = "128";
            if (suggestionLimitInput != null) suggestionLimitInput.Text = "-1";
            sortSelect.SelectedValue = settings.Scan.SortMode;
            settings.Sandbox.AllowedRoots = SandboxSettings.NormalizeAllowedRoots(settings.Sandbox.AllowedRoots);
            allowRootsInput.Text = string.Join(Environment.NewLine, new List<string>(settings.Sandbox.AllowedRoots).ToArray());
        }

        private void PopulateAiAccessModes()
        {
            if (aiAccessModeSelect == null) return;

            aiAccessModeSelect.Items.Clear();
            aiAccessModeSelect.Items.Add(new AntdUI.SelectItem("标准 API", AiSettings.StandardApiAccessMode));
            aiAccessModeSelect.Items.Add(new AntdUI.SelectItem("2API", AiSettings.TwoApiAccessMode));
        }

        private void PopulateAiProviderPresets()
        {
            if (aiProviderPresetSelect == null) return;

            aiProviderPresetSelect.Items.Clear();
            aiProviderPresetSelect.Items.Add(new AntdUI.SelectItem("自定义", CustomAiProviderPresetKey));
            for (int index = 0; index < AiProviderPresets.Length; index++)
            {
                AiProviderPreset preset = AiProviderPresets[index];
                aiProviderPresetSelect.Items.Add(new AntdUI.SelectItem(preset.Name, preset.Key));
            }
        }

        private void PopulateAiPromptPresets()
        {
            if (aiPromptPresetSelect == null) return;

            aiPromptPresetSelect.Items.Clear();
            aiPromptPresetSelect.Items.Add(new AntdUI.SelectItem("自定义", CustomAiPromptPresetKey));
            for (int index = 0; index < AiPromptPresets.Length; index++)
            {
                AiPromptPreset preset = AiPromptPresets[index];
                aiPromptPresetSelect.Items.Add(new AntdUI.SelectItem(preset.Name, preset.Key));
            }
        }

        private void SelectAiProviderPresetForSettings(string endpoint, string model)
        {
            if (aiProviderPresetSelect == null) return;

            AiProviderPreset preset = FindAiProviderPreset(endpoint, model);
            syncingAiProviderPreset = true;
            try
            {
                aiProviderPresetSelect.SelectedValue = preset == null ? CustomAiProviderPresetKey : preset.Key;
            }
            finally
            {
                syncingAiProviderPreset = false;
            }
        }

        private void SelectAiPromptPresetForPrompt(string prompt)
        {
            if (aiPromptPresetSelect == null) return;

            AiPromptPreset preset = FindAiPromptPresetByPrompt(prompt);
            syncingAiPromptPreset = true;
            try
            {
                aiPromptPresetSelect.SelectedValue = preset == null ? CustomAiPromptPresetKey : preset.Key;
            }
            finally
            {
                syncingAiPromptPreset = false;
            }
        }

        private static AiProviderPreset FindAiProviderPreset(string endpoint, string model)
        {
            string normalizedEndpoint = NormalizeEndpoint(endpoint);
            string normalizedModel = NormalizeValue(model);
            if (string.IsNullOrWhiteSpace(normalizedEndpoint) || string.IsNullOrWhiteSpace(normalizedModel)) return null;

            for (int index = 0; index < AiProviderPresets.Length; index++)
            {
                AiProviderPreset preset = AiProviderPresets[index];
                if (string.Equals(NormalizeEndpoint(preset.Endpoint), normalizedEndpoint, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(NormalizeValue(preset.Model), normalizedModel, StringComparison.OrdinalIgnoreCase))
                {
                    return preset;
                }
            }

            return null;
        }

        private static AiPromptPreset FindAiPromptPreset(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            for (int index = 0; index < AiPromptPresets.Length; index++)
            {
                if (string.Equals(AiPromptPresets[index].Key, key, StringComparison.OrdinalIgnoreCase)) return AiPromptPresets[index];
            }

            return null;
        }

        private static AiPromptPreset FindAiPromptPresetByPrompt(string prompt)
        {
            string normalizedPrompt = NormalizePromptForComparison(prompt);
            if (string.IsNullOrWhiteSpace(normalizedPrompt)) return null;

            for (int index = 0; index < AiPromptPresets.Length; index++)
            {
                if (string.Equals(NormalizePromptForComparison(AiPromptPresets[index].Prompt), normalizedPrompt, StringComparison.Ordinal)) return AiPromptPresets[index];
            }

            return null;
        }

        private static string NormalizePromptForComparison(string prompt)
        {
            string normalized = (prompt ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            normalized = Regex.Replace(normalized, "[A-Za-z]\\s*盘", "{driveLabel}");
            normalized = Regex.Replace(normalized, "[A-Za-z]:\\\\", "{driveRoot}");
            normalized = normalized.Replace("当前重点分析 Windows {driveLabel}（{driveRoot}）下的候选路径。", string.Empty);
            normalized = Regex.Replace(normalized, "Windows\\s*\\{driveLabel\\}\\s*清理助手", "Windows 磁盘清理助手");
            return normalized;
        }

        private static string NormalizeEndpoint(string endpoint)
        {
            string normalized = NormalizeValue(endpoint);
            if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;
            return normalized.TrimEnd('/');
        }

        private static string NormalizeValue(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string BuildAiProfileDisplayName(AiProfile profile)
        {
            if (profile == null) return string.Empty;
            string name = NormalizeValue(profile.Name);
            string endpoint = NormalizeEndpoint(profile.Endpoint);
            if (string.IsNullOrWhiteSpace(endpoint)) return name;

            Uri uri;
            string host = Uri.TryCreate(endpoint, UriKind.Absolute, out uri) ? uri.Host : endpoint;
            return string.IsNullOrWhiteSpace(host) ? name : name + " · " + host;
        }

        private static AiProviderPreset FindAiProviderPresetByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            for (int index = 0; index < AiProviderPresets.Length; index++)
            {
                if (string.Equals(AiProviderPresets[index].Key, key, StringComparison.OrdinalIgnoreCase)) return AiProviderPresets[index];
            }

            return null;
        }

        private void PrivilegedCheckbox_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            if (syncingPrivilegeCheckboxes) return;
            AntdUI.Checkbox source = sender as AntdUI.Checkbox;
            if (source == null) return;
            ApplyPrivilegedCheckboxState(source.Checked);
            if (settings != null && settings.Sandbox != null)
            {
                settings.Sandbox.FullyPrivilegedMode = source.Checked;
                RefreshSuggestionSandboxFromCurrentSettings();
            }
        }

        private void ApplyPrivilegedCheckboxState(bool value)
        {
            syncingPrivilegeCheckboxes = true;
            try
            {
                if (privilegedCheckbox != null) privilegedCheckbox.Checked = value;
                if (privilegedQuickCheckbox != null) privilegedQuickCheckbox.Checked = value;
            }
            finally
            {
                syncingPrivilegeCheckboxes = false;
            }
        }

        private bool IsFullyPrivilegedChecked()
        {
            if (privilegedQuickCheckbox != null) return privilegedQuickCheckbox.Checked;
            return privilegedCheckbox != null && privilegedCheckbox.Checked;
        }

        private void AiAccessModeSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            UpdateAiAccessModeUi();
        }

        private void UpdateAiAccessModeUi()
        {
            bool twoApi = string.Equals(ResolveSelectedAiAccessMode(), AiSettings.TwoApiAccessMode, StringComparison.OrdinalIgnoreCase);
            if (apiKeyInput != null)
            {
                apiKeyInput.Enabled = !twoApi;
                apiKeyInput.PlaceholderText = twoApi ? "2API 模式不使用 API Key" : "sk-...";
            }
            if (modelCookieMappingsInput != null)
            {
                modelCookieMappingsInput.Enabled = true;
            }
        }

        private string ResolveSelectedAiAccessMode()
        {
            if (aiAccessModeSelect == null || aiAccessModeSelect.SelectedValue == null) return AiSettings.StandardApiAccessMode;
            return AiSettings.NormalizeAccessMode(aiAccessModeSelect.SelectedValue.ToString());
        }

        private void AiProviderPresetSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (syncingAiProviderPreset || e.Value == null) return;

            string key = e.Value.ToString();
            if (string.Equals(key, CustomAiProviderPresetKey, StringComparison.OrdinalIgnoreCase)) return;

            AiProviderPreset preset = FindAiProviderPresetByKey(key);
            if (preset == null) return;

            syncingAiProviderPreset = true;
            try
            {
                if (endpointInput != null) endpointInput.Text = preset.Endpoint;
                if (modelInput != null) modelInput.Text = preset.Model;
            }
            finally
            {
                syncingAiProviderPreset = false;
            }
        }

        private void AiEndpointOrModelInput_TextChanged(object sender, EventArgs e)
        {
            if (syncingAiProviderPreset) return;
            SelectAiProviderPresetForSettings(endpointInput == null ? null : endpointInput.Text, modelInput == null ? null : modelInput.Text);
        }

        private void SaveSettings()
        {
            try
            {
                SaveSettingsFromUi();
                SaveCurrentAiProfileAutomatic();
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

        private void PopulateAiProfiles()
        {
            if (aiProfileSelect == null) return;

            aiProfileSelect.Items.Clear();
            settings.Ai.Profiles = AiSettings.NormalizeProfiles(settings.Ai.Profiles);
            if (settings.Ai.Profiles.Count == 0)
            {
                aiProfileSelect.Items.Add(new AntdUI.SelectItem("暂无历史配置", string.Empty));
                aiProfileSelect.SelectedValue = string.Empty;
                return;
            }

            for (int index = 0; index < settings.Ai.Profiles.Count; index++)
            {
                AiProfile profile = settings.Ai.Profiles[index];
                aiProfileSelect.Items.Add(new AntdUI.SelectItem(BuildAiProfileDisplayName(profile), index.ToString()));
            }
            aiProfileSelect.SelectedValue = "0";
        }

        private void TestAiSettings()
        {
            try
            {
                SaveSettingsFromUi();
                settings.Ai.Enabled = IsAiConfigured(settings.Ai);
                aiEnabledSwitch.Checked = settings.Ai.Enabled;
                Log("AI 配置测试开始：Enabled=" + settings.Ai.Enabled + "，AccessMode=" + settings.Ai.AccessMode + "，Endpoint=" + settings.Ai.Endpoint + "，Model=" + settings.Ai.Model + "。");
            }
            catch (Exception ex)
            {
                Log("AI 配置测试准备失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "测试失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string resultMessage = null;
            bool success = false;
            RunBackground("正在测试 AI 配置…", delegate
            {
                AiConnectionTestResult result = aiAdvisor.TestConnection(settings);
                success = result != null && result.Success;
                resultMessage = result == null ? "AI 配置测试失败：未返回测试结果。" : result.Message;
                LogBackground(resultMessage);
            }, delegate
            {
                MessageBox.Show(this, resultMessage ?? "AI 配置测试完成。", success ? "测试成功" : "测试失败", MessageBoxButtons.OK, success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });
        }

        private void SaveSettingsFromUi()
        {
            settings.Ai.Enabled = aiEnabledSwitch.Checked;
            settings.Ai.AccessMode = ResolveSelectedAiAccessMode();
            settings.Ai.Endpoint = endpointInput.Text.Trim();
            settings.Ai.ApiKey = apiKeyInput.Text.Trim();
            settings.Ai.Model = modelInput.Text.Trim();
            settings.Ai.MaxSuggestions = ParsePositiveInt(maxSuggestionsInput.Text, 30);
            settings.Ai.SystemPrompt = systemPromptInput.Text.Trim();
            settings.Ai.ModelCookieMappings = ParseModelCookieMappings(modelCookieMappingsInput.Text, settings.Ai.Model);
            settings.Sandbox.UseRecycleBin = recycleSwitch.Checked;
            settings.Sandbox.FullyPrivilegedMode = IsFullyPrivilegedChecked();
            settings.Sandbox.AllowedRoots = SandboxSettings.NormalizeAllowedRoots(ParseLines(allowRootsInput.Text));
            settings.Scan.MinSizeMb = ParseInt(minSizeInput.Text, -1);
            settings.Scan.PerLevelLimit = ParseInt(limitInput.Text, -1);
            if (sortSelect.SelectedValue is ScanSortMode) settings.Scan.SortMode = (ScanSortMode)sortSelect.SelectedValue;
            settings.EnsureDefaults();
        }

        private void SaveCurrentAiProfileAutomatic()
        {
            AiProfile profile = CreateCurrentAiProfile(null);
            profile.Name = AiSettings.BuildProfileAutoName(profile.Model, profile.SavedAt);
            UpsertAiProfile(profile, false);
            PopulateAiProfiles();
        }

        private void SaveCurrentAiProfileWithPrompt()
        {
            try
            {
                SaveSettingsFromUi();
                string defaultName = AiSettings.BuildProfileAutoName(settings.Ai.Model, DateTime.Now);
                string name = PromptForAiProfileName(defaultName);
                if (string.IsNullOrWhiteSpace(name)) return;

                AiProfile profile = CreateCurrentAiProfile(name);
                UpsertAiProfile(profile, true);
                settingsStore.Save(settings);
                PopulateAiProfiles();
                Log("AI 配置方案已保存：" + profile.Name + "。");
                MessageBox.Show(this, "AI 配置方案已保存。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("保存 AI 配置方案失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplySelectedAiProfile()
        {
            AiProfile profile = ResolveSelectedAiProfile();
            if (profile == null)
            {
                MessageBox.Show(this, "暂无可应用的 AI 历史配置。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ApplyAiProfileToUi(profile);
            Log("已应用 AI 配置方案到界面：" + profile.Name + "。点击保存配置后生效。");
        }

        private AiProfile ResolveSelectedAiProfile()
        {
            if (aiProfileSelect == null || aiProfileSelect.SelectedValue == null || settings == null || settings.Ai == null || settings.Ai.Profiles == null) return null;
            int index;
            if (!int.TryParse(aiProfileSelect.SelectedValue.ToString(), out index)) return null;
            if (index < 0 || index >= settings.Ai.Profiles.Count) return null;
            return settings.Ai.Profiles[index];
        }

        private AiProfile CreateCurrentAiProfile(string name)
        {
            AiProfile profile = new AiProfile
            {
                Name = NormalizeValue(name),
                SavedAt = DateTime.Now,
                AccessMode = settings.Ai.AccessMode,
                Endpoint = settings.Ai.Endpoint,
                ApiKey = settings.Ai.ApiKey,
                Model = settings.Ai.Model,
                MaxSuggestions = settings.Ai.MaxSuggestions,
                SystemPrompt = settings.Ai.SystemPrompt,
                ModelCookieMappings = new List<AiModelCookieMapping>()
            };

            IList<AiModelCookieMapping> mappings = AiSettings.NormalizeModelCookieMappings(settings.Ai.ModelCookieMappings);
            for (int i = 0; i < mappings.Count; i++)
            {
                profile.ModelCookieMappings.Add(new AiModelCookieMapping
                {
                    Model = mappings[i].Model,
                    Cookie = mappings[i].Cookie
                });
            }

            if (string.IsNullOrWhiteSpace(profile.Name)) profile.Name = AiSettings.BuildProfileAutoName(profile.Model, profile.SavedAt);
            return profile;
        }

        private void UpsertAiProfile(AiProfile profile, bool matchByName)
        {
            if (profile == null) return;
            if (settings.Ai.Profiles == null) settings.Ai.Profiles = new List<AiProfile>();

            List<AiProfile> profiles = new List<AiProfile>(AiSettings.NormalizeProfiles(settings.Ai.Profiles));
            string fingerprint = profile.BuildFingerprint();
            int matchIndex = -1;
            for (int i = 0; i < profiles.Count; i++)
            {
                if ((matchByName && string.Equals(NormalizeValue(profiles[i].Name), NormalizeValue(profile.Name), StringComparison.OrdinalIgnoreCase)) ||
                    string.Equals(profiles[i].BuildFingerprint(), fingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    matchIndex = i;
                    break;
                }
            }

            if (matchIndex >= 0) profiles.RemoveAt(matchIndex);
            profiles.Insert(0, profile.Clone());
            while (profiles.Count > 10) profiles.RemoveAt(profiles.Count - 1);
            settings.Ai.Profiles = profiles;
        }

        private void ApplyAiProfileToUi(AiProfile profile)
        {
            if (profile == null) return;
            aiAccessModeSelect.SelectedValue = AiSettings.NormalizeAccessMode(profile.AccessMode);
            endpointInput.Text = NormalizeValue(profile.Endpoint);
            apiKeyInput.Text = NormalizeValue(profile.ApiKey);
            modelInput.Text = NormalizeValue(profile.Model);
            maxSuggestionsInput.Text = (profile.MaxSuggestions <= 0 ? 30 : profile.MaxSuggestions).ToString();
            systemPromptInput.Text = NormalizeValue(profile.SystemPrompt);
            modelCookieMappingsInput.Text = FormatModelCookieMappings(profile.ModelCookieMappings, profile.Model);
            UpdateAiAccessModeUi();
            SelectAiProviderPresetForSettings(endpointInput.Text, modelInput.Text);
            SelectAiPromptPresetForPrompt(systemPromptInput.Text);
        }

        private string PromptForAiProfileName(string defaultName)
        {
            using (Form form = new Form())
            using (TextBox input = new TextBox())
            using (Button okButton = new Button())
            using (Button cancelButton = new Button())
            {
                form.Text = "保存 AI 配置方案";
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(420, 116);
                form.Font = Font;

                Label label = new Label();
                label.Text = "配置名称";
                label.AutoSize = true;
                label.Location = new Point(14, 18);

                input.Text = defaultName ?? string.Empty;
                input.Location = new Point(84, 14);
                input.Width = 318;

                okButton.Text = "保存";
                okButton.DialogResult = DialogResult.OK;
                okButton.Location = new Point(246, 70);
                okButton.Width = 75;

                cancelButton.Text = "取消";
                cancelButton.DialogResult = DialogResult.Cancel;
                cancelButton.Location = new Point(327, 70);
                cancelButton.Width = 75;

                form.Controls.Add(label);
                form.Controls.Add(input);
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                return form.ShowDialog(this) == DialogResult.OK ? NormalizeValue(input.Text) : null;
            }
        }

        private static bool IsAiConfigured(AiSettings ai)
        {
            return ai != null && !string.IsNullOrWhiteSpace(ai.Endpoint) && !string.IsNullOrWhiteSpace(ai.Model);
        }

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

            string defaultDrive = Environment.GetEnvironmentVariable("SystemDrive");
            if (string.IsNullOrWhiteSpace(defaultDrive)) defaultDrive = "C:";
            defaultDrive = defaultDrive.TrimEnd('\\') + "\\";
            driveSelect.SelectedValue = defaultDrive;
            if (suggestionDriveSelect != null) suggestionDriveSelect.SelectedValue = defaultDrive;
            pathInput.Text = defaultDrive;
            UpdateDriveSummaryForLocation(defaultDrive);
            RefreshPromptForCurrentLocation();
        }

        private void DriveSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (e.Value == null) return;
            pathInput.Text = e.Value.ToString();
            UpdateDriveSummaryForLocation(pathInput.Text);
            RefreshPromptForCurrentLocation();
        }

        private void SuggestionDriveSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            RefreshPromptForCurrentLocation();
        }

        private void PathInput_TextChanged(object sender, EventArgs e)
        {
            string location = pathInput.Text;
            if (string.IsNullOrWhiteSpace(location) && driveSelect != null && driveSelect.SelectedValue != null)
            {
                location = driveSelect.SelectedValue.ToString();
            }
            UpdateDriveSummaryForLocation(location);
            RefreshPromptForCurrentLocation();
        }

        private void AiPromptPresetSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (syncingAiPromptPreset || e.Value == null) return;

            string key = e.Value.ToString();
            if (string.Equals(key, CustomAiPromptPresetKey, StringComparison.OrdinalIgnoreCase)) return;

            AiPromptPreset preset = FindAiPromptPreset(key);
            if (preset == null || systemPromptInput == null) return;

            syncingAiPromptPreset = true;
            try
            {
                systemPromptInput.Text = preset.BuildPrompt(GetPromptDriveRoot());
            }
            finally
            {
                syncingAiPromptPreset = false;
            }
        }

        private void SystemPromptInput_TextChanged(object sender, EventArgs e)
        {
            if (syncingAiPromptPreset || systemPromptInput == null) return;
            SelectAiPromptPresetForPrompt(systemPromptInput.Text);
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
            UpdateScanProgressState(progressText, 0.56F, true, AntdUI.TType.None);
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
                List<StorageEntryRow> rows = new List<StorageEntryRow> { new StorageEntryRow(result) };
                storageTable.DataSource = rows;
                UpdateDriveSummaryForLocation(result.Path);
                UpdateScanProgressState("扫描完成 " + elapsed.TotalSeconds.ToString("0.00") + " 秒", 1F, false, AntdUI.TType.Success);
                Log("扫描完成：" + result.Path + "，大小 " + StorageFormatting.FormatBytes(result.Bytes) + "，耗时 " + elapsed.TotalSeconds.ToString("0.00") + " 秒，子项 " + (result.Children == null ? 0 : result.Children.Count) + "。");
                if (onCompleted != null) onCompleted();
            }, delegate
            {
                UpdateScanProgressState("扫描失败", 1F, false, AntdUI.TType.Error);
            });
        }

        private void AnalyzeSuggestions()
        {
            AnalyzeSuggestionsCore(true);
        }

        private void AnalyzeRegularSuggestions()
        {
            AnalyzeSuggestionsCore(false);
        }

        private void AnalyzeSuggestionsCore(bool preferAi)
        {
            string location = ResolveSuggestionLocation();
            if (NeedAutoScanBeforeAnalyze(location))
            {
                string actionName = preferAi ? "AI 识别" : "常规清理";
                Log("未发现当前所选位置的扫描结果，先自动扫描：" + location);
                ScanSuggestionLocation(location, delegate
                {
                    Log("自动扫描完成，继续执行" + actionName + "。");
                    AnalyzeSuggestionsCore(preferAi);
                }, "未发现当前所选位置的扫描结果，正在自动扫描...");
                return;
            }

            SaveSettingsFromUi();
            if (preferAi && !settings.Ai.Enabled && IsAiConfigured(settings.Ai))
            {
                settings.Ai.Enabled = true;
                aiEnabledSwitch.Checked = true;
                Log("AI 配置已填写，自动启用 AI 识别。");
            }
            IList<CleanupSuggestion> suggestions = null;
            StorageItem analysisRoot = null;
            ScanRequest request = BuildSuggestionScanRequest(location, -1);
            string caption = preferAi ? "正在生成 AI 清理建议…" : "正在生成常规清理列表…";
            DateTime analyzeStartedAt = DateTime.UtcNow;
            Log((preferAi ? "AI 识别" : "常规清理") + "开始：" + DescribeScanRequest(request) + "，AIEnabled=" + settings.Ai.Enabled + "，AccessMode=" + settings.Ai.AccessMode + "，Endpoint=" + settings.Ai.Endpoint + "，Model=" + settings.Ai.Model + "，CookieMappings=" + (settings.Ai.ModelCookieMappings == null ? 0 : settings.Ai.ModelCookieMappings.Count) + "。");

            RunBackground(caption, delegate
            {
                analysisRoot = scanProvider.Scan(request);
                LogBackground("候选构建开始：root=" + (analysisRoot == null ? string.Empty : analysisRoot.Path) + "，rootSize=" + (analysisRoot == null ? string.Empty : StorageFormatting.FormatBytes(analysisRoot.Bytes)) + "。");
                IList<CleanupCandidate> candidates = candidatePlanner.BuildCandidates(
                    analysisRoot,
                    ResolveCandidateMinBytes(preferAi),
                    settings.Ai.MaxSuggestions * (preferAi ? 4 : 6));
                LogBackground("候选构建完成：count=" + candidates.Count + "，minBytes=" + StorageFormatting.FormatBytes(ResolveCandidateMinBytes(preferAi)) + "。");
                suggestions = preferAi ? aiAdvisor.Analyze(analysisRoot, candidates, settings) : localAdvisor.Analyze(analysisRoot, candidates, settings);
                LogBackground((preferAi ? "AI/回退" : "常规") + "建议原始结果：count=" + (suggestions == null ? 0 : suggestions.Count) + "。");
                EvaluateSandbox(suggestions);
            }, delegate
            {
                BindSuggestions(suggestions);
                string sourceName;
                if (preferAi) sourceName = settings.Ai.Enabled ? "AI 建议" : "本地规则回退";
                else sourceName = "常规清理";
                TimeSpan elapsed = DateTime.UtcNow - analyzeStartedAt;
                Log(sourceName + "生成完成，共 " + suggestionRows.Count + " 项，耗时 " + elapsed.TotalSeconds.ToString("0.00") + " 秒。");
            });
        }

        private void DeleteSelectedSuggestions()
        {
            SaveSettingsFromUi();
            RefreshSuggestionSandboxFromCurrentSettings();
            if (suggestionRows == null || suggestionRows.Count == 0)
            {
                AntdUI.Modal.open(this, "提示", "当前没有可删除的建议项。", AntdUI.TType.Info);
                return;
            }

            List<CleanupSuggestionRow> selectedRows = new List<CleanupSuggestionRow>();
            int needConfirmation = 0;
            long totalBytes = 0;
            for (int i = 0; i < suggestionRows.Count; i++)
            {
                CleanupSuggestionRow row = suggestionRows[i];
                if (!row.Suggestion.Selected || row.Suggestion.Status == CleanupStatus.Deleted) continue;
                selectedRows.Add(row);
                totalBytes += row.Suggestion.Bytes;
                if (row.Suggestion.Sandbox != null && row.Suggestion.Sandbox.Action == SandboxAction.RequireConfirmation) needConfirmation++;
            }

            if (selectedRows.Count == 0)
            {
                AntdUI.Modal.open(this, "提示", "请先勾选至少一项。", AntdUI.TType.Info);
                return;
            }

            DeleteSuggestionRows(selectedRows);
        }

        private void DeleteSingleSuggestion(CleanupSuggestionRow row)
        {
            if (row == null || row.Suggestion == null) return;
            SaveSettingsFromUi();
            RefreshSuggestionSandboxFromCurrentSettings();
            if (row.Suggestion.Status == CleanupStatus.Deleted)
            {
                AntdUI.Modal.open(this, "提示", "该建议项已删除。", AntdUI.TType.Info);
                return;
            }

            DeleteSuggestionRows(new List<CleanupSuggestionRow> { row });
        }

        private void DeleteSuggestionRows(List<CleanupSuggestionRow> rows)
        {
            if (rows == null || rows.Count == 0) return;

            int needConfirmation = 0;
            long totalBytes = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                CleanupSuggestionRow row = rows[i];
                if (row == null || row.Suggestion == null) continue;
                totalBytes += row.Suggestion.Bytes;
                if (row.Suggestion.Sandbox != null && row.Suggestion.Sandbox.Action == SandboxAction.RequireConfirmation) needConfirmation++;
            }

            string message = "即将删除 " + rows.Count + " 项。" +
                Environment.NewLine + Environment.NewLine +
                "总大小：" + StorageFormatting.FormatBytes(totalBytes);
            if (needConfirmation > 0)
            {
                message += Environment.NewLine + Environment.NewLine + "其中 " + needConfirmation + " 项未命中白名单，需要你承担确认责任。";
            }

            message += Environment.NewLine + Environment.NewLine + "当前使用 WinAPI 直接删除，不经过回收站，无法从回收站恢复。";

            AntdUI.TType icon = AntdUI.TType.Warn;
            AntdUI.Modal.Config config = AntdUI.Modal.config(this, "确认删除", message, icon);
            config.OkText = "确认删除";
            config.CancelText = "取消";
            config.OkType = AntdUI.TTypeMini.Error;
            config.MaskClosable = false;
            DialogResult confirm = AntdUI.Modal.open(config);
            if (confirm != DialogResult.OK) return;

            List<DeletionOutcome> outcomes = new List<DeletionOutcome>();
            DateTime deleteStartedAt = DateTime.UtcNow;
            RunBackground("正在执行删除…", delegate
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    CleanupSuggestionRow row = rows[i];
                    if (row == null || row.Suggestion == null) continue;
                    CleanupResult result = deletionService.Delete(row.Suggestion, settings.Sandbox.UseRecycleBin);
                    outcomes.Add(new DeletionOutcome { Row = row, Result = result });
                }
            }, delegate
            {
                int successCount = 0;
                int failedCount = 0;
                for (int i = 0; i < outcomes.Count; i++)
                {
                    DeletionOutcome outcome = outcomes[i];
                    if (outcome.Result.Success)
                    {
                        outcome.Row.SetStatus(CleanupStatus.Deleted, outcome.Result.Message);
                        successCount++;
                    }
                    else
                    {
                        outcome.Row.SetStatus(CleanupStatus.Failed, outcome.Result.Message);
                        failedCount++;
                    }
                }
                suggestionTable.Refresh();
                TimeSpan elapsed = DateTime.UtcNow - deleteStartedAt;
                Log("删除流程执行完成：成功 " + successCount + " 项，失败 " + failedCount + " 项，耗时 " + elapsed.TotalSeconds.ToString("0.00") + " 秒。");
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

        private void RefreshSuggestionSandboxFromCurrentSettings()
        {
            if (suggestionRows == null || suggestionRows.Count == 0 || settings == null || settings.Sandbox == null) return;

            bool elevated = privilegeService.IsProcessElevated();
            for (int i = 0; i < suggestionRows.Count; i++)
            {
                CleanupSuggestionRow row = suggestionRows[i];
                row.Suggestion.Sandbox = deletionSandbox.Evaluate(row.Suggestion.Path, settings.Sandbox, elevated);
                row.RefreshSandbox();
            }

            if (suggestionTable != null) suggestionTable.Refresh();
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

        private void StorageTable_CellClick(object sender, AntdUI.TableClickEventArgs eventArgs)
        {
            if (storageTable != null && storageTable.CanFocus) storageTable.Focus();
            StorageEntryRow row = eventArgs.Record as StorageEntryRow;
            if (row == null || row.Item == null) return;

            storageContextRow = row;
            storageTable.SetSelected(row);
            if (eventArgs.Button != MouseButtons.Right) return;

            openStorageMenuItem.Enabled = !string.IsNullOrWhiteSpace(row.Item.Path);
            deleteStorageMenuItem.Enabled = CanOfferStorageDelete(row);
            deleteStorageMenuItem.Text = "删除" + (row.Item.IsDirectory ? "文件夹" : "文件");
            storageContextMenu.Show(storageTable, new Point(eventArgs.X, eventArgs.Y));
        }

        private void OpenStorageMenuItem_Click(object sender, EventArgs eventArgs)
        {
            OpenStorageRow(storageContextRow);
        }

        private void OpenStorageRow(StorageEntryRow row)
        {
            if (row == null || row.Item == null || string.IsNullOrWhiteSpace(row.Item.Path)) return;
            explorerService.OpenPath(row.Item.Path, !row.Item.IsDirectory);
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
            if (busy || activePageId != PageScan) return false;

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
            if (selectedIndex > 0)
            {
                StorageEntryRow indexedRowFallback = GetStorageRowAtIndex(selectedIndex - 1);
                if (indexedRowFallback != null) return indexedRowFallback;
            }

            return storageContextRow;
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
            string message = "确认要删除此文件（夹）吗？" +
                Environment.NewLine + Environment.NewLine +
                "路径：" + row.Item.Path +
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

            AntdUI.TType icon = !settings.Sandbox.UseRecycleBin || (sandbox != null && sandbox.Action == SandboxAction.RequireConfirmation)
                ? AntdUI.TType.Warn
                : AntdUI.TType.Info;
            AntdUI.Modal.Config config = AntdUI.Modal.config(this, "确认删除", message, icon);
            config.OkText = "确认删除";
            config.CancelText = "取消";
            config.OkType = AntdUI.TTypeMini.Error;
            config.MaskClosable = false;
            DialogResult confirm = AntdUI.Modal.open(config);
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

            StorageItem removedItem = row.Item;
            List<StorageItem> ancestors = new List<StorageItem>();
            if (!TryRemoveStorageItem(currentRoot, removedItem, ancestors))
            {
                storageTable.Refresh();
                return;
            }

            AdjustAncestorStats(ancestors, removedItem);
            UpdatePathAfterStorageDelete(row, ancestors);
            RemoveStorageRowFromParent(row);
            RefreshStorageAncestorRows(row.Parent);
            RemoveExpandedStoragePathsFor(removedItem.Path);
            currentTreeVersion++;
            storageTreeDeleteDirty = true;
            if (row.Parent != null) storageTable.SetSelected(row.Parent);
            storageTable.Refresh();
        }

        private static void RemoveStorageRowFromParent(StorageEntryRow row)
        {
            if (row == null || row.Parent == null || row.Parent.Children == null) return;
            row.Parent.Children.Remove(row);
        }

        private static void RefreshStorageAncestorRows(StorageEntryRow row)
        {
            StorageEntryRow current = row;
            while (current != null)
            {
                current.RefreshDisplayValues();
                current = current.Parent;
            }
        }

        private void RemoveExpandedStoragePathsFor(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || expandedStoragePaths.Count == 0) return;

            List<string> removeKeys = new List<string>();
            foreach (string expandedPath in expandedStoragePaths)
            {
                if (IsSameOrChildPath(expandedPath, path)) removeKeys.Add(expandedPath);
            }

            for (int index = 0; index < removeKeys.Count; index++) expandedStoragePaths.Remove(removeKeys[index]);
        }

        private void TrackStorageExpandedPath(StorageEntryRow row, bool expanded)
        {
            if (row == null || row.Item == null || string.IsNullOrWhiteSpace(row.Item.Path)) return;

            string key = NormalizePathForComparison(row.Item.Path);
            if (string.IsNullOrWhiteSpace(key)) return;

            if (expanded) expandedStoragePaths.Add(key);
            else expandedStoragePaths.Remove(key);
        }

        private bool IsStorageRowExpanded(StorageEntryRow row)
        {
            if (row == null || row.Item == null || string.IsNullOrWhiteSpace(row.Item.Path)) return false;
            return expandedStoragePaths.Contains(NormalizePathForComparison(row.Item.Path));
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

        private bool NeedAutoScanBeforeAnalyze(string location)
        {
            if (currentRoot == null) return true;
            return !IsSamePath(currentRoot.Path, location);
        }

        private void SetSuggestionSelection(bool selected)
        {
            if (suggestionRows == null || suggestionRows.Count == 0) return;

            for (int i = 0; i < suggestionRows.Count; i++)
            {
                CleanupSuggestionRow row = suggestionRows[i];
                if (row == null || row.Suggestion == null || row.Suggestion.Status == CleanupStatus.Deleted) continue;
                row.selected = selected;
            }

            if (suggestionTable != null) suggestionTable.Refresh();
        }

        private void InvertSuggestionSelection()
        {
            if (suggestionRows == null || suggestionRows.Count == 0) return;

            for (int i = 0; i < suggestionRows.Count; i++)
            {
                CleanupSuggestionRow row = suggestionRows[i];
                if (row == null || row.Suggestion == null || row.Suggestion.Status == CleanupStatus.Deleted) continue;
                row.selected = !row.selected;
            }

            if (suggestionTable != null) suggestionTable.Refresh();
        }

        private void RefreshPromptForCurrentLocation()
        {
            if (systemPromptInput == null) return;

            AiPromptPreset preset = null;
            if (aiPromptPresetSelect != null && aiPromptPresetSelect.SelectedValue != null)
            {
                string key = aiPromptPresetSelect.SelectedValue.ToString();
                if (!string.Equals(key, CustomAiPromptPresetKey, StringComparison.OrdinalIgnoreCase))
                {
                    preset = FindAiPromptPreset(key);
                }
            }

            if (preset == null) preset = FindAiPromptPresetByPrompt(systemPromptInput.Text);
            if (preset == null) return;

            syncingAiPromptPreset = true;
            try
            {
                if (aiPromptPresetSelect != null) aiPromptPresetSelect.SelectedValue = preset.Key;
                systemPromptInput.Text = preset.BuildPrompt(GetPromptDriveRoot());
            }
            finally
            {
                syncingAiPromptPreset = false;
            }
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
                SortMode = sortSelect.SelectedValue is ScanSortMode ? (ScanSortMode)sortSelect.SelectedValue : ScanSortMode.Allocated,
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
            request.SessionIdentity = template.SessionIdentity;
            request.SessionNodeId = template.SessionNodeId;
            return request;
        }

        private static string DescribeScanRequest(ScanRequest request)
        {
            if (request == null) return "<null>";
            return "location=" + request.Location + "，minSize=" + (request.MinSizeBytes < 0 ? "不限" : StorageFormatting.FormatBytes(request.MinSizeBytes)) + "，limit=" + request.PerLevelLimit + "，sort=" + request.SortMode + "，loadDepth=" + request.LoadDepth + "，session=" + request.SessionIdentity + "/" + request.SessionNodeId;
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
            string key = e.Btn == null ? null : e.Btn.Id;
            if (string.Equals(key, "delete", StringComparison.OrdinalIgnoreCase)) DeleteSingleSuggestion(row);
            else explorerService.OpenPath(row.path, !row.Suggestion.IsDirectory);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Delete && TryHandleStorageDeleteShortcut()) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && TryHandleStorageDeleteShortcut())
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            base.OnKeyDown(e);
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
                        Log(caption + "失败：" + error.Message + Environment.NewLine + error);
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
            if (suggestionDriveSelect != null) suggestionDriveSelect.Enabled = !busy;
            if (suggestionMinSizeInput != null) suggestionMinSizeInput.Enabled = !busy;
            if (suggestionLimitInput != null) suggestionLimitInput.Enabled = !busy;
            if (sortSelect != null) sortSelect.Enabled = !busy;
            analyzeButton.Enabled = !busy;
            regularCleanButton.Enabled = !busy;
            deleteButton.Enabled = !busy;
            saveSettingsButton.Enabled = !busy;
            if (testAiSettingsButton != null) testAiSettingsButton.Enabled = !busy;
            if (applyAiProfileButton != null) applyAiProfileButton.Enabled = !busy;
            if (saveAiProfileButton != null) saveAiProfileButton.Enabled = !busy;
            if (selectAllSuggestionsButton != null) selectAllSuggestionsButton.Enabled = !busy;
            if (clearAllSuggestionsButton != null) clearAllSuggestionsButton.Enabled = !busy;
            if (invertSuggestionsButton != null) invertSuggestionsButton.Enabled = !busy;
            if (privilegedCheckbox != null) privilegedCheckbox.Enabled = !busy;
            if (privilegedQuickCheckbox != null) privilegedQuickCheckbox.Enabled = !busy;
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

        private static IList<AiModelCookieMapping> ParseModelCookieMappings(string text, string currentModel)
        {
            List<AiModelCookieMapping> mappings = new List<AiModelCookieMapping>();
            string[] parts = (text ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string line = parts[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                int separatorIndex = line.IndexOf('=');
                string model;
                string cookie;
                if (separatorIndex > 0 && separatorIndex < line.Length - 1 && LooksLikeModelCookieMapping(line, separatorIndex))
                {
                    model = line.Substring(0, separatorIndex).Trim();
                    cookie = line.Substring(separatorIndex + 1).Trim();
                }
                else
                {
                    model = currentModel;
                    cookie = line;
                }
                if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(cookie)) continue;

                mappings.Add(new AiModelCookieMapping
                {
                    Model = model,
                    Cookie = cookie
                });
            }

            return AiSettings.NormalizeModelCookieMappings(mappings);
        }

        private static bool LooksLikeModelCookieMapping(string line, int separatorIndex)
        {
            string left = line.Substring(0, separatorIndex).Trim();
            if (string.IsNullOrWhiteSpace(left)) return false;
            if (left.IndexOf(';') >= 0 || left.IndexOf(' ') >= 0 || left.IndexOf('\t') >= 0) return false;
            return left.IndexOf('/') >= 0 || left.IndexOf(':') >= 0 || left.IndexOf('.') >= 0 || left.StartsWith("gpt", StringComparison.OrdinalIgnoreCase) || left.StartsWith("claude", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatModelCookieMappings(IEnumerable<AiModelCookieMapping> mappings, string currentModel)
        {
            IList<AiModelCookieMapping> normalized = AiSettings.NormalizeModelCookieMappings(mappings);
            string model = (currentModel ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(model))
            {
                for (int i = normalized.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(normalized[i].Model, model, StringComparison.OrdinalIgnoreCase)) return normalized[i].Cookie;
                }
            }

            List<string> lines = new List<string>();
            for (int i = 0; i < normalized.Count; i++)
            {
                lines.Add(normalized[i].Model + "=" + normalized[i].Cookie);
            }

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static int ParsePositiveInt(string text, int fallback)
        {
            int parsed;
            return int.TryParse(text, out parsed) && parsed > 0 ? parsed : fallback;
        }

        private long ResolveCandidateMinBytes(bool preferAi)
        {
            long configured = settings != null && settings.Scan != null && settings.Scan.MinSizeMb > 0
                ? settings.Scan.MinSizeMb * 1024L * 1024L / 2L
                : -1L;
            long baseline = preferAi ? 67108864L : 16777216L;
            return Math.Max(baseline, configured);
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

        private static AntdUI.Button CreateSuggestionActionButton(string text, AntdUI.TTypeMini type)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.Text = text;
            button.Type = type;
            button.Width = 78;
            button.Height = 28;
            button.Radius = 6;
            button.BorderWidth = 1F;
            button.Margin = new Padding(8, 0, 0, 0);
            return button;
        }

        private static AntdUI.Button CreateSettingsActionButton(string text, AntdUI.TTypeMini type)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.Dock = DockStyle.Fill;
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.Text = text;
            button.Type = type;
            button.Height = 34;
            button.Radius = 6;
            button.BorderWidth = 1F;
            button.Margin = new Padding(0, 4, 8, 4);
            return button;
        }

        private Control CreateAiSettingsHeaderControls()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.Transparent;
            panel.ColumnCount = 2;
            panel.RowCount = 1;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            panel.Controls.Add(aiEnabledSwitch, 0, 0);
            panel.Controls.Add(testAiSettingsButton, 1, 0);
            return panel;
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

        private static AntdUI.Checkbox CreateCheckbox(string text)
        {
            AntdUI.Checkbox checkbox = new AntdUI.Checkbox();
            checkbox.Dock = DockStyle.Fill;
            checkbox.Text = text;
            checkbox.Font = new Font("Microsoft YaHei UI", 9.5F);
            checkbox.ForeColor = TextPrimaryColor;
            checkbox.BackColor = Color.Transparent;
            return checkbox;
        }

        private AntdUI.Select CreateSelect()
        {
            AntdUI.Select select = new AntdUI.Select();
            select.Dock = DockStyle.Fill;
            select.DropDownArrow = true;
            select.ListAutoWidth = true;
            select.Font = Font;
            return select;
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

        private static void ConfigureCleanupListSurface(AntdUI.Table table)
        {
            ConfigureTableSurface(table);
            table.RowHeight = 54;
            table.RowHeightHeader = 42;
            table.GapCell = 8;
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

        private sealed class AiPromptPreset
        {
            public AiPromptPreset(string key, string name, string prompt)
            {
                Key = key;
                Name = name;
                Prompt = prompt;
            }

            public string Key { get; private set; }
            public string Name { get; private set; }
            public string Prompt { get; private set; }

            public string BuildPrompt(string driveRoot)
            {
                return BuildDriveScopedPrompt(Prompt, driveRoot);
            }
        }

        private static string BuildDriveScopedPrompt(string prompt, string driveRoot)
        {
            string driveLabel = FormatDriveLabel(driveRoot);
            string normalizedRoot = NormalizeDriveRootText(driveRoot);
            return "当前重点分析 Windows " + driveLabel + "（" + normalizedRoot + "）下的候选路径。" + prompt;
        }

        private static string NormalizeDriveRootText(string driveRoot)
        {
            string root = TryGetDriveRoot(driveRoot);
            return string.IsNullOrWhiteSpace(root) ? "当前所选位置" : root;
        }

        private static string FormatDriveLabel(string driveRoot)
        {
            string root = TryGetDriveRoot(driveRoot);
            if (string.IsNullOrWhiteSpace(root) || root.Length < 2) return "当前磁盘";
            return char.ToUpperInvariant(root[0]) + "盘";
        }

        private sealed class AiProviderPreset
        {
            public AiProviderPreset(string key, string name, string endpoint, string model)
            {
                Key = key;
                Name = name;
                Endpoint = endpoint;
                Model = model;
            }

            public string Key { get; private set; }
            public string Name { get; private set; }
            public string Endpoint { get; private set; }
            public string Model { get; private set; }
        }
    }
}

