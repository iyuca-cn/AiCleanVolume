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
    public sealed class MainWindow : AntdUI.Window
    {
        private static readonly Color PageBackground = Color.FromArgb(250, 250, 250);
        private static readonly Color SurfaceColor = Color.White;
        private static readonly Color FillSecondary = Color.FromArgb(247, 249, 252);
        private static readonly Color BorderDefaultColor = Color.FromArgb(217, 224, 236);
        private static readonly Color BorderLightColor = Color.FromArgb(235, 238, 245);
        private static readonly Color PrimaryColor = Color.FromArgb(22, 119, 255);
        private static readonly Color PrimarySoftColor = Color.FromArgb(230, 244, 255);
        private static readonly Color TextPrimaryColor = Color.FromArgb(31, 31, 31);
        private static readonly Color TextSecondaryColor = Color.FromArgb(89, 89, 89);
        private static readonly Color TextTertiaryColor = Color.FromArgb(140, 140, 140);
        private const string PageScan = "scan";
        private const string PageSuggestions = "suggestions";
        private const string PageLog = "log";
        private const string PageSettings = "settings";
        private const string PageAiProfileCreate = "ai_profile_create";
        private const string AppDisplayName = "AI智能清盘";
        private const int WmSize = 0x0005;
        private const int WmEraseBackground = 0x0014;
        private const int WmGetMinMaxInfo = 0x0024;
        private const int WmSetRedraw = 0x000B;
        private const int RedrawWindowInvalidate = 0x0001;
        private const int RedrawWindowErase = 0x0004;
        private const int RedrawWindowAllChildren = 0x0080;
        private const int RedrawWindowUpdateNow = 0x0100;
        private const int RedrawWindowEraseNow = 0x0200;
        private const int RedrawWindowFrame = 0x0400;
        private const int RestoreRedrawFlags = RedrawWindowInvalidate | RedrawWindowErase | RedrawWindowAllChildren | RedrawWindowUpdateNow | RedrawWindowEraseNow | RedrawWindowFrame;
        private static readonly IntPtr SizeRestored = IntPtr.Zero;
        private static readonly IntPtr SizeMaximized = new IntPtr(2);
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
        private readonly ConfiguredPathCleanupPlanner configuredPathCleanupPlanner;
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
        private AntdUI.Panel pageHost;
        private AntdUI.Panel sidebarHost;
        private AntdUI.Panel sidebarPanel;
        private AntdUI.Panel sidebarBrandPanel;
        private AntdUI.Label sidebarBrandTextLabel;
        private AntdUI.Panel sidebarResizeRail;
        private AntdUI.Button settingsNavButton;
        private AntdUI.Panel scanPage;
        private AntdUI.Panel suggestionsPage;
        private AntdUI.Panel logPage;
        private AntdUI.Panel settingsPage;
        private AntdUI.Panel aiProfileCreatePage;
        private AntdUI.Button scanButton;
        private AntdUI.Button analyzeButton;
        private AntdUI.Button regularCleanButton;
        private AntdUI.Button superCleanButton;
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
        private AntdUI.StackPanel aiProfileListPanel;
        private AntdUI.Button applyAiProfileButton;
        private AntdUI.Button addAiProfileButton;
        private AntdUI.Button saveAiProfilePageButton;
        private AntdUI.Button cancelAiProfilePageButton;
        private AntdUI.Button backAiProfilePageButton;
        private AntdUI.Input aiProfileNameInput;
        private AntdUI.Select aiProfileAccessModeSelect;
        private AntdUI.Select aiProfileProviderPresetSelect;
        private AntdUI.Input aiProfileEndpointInput;
        private AntdUI.Input aiProfileApiKeyInput;
        private AntdUI.Input aiProfileModelInput;
        private AntdUI.Input aiProfileMaxSuggestionsInput;
        private AntdUI.Select aiProfilePromptPresetSelect;
        private AntdUI.Input aiProfileCookieMappingsInput;
        private AntdUI.Input aiProfileSystemPromptInput;
        private AntdUI.Select aiProviderPresetSelect;
        private AntdUI.Select aiPromptPresetSelect;
        private AntdUI.Input systemPromptInput;
        private AntdUI.Input modelCookieMappingsInput;
        private AntdUI.Input allowRootsInput;
        private AntdUI.Input logInput;

        private AntdUI.Label selectedDriveValueLabel;
        private AntdUI.Label totalSpaceValueLabel;
        private AntdUI.Label usedSpaceValueLabel;
        private AntdUI.Label availableSpaceValueLabel;
        private AntdUI.Label reservedSpaceValueLabel;
        private AntdUI.Label scanStatusLabel;
        private AntdUI.Progress scanProgress;

        private readonly string defaultDescription = "选择磁盘或目录，扫描空间占用，生成可确认的安全清理建议";
        private FormWindowState lastWindowState;
        private bool applyingNormalBounds;
        private bool startupRedrawSuspended;
        private bool startupRedrawCompleted;
        private bool startupRevealQueued;
        private bool startupUiBindingQueued;
        private bool startupUiBindingCompleted;
        private bool loadingStartupUi;
        private bool restoreBoundsQueued;
        private bool busy;
        private bool sidebarResizing;
        private bool syncingAiPromptPreset;
        private bool syncingAiProviderPreset;
        private bool syncingAiProfilePromptPreset;
        private bool syncingAiProfileProviderPreset;
        private bool syncingPrivilegeCheckboxes;
        private bool storageTreeDeleteDirty;
        private int sidebarWidth;
        private int sidebarResizeStartX;
        private int sidebarResizeStartWidth;
        private readonly HashSet<string> expandedStoragePaths;
        private const string StorageContextOpenId = "open";
        private const string StorageContextDeleteId = "delete";

        public MainWindow()
        {
            settingsStore = new SettingsStore();
            settings = settingsStore.Load();
            candidatePlanner = new CandidatePlanner();
            configuredPathCleanupPlanner = new ConfiguredPathCleanupPlanner();
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
            ApplyInitialUiPlaceholders();
        }

        private void InitializeComponent()
        {
            SetStyle(ControlStyles.ResizeRedraw, true);
            UpdateStyles();
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
            appBar.ShowIcon = true;
            appBar.IconSvg = "RobotFilled";
            appBar.IconRatio = 0.62F;
            appBar.UseTitleFont = false;
            appBar.DividerShow = true;
            appBar.DividerMargin = 3;
            appBar.DividerColor = BorderLightColor;
            appBar.Padding = new Padding(12, 0, 0, 0);
            appBar.Text = AppDisplayName;
            appBar.SubText = "扫描界面";
            appBar.Description = string.Empty;
            appBar.Height = 40;

            titleBar = new AntdUI.PageHeader();
            titleBar.Dock = DockStyle.Top;
            titleBar.BackColor = PageBackground;
            titleBar.ShowButton = false;
            titleBar.ShowIcon = false;
            titleBar.UseTitleFont = true;
            titleBar.DividerShow = false;
            titleBar.Padding = new Padding(24, 6, 0, 10);
            titleBar.Text = AppDisplayName;
            titleBar.Description = defaultDescription;
            titleBar.Height = 78;

            saveSettingsButton = CreateHeaderButton("保存配置", AntdUI.TTypeMini.Default);
            saveSettingsButton.Click += delegate { SaveSettings(); };

            deleteButton = CreateHeaderButton("删除勾选", AntdUI.TTypeMini.Error);
            deleteButton.Click += delegate { DeleteSelectedSuggestions(); };

            analyzeButton = CreateHeaderButton("AI 识别", AntdUI.TTypeMini.Success);
            analyzeButton.Click += delegate { AnalyzeSuggestions(); };

            regularCleanButton = CreateHeaderButton("常规清理", AntdUI.TTypeMini.Primary);
            regularCleanButton.Click += delegate { AnalyzeRegularSuggestions(); };

            superCleanButton = CreateHeaderButton("超级清理", AntdUI.TTypeMini.Warn);
            superCleanButton.Click += delegate { AnalyzeSuperSuggestions(); };

            scanButton = CreateToolbarActionButton("扫描", AntdUI.TTypeMini.Primary);
            scanButton.Click += delegate { ScanCurrentLocation(); };

            titleBar.Controls.Add(saveSettingsButton);
            titleBar.Controls.Add(deleteButton);
            titleBar.Controls.Add(analyzeButton);
            titleBar.Controls.Add(superCleanButton);
            titleBar.Controls.Add(regularCleanButton);

            AntdUI.Panel shell = CreateFlatPanel();
            shell.Dock = DockStyle.Fill;
            shell.BackColor = SurfaceColor;
            shell.Padding = Padding.Empty;

            AntdUI.Panel contentHost = CreateFlatPanel();
            contentHost.Dock = DockStyle.Fill;
            contentHost.BackColor = SurfaceColor;
            contentHost.Padding = Padding.Empty;

            pageHost = CreateFlatPanel();
            pageHost.Dock = DockStyle.Fill;
            pageHost.BackColor = PageBackground;
            pageHost.Padding = new Padding(24, 12, 24, 24);

            scanPage = CreatePageContainer();
            scanPage.Controls.Add(CreateStoragePanel());
            scanPage.Controls.Add(CreateScanToolbarPanel());

            suggestionsPage = CreatePageContainer();
            suggestionsPage.Controls.Add(CreateSuggestionPanel());

            logPage = CreatePageContainer();
            logPage.Controls.Add(CreateLogPanel());

            settingsPage = CreatePageContainer();
            settingsPage.Controls.Add(CreateSettingsPanel());

            aiProfileCreatePage = CreatePageContainer();
            aiProfileCreatePage.Controls.Add(CreateAiProfileCreatePage());

            pageHost.Controls.Add(aiProfileCreatePage);
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

        private AntdUI.Panel CreatePageContainer()
        {
            AntdUI.Panel page = CreateFlatPanel();
            page.Dock = DockStyle.Fill;
            page.BackColor = PageBackground;
            return page;
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
            sidebarPanel.Back = SurfaceColor;
            sidebarPanel.BorderWidth = 1F;
            sidebarPanel.BorderColor = BorderLightColor;
            sidebarPanel.Radius = 0;
            sidebarPanel.Shadow = 0;
            sidebarPanel.Padding = new Padding(14, 12, 14, 14);

            AntdUI.Panel footerPanel = CreateSidebarFooterPanel();
            AntdUI.Panel dividerPanel = CreateFlatPanel();
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

        private AntdUI.Panel CreateSidebarBrandPanel()
        {
            AntdUI.Panel brandPanel = CreateFlatPanel();
            brandPanel.Dock = DockStyle.Top;
            brandPanel.Height = 70;
            brandPanel.Padding = new Padding(6, 10, 6, 14);
            brandPanel.BackColor = Color.Transparent;

            sidebarBrandTextLabel = new AntdUI.Label();
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
            menu.Radius = 9;
            menu.Indent = false;
            menu.Gap = 12;
            menu.IconGap = 10;
            menu.itemMargin = 5;
            menu.IconRatio = 1.08F;
            menu.Padding = new Padding(2, 6, 2, 6);
            menu.ForeColor = TextSecondaryColor;
            menu.BackHover = Color.FromArgb(245, 248, 255);
            menu.BackActive = PrimarySoftColor;
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

        private AntdUI.Panel CreateSidebarFooterPanel()
        {
            AntdUI.Panel footerPanel = CreateFlatPanel();
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
            settingsNavButton.Ghost = true;
            settingsNavButton.WaveSize = 2;
            settingsNavButton.DefaultBorderColor = BorderLightColor;
            settingsNavButton.Click += SettingsNavButton_Click;

            footerPanel.Controls.Add(settingsNavButton);
            return footerPanel;
        }

        private Control CreateScanToolbarPanel()
        {
            AntdUI.Panel toolbarHost = CreateFlatPanel();
            toolbarHost.Dock = DockStyle.Top;
            toolbarHost.BackColor = PageBackground;
            toolbarHost.Height = 188;
            toolbarHost.Padding = new Padding(0, 0, 0, 12);

            AntdUI.Panel toolbarCard = CreateCardPanel(16);
            toolbarCard.Dock = DockStyle.Fill;
            toolbarCard.Shadow = 14;
            toolbarCard.ShadowOpacity = 0.07F;

            AntdUI.GridPanel toolbarLayout = CreateGridPanel("fill 1 336");
            toolbarLayout.Dock = DockStyle.Fill;
            toolbarLayout.BackColor = Color.Transparent;

            Control filtersPanel = CreateScanFiltersPanel();
            AntdUI.Divider divider = new AntdUI.Divider();
            divider.Dock = DockStyle.Fill;
            divider.Vertical = true;
            divider.ColorSplit = BorderLightColor;
            divider.Margin = new Padding(18, 4, 18, 8);

            Control summaryPanel = CreateDriveSummaryPanel();
            Control statusPanel = CreateScanStatusPanel();
            AntdUI.GridPanel leftLayout = CreateGridPanel("84:fill;fill:fill");
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
            AntdUI.GridPanel host = CreateGridPanel("42:fill;42:fill");
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.Transparent;

            AntdUI.GridPanel topRow = CreateGridPanel("48 216 92 48 fill");
            topRow.Dock = DockStyle.Fill;
            topRow.BackColor = Color.Transparent;

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

            AddGridControl(topRow, CreateToolbarCaption("选择:"), 0);
            AddGridControl(topRow, driveSelect, 1);
            AddGridControl(topRow, scanButton, 2);
            AddGridControl(topRow, CreateToolbarCaption("位置:"), 3);
            AddGridControl(topRow, pathInput, 4);

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
            sortSelect.Width = sortSelectWidth;
            AntdUI.GridPanel bottomRow = CreateGridPanel("48 58 48 58 56 " + sortSelectWidth.ToString() + " fill");
            bottomRow.Dock = DockStyle.Fill;
            bottomRow.BackColor = Color.Transparent;

            AddGridControl(bottomRow, CreateToolbarCaption("最小:"), 0);
            AddGridControl(bottomRow, minSizeInput, 1);
            AddGridControl(bottomRow, CreateToolbarCaption("限制:"), 2);
            AddGridControl(bottomRow, limitInput, 3);
            AddGridControl(bottomRow, CreateToolbarCaption("排序:"), 4);
            AddGridControl(bottomRow, sortSelect, 5);
            AddGridControl(bottomRow, CreateGridSpacer(), 6);

            AddGridControl(host, topRow, 0);
            AddGridControl(host, bottomRow, 1);
            return host;
        }

        private Control CreateDriveSummaryPanel()
        {
            AntdUI.GridPanel layout = CreateGridPanel("24:48 fill;24:48 fill 48 96;24:48 fill;24:48 fill");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;
            layout.Padding = new Padding(0, 2, 0, 0);

            selectedDriveValueLabel = CreateSummaryValueLabel(true);
            selectedDriveValueLabel.AutoEllipsis = true;
            totalSpaceValueLabel = CreateSummaryValueLabel(true);
            usedSpaceValueLabel = CreateSummaryValueLabel(true);
            availableSpaceValueLabel = CreateSummaryValueLabel(true);
            reservedSpaceValueLabel = CreateSummaryValueLabel(true);

            AddGridControl(layout, CreateSummaryCaption("选择:"), 0);
            AddGridControl(layout, selectedDriveValueLabel, 1);
            AddGridControl(layout, CreateSummaryCaption("总空间:"), 2);
            AddGridControl(layout, totalSpaceValueLabel, 3);
            AddGridControl(layout, CreateSummaryCaption("预留:"), 4);
            AddGridControl(layout, reservedSpaceValueLabel, 5);
            AddGridControl(layout, CreateSummaryCaption("已用:"), 6);
            AddGridControl(layout, usedSpaceValueLabel, 7);
            AddGridControl(layout, CreateSummaryCaption("可用:"), 8);
            AddGridControl(layout, availableSpaceValueLabel, 9);
            return layout;
        }

        private Control CreateScanStatusPanel()
        {
            AntdUI.Panel panel = CreateFlatPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.Transparent;
            panel.Padding = new Padding(0, 6, 0, 0);

            scanStatusLabel = new AntdUI.Label();
            scanStatusLabel.Dock = DockStyle.Top;
            scanStatusLabel.Height = 20;
            scanStatusLabel.Font = new Font("Microsoft YaHei UI", 9F);
            scanStatusLabel.ForeColor = TextSecondaryColor;
            scanStatusLabel.BackColor = Color.Transparent;
            scanStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            scanStatusLabel.Text = "等待开始扫描";

            scanProgress = new AntdUI.Progress();
            scanProgress.Dock = DockStyle.Top;
            scanProgress.Height = 16;
            scanProgress.Shape = AntdUI.TShapeProgress.Round;
            scanProgress.Radius = 8;
            scanProgress.Value = 0F;
            scanProgress.State = AntdUI.TType.Success;
            scanProgress.UseSystemText = false;

            panel.Controls.Add(scanProgress);
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

            panel.Controls.Add(storageTable);
            return panel;
        }

        private Control CreateSuggestionPanel()
        {
            AntdUI.Panel panel = CreateCardPanel(20);
            panel.Dock = DockStyle.Fill;

            AntdUI.Label heading = CreateSectionTitle("清理建议");

            AntdUI.Label desc = CreateSectionDescription("支持“常规清理”（仅内置/配置路径汇总）、“超级清理”（扫描规则）和“AI 识别”；列表默认勾选可安全处理项。");

            AntdUI.Panel optionsBar = CreateFlatPanel();
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

            AntdUI.GridPanel scanOptionsBar = CreateGridPanel("48 160 150 78 90 78 fill");
            scanOptionsBar.Dock = DockStyle.Top;
            scanOptionsBar.Height = 42;
            scanOptionsBar.Padding = new Padding(0, 0, 0, 8);
            scanOptionsBar.BackColor = Color.Transparent;

            suggestionDriveSelect = CreateSelect();
            suggestionDriveSelect.ListAutoWidth = true;
            suggestionDriveSelect.SelectedValueChanged += SuggestionDriveSelect_SelectedValueChanged;
            suggestionMinSizeInput = CreateInput("最小值（单位MB）");
            suggestionMinSizeInput.Text = "128";
            suggestionLimitInput = CreateInput("数量限制，-1 不限");
            suggestionLimitInput.Text = "-1";

            AddGridControl(scanOptionsBar, CreateToolbarCaption("盘符:"), 0);
            AddGridControl(scanOptionsBar, suggestionDriveSelect, 1);
            AddGridControl(scanOptionsBar, CreateToolbarCaption("最小值（MB）:"), 2);
            AddGridControl(scanOptionsBar, suggestionMinSizeInput, 3);
            AddGridControl(scanOptionsBar, CreateToolbarCaption("数量限制:"), 4);
            AddGridControl(scanOptionsBar, suggestionLimitInput, 5);
            AddGridControl(scanOptionsBar, CreateGridSpacer(), 6);

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
            AntdUI.Panel panel = CreateFlatPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = PageBackground;
            panel.Padding = new Padding(20);

            AntdUI.Label heading = CreateSectionTitle("设置");
            AntdUI.Label desc = CreateSectionDescription("管理 AI 接入、提示词、沙盒白名单和删除策略。");

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
            aiProfileSelect = CreateSettingsSelect();
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
            aiPromptPresetSelect = CreateSettingsSelect();
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

            AntdUI.StackPanel scrollHost = CreateVerticalScrollPanel();
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.AutoScroll = true;
            scrollHost.BackColor = PageBackground;
            scrollHost.Padding = new Padding(0, 0, 4, 12);

            AntdUI.GridPanel layout = CreateGridPanel("82:fill;520:fill;266:fill");
            layout.Dock = DockStyle.Top;
            layout.Height = 868;
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
            overviewSection.Margin = new Padding(0, 0, 0, 12);
            profilesSection.Margin = new Padding(0, 0, 0, 12);
            sandboxSection.Margin = new Padding(0);

            AddGridControl(layout, overviewSection, 0);
            AddGridControl(layout, profilesSection, 1);
            AddGridControl(layout, sandboxSection, 2);

            scrollHost.Controls.Add(layout);

            panel.Controls.Add(scrollHost);
            panel.Controls.Add(desc);
            panel.Controls.Add(heading);
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

        private static AntdUI.Panel CreateSettingsSurfacePanel(int padding)
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(padding);
            panel.Radius = 14;
            panel.Back = SurfaceColor;
            panel.BorderWidth = 1F;
            panel.BorderColor = BorderLightColor;
            panel.Shadow = 0;
            return panel;
        }

        private static AntdUI.Panel CreateSettingsGroupPanel(string title, string description, out AntdUI.Panel body)
        {
            AntdUI.Panel panel = CreateSettingsSurfacePanel(16);

            AntdUI.Label titleLabel = CreateSettingsGroupTitle(title);
            AntdUI.Label descLabel = CreateSmallMutedLabel(description);
            descLabel.Dock = DockStyle.Top;
            descLabel.Height = 22;

            body = CreateFlatPanel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Color.Transparent;
            body.Padding = new Padding(0, 8, 0, 0);

            panel.Controls.Add(body);
            panel.Controls.Add(descLabel);
            panel.Controls.Add(titleLabel);
            return panel;
        }

        private static AntdUI.Label CreateSettingsGroupTitle(string text)
        {
            AntdUI.Label label = new AntdUI.Label();
            label.Dock = DockStyle.Top;
            label.Height = 26;
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            label.ForeColor = TextPrimaryColor;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static AntdUI.Label CreateSmallMutedLabel(string text)
        {
            AntdUI.Label label = new AntdUI.Label();
            label.Dock = DockStyle.Fill;
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = new Font("Microsoft YaHei UI", 9F);
            label.ForeColor = TextTertiaryColor;
            label.BackColor = Color.Transparent;
            return label;
        }

        private Control CreateLogPanel()
        {
            AntdUI.Panel panel = CreateCardPanel(20);
            panel.Dock = DockStyle.Fill;

            AntdUI.Label heading = CreateSectionTitle("执行日志");

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
            if (sidebarPanel != null) sidebarPanel.Padding = new Padding(14, 12, 14, 14);
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
            settingsNavButton.BackColor = selected ? PrimarySoftColor : SurfaceColor;
            settingsNavButton.DefaultBorderColor = selected ? Color.FromArgb(145, 202, 255) : BorderLightColor;
            settingsNavButton.ForeColor = selected ? PrimaryColor : TextSecondaryColor;
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

                string title = GetPageTitle(pageId);
                titleBar.Text = title;
                titleBar.Description = GetPageDescription(pageId);
                appBar.SubText = title;

                scanButton.Visible = pageId == PageScan;
                analyzeButton.Visible = pageId == PageSuggestions;
                regularCleanButton.Visible = pageId == PageSuggestions;
                superCleanButton.Visible = pageId == PageSuggestions;
                deleteButton.Visible = pageId == PageSuggestions;
                saveSettingsButton.Visible = pageId == PageSettings;
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
            if (titleBar != null) titleBar.SuspendLayout();
        }

        private void ResumePageSwitchLayout()
        {
            if (titleBar != null) titleBar.ResumeLayout(true);
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

        private void ApplyInitialUiPlaceholders()
        {
            loadingStartupUi = true;
            try
            {
                string defaultDrive = ResolveDefaultDrive();
                if (driveSelect != null)
                {
                    driveSelect.Items.Clear();
                    driveSelect.Items.Add(new AntdUI.SelectItem(defaultDrive, defaultDrive));
                    driveSelect.SelectedValue = defaultDrive;
                }

                if (suggestionDriveSelect != null)
                {
                    suggestionDriveSelect.Items.Clear();
                    suggestionDriveSelect.Items.Add(new AntdUI.SelectItem(defaultDrive, defaultDrive));
                    suggestionDriveSelect.SelectedValue = defaultDrive;
                }

                if (pathInput != null) pathInput.Text = defaultDrive;
                SetDriveSummaryValue(selectedDriveValueLabel, defaultDrive);
                SetDriveSummaryValue(totalSpaceValueLabel, "-");
                SetDriveSummaryValue(usedSpaceValueLabel, "-");
                SetDriveSummaryValue(availableSpaceValueLabel, "-");
                SetDriveSummaryValue(reservedSpaceValueLabel, "-");
            }
            finally
            {
                loadingStartupUi = false;
            }
        }

        private static string ResolveDefaultDrive()
        {
            string defaultDrive = Environment.GetEnvironmentVariable("SystemDrive");
            if (string.IsNullOrWhiteSpace(defaultDrive)) defaultDrive = "C:";
            return defaultDrive.TrimEnd('\\') + "\\";
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
            PopulateAiAccessModes(aiAccessModeSelect);
        }

        private static void PopulateAiAccessModes(AntdUI.Select select)
        {
            if (select == null) return;

            select.Items.Clear();
            select.Items.Add(new AntdUI.SelectItem("标准 API", AiSettings.StandardApiAccessMode));
            select.Items.Add(new AntdUI.SelectItem("2API", AiSettings.TwoApiAccessMode));
        }

        private void PopulateAiProviderPresets()
        {
            PopulateAiProviderPresets(aiProviderPresetSelect);
        }

        private static void PopulateAiProviderPresets(AntdUI.Select select)
        {
            if (select == null) return;

            select.Items.Clear();
            select.Items.Add(new AntdUI.SelectItem("自定义", CustomAiProviderPresetKey));
            for (int index = 0; index < AiProviderPresets.Length; index++)
            {
                AiProviderPreset preset = AiProviderPresets[index];
                select.Items.Add(new AntdUI.SelectItem(preset.Name, preset.Key));
            }
        }

        private void PopulateAiPromptPresets()
        {
            PopulateAiPromptPresets(aiPromptPresetSelect);
        }

        private static void PopulateAiPromptPresets(AntdUI.Select select)
        {
            if (select == null) return;

            select.Items.Clear();
            select.Items.Add(new AntdUI.SelectItem("自定义", CustomAiPromptPresetKey));
            for (int index = 0; index < AiPromptPresets.Length; index++)
            {
                AiPromptPreset preset = AiPromptPresets[index];
                select.Items.Add(new AntdUI.SelectItem(preset.Name, preset.Key));
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

        private void SelectAiProfileProviderPresetForValues(string endpoint, string model)
        {
            if (aiProfileProviderPresetSelect == null) return;

            AiProviderPreset preset = FindAiProviderPreset(endpoint, model);
            syncingAiProfileProviderPreset = true;
            try
            {
                aiProfileProviderPresetSelect.SelectedValue = preset == null ? CustomAiProviderPresetKey : preset.Key;
            }
            finally
            {
                syncingAiProfileProviderPreset = false;
            }
        }

        private void SelectAiProfilePromptPresetForPrompt(string prompt)
        {
            if (aiProfilePromptPresetSelect == null) return;

            AiPromptPreset preset = FindAiPromptPresetByPrompt(prompt);
            syncingAiProfilePromptPreset = true;
            try
            {
                aiProfilePromptPresetSelect.SelectedValue = preset == null ? CustomAiPromptPresetKey : preset.Key;
            }
            finally
            {
                syncingAiProfilePromptPreset = false;
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
            if (loadingStartupUi) return;
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
            if (loadingStartupUi) return;
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
            if (loadingStartupUi) return;
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

        private void AiProfileProviderPresetSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (loadingStartupUi) return;
            if (syncingAiProfileProviderPreset || e.Value == null) return;

            string key = e.Value.ToString();
            if (string.Equals(key, CustomAiProviderPresetKey, StringComparison.OrdinalIgnoreCase)) return;

            AiProviderPreset preset = FindAiProviderPresetByKey(key);
            if (preset == null) return;

            syncingAiProfileProviderPreset = true;
            try
            {
                if (aiProfileEndpointInput != null) aiProfileEndpointInput.Text = preset.Endpoint;
                if (aiProfileModelInput != null) aiProfileModelInput.Text = preset.Model;
            }
            finally
            {
                syncingAiProfileProviderPreset = false;
            }
        }

        private void AiEndpointOrModelInput_TextChanged(object sender, EventArgs e)
        {
            if (loadingStartupUi) return;
            if (syncingAiProviderPreset) return;
            SelectAiProviderPresetForSettings(endpointInput == null ? null : endpointInput.Text, modelInput == null ? null : modelInput.Text);
        }

        private void AiProfileEndpointOrModelInput_TextChanged(object sender, EventArgs e)
        {
            if (loadingStartupUi) return;
            if (syncingAiProfileProviderPreset) return;
            SelectAiProfileProviderPresetForValues(aiProfileEndpointInput == null ? null : aiProfileEndpointInput.Text, aiProfileModelInput == null ? null : aiProfileModelInput.Text);
        }

        private void AiProfileAccessModeSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            UpdateAiProfileAccessModeUi();
        }

        private string ResolveSelectedAiProfileAccessMode()
        {
            if (aiProfileAccessModeSelect == null || aiProfileAccessModeSelect.SelectedValue == null) return AiSettings.StandardApiAccessMode;
            return AiSettings.NormalizeAccessMode(aiProfileAccessModeSelect.SelectedValue.ToString());
        }

        private void UpdateAiProfileAccessModeUi()
        {
            bool twoApi = string.Equals(ResolveSelectedAiProfileAccessMode(), AiSettings.TwoApiAccessMode, StringComparison.OrdinalIgnoreCase);
            if (aiProfileApiKeyInput != null)
            {
                aiProfileApiKeyInput.Enabled = !twoApi;
                aiProfileApiKeyInput.PlaceholderText = twoApi ? "2API 模式不使用 API Key" : "sk-...";
            }
            if (aiProfileCookieMappingsInput != null)
            {
                aiProfileCookieMappingsInput.Enabled = true;
            }
        }

        private void SaveSettings()
        {
            try
            {
                SaveSettingsFromUi();
                SaveCurrentAiProfileAutomatic();
                settingsStore.Save(settings);
                Log("配置已保存。");
                ShowInfo("完成", "配置已保存。");
            }
            catch (Exception ex)
            {
                Log("保存配置失败：" + ex.Message);
                ShowError("保存失败", ex.Message);
            }
        }

        private void PopulateAiProfiles()
        {
            if (aiProfileSelect == null)
            {
                RefreshAiProfileCards();
                return;
            }

            string selectedValue = aiProfileSelect.SelectedValue == null ? null : aiProfileSelect.SelectedValue.ToString();
            aiProfileSelect.Items.Clear();
            settings.Ai.Profiles = AiSettings.NormalizeProfiles(settings.Ai.Profiles);
            if (settings.Ai.Profiles.Count == 0)
            {
                aiProfileSelect.Items.Add(new AntdUI.SelectItem("暂无历史配置", string.Empty));
                aiProfileSelect.SelectedValue = string.Empty;
                RefreshAiProfileCards();
                return;
            }

            for (int index = 0; index < settings.Ai.Profiles.Count; index++)
            {
                AiProfile profile = settings.Ai.Profiles[index];
                aiProfileSelect.Items.Add(new AntdUI.SelectItem(BuildAiProfileDisplayName(profile), index.ToString()));
            }
            int selectedIndex;
            if (!int.TryParse(selectedValue, out selectedIndex) || selectedIndex < 0 || selectedIndex >= settings.Ai.Profiles.Count)
            {
                selectedValue = "0";
            }
            aiProfileSelect.SelectedValue = selectedValue;
            RefreshAiProfileCards();
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
                ShowError("测试失败", ex.Message);
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
                ShowNotice(success ? "测试成功" : "测试失败", resultMessage ?? "AI 配置测试完成。", success ? AntdUI.TType.Success : AntdUI.TType.Warn);
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

        private void OpenAiProfileCreatePage()
        {
            InitializeAiProfilePageValues();
            SetActivePage(PageAiProfileCreate);
        }

        private Control CreateAiProfileCreatePage()
        {
            AntdUI.Panel panel = CreateFlatPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = PageBackground;
            panel.Padding = new Padding(20);

            AntdUI.GridPanel pageLayout = CreateGridPanel("fill;56:fill");
            pageLayout.Dock = DockStyle.Fill;
            pageLayout.BackColor = Color.Transparent;

            AntdUI.StackPanel scrollHost = CreateVerticalScrollPanel();
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.AutoScroll = true;
            scrollHost.BackColor = PageBackground;
            scrollHost.Padding = new Padding(0, 0, 4, 12);

            AntdUI.GridPanel content = CreateGridPanel("88:fill;196:fill;196:fill;390:fill");
            content.Dock = DockStyle.Top;
            content.BackColor = PageBackground;
            content.Height = 870;
            content.Width = Math.Max(720, scrollHost.ClientSize.Width - 8);
            scrollHost.Resize += delegate { content.Width = Math.Max(720, scrollHost.ClientSize.Width - 8); };

            AntdUI.Panel header = CreateAiProfileCreateHeader();
            Control basicSection = CreateAiProfileBasicSection();
            Control endpointSection = CreateAiProfileEndpointSection();
            Control promptSection = CreateAiProfilePromptSection();
            header.Margin = new Padding(0, 0, 0, 12);
            basicSection.Margin = new Padding(0, 0, 0, 12);
            endpointSection.Margin = new Padding(0, 0, 0, 12);
            promptSection.Margin = new Padding(0);

            AddGridControl(content, header, 0);
            AddGridControl(content, basicSection, 1);
            AddGridControl(content, endpointSection, 2);
            AddGridControl(content, promptSection, 3);
            scrollHost.Controls.Add(content);

            AntdUI.Panel footer = CreateAiProfileCreateFooter();
            AddGridControl(pageLayout, scrollHost, 0);
            AddGridControl(pageLayout, footer, 1);
            panel.Controls.Add(pageLayout);
            return panel;
        }

        private AntdUI.Panel CreateAiProfileCreateHeader()
        {
            AntdUI.Panel header = CreateFlatPanel();
            header.Dock = DockStyle.Fill;
            header.BackColor = PageBackground;

            AntdUI.GridPanel layout = CreateGridPanel("48 fill");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            backAiProfilePageButton = new AntdUI.Button();
            backAiProfilePageButton.Dock = DockStyle.Fill;
            backAiProfilePageButton.AutoSizeMode = AntdUI.TAutoSize.None;
            backAiProfilePageButton.DisplayStyle = AntdUI.TButtonDisplayStyle.Image;
            backAiProfilePageButton.Shape = AntdUI.TShape.Circle;
            backAiProfilePageButton.IconSvg = "ArrowLeftOutlined";
            backAiProfilePageButton.Type = AntdUI.TTypeMini.Default;
            backAiProfilePageButton.Ghost = true;
            backAiProfilePageButton.BorderWidth = 1F;
            backAiProfilePageButton.DefaultBorderColor = BorderLightColor;
            backAiProfilePageButton.Margin = new Padding(0, 0, 10, 0);
            backAiProfilePageButton.WaveSize = 2;
            backAiProfilePageButton.Click += delegate { CancelAiProfileCreatePage(); };

            AntdUI.Label title = CreateSectionTitle("新增 AI 配置");
            title.Dock = DockStyle.Fill;
            AntdUI.Label desc = CreateSectionDescription("填写接入参数并保存为配置卡片。");
            desc.Dock = DockStyle.Fill;

            AntdUI.GridPanel textLayout = CreateGridPanel("36:fill;30:fill");
            textLayout.Dock = DockStyle.Fill;
            AddGridControl(textLayout, title, 0);
            AddGridControl(textLayout, desc, 1);

            AddGridControl(layout, backAiProfilePageButton, 0);
            AddGridControl(layout, textLayout, 1);
            header.Controls.Add(layout);
            return header;
        }

        private Control CreateAiProfileBasicSection()
        {
            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("基础信息", "命名这套配置，并选择访问方式。", out body);

            AntdUI.GridPanel form = CreateTwoColumnProfileForm(2);
            aiProfileNameInput = CreateInput("例如：开发环境");
            aiProfileAccessModeSelect = CreateSettingsSelect();
            PopulateAiAccessModes(aiProfileAccessModeSelect);
            aiProfileMaxSuggestionsInput = CreateInput("30");

            aiProfileAccessModeSelect.SelectedValueChanged += AiProfileAccessModeSelect_SelectedValueChanged;

            AddProfileField(form, "配置名称", aiProfileNameInput, 0, 0);
            AddProfileField(form, "接入类型", aiProfileAccessModeSelect, 2, 0);
            AddProfileField(form, "建议条数", aiProfileMaxSuggestionsInput, 0, 1);

            body.Controls.Add(form);
            return section;
        }

        private Control CreateAiProfileEndpointSection()
        {
            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("接口参数", "配置 OpenAI 兼容接口地址、密钥和模型。", out body);

            AntdUI.GridPanel form = CreateTwoColumnProfileForm(2);
            aiProfileProviderPresetSelect = CreateSettingsSelect();
            PopulateAiProviderPresets(aiProfileProviderPresetSelect);
            aiProfileEndpointInput = CreateInput("https://api.openai.com");
            aiProfileApiKeyInput = CreateInput("sk-...");
            aiProfileModelInput = CreateInput(AiSettings.DefaultModel);

            aiProfileProviderPresetSelect.SelectedValueChanged += AiProfileProviderPresetSelect_SelectedValueChanged;
            aiProfileEndpointInput.TextChanged += AiProfileEndpointOrModelInput_TextChanged;
            aiProfileModelInput.TextChanged += AiProfileEndpointOrModelInput_TextChanged;

            AddProfileField(form, "接口预设", aiProfileProviderPresetSelect, 0, 0);
            AddProfileField(form, "接口地址", aiProfileEndpointInput, 2, 0);
            AddProfileField(form, "SK / API Key", aiProfileApiKeyInput, 0, 1);
            AddProfileField(form, "模型", aiProfileModelInput, 2, 1);

            body.Controls.Add(form);
            return section;
        }

        private Control CreateAiProfilePromptSection()
        {
            AntdUI.Panel body;
            AntdUI.Panel section = CreateSettingsGroupPanel("提示词与 Cookie", "多行内容使用完整宽度，避免长文本挤压。", out body);

            AntdUI.GridPanel form = CreateGridPanel("44:92 fill;104:92 fill;148:92 fill");
            form.Dock = DockStyle.Fill;
            form.BackColor = Color.Transparent;

            aiProfilePromptPresetSelect = CreateSettingsSelect();
            PopulateAiPromptPresets(aiProfilePromptPresetSelect);
            aiProfileCookieMappingsInput = CreateInput("直接粘贴当前模型的一整行 Cookie；也兼容 model=Cookie");
            aiProfileCookieMappingsInput.Multiline = true;
            aiProfileCookieMappingsInput.AutoScroll = true;
            aiProfileSystemPromptInput = CreateInput("系统提示词");
            aiProfileSystemPromptInput.Multiline = true;
            aiProfileSystemPromptInput.AutoScroll = true;

            aiProfilePromptPresetSelect.SelectedValueChanged += AiProfilePromptPresetSelect_SelectedValueChanged;
            aiProfileSystemPromptInput.TextChanged += AiProfileSystemPromptInput_TextChanged;

            AddWideProfileField(form, "AI 预设", aiProfilePromptPresetSelect, 0);
            AddWideProfileField(form, "模型 Cookie", aiProfileCookieMappingsInput, 1);
            AddWideProfileField(form, "系统提示词", aiProfileSystemPromptInput, 2);

            body.Controls.Add(form);
            return section;
        }

        private AntdUI.Panel CreateAiProfileCreateFooter()
        {
            AntdUI.Panel footer = CreateFlatPanel();
            footer.Dock = DockStyle.Fill;
            footer.BackColor = PageBackground;
            footer.Padding = new Padding(0, 10, 0, 0);

            AntdUI.GridPanel layout = CreateGridPanel("108 116");
            layout.Dock = DockStyle.Right;
            layout.BackColor = Color.Transparent;
            layout.Width = 236;

            cancelAiProfilePageButton = CreateSettingsActionButton("取消", AntdUI.TTypeMini.Default);
            cancelAiProfilePageButton.Dock = DockStyle.Fill;
            cancelAiProfilePageButton.Margin = new Padding(0, 4, 8, 4);
            cancelAiProfilePageButton.Click += delegate { CancelAiProfileCreatePage(); };

            saveAiProfilePageButton = CreateSettingsActionButton("保存", AntdUI.TTypeMini.Primary);
            saveAiProfilePageButton.Dock = DockStyle.Fill;
            saveAiProfilePageButton.Margin = new Padding(0, 4, 0, 4);
            saveAiProfilePageButton.Click += delegate { SaveAiProfileFromPage(); };

            AddGridControl(layout, cancelAiProfilePageButton, 0);
            AddGridControl(layout, saveAiProfilePageButton, 1);
            footer.Controls.Add(layout);
            return footer;
        }

        private static AntdUI.GridPanel CreateTwoColumnProfileForm(int rows)
        {
            string[] rowDefinitions = new string[rows];
            for (int row = 0; row < rows; row++) rowDefinitions[row] = "44:92 fill 92 fill";
            AntdUI.GridPanel form = CreateGridPanel(string.Join(";", rowDefinitions));
            form.Dock = DockStyle.Fill;
            form.BackColor = Color.Transparent;
            return form;
        }

        private static void AddProfileField(AntdUI.GridPanel form, string caption, Control control, int column, int row)
        {
            AntdUI.Label label = CreateCaption(caption);
            label.Margin = new Padding(0, 0, 8, 8);
            control.Margin = new Padding(0, 0, 0, 8);
            int index = row * 4 + column;
            AddGridControl(form, label, index);
            AddGridControl(form, control, index + 1);
        }

        private static void AddWideProfileField(AntdUI.GridPanel form, string caption, Control control, int row)
        {
            AntdUI.Label label = CreateCaption(caption);
            label.Margin = new Padding(0, 0, 8, 8);
            control.Margin = new Padding(0, 0, 0, 8);
            int index = row * 2;
            AddGridControl(form, label, index);
            AddGridControl(form, control, index + 1);
        }

        private void InitializeAiProfilePageValues()
        {
            string model = settings == null || settings.Ai == null ? AiSettings.DefaultModel : settings.Ai.Model;
            aiProfileNameInput.Text = AiSettings.BuildProfileAutoName(model, DateTime.Now);
            aiProfileAccessModeSelect.SelectedValue = settings == null || settings.Ai == null ? AiSettings.StandardApiAccessMode : settings.Ai.AccessMode;
            aiProfileEndpointInput.Text = settings == null || settings.Ai == null ? string.Empty : NormalizeValue(settings.Ai.Endpoint);
            aiProfileApiKeyInput.Text = settings == null || settings.Ai == null ? string.Empty : NormalizeValue(settings.Ai.ApiKey);
            aiProfileModelInput.Text = string.IsNullOrWhiteSpace(model) ? AiSettings.DefaultModel : NormalizeValue(model);
            aiProfileMaxSuggestionsInput.Text = settings == null || settings.Ai == null ? "30" : settings.Ai.MaxSuggestions.ToString();
            aiProfileCookieMappingsInput.Text = settings == null || settings.Ai == null ? string.Empty : FormatModelCookieMappings(settings.Ai.ModelCookieMappings, settings.Ai.Model);
            aiProfileSystemPromptInput.Text = settings == null || settings.Ai == null ? DefaultAiSystemPrompt : NormalizeValue(settings.Ai.SystemPrompt);
            UpdateAiProfileAccessModeUi();
            SelectAiProfileProviderPresetForValues(aiProfileEndpointInput.Text, aiProfileModelInput.Text);
            SelectAiProfilePromptPresetForPrompt(aiProfileSystemPromptInput.Text);
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
                ShowInfo("完成", "AI 配置方案已保存。");
            }
            catch (Exception ex)
            {
                Log("保存 AI 配置方案失败：" + ex.Message);
                ShowError("保存失败", ex.Message);
            }
        }

        private void SaveAiProfileFromPage()
        {
            try
            {
                AiProfile profile = CreateAiProfileFromPage();
                UpsertAiProfile(profile, true);
                settingsStore.Save(settings);
                PopulateAiProfiles();
                SelectAiProfile(0);
                SetActivePage(PageSettings);
                Log("AI 配置方案已新增：" + profile.Name + "。");
                ShowInfo("完成", "AI 配置方案已新增。");
            }
            catch (Exception ex)
            {
                Log("新增 AI 配置方案失败：" + ex.Message);
                ShowError("保存失败", ex.Message);
            }
        }

        private AiProfile CreateAiProfileFromPage()
        {
            DateTime savedAt = DateTime.Now;
            string name = aiProfileNameInput == null ? null : NormalizeValue(aiProfileNameInput.Text);
            string model = aiProfileModelInput == null ? null : NormalizeValue(aiProfileModelInput.Text);

            AiProfile profile = new AiProfile
            {
                Name = name,
                SavedAt = savedAt,
                AccessMode = ResolveSelectedAiProfileAccessMode(),
                Endpoint = aiProfileEndpointInput == null ? string.Empty : NormalizeValue(aiProfileEndpointInput.Text),
                ApiKey = aiProfileApiKeyInput == null ? string.Empty : NormalizeValue(aiProfileApiKeyInput.Text),
                Model = model,
                MaxSuggestions = ParsePositiveInt(aiProfileMaxSuggestionsInput == null ? null : aiProfileMaxSuggestionsInput.Text, 30),
                SystemPrompt = aiProfileSystemPromptInput == null ? string.Empty : NormalizeValue(aiProfileSystemPromptInput.Text),
                ModelCookieMappings = new List<AiModelCookieMapping>()
            };

            if (string.IsNullOrWhiteSpace(profile.Endpoint)) throw new InvalidOperationException("请填写接口地址。");
            if (string.IsNullOrWhiteSpace(profile.Model)) throw new InvalidOperationException("请填写模型。");

            IList<AiModelCookieMapping> mappings = ParseModelCookieMappings(aiProfileCookieMappingsInput == null ? null : aiProfileCookieMappingsInput.Text, profile.Model);
            for (int index = 0; index < mappings.Count; index++)
            {
                profile.ModelCookieMappings.Add(new AiModelCookieMapping
                {
                    Model = mappings[index].Model,
                    Cookie = mappings[index].Cookie
                });
            }

            if (string.IsNullOrWhiteSpace(profile.Name)) profile.Name = AiSettings.BuildProfileAutoName(profile.Model, profile.SavedAt);
            return profile;
        }

        private void CancelAiProfileCreatePage()
        {
            SetActivePage(PageSettings);
        }

        private void ApplySelectedAiProfile()
        {
            AiProfile profile = ResolveSelectedAiProfile();
            if (profile == null)
            {
                ShowInfo("提示", "暂无可应用的 AI 历史配置。");
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

        private void RefreshAiProfileCards()
        {
            if (aiProfileListPanel == null) return;

            aiProfileListPanel.SuspendLayout();
            try
            {
                aiProfileListPanel.Controls.Clear();
                if (settings == null || settings.Ai == null || settings.Ai.Profiles == null || settings.Ai.Profiles.Count == 0)
                {
                    aiProfileListPanel.Controls.Add(CreateEmptyAiProfileCard());
                    return;
                }

                int selectedIndex = GetSelectedAiProfileIndex();
                for (int index = 0; index < settings.Ai.Profiles.Count; index++)
                {
                    aiProfileListPanel.Controls.Add(CreateAiProfileCard(settings.Ai.Profiles[index], index, index == selectedIndex));
                }
            }
            finally
            {
                aiProfileListPanel.ResumeLayout();
                ResizeAiProfileCards();
            }
        }

        private Control CreateEmptyAiProfileCard()
        {
            AntdUI.Panel card = CreateAiProfileCardSurface(false);
            card.Height = 88;

            AntdUI.Label label = CreateSmallMutedLabel("还没有保存过 AI 配置。填写接入参数后点击“保存为配置”，这里会生成类似接口列表的配置卡片。");
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleCenter;
            card.Controls.Add(label);
            return card;
        }

        private Control CreateAiProfileCard(AiProfile profile, int index, bool selected)
        {
            AntdUI.Panel card = CreateAiProfileCardSurface(selected);

            AntdUI.GridPanel layout = CreateGridPanel("54 fill 122");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            AntdUI.Panel avatarCell = CreateFlatPanel();
            avatarCell.Dock = DockStyle.Fill;
            avatarCell.BackColor = Color.Transparent;
            AntdUI.Avatar avatar = new AntdUI.Avatar();
            avatar.Width = 40;
            avatar.Height = 40;
            avatar.Left = 3;
            avatar.Top = 20;
            avatar.Text = BuildAiProfileAvatarText(profile);
            avatar.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            avatar.ForeColor = TextSecondaryColor;
            avatar.BackColor = Color.FromArgb(248, 250, 252);
            avatar.BorderWidth = 1F;
            avatar.BorderColor = BorderLightColor;
            avatar.Radius = 18;
            avatarCell.Controls.Add(avatar);

            AntdUI.GridPanel content = CreateGridPanel("fill;fill;fill-30 24 fill");
            content.Dock = DockStyle.Fill;
            content.BackColor = Color.Transparent;

            AntdUI.GridPanel titleRow = CreateGridPanel("fill 182");
            titleRow.Dock = DockStyle.Fill;
            titleRow.BackColor = Color.Transparent;
            titleRow.Margin = Padding.Empty;
            titleRow.Padding = Padding.Empty;

            AntdUI.Label title = new AntdUI.Label();
            title.Dock = DockStyle.Fill;
            title.AutoEllipsis = true;
            title.Text = BuildAiProfileDisplayName(profile);
            title.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
            title.ForeColor = TextPrimaryColor;
            title.BackColor = Color.Transparent;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.Margin = new Padding(0, 3, 8, 0);

            AntdUI.FlowPanel tagRow = new AntdUI.FlowPanel();
            tagRow.Dock = DockStyle.Fill;
            tagRow.BackColor = Color.Transparent;
            tagRow.Align = AntdUI.TAlignFlow.LeftCenter;
            tagRow.Margin = Padding.Empty;
            tagRow.Padding = Padding.Empty;

            AddGridControl(titleRow, title, 0);
            tagRow.Controls.Add(CreateAiProfileTag(IsAiProfileConfigured(profile) ? "正常" : "待补全", IsAiProfileConfigured(profile) ? AntdUI.TTypeMini.Success : AntdUI.TTypeMini.Warn));
            tagRow.Controls.Add(CreateAiProfileTag(FormatAiAccessModeLabel(profile.AccessMode), AntdUI.TTypeMini.Info));
            tagRow.Controls.Add(CreateAiProfileTag("P" + (index + 1).ToString(), AntdUI.TTypeMini.Primary));
            AddGridControl(titleRow, tagRow, 1);

            AntdUI.Label endpoint = new AntdUI.Label();
            endpoint.Dock = DockStyle.Fill;
            endpoint.Text = NormalizeEndpoint(profile.Endpoint);
            endpoint.Font = new Font("Microsoft YaHei UI", 10F);
            endpoint.ForeColor = PrimaryColor;
            endpoint.BackColor = Color.Transparent;
            endpoint.TextAlign = ContentAlignment.MiddleLeft;
            endpoint.AutoEllipsis = true;

            AntdUI.Label meta = CreateSmallMutedLabel(BuildAiProfileMeta(profile));

            AddGridControl(content, titleRow, 0);
            AddGridControl(content, endpoint, 1);
            AddGridControl(content, meta, 2);

            AntdUI.GridPanel actions = CreateGridPanel("fill;fill;fill-fill 34 fill");
            actions.Dock = DockStyle.Fill;
            actions.BackColor = Color.Transparent;

            AntdUI.Button applyButton = CreateAiProfileCardActionButton(selected ? "已选中" : "应用", "CheckOutlined", selected);
            applyButton.Click += delegate
            {
                SelectAiProfile(index);
                ApplySelectedAiProfile();
            };
            AddGridControl(actions, CreateGridSpacer(), 0);
            AddGridControl(actions, applyButton, 1);
            AddGridControl(actions, CreateGridSpacer(), 2);

            AddGridControl(layout, avatarCell, 0);
            AddGridControl(layout, content, 1);
            AddGridControl(layout, actions, 2);
            card.Controls.Add(layout);

            BindAiProfileCardSelection(card, index);
            return card;
        }

        private AntdUI.Panel CreateAiProfileCardSurface(bool selected)
        {
            AntdUI.Panel card = new AntdUI.Panel();
            card.Width = ResolveAiProfileCardWidth();
            card.Height = 102;
            card.Margin = new Padding(0, 0, 0, 10);
            card.Padding = new Padding(14, 10, 14, 10);
            card.Radius = 14;
            card.Back = selected ? Color.FromArgb(247, 255, 252) : SurfaceColor;
            card.BorderWidth = 1F;
            card.BorderColor = selected ? Color.FromArgb(91, 213, 163) : BorderDefaultColor;
            card.Shadow = 0;
            card.ShadowOpacity = 0F;
            card.ShadowOffsetY = 0;
            return card;
        }

        private AntdUI.Button CreateAiProfileCardActionButton(string text, string iconSvg, bool selected)
        {
            AntdUI.Button button = new AntdUI.Button();
            button.Dock = DockStyle.Fill;
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.Text = text;
            button.IconSvg = iconSvg;
            button.Type = selected ? AntdUI.TTypeMini.Default : AntdUI.TTypeMini.Primary;
            button.Ghost = selected;
            button.Height = 32;
            button.Radius = 8;
            button.BorderWidth = 1F;
            button.WaveSize = 2;
            button.Margin = new Padding(8, 0, 0, 0);
            if (selected)
            {
                button.DefaultBorderColor = Color.FromArgb(186, 231, 204);
                button.ForeColor = Color.FromArgb(0, 120, 75);
                button.BackColor = Color.FromArgb(240, 253, 244);
            }
            return button;
        }

        private static AntdUI.Tag CreateAiProfileTag(string text, AntdUI.TTypeMini type)
        {
            AntdUI.Tag tag = new AntdUI.Tag();
            tag.AutoSizeMode = AntdUI.TAutoSize.Auto;
            tag.Text = text;
            tag.Type = type;
            tag.BorderWidth = 0F;
            tag.Radius = 8;
            tag.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            tag.Margin = new Padding(0, 3, 6, 0);
            return tag;
        }

        private void BindAiProfileCardSelection(Control control, int index)
        {
            if (!(control is AntdUI.Button))
            {
                control.Cursor = Cursors.Hand;
                control.Click += delegate { SelectAiProfile(index); };
            }

            foreach (Control child in control.Controls)
            {
                BindAiProfileCardSelection(child, index);
            }
        }

        private void SelectAiProfile(int index)
        {
            if (aiProfileSelect == null || settings == null || settings.Ai == null || settings.Ai.Profiles == null) return;
            if (index < 0 || index >= settings.Ai.Profiles.Count) return;
            string selectedValue = index.ToString();
            bool selectionChanged = aiProfileSelect.SelectedValue == null || !string.Equals(aiProfileSelect.SelectedValue.ToString(), selectedValue, StringComparison.Ordinal);
            if (selectionChanged)
            {
                aiProfileSelect.SelectedValue = selectedValue;
            }
            if (selectionChanged) RefreshAiProfileCards();
        }

        private int GetSelectedAiProfileIndex()
        {
            if (aiProfileSelect == null || aiProfileSelect.SelectedValue == null) return 0;
            int index;
            if (!int.TryParse(aiProfileSelect.SelectedValue.ToString(), out index)) return 0;
            return index;
        }

        private void ResizeAiProfileCards()
        {
            if (aiProfileListPanel == null) return;
            int width = ResolveAiProfileCardWidth();
            foreach (Control control in aiProfileListPanel.Controls)
            {
                control.Width = width;
            }
        }

        private int ResolveAiProfileCardWidth()
        {
            if (aiProfileListPanel == null) return 640;
            int scrollBarOffset = aiProfileListPanel.ScrollBar != null && aiProfileListPanel.ScrollBar.ShowY ? aiProfileListPanel.ScrollBar.SIZE : 0;
            return Math.Max(420, aiProfileListPanel.ClientSize.Width - scrollBarOffset - 8);
        }

        private static bool IsAiProfileConfigured(AiProfile profile)
        {
            return profile != null && !string.IsNullOrWhiteSpace(profile.Endpoint) && !string.IsNullOrWhiteSpace(profile.Model);
        }

        private static string FormatAiAccessModeLabel(string accessMode)
        {
            return string.Equals(AiSettings.NormalizeAccessMode(accessMode), AiSettings.TwoApiAccessMode, StringComparison.OrdinalIgnoreCase) ? "2API" : "标准 API";
        }

        private static string BuildAiProfileMeta(AiProfile profile)
        {
            if (profile == null) return string.Empty;
            string model = string.IsNullOrWhiteSpace(profile.Model) ? "未填写模型" : profile.Model.Trim();
            int maxSuggestions = profile.MaxSuggestions <= 0 ? 30 : profile.MaxSuggestions;
            return "模型：" + model + "    建议：" + maxSuggestions.ToString() + " 条    保存：" + profile.SavedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        private static string BuildAiProfileAvatarText(AiProfile profile)
        {
            string source = profile == null ? null : (string.IsNullOrWhiteSpace(profile.Name) ? profile.Model : profile.Name);
            source = NormalizeValue(source);
            if (string.IsNullOrWhiteSpace(source)) return "AI";

            string[] parts = Regex.Split(source, "[^A-Za-z0-9]+");
            string result = string.Empty;
            for (int i = 0; i < parts.Length && result.Length < 2; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) continue;
                result += char.ToUpperInvariant(parts[i][0]).ToString();
            }

            if (result.Length == 0)
            {
                result = source.Substring(0, Math.Min(2, source.Length)).ToUpperInvariant();
            }
            else if (result.Length == 1 && source.Length > 1)
            {
                result += char.ToUpperInvariant(source[1]).ToString();
            }

            return result.Length > 2 ? result.Substring(0, 2) : result;
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
            AntdUI.Panel content = CreateFlatPanel();
            content.Width = 420;
            content.Height = 72;
            content.Padding = new Padding(0, 4, 0, 0);
            content.BackColor = Color.Transparent;

            AntdUI.GridPanel layout = CreateGridPanel("78 fill");
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;

            AntdUI.Label label = CreateCaption("配置名称");
            AntdUI.Input input = CreateInput("例如：开发环境");
            input.Text = defaultName ?? string.Empty;
            AddGridControl(layout, label, 0);
            AddGridControl(layout, input, 1);
            content.Controls.Add(layout);

            AntdUI.Modal.Config config = AntdUI.Modal.config(this, "保存 AI 配置方案", content, AntdUI.TType.Info);
            config.OkText = "保存";
            config.CancelText = "取消";
            config.OkType = AntdUI.TTypeMini.Primary;
            config.Width = 480;
            config.MaskClosable = false;
            return AntdUI.Modal.open(config) == DialogResult.OK ? NormalizeValue(input.Text) : null;
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

            string defaultDrive = ResolveDefaultDrive();
            driveSelect.SelectedValue = defaultDrive;
            if (suggestionDriveSelect != null) suggestionDriveSelect.SelectedValue = defaultDrive;
            pathInput.Text = defaultDrive;
            UpdateDriveSummaryForLocation(defaultDrive);
            RefreshPromptForCurrentLocation();
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

        private void AiPromptPresetSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (loadingStartupUi) return;
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
            if (loadingStartupUi) return;
            if (syncingAiPromptPreset || systemPromptInput == null) return;
            SelectAiPromptPresetForPrompt(systemPromptInput.Text);
        }

        private void AiProfilePromptPresetSelect_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            if (loadingStartupUi) return;
            if (syncingAiProfilePromptPreset || e.Value == null) return;

            string key = e.Value.ToString();
            if (string.Equals(key, CustomAiPromptPresetKey, StringComparison.OrdinalIgnoreCase)) return;

            AiPromptPreset preset = FindAiPromptPreset(key);
            if (preset == null || aiProfileSystemPromptInput == null) return;

            syncingAiProfilePromptPreset = true;
            try
            {
                aiProfileSystemPromptInput.Text = preset.BuildPrompt(GetPromptDriveRoot());
            }
            finally
            {
                syncingAiProfilePromptPreset = false;
            }
        }

        private void AiProfileSystemPromptInput_TextChanged(object sender, EventArgs e)
        {
            if (loadingStartupUi) return;
            if (syncingAiProfilePromptPreset || aiProfileSystemPromptInput == null) return;
            SelectAiProfilePromptPresetForPrompt(aiProfileSystemPromptInput.Text);
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
                List<StorageEntryRow> rows = new List<StorageEntryRow> { new StorageEntryRow(result, true) };
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
            AnalyzeConfiguredPathSuggestions();
        }

        private void AnalyzeSuperSuggestions()
        {
            AnalyzeSuggestionsCore(false);
        }

        private void AnalyzeConfiguredPathSuggestions()
        {
            SaveSettingsFromUi();
            IList<CleanupSuggestion> suggestions = null;
            DateTime analyzeStartedAt = DateTime.UtcNow;
            string caption = "正在汇总常规清理路径…";
            Log("常规清理开始：仅使用内置和已配置允许位置进行汇总清理。");

            RunBackground(caption, delegate
            {
                int maxCount = settings != null && settings.Ai != null ? settings.Ai.MaxSuggestions : 30;
                suggestions = configuredPathCleanupPlanner.BuildSuggestions(settings, maxCount);
                LogBackground("常规路径汇总完成：count=" + (suggestions == null ? 0 : suggestions.Count) + "。");
                EvaluateSandbox(suggestions);
            }, delegate
            {
                BindSuggestions(suggestions);
                TimeSpan elapsed = DateTime.UtcNow - analyzeStartedAt;
                Log("常规清理生成完成，共 " + suggestionRows.Count + " 项，耗时 " + elapsed.TotalSeconds.ToString("0.00") + " 秒。");
            });
        }

        private void AnalyzeSuggestionsCore(bool preferAi)
        {
            string location = ResolveSuggestionLocation();
            if (NeedAutoScanBeforeAnalyze(location))
            {
                string actionName = preferAi ? "AI 识别" : "超级清理";
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
            string caption = preferAi ? "正在生成 AI 清理建议…" : "正在生成超级清理列表…";
            DateTime analyzeStartedAt = DateTime.UtcNow;
            Log((preferAi ? "AI 识别" : "超级清理") + "开始：" + DescribeScanRequest(request) + "，AIEnabled=" + settings.Ai.Enabled + "，AccessMode=" + settings.Ai.AccessMode + "，Endpoint=" + settings.Ai.Endpoint + "，Model=" + settings.Ai.Model + "，CookieMappings=" + (settings.Ai.ModelCookieMappings == null ? 0 : settings.Ai.ModelCookieMappings.Count) + "。");

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
                LogBackground((preferAi ? "AI/回退" : "超级") + "建议原始结果：count=" + (suggestions == null ? 0 : suggestions.Count) + "。");
                EvaluateSandbox(suggestions);
            }, delegate
            {
                BindSuggestions(suggestions);
                string sourceName;
                if (preferAi) sourceName = settings.Ai.Enabled ? "AI 建议" : "本地规则回退";
                else sourceName = "超级清理";
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

        private void StorageTable_CellClick(object sender, AntdUI.TableClickEventArgs eventArgs)
        {
            if (storageTable != null && storageTable.CanFocus) storageTable.Focus();
            StorageEntryRow row = eventArgs.Record as StorageEntryRow;
            if (row == null || row.Item == null) return;

            storageContextRow = row;
            storageTable.SetSelected(row);
            if (eventArgs.Button != MouseButtons.Right) return;

            ShowStorageContextMenu(row, eventArgs.X, eventArgs.Y);
        }

        private void OpenStorageRow(StorageEntryRow row)
        {
            if (row == null || row.Item == null || string.IsNullOrWhiteSpace(row.Item.Path)) return;
            explorerService.OpenPath(row.Item.Path, !row.Item.IsDirectory);
        }

        private void ShowStorageContextMenu(StorageEntryRow row, int x, int y)
        {
            if (row == null || row.Item == null) return;

            bool canOpen = !string.IsNullOrWhiteSpace(row.Item.Path);
            bool canDelete = CanOfferStorageDelete(row);
            AntdUI.IContextMenuStripItem[] items =
            {
                new AntdUI.ContextMenuStripItem("在文件资源管理器打开")
                {
                    ID = StorageContextOpenId,
                    IconSvg = "FolderOpenOutlined",
                    Enabled = canOpen
                },
                new AntdUI.ContextMenuStripItemDivider(),
                new AntdUI.ContextMenuStripItem("删除" + (row.Item.IsDirectory ? "文件夹" : "文件"))
                {
                    ID = StorageContextDeleteId,
                    IconSvg = "DeleteOutlined",
                    Fore = AntdUI.Style.Db.Error,
                    Enabled = canDelete
                }
            };

            Point menuPoint = storageTable == null ? Cursor.Position : storageTable.PointToScreen(new Point(x, y));
            AntdUI.ContextMenuStrip.Config config = new AntdUI.ContextMenuStrip.Config(storageTable ?? (Control)this, StorageContextMenu_Click, items);
            config.Location = menuPoint;
            config.Align = AntdUI.TAlign.BR;
            AntdUI.ContextMenuStrip.open(config);
        }

        private void StorageContextMenu_Click(AntdUI.IContextMenuStrip item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ID)) return;
            if (string.Equals(item.ID, StorageContextOpenId, StringComparison.OrdinalIgnoreCase))
            {
                OpenStorageRow(storageContextRow);
                return;
            }

            if (string.Equals(item.ID, StorageContextDeleteId, StringComparison.OrdinalIgnoreCase))
            {
                DeleteStorageRow(storageContextRow);
            }
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
                ShowError("删除失败", message);
            });
        }

        private bool ValidateStorageDeleteTarget(StorageEntryRow row)
        {
            if (row == null || row.Item == null || string.IsNullOrWhiteSpace(row.Item.Path))
            {
                ShowInfo("提示", "删除目标为空。");
                return false;
            }

            if (IsProtectedStorageDeleteTarget(row.Item.Path))
            {
                ShowWarning("提示", "为避免误删，不支持直接删除当前扫描根或磁盘根目录。请展开到具体子项后再删除。");
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

        protected override void OnHandleCreated(EventArgs e)
        {
            if (!startupRedrawCompleted)
            {
                SuspendControlRedraw(this);
                startupRedrawSuspended = true;
            }

            base.OnHandleCreated(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            QueueStartupReveal();
            QueueStartupUiBinding();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && backgroundWorker != null) backgroundWorker.Dispose();
            base.Dispose(disposing);
        }

        private static void SuspendControlRedraw(Control control)
        {
            if (control == null || !control.IsHandleCreated) return;
            SendMessage(control.Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
        }

        private static void ResumeControlRedraw(Control control)
        {
            ResumeControlRedraw(control, true, true);
        }

        private static void ResumeControlRedraw(Control control, bool invalidateChildren, bool updateImmediately)
        {
            if (control == null || !control.IsHandleCreated) return;
            SendMessage(control.Handle, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
            control.Invalidate(invalidateChildren);
            if (updateImmediately) control.Update();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, int flags);

        protected override void OnSizeChanged(EventArgs e)
        {
            FormWindowState previousState = lastWindowState;
            base.OnSizeChanged(e);
            if (!applyingNormalBounds && WindowState == FormWindowState.Normal && previousState != FormWindowState.Normal && IsHandleCreated)
            {
                if (previousState == FormWindowState.Minimized)
                {
                    QueueWindowRestoreCompletion();
                }
                else
                {
                    BeginInvoke((MethodInvoker)delegate { ApplyNormalWindowBounds(false); });
                }
            }
            lastWindowState = WindowState;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmEraseBackground)
            {
                PaintEraseBackground(m.WParam);
                m.Result = new IntPtr(1);
                return;
            }

            if (m.Msg == WmGetMinMaxInfo)
            {
                UpdateMaximizedBounds(m.LParam);
                m.Result = IntPtr.Zero;
                return;
            }

            bool restoreFromMinimized = IsRestoreFromMinimizedSizeMessage(m);

            base.WndProc(ref m);

            if (restoreFromMinimized && !restoreBoundsQueued)
            {
                ForceWindowRestoreRepaint();
                QueueWindowRestoreCompletion();
            }
        }

        private bool IsRestoreFromMinimizedSizeMessage(Message message)
        {
            if (message.Msg != WmSize || lastWindowState != FormWindowState.Minimized) return false;
            return message.WParam == SizeRestored || message.WParam == SizeMaximized;
        }

        private void QueueWindowRestoreCompletion()
        {
            if (restoreBoundsQueued) return;
            restoreBoundsQueued = true;
            BeginInvoke((MethodInvoker)delegate
            {
                try
                {
                    if (WindowState == FormWindowState.Normal) ApplyNormalWindowBounds(false);
                }
                finally
                {
                    restoreBoundsQueued = false;
                    ForceWindowRestoreRepaint();
                }
            });
        }

        private void ForceWindowRestoreRepaint()
        {
            if (!IsHandleCreated || IsDisposed || WindowState == FormWindowState.Minimized) return;
            PerformLayout();
            RefreshDWM();
            Invalidate(true);
            RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, RestoreRedrawFlags);
        }

        private void PaintEraseBackground(IntPtr hdc)
        {
            if (hdc == IntPtr.Zero) return;
            using (Graphics graphics = Graphics.FromHdc(hdc))
            using (SolidBrush brush = new SolidBrush(PageBackground))
            {
                graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        private void ResumeStartupRedraw()
        {
            if (startupRedrawCompleted) return;
            if (!startupRedrawSuspended)
            {
                startupRedrawCompleted = true;
                return;
            }

            startupRedrawSuspended = false;
            startupRedrawCompleted = true;
            ResumeControlRedraw(this, true, false);
        }

        private void QueueStartupReveal()
        {
            if (startupRedrawCompleted || startupRevealQueued || IsDisposed) return;
            startupRevealQueued = true;
            BeginInvoke((MethodInvoker)delegate
            {
                startupRevealQueued = false;
                CompleteStartupReveal();
            });
        }

        private void CompleteStartupReveal()
        {
            if (startupRedrawCompleted || IsDisposed) return;

            if (IsHandleCreated)
            {
                ApplyNormalWindowBounds(true);
                PerformLayout();
                ResumeStartupRedraw();
                RefreshDWM();
                Update();
            }
            else
            {
                startupRedrawCompleted = true;
            }
        }

        private void QueueStartupUiBinding()
        {
            if (startupUiBindingCompleted || startupUiBindingQueued || IsDisposed) return;
            startupUiBindingQueued = true;
            BeginInvoke((MethodInvoker)delegate
            {
                startupUiBindingQueued = false;
                CompleteStartupUiBinding();
            });
        }

        private void CompleteStartupUiBinding()
        {
            if (startupUiBindingCompleted || IsDisposed) return;
            startupUiBindingCompleted = true;

            loadingStartupUi = true;
            try
            {
                LoadSettingsToUi();
                LoadDrives();
            }
            finally
            {
                loadingStartupUi = false;
            }

            UpdateDriveSummaryForLocation(ResolveSelectedLocation());
            RefreshPromptForCurrentLocation();
            Log("应用已启动。若 AI 未启用，将自动回退到本地启发式规则。");
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
                        ShowError("操作失败", error.Message);
                        return;
                    }

                    if (onSuccess != null) onSuccess();
                });
            });
        }

        private void ShowInfo(string title, string message)
        {
            ShowNotice(title, message, AntdUI.TType.Info);
        }

        private void ShowWarning(string title, string message)
        {
            ShowNotice(title, message, AntdUI.TType.Warn);
        }

        private void ShowError(string title, string message)
        {
            ShowNotice(title, message, AntdUI.TType.Error);
        }

        private void ShowNotice(string title, string message, AntdUI.TType icon)
        {
            AntdUI.Modal.open(this, title, message ?? string.Empty, icon);
        }

        private void SetBusy(bool busy, string description)
        {
            this.busy = busy;
            UseWaitCursor = busy;
            if (appBar != null) appBar.Loading = busy;
            if (titleBar != null) titleBar.Loading = busy;
            if (navigationMenu != null) navigationMenu.Enabled = !busy;
            if (settingsNavButton != null) settingsNavButton.Enabled = !busy;
            if (sidebarResizeRail != null) sidebarResizeRail.Enabled = !busy;
            scanButton.Enabled = !busy;
            scanButton.Loading = busy && activePageId == PageScan;
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
            superCleanButton.Enabled = !busy;
            analyzeButton.Loading = busy && activePageId == PageSuggestions;
            regularCleanButton.Loading = busy && activePageId == PageSuggestions;
            superCleanButton.Loading = busy && activePageId == PageSuggestions;
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
            if (scanProgress == null) return;
            scanProgress.Value = value;
            scanProgress.State = state == AntdUI.TType.None && loading ? AntdUI.TType.Info : state;
            scanProgress.Loading = loading;
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

        private static AntdUI.Label CreateToolbarCaption(string text)
        {
            AntdUI.Label label = new AntdUI.Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Text = text;
            label.Font = new Font("Microsoft YaHei UI", 9.5F);
            label.ForeColor = TextSecondaryColor;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static AntdUI.Label CreateSummaryCaption(string text)
        {
            AntdUI.Label label = new AntdUI.Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Text = text;
            label.Font = new Font("Microsoft YaHei UI", 9F);
            label.ForeColor = TextSecondaryColor;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static AntdUI.Label CreateSummaryValueLabel(bool bold)
        {
            AntdUI.Label label = new AntdUI.Label();
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
            return CreateCardPanel(padding, false);
        }

        private static AntdUI.Panel CreateCardPanel(int padding, bool animateShadowOnHover)
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Padding = new Padding(padding);
            panel.Radius = 14;
            panel.Back = SurfaceColor;
            panel.BorderWidth = 1F;
            panel.BorderColor = BorderLightColor;
            panel.Shadow = 18;
            panel.ShadowOpacity = 0.08F;
            panel.ShadowOpacityHover = animateShadowOnHover ? 0.14F : 0.08F;
            panel.ShadowOpacityAnimation = animateShadowOnHover;
            panel.ShadowOffsetY = 5;
            return panel;
        }

        private static AntdUI.Panel CreateFlatPanel()
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Radius = 0;
            panel.Shadow = 0;
            panel.BorderWidth = 0F;
            panel.Back = Color.Transparent;
            return panel;
        }

        private static AntdUI.StackPanel CreateVerticalScrollPanel()
        {
            AntdUI.StackPanel panel = new SmoothScrollStackPanel();
            panel.Vertical = true;
            panel.Radius = 0;
            panel.Gap = 0;
            return panel;
        }

        private static AntdUI.GridPanel CreateGridPanel(string span)
        {
            AntdUI.GridPanel panel = new AntdUI.GridPanel();
            panel.Span = span;
            panel.Radius = 0;
            panel.BorderWidth = 0F;
            panel.Back = Color.Transparent;
            panel.BackColor = Color.Transparent;
            return panel;
        }

        private static void AddGridControl(AntdUI.GridPanel panel, Control control, int index)
        {
            panel.Controls.Add(control);
            panel.SetIndex(control, index);
        }

        private static Control CreateGridSpacer()
        {
            return CreateFlatPanel();
        }

        private static AntdUI.Label CreateSectionTitle(string text)
        {
            AntdUI.Label heading = new AntdUI.Label();
            heading.Dock = DockStyle.Top;
            heading.Height = 34;
            heading.Text = text;
            heading.Font = new Font("Microsoft YaHei UI", 13.5F, FontStyle.Bold);
            heading.ForeColor = TextPrimaryColor;
            heading.BackColor = Color.Transparent;
            return heading;
        }

        private static AntdUI.Label CreateSectionDescription(string text)
        {
            AntdUI.Label desc = new AntdUI.Label();
            desc.Dock = DockStyle.Top;
            desc.Height = 26;
            desc.Text = text;
            desc.ForeColor = TextSecondaryColor;
            desc.Font = new Font("Microsoft YaHei UI", 9.5F);
            desc.BackColor = Color.Transparent;
            return desc;
        }

        private static void ConfigureTableSurface(AntdUI.Table table)
        {
            table.Bordered = true;
            table.Radius = 10;
            table.BorderWidth = 1F;
            table.BorderCellWidth = 1F;
            table.BorderColor = BorderLightColor;
            table.ColumnBack = FillSecondary;
            table.ColumnFore = TextSecondaryColor;
            table.RowHoverBg = Color.FromArgb(245, 248, 255);
            table.RowSelectedBg = PrimarySoftColor;
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
            button.Radius = 9;
            button.BorderWidth = 1F;
            button.Ghost = true;
            button.IconSvg = GetHeaderButtonIconSvg(text);
            button.WaveSize = 2;
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
            button.Height = 40;
            button.Radius = 9;
            button.BorderWidth = 0F;
            button.IconSvg = text == "扫描" ? "SearchOutlined" : null;
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
            button.Radius = 8;
            button.BorderWidth = 1F;
            button.Ghost = type == AntdUI.TTypeMini.Default;
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
            button.Radius = 8;
            button.BorderWidth = 1F;
            button.Ghost = type == AntdUI.TTypeMini.Default;
            button.Margin = new Padding(0, 4, 8, 4);
            return button;
        }

        private static AntdUI.Button CreateAddAiProfileButton()
        {
            AntdUI.Button button = new AntdUI.Button();
            button.Dock = DockStyle.Fill;
            button.AutoSizeMode = AntdUI.TAutoSize.None;
            button.DisplayStyle = AntdUI.TButtonDisplayStyle.Image;
            button.Shape = AntdUI.TShape.Circle;
            button.IconSvg = "PlusOutlined";
            button.Type = AntdUI.TTypeMini.Warn;
            button.Width = 34;
            button.Height = 34;
            button.BorderWidth = 0F;
            button.Margin = new Padding(8, 4, 0, 4);
            button.WaveSize = 2;
            return button;
        }

        private static AntdUI.Switch CreateSettingsSwitch()
        {
            AntdUI.Switch control = new AntdUI.Switch();
            control.Width = 60;
            control.Height = 34;
            control.Anchor = AnchorStyles.Left;
            control.Margin = new Padding(0, 8, 16, 8);
            control.WaveSize = 2;
            return control;
        }

        private static string GetHeaderButtonIconSvg(string text)
        {
            switch (text)
            {
                case "保存配置":
                    return "SaveFilled";
                case "删除勾选":
                    return "DeleteFilled";
                case "AI 识别":
                    return "RobotFilled";
                case "常规清理":
                    return "SearchOutlined";
                case "超级清理":
                    return "RocketFilled";
                default:
                    return null;
            }
        }

        private static AntdUI.Input CreateInput(string placeholder)
        {
            AntdUI.Input input = new AntdUI.Input();
            input.Dock = DockStyle.Fill;
            input.PlaceholderText = placeholder;
            input.Font = new Font("Microsoft YaHei UI", 10.5F);
            input.Radius = 8;
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
            select.DropDownRadius = 8;
            select.Radius = 8;
            select.BorderWidth = 1F;
            select.BorderColor = BorderDefaultColor;
            select.BorderHover = PrimaryColor;
            select.BorderActive = PrimaryColor;
            select.BackColor = SurfaceColor;
            select.Font = Font;
            return select;
        }

        private AntdUI.Select CreateSettingsSelect()
        {
            AntdUI.Select select = CreateSelect();
            select.WheelModifyEnabled = false;
            return select;
        }

        private static AntdUI.Label CreateCaption(string text)
        {
            AntdUI.Label label = new AntdUI.Label();
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

        private static Control CreateInfoCard(string title, out AntdUI.Label valueLabel)
        {
            AntdUI.Panel panel = new AntdUI.Panel();
            panel.Dock = DockStyle.Fill;
            panel.Radius = 8;
            panel.Padding = new Padding(12, 8, 12, 8);
            panel.Back = FillSecondary;
            panel.BorderWidth = 1F;
            panel.BorderColor = BorderLightColor;

            AntdUI.Label titleLabel = new AntdUI.Label();
            titleLabel.Dock = DockStyle.Top;
            titleLabel.Height = 18;
            titleLabel.Text = title;
            titleLabel.Font = new Font("Microsoft YaHei UI", 9F);
            titleLabel.ForeColor = TextTertiaryColor;
            titleLabel.BackColor = Color.Transparent;

            valueLabel = new AntdUI.Label();
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

