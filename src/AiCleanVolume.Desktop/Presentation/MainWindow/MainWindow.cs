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
using AiCleanVolume.Desktop.Composition;
using AiCleanVolume.Desktop.Controls;
using AiCleanVolume.Desktop.Infrastructure.Ai;
using AiCleanVolume.Desktop.Infrastructure.Scanning;
using AiCleanVolume.Desktop.Infrastructure.Settings;
using AiCleanVolume.Desktop.Infrastructure.Windows;
using AiCleanVolume.Desktop.Presentation.Features.Logs;
using AiCleanVolume.Desktop.Presentation.Features.Scan;
using AiCleanVolume.Desktop.Presentation.Features.Settings;
using AiCleanVolume.Desktop.Presentation.Features.Suggestions;
using AiCleanVolume.Desktop.Presentation.Shared;
using AiCleanVolume.Desktop.ViewModels;


namespace AiCleanVolume.Desktop
{
    public sealed partial class MainWindow : AntdUI.Window, IMainWindowShell
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

        private const int DeletionProgressTimerIntervalMs = 16;

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

        private static readonly Size DefaultClientArea = new Size(1560, 940);

        private static readonly Size BaseMinimumWindowSize = new Size(1280, 760);

        private readonly ISettingsStore settingsStore;

        private readonly IScanProvider scanProvider;

        private readonly ReusableBackgroundWorker backgroundWorker;

        private readonly BackgroundOperationRunner backgroundOperationRunner;

        private readonly CandidatePlanner candidatePlanner;

        private readonly ConfiguredPathCleanupPlanner configuredPathCleanupPlanner;

        private readonly IAiCleanupAdvisor localAdvisor;

        private readonly OpenAiCompatibleAdvisor aiAdvisor;

        private readonly CleanupDeletionWorkflow deletionWorkflow;

        private readonly IExplorerService explorerService;

        private ApplicationSettings settings;

        private readonly ScanPageState scanState;

        private readonly SettingsPageState settingsState;

        private SettingsPageView settingsPageView;

        private SettingsPageController settingsPageController;

        private readonly SuggestionsPageState suggestionsState;

        private AiProfilePageView aiProfilePageView;

        private AiProfilePageController aiProfilePageController;

        private SuggestionsPageView suggestionsPageView;

        private SuggestionsPageController suggestionsPageController;

        private LogPageFeature logPageFeature;

        private AntdUI.PageHeader appBar;

        private AntdUI.Panel pageHost;

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

        private AntdUI.Button suggestionPromptButton;

        private AntdUI.Button selectAllSuggestionsButton;

        private AntdUI.Button clearAllSuggestionsButton;

        private AntdUI.Button invertSuggestionsButton;

        private string activePageId;

        private AntdUI.Select driveSelect;

        private AntdUI.Select suggestionDriveSelect;

        private AntdUI.Input pathInput;

        private AntdUI.Input minSizeInput;

        private AntdUI.Input limitInput;

        private AntdUI.Input suggestionMinSizeInput;

        private AntdUI.Input suggestionLimitInput;

        private AntdUI.Table storageTable;

        private AntdUI.Table suggestionTable;

        private AntdUI.Column storageSizeColumn;

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

        private AntdUI.Input aiProfileCookieMappingsInput;

        private AntdUI.Label aiProfilePageTitle;

        private AntdUI.Label aiProfilePageDesc;

        private AntdUI.Select aiProviderPresetSelect;

        private AntdUI.Input modelCookieMappingsInput;

        private AntdUI.Input allowRootsInput;

        private AntdUI.Input logInput;

        private AntdUI.Label scanStatusLabel;

        private AntdUI.Label scanElapsedLabel;

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

        private DeletionProgressState activeDeletionProgress;

        private Timer deletionProgressTimer;

        private long lastDeletionProgressVersion;

        private string lastDeletionProgressText;

        private const string StorageContextOpenId = "open";

