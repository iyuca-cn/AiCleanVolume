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
        // 颜色全部通过 AntdUI.Style.Db 主题令牌动态读取，跟随 Light/Dark 主题切换。
        private static Color PageBackground { get { return AntdUI.Style.Db.BgLayout; } }

        private static Color SurfaceColor { get { return AntdUI.Style.Db.BgContainer; } }

        private static Color FillSecondary { get { return AntdUI.Style.Db.FillQuaternary; } }

        private static Color BorderDefaultColor { get { return AntdUI.Style.Db.BorderColor; } }

        private static Color BorderLightColor { get { return AntdUI.Style.Db.BorderSecondary; } }

        private static Color PrimaryColor { get { return AntdUI.Style.Db.Primary; } }

        private static Color PrimarySoftColor { get { return AntdUI.Style.Db.PrimaryBg; } }

        private static Color TextPrimaryColor { get { return AntdUI.Style.Db.Text; } }

        private static Color TextSecondaryColor { get { return AntdUI.Style.Db.TextSecondary; } }

        private static Color TextTertiaryColor { get { return AntdUI.Style.Db.TextTertiary; } }

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

        private const int SidebarCollapsedWidth = 64;

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

        private AntdUI.Menu navigationMenu;

        private AntdUI.Panel pageHost;

        private AntdUI.Panel sidebarHost;

        private AntdUI.Panel sidebarPanel;

        private AntdUI.Panel sidebarBrandPanel;

        private AntdUI.Label sidebarBrandIconLabel;

        private AntdUI.Label sidebarBrandTextLabel;

        private AntdUI.Panel sidebarResizeRail;

        private AntdUI.Button sidebarCollapseButton;

        private AntdUI.Button settingsNavButton;

        private AntdUI.Panel scanPage;

        private AntdUI.Panel suggestionsPage;

        private AntdUI.Panel logPage;

        private AntdUI.Panel settingsPage;

        private AntdUI.Panel aiProfileCreatePage;

        private AntdUI.StackPanel settingsScrollHost;

        private AntdUI.GridPanel settingsContentLayout;

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

        private AntdUI.Column storageSizeColumn;

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

        private AntdUI.Label aiProfilePageTitle;

        private AntdUI.Label aiProfilePageDesc;

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

        private FormWindowState lastWindowState;

        private bool applyingNormalBounds;

        private bool startupRedrawSuspended;

        private bool startupRedrawCompleted;

        private bool startupRevealQueued;

        private bool startupPostShowRefreshCompleted;

        private bool initialUiBound;

        private bool loadingStartupUi;

        private bool restoreBoundsQueued;

        private bool busy;

        private bool sidebarResizing;

        private bool sidebarCollapsed;

        private bool syncingAiPromptPreset;

        private bool syncingAiProviderPreset;

        private bool syncingAiProfilePromptPreset;

        private bool syncingAiProfileProviderPreset;

        private bool syncingPrivilegeCheckboxes;

        private bool storageTreeDeleteDirty;

        private int editingAiProfileIndex = -1;

        private int selectedAiProfileIndex = -1;

        private int sidebarWidth;

        private int expandedSidebarWidth;

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
            expandedSidebarWidth = 0;

            InitializeComponent();
            ConfigureTables();
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
            appBar.SubText = string.Empty;
            appBar.Description = string.Empty;
            appBar.Height = 40;

            saveSettingsButton = CreateToolbarActionButton("保存配置", AntdUI.TTypeMini.Primary);
            saveSettingsButton.Click += delegate { SaveSettings(); };

            deleteButton = CreateToolbarActionButton("删除勾选", AntdUI.TTypeMini.Error);
            deleteButton.Click += delegate { DeleteSelectedSuggestions(); };

            analyzeButton = CreateToolbarActionButton("AI 识别", AntdUI.TTypeMini.Success);
            analyzeButton.Click += delegate { AnalyzeSuggestions(); };

            regularCleanButton = CreateToolbarActionButton("常规清理", AntdUI.TTypeMini.Primary);
            regularCleanButton.Click += delegate { AnalyzeRegularSuggestions(); };

            superCleanButton = CreateToolbarActionButton("超级清理", AntdUI.TTypeMini.Warn);
            superCleanButton.Click += delegate { AnalyzeSuperSuggestions(); };

            scanButton = CreateToolbarActionButton("扫描", AntdUI.TTypeMini.Primary);
            scanButton.Click += delegate { ScanCurrentLocation(); };

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
            pageHost.Padding = new Padding(8, 6, 8, 8);

            scanPage = CreatePageContainer();
            scanPage.Controls.Add(CreateStoragePanel());
            scanPage.Controls.Add(CreateScanToolbarPanel());
            scanPage.Controls.Add(CreatePageHeader(GetPageTitle(PageScan), GetPageDescription(PageScan)));

            suggestionsPage = CreatePageContainer();
            suggestionsPage.Controls.Add(CreateSuggestionPanel());
            suggestionsPage.Controls.Add(CreatePageHeader(GetPageTitle(PageSuggestions), GetPageDescription(PageSuggestions)));

            logPage = CreatePageContainer();
            logPage.Controls.Add(CreateLogPanel());
            logPage.Controls.Add(CreatePageHeader(GetPageTitle(PageLog), GetPageDescription(PageLog)));

            settingsPage = CreatePageContainer();
            settingsPage.Controls.Add(CreateSettingsPanel());
            settingsPage.Controls.Add(CreatePageHeader(GetPageTitle(PageSettings), GetPageDescription(PageSettings)));

            aiProfileCreatePage = CreatePageContainer();
            aiProfileCreatePage.Controls.Add(CreateAiProfileCreatePage());

            pageHost.Controls.Add(aiProfileCreatePage);
            pageHost.Controls.Add(settingsPage);
            pageHost.Controls.Add(logPage);
            pageHost.Controls.Add(suggestionsPage);
            pageHost.Controls.Add(scanPage);

            contentHost.Controls.Add(pageHost);

            sidebarHost = CreateSidebarHost();

            shell.Controls.Add(contentHost);
            shell.Controls.Add(sidebarHost);

            Controls.Add(shell);
            Controls.Add(appBar);
            SetActivePage(PageScan);
            ApplySidebarWidth(ResolveInitialSidebarWidth());
        }

        private void ConfigureTables()
        {
            storageSizeColumn = new AntdUI.Column("size", "占用大小", AntdUI.ColumnAlign.Right).SetWidth("126");
            storageTable.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("name", "名称").SetTree("Children").SetWidth("auto"),
                storageSizeColumn,
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

        private void BindInitialUiBeforeFirstFrame()
        {
            if (initialUiBound) return;
            initialUiBound = true;
            loadingStartupUi = true;
            SuspendLayout();
            if (pageHost != null) pageHost.SuspendLayout();
            try
            {
                LoadSettingsToUi();
                LoadDrives();
                UpdateDriveSummaryForLocation(ResolveSelectedLocation());
                RefreshPromptForCurrentLocation();
                RefreshSettingsPageLayout(true);
            }
            finally
            {
                if (pageHost != null) pageHost.ResumeLayout(true);
                ResumeLayout(true);
                loadingStartupUi = false;
            }
        }

        private static string ResolveDefaultDrive()
        {
            string defaultDrive = Environment.GetEnvironmentVariable("SystemDrive");
            if (string.IsNullOrWhiteSpace(defaultDrive)) defaultDrive = "C:";
            return defaultDrive.TrimEnd('\\') + "\\";
        }
    }
}