        private const string StorageContextAskAiId = "ask_ai";

        private const string StorageContextDeleteId = "delete";

        public MainWindow()
            : this(DesktopCompositionRoot.CreateDependencies())
        {
        }

        public MainWindow(MainWindowDependencies dependencies)
        {
            if (dependencies == null) throw new ArgumentNullException("dependencies");

            settingsStore = RequireDependency(dependencies.SettingsStore, "SettingsStore");
            settings = settingsStore.Load();
            scanState = new ScanPageState();
            settingsState = new SettingsPageState();
            suggestionsState = new SuggestionsPageState();
            candidatePlanner = RequireDependency(dependencies.CandidatePlanner, "CandidatePlanner");
            configuredPathCleanupPlanner = RequireDependency(dependencies.ConfiguredPathCleanupPlanner, "ConfiguredPathCleanupPlanner");
            scanProvider = RequireDependency(dependencies.ScanProvider, "ScanProvider");
            backgroundWorker = RequireDependency(dependencies.BackgroundWorker, "BackgroundWorker");
            backgroundOperationRunner = new BackgroundOperationRunner(backgroundWorker, this, this, GetActivePageDescription);
            localAdvisor = RequireDependency(dependencies.LocalAdvisor, "LocalAdvisor");
            aiAdvisor = RequireDependency(dependencies.CreateAiAdvisor(LogBackground), "AiAdvisor");
            deletionWorkflow = RequireDependency(dependencies.DeletionWorkflow, "DeletionWorkflow");
            explorerService = RequireDependency(dependencies.ExplorerService, "ExplorerService");
            lastWindowState = FormWindowState.Normal;

            InitializeComponent();
            ConfigureTables();
        }

        private static T RequireDependency<T>(T dependency, string name) where T : class
        {
            if (dependency == null) throw new ArgumentNullException(name);
            return dependency;
        }

        private StorageItem currentRoot
        {
            get { return scanState.CurrentRoot; }
            set { scanState.CurrentRoot = value; }
        }

        private ScanRequest currentTreeRequest
        {
            get { return scanState.CurrentTreeRequest; }
            set { scanState.CurrentTreeRequest = value; }
        }

        private int currentTreeVersion
        {
            get { return scanState.CurrentTreeVersion; }
            set { scanState.CurrentTreeVersion = value; }
        }

        private StorageEntryRow storageContextRow
        {
            get { return scanState.StorageContextRow; }
            set { scanState.StorageContextRow = value; }
        }

        private bool storageTreeDeleteDirty
        {
            get { return scanState.StorageTreeDeleteDirty; }
            set { scanState.StorageTreeDeleteDirty = value; }
        }

        private HashSet<string> expandedStoragePaths
        {
            get { return scanState.ExpandedStoragePaths; }
        }

        private Timer scanProgressTimer
        {
            get { return scanState.ScanProgressTimer; }
            set { scanState.ScanProgressTimer = value; }
        }

        private DateTime scanProgressStartedAt
        {
            get { return scanState.ScanProgressStartedAt; }
            set { scanState.ScanProgressStartedAt = value; }
        }

        private string scanProgressActiveText
        {
            get { return scanState.ScanProgressActiveText; }
            set { scanState.ScanProgressActiveText = value; }
        }

        private List<CleanupSuggestionRow> suggestionRows
        {
            get { return suggestionsState.Rows; }
            set { suggestionsState.Rows = value; }
        }

        private bool syncingAiProviderPreset
        {
            get { return settingsState.SyncingAiProviderPreset; }
            set { settingsState.SyncingAiProviderPreset = value; }
        }

        private bool syncingAiProfileProviderPreset
        {
            get { return settingsState.SyncingAiProfileProviderPreset; }
            set { settingsState.SyncingAiProfileProviderPreset = value; }
        }

        private bool syncingPrivilegeCheckboxes
        {
            get { return settingsState.SyncingPrivilegeCheckboxes; }
            set { settingsState.SyncingPrivilegeCheckboxes = value; }
        }

        private int editingAiProfileIndex
        {
            get { return settingsState.EditingAiProfileIndex; }
            set { settingsState.EditingAiProfileIndex = value; }
        }

        private int selectedAiProfileIndex
        {
            get { return settingsState.SelectedAiProfileIndex; }
            set { settingsState.SelectedAiProfileIndex = value; }
        }

        private string pendingSystemPrompt
        {
            get { return settingsState.PendingSystemPrompt; }
            set { settingsState.PendingSystemPrompt = value; }
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

            BuildAppBar();

            pageHost = CreateFlatPanel();
            pageHost.Dock = DockStyle.Fill;
            pageHost.BackColor = PageBackground;
            pageHost.Padding = Padding.Empty;

            EnsureScanToolbarControls();

            suggestionsPageView = new SuggestionsPageView(Font);
            suggestionsPage = suggestionsPageView.Panel;
            BindSuggestionsPageView(suggestionsPageView);
            suggestionsPageController = new SuggestionsPageController(this, suggestionsPageView, AnalyzeRegularSuggestions, AnalyzeSuperSuggestions, AnalyzeSuggestions, ShowSuggestionPromptEditor, DeleteSelectedSuggestions, delegate { SetSuggestionSelection(true); }, delegate { SetSuggestionSelection(false); }, InvertSuggestionSelection, SuggestionDriveSelect_SelectedValueChanged, PrivilegedCheckbox_CheckedChanged, SuggestionTable_CellDoubleClick, SuggestionTable_CellButtonClick);

            logPageFeature = new LogPageFeature();
            logInput = logPageFeature.LogInput;
            logPage = CreatePageContainer();
            logPage.Controls.Add(logPageFeature.View);

            settingsPageView = new SettingsPageView(Font);
            settingsPage = settingsPageView.ViewPanel;
            BindSettingsPageView(settingsPageView);
            settingsPageController = new SettingsPageController(this, settingsPageView, SaveSettings, TestAiSettings, ApplySelectedAiProfile, OpenAiProfileCreatePage, AiAccessModeSelect_SelectedValueChanged, AiProviderPresetSelect_SelectedValueChanged, AiEndpointOrModelInput_TextChanged, PrivilegedCheckbox_CheckedChanged, ResizeAiProfileCards);

            aiProfilePageView = new AiProfilePageView(Font);
            aiProfileCreatePage = aiProfilePageView.ViewPanel;
            BindAiProfilePageView(aiProfilePageView);
            aiProfilePageController = new AiProfilePageController(this, aiProfilePageView, CancelAiProfileCreatePage, CancelAiProfileCreatePage, SaveAiProfileFromPage, AiProfileAccessModeSelect_SelectedValueChanged, AiProfileProviderPresetSelect_SelectedValueChanged, AiProfileEndpointOrModelInput_TextChanged);

            BuildColumnsHost();
            BuildStorageTreeColumn();
            BuildAiChatColumn();
            suggestionsPage.Dock = DockStyle.Fill;
            rightColumnPanel.Controls.Add(suggestionsPage);

            logPage.Visible = false;
            settingsPage.Visible = false;
            aiProfileCreatePage.Visible = false;

            pageHost.Controls.Add(aiProfileCreatePage);
            pageHost.Controls.Add(settingsPage);
            pageHost.Controls.Add(logPage);
            pageHost.Controls.Add(columnsHost);

            Controls.Add(pageHost);
            Controls.Add(BuildStatusBar());
            Controls.Add(BuildColumnsSeparatorlessToolbarStack());
            Controls.Add(appBar);
            SetActivePage(PageScan);
        }

        // 工具栏与提升横幅合成一个 Dock=Top 容器：工具栏贴顶、横幅贴底，隐藏横幅时收缩高度
        private Control BuildColumnsSeparatorlessToolbarStack()
        {
            AntdUI.Panel stack = CreateFlatPanel();
            stack.Dock = DockStyle.Top;
            AntdUI.Panel banner = BuildElevationBanner();
            banner.Dock = DockStyle.Bottom;
            AntdUI.Panel toolbar = BuildToolbar();
            stack.Controls.Add(toolbar);
            stack.Controls.Add(banner);
            stack.Height = 60 + (banner.Visible ? 34 : 0);
            banner.VisibleChanged += delegate { stack.Height = 60 + (banner.Visible ? 34 : 0); };
            return stack;
        }

        private void BindSuggestionsPageView(SuggestionsPageView view)
        {
            suggestionDriveSelect = view.DriveSelect;
            suggestionMinSizeInput = view.MinSizeInput;
            suggestionLimitInput = view.LimitInput;
            privilegedQuickCheckbox = view.PrivilegedQuickCheckbox;
            suggestionPromptButton = view.PromptButton;
            selectAllSuggestionsButton = view.SelectAllButton;
            clearAllSuggestionsButton = view.ClearAllButton;
            invertSuggestionsButton = view.InvertButton;
            regularCleanButton = view.RegularCleanButton;
            superCleanButton = view.SuperCleanButton;
            analyzeButton = view.AnalyzeButton;
            deleteButton = view.DeleteButton;
            suggestionTable = view.SuggestionTable;

        }

        private void BindSettingsPageView(SettingsPageView view)
        {
            aiAccessModeSelect = view.AiAccessModeSelect;
            endpointInput = view.EndpointInput;
            apiKeyInput = view.ApiKeyInput;
            modelInput = view.ModelInput;
            maxSuggestionsInput = view.MaxSuggestionsInput;
            aiProviderPresetSelect = view.AiProviderPresetSelect;
            modelCookieMappingsInput = view.ModelCookieMappingsInput;
            allowRootsInput = view.AllowRootsInput;
            settingsScrollHost = view.SettingsScrollHost;
            settingsContentLayout = view.SettingsContentLayout;
            aiProfileListPanel = view.AiProfileListPanel;
            saveSettingsButton = view.SaveSettingsButton;
            testAiSettingsButton = view.TestAiSettingsButton;
            applyAiProfileButton = view.ApplyAiProfileButton;
            addAiProfileButton = view.AddAiProfileButton;
            aiEnabledSwitch = view.AiEnabledSwitch;
            recycleSwitch = view.RecycleSwitch;
            privilegedCheckbox = view.PrivilegedCheckbox;

            PopulateAiAccessModes();
            PopulateAiProviderPresets();
        }

        private void BindAiProfilePageView(AiProfilePageView view)
        {
            backAiProfilePageButton = view.BackButton;
            saveAiProfilePageButton = view.SaveButton;
            cancelAiProfilePageButton = view.CancelButton;
            aiProfilePageTitle = view.TitleLabel;
            aiProfilePageDesc = view.DescLabel;
            aiProfileNameInput = view.NameInput;
            aiProfileAccessModeSelect = view.AccessModeSelect;
            aiProfileMaxSuggestionsInput = view.MaxSuggestionsInput;
            aiProfileProviderPresetSelect = view.ProviderPresetSelect;
            aiProfileEndpointInput = view.EndpointInput;
            aiProfileApiKeyInput = view.ApiKeyInput;
            aiProfileModelInput = view.ModelInput;
            aiProfileCookieMappingsInput = view.CookieMappingsInput;

        }

        private void ConfigureTables()
        {
            storageSizeColumn = new AntdUI.Column("size", "占用", AntdUI.ColumnAlign.Right).SetWidth("92");
            storageTable.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.ColumnCheck("selected").SetWidth("36"),
                new AntdUI.Column("name", "名称").SetTree("Children"),
                new AntdUI.Column("tag", "").SetWidth("92"),
                storageSizeColumn,
                new AntdUI.Column("ratio", "").SetWidth("84")
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
