# Project Architecture Reorganization Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize the project into explicit Core microkernel, Desktop infrastructure, composition, and Presentation feature boundaries while preserving current behavior.

**Architecture:** Core owns domain models, ports, and application workflows. Desktop owns composition, platform adapters, AntdUI views, feature controllers, and shared presentation utilities. `MainWindow` becomes a shell instead of the owner of all service construction and feature workflows.

**Tech Stack:** C# 7.3, .NET Framework 4.0/4.8, WinForms, AntdUI, Newtonsoft.Json, RestSharp 106.15.0, folder-size-ranker-cli.

---

## File Structure

- Create: `src/AiCleanVolume.Desktop/Composition/DesktopCompositionRoot.cs`
  - Creates configured Desktop services and returns the main window.
- Create: `src/AiCleanVolume.Desktop/Composition/MainWindowDependencies.cs`
  - Carries concrete dependencies into `MainWindow` without requiring a DI container.
- Modify: `src/AiCleanVolume.Desktop/Program.cs`
  - Starts the app through `DesktopCompositionRoot`.
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
  - Accepts dependencies instead of directly constructing all services.
- Move Core files into:
  - `src/AiCleanVolume.Core/Domain/Storage`
  - `src/AiCleanVolume.Core/Domain/Cleanup`
  - `src/AiCleanVolume.Core/Domain/Sandbox`
  - `src/AiCleanVolume.Core/Domain/Settings`
  - `src/AiCleanVolume.Core/Kernel/Ports`
  - `src/AiCleanVolume.Core/Application/Scanning`
  - `src/AiCleanVolume.Core/Application/CleanupPlanning`
  - `src/AiCleanVolume.Core/Application/Deletion`
- Move Desktop adapter files into:
  - `src/AiCleanVolume.Desktop/Infrastructure/Scanning`
  - `src/AiCleanVolume.Desktop/Infrastructure/Ai`
  - `src/AiCleanVolume.Desktop/Infrastructure/Settings`
  - `src/AiCleanVolume.Desktop/Infrastructure/Windows`
- Create Presentation shared files:
  - `src/AiCleanVolume.Desktop/Presentation/Shared/IMainWindowShell.cs`
  - `src/AiCleanVolume.Desktop/Presentation/Shared/IFeaturePage.cs`
  - `src/AiCleanVolume.Desktop/Presentation/Shared/BackgroundOperationRunner.cs`
  - `src/AiCleanVolume.Desktop/Presentation/Shared/Antd/AntdControlFactory.cs`
- Later feature extraction targets:
  - `src/AiCleanVolume.Desktop/Presentation/Features/Scan`
  - `src/AiCleanVolume.Desktop/Presentation/Features/Suggestions`
  - `src/AiCleanVolume.Desktop/Presentation/Features/Settings`
  - `src/AiCleanVolume.Desktop/Presentation/Features/Logs`
  - `src/AiCleanVolume.Desktop/Presentation/MainWindow`

Repository rule: do not commit during this plan. Stop after verification unless the user explicitly asks for a commit.

## Chunk 1: Baseline And Composition Root

### Task 1: Capture Current Build Baseline

**Files:**
- Inspect only.

- [ ] **Step 1: Check working tree**

Run:

```pwsh
git status --short --branch
```

Expected: record pre-existing changes and do not revert unrelated work.

- [ ] **Step 2: Build Debug baseline**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds, or existing warnings/errors are recorded before refactoring.

### Task 2: Add Desktop Composition Root

**Files:**
- Create: `src/AiCleanVolume.Desktop/Composition/MainWindowDependencies.cs`
- Create: `src/AiCleanVolume.Desktop/Composition/DesktopCompositionRoot.cs`
- Modify: `src/AiCleanVolume.Desktop/Program.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: Create dependency holder**

Add `MainWindowDependencies` with properties for the services currently created in `MainWindow`:

```csharp
public sealed class MainWindowDependencies
{
    public SettingsStore SettingsStore { get; set; }
    public IScanProvider ScanProvider { get; set; }
    public ReusableBackgroundWorker BackgroundWorker { get; set; }
    public CandidatePlanner CandidatePlanner { get; set; }
    public ConfiguredPathCleanupPlanner ConfiguredPathCleanupPlanner { get; set; }
    public IAiCleanupAdvisor LocalAdvisor { get; set; }
    public OpenAiCompatibleAdvisor AiAdvisor { get; set; }
    public IDeletionSandbox DeletionSandbox { get; set; }
    public IDeletionService DeletionService { get; set; }
    public IExplorerService ExplorerService { get; set; }
    public IPrivilegeService PrivilegeService { get; set; }
}
```

- [ ] **Step 2: Create composition root**

Create `DesktopCompositionRoot.CreateMainWindow()` and move the current `new SettingsStore()`, `new FolderSizeRankerScanProvider()`, `new HeuristicCleanupAdvisor()`, and related service construction there.

- [ ] **Step 3: Add dependency constructor**

Change `MainWindow` to receive `MainWindowDependencies dependencies`, assign fields from it, then load settings. Keep a parameterless constructor only if it delegates to the composition root without duplicating construction logic.

- [ ] **Step 4: Update app entry**

Change `Program.cs`:

```csharp
Application.Run(DesktopCompositionRoot.CreateMainWindow());
```

- [ ] **Step 5: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds and UI behavior is unchanged.

## Chunk 2: Core Microkernel Layout

### Task 3: Split Core Domain Models

**Files:**
- Move from: `src/AiCleanVolume.Core/Models/CoreModels.cs`
- Move/create under: `src/AiCleanVolume.Core/Domain/Storage`
- Move/create under: `src/AiCleanVolume.Core/Domain/Cleanup`
- Move/create under: `src/AiCleanVolume.Core/Domain/Sandbox`
- Move from: `src/AiCleanVolume.Core/Models/ApplicationSettings.cs`
- Move from: `src/AiCleanVolume.Core/Models/AiSettings.cs`
- Move from: `src/AiCleanVolume.Core/Models/AiProfile.cs`
- Move from: `src/AiCleanVolume.Core/Models/AiModelCookieMapping.cs`
- Move from: `src/AiCleanVolume.Core/Models/SandboxSettings.cs`
- Move from: `src/AiCleanVolume.Core/Models/ScanSettings.cs`
- Move from: `src/AiCleanVolume.Core/Models/UiSettings.cs`
- Move/create under: `src/AiCleanVolume.Core/Domain/Settings`

- [ ] **Step 1: Move storage models**

Move `ScanRequest`, `StorageItem`, and `ScanSortMode` to `AiCleanVolume.Core.Domain.Storage`.

- [ ] **Step 2: Move cleanup models**

Move `CleanupCandidate`, `CleanupSuggestion`, `CleanupRisk`, `CleanupStatus`, and `CleanupResult` to `AiCleanVolume.Core.Domain.Cleanup`.

- [ ] **Step 3: Move sandbox models**

Move `SandboxEvaluation` and `SandboxAction` to `AiCleanVolume.Core.Domain.Sandbox`.

- [ ] **Step 4: Move settings models**

Move all settings models to `AiCleanVolume.Core.Domain.Settings`. Preserve public type names and property names.

- [ ] **Step 5: Update Core references**

Replace `using AiCleanVolume.Core.Models;` in Core with the new domain namespaces.

- [ ] **Step 6: Update Desktop references**

Replace Desktop references to model namespaces. Prefer adding specific usings over fully qualified names.

- [ ] **Step 7: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds with behavior-preserving model moves.

### Task 4: Split Core Ports And Application Services

**Files:**
- Move from: `src/AiCleanVolume.Core/Services/Interfaces.cs`
- Create under: `src/AiCleanVolume.Core/Kernel/Ports/*.cs`
- Move from: `src/AiCleanVolume.Core/Services/StorageFormatting.cs`
- Move from: `src/AiCleanVolume.Core/Services/CandidatePlanner.cs`
- Move from: `src/AiCleanVolume.Core/Services/ConfiguredPathCleanupPlanner.cs`
- Move from: `src/AiCleanVolume.Core/Services/HeuristicCleanupAdvisor.cs`
- Move from: `src/AiCleanVolume.Core/Services/DeletionSandbox.cs`

- [ ] **Step 1: Create one port per file**

Move each interface into `AiCleanVolume.Core.Kernel.Ports`.

- [ ] **Step 2: Add settings store port**

Create `ISettingsStore` in `Kernel/Ports`:

```csharp
public interface ISettingsStore
{
    ApplicationSettings Load();
    void Save(ApplicationSettings settings);
}
```

- [ ] **Step 3: Move scanning helpers**

Move `StorageFormatting` to `AiCleanVolume.Core.Application.Scanning`.

- [ ] **Step 4: Move cleanup planning services**

Move `CandidatePlanner`, `ConfiguredPathCleanupPlanner`, and `HeuristicCleanupAdvisor` to `AiCleanVolume.Core.Application.CleanupPlanning`.

- [ ] **Step 5: Move sandbox service**

Move `DeletionSandbox` to `AiCleanVolume.Core.Application.Deletion` unless it remains purely sandbox-domain specific; keep behavior unchanged.

- [ ] **Step 6: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds after namespace updates.

### Task 5: Introduce Deletion Workflow

**Files:**
- Create: `src/AiCleanVolume.Core/Application/Deletion/CleanupDeletionWorkflow.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.Deletion.cs`
- Modify later callers as needed.

- [ ] **Step 1: Extract non-UI deletion orchestration**

Create a workflow that receives sandbox, deletion, and privilege ports. It evaluates a suggestion and returns a result object that tells the UI whether confirmation is required or deletion was performed.

- [ ] **Step 2: Keep confirmation in UI**

Do not move AntdUI dialogs into Core. UI remains responsible for asking the user.

- [ ] **Step 3: Migrate one call path**

Update suggestion deletion to call the workflow while preserving existing user messages and status updates.

- [ ] **Step 4: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: deletion code compiles and still uses existing sandbox semantics.

## Chunk 3: Desktop Infrastructure Layout

### Task 6: Move Scanning Adapter

**Files:**
- Move from: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.cs`
- Move from: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.Json.cs`
- Move from: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.Platform.cs`
- Move from: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.Paths.cs`
- Move from: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.Session.cs`
- Move to: `src/AiCleanVolume.Desktop/Infrastructure/Scanning/`

- [ ] **Step 1: Move files**

Use `git mv` for each file to preserve history.

- [ ] **Step 2: Change namespace**

Use `AiCleanVolume.Desktop.Infrastructure.Scanning`.

- [ ] **Step 3: Update references**

Update composition root and any other Desktop references.

- [ ] **Step 4: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

### Task 7: Move AI Adapter

**Files:**
- Move from: `src/AiCleanVolume.Desktop/Services/OpenAiCompatibleAdvisor.cs`
- Move from: `src/AiCleanVolume.Desktop/Services/OpenAiCompatibleAdvisor.Requests.cs`
- Move from: `src/AiCleanVolume.Desktop/Services/OpenAiCompatibleAdvisor.Mapping.cs`
- Move from: `src/AiCleanVolume.Desktop/Services/OpenAiCompatibleAdvisor.Dto.cs`
- Move from: `src/AiCleanVolume.Desktop/Services/AiConnectionTestResult.cs`
- Move to: `src/AiCleanVolume.Desktop/Infrastructure/Ai/`

- [ ] **Step 1: Move files**

Use `git mv` for each file.

- [ ] **Step 2: Change namespace**

Use `AiCleanVolume.Desktop.Infrastructure.Ai`.

- [ ] **Step 3: Update references**

Update settings page, composition root, and any connection-test callers.

- [ ] **Step 4: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

### Task 8: Move Settings And Windows Adapters

**Files:**
- Move from: `src/AiCleanVolume.Desktop/Services/SettingsStore.cs`
- Move to: `src/AiCleanVolume.Desktop/Infrastructure/Settings/JsonSettingsStore.cs`
- Move from: `src/AiCleanVolume.Desktop/Services/RecycleBinDeletionService.cs`
- Move from: `src/AiCleanVolume.Desktop/Services/ShellExplorerService.cs`
- Move from: `src/AiCleanVolume.Desktop/Services/WindowsPrivilegeService.cs`
- Move to: `src/AiCleanVolume.Desktop/Infrastructure/Windows/`

- [ ] **Step 1: Rename settings store**

Rename `SettingsStore` to `JsonSettingsStore` only if all callers are updated in the same step. Preserve config file path and JSON behavior.

- [ ] **Step 2: Implement settings store port**

Make `JsonSettingsStore` implement `ISettingsStore`.

- [ ] **Step 3: Move Windows adapters**

Move deletion, Explorer, and privilege services to `Infrastructure.Windows`.

- [ ] **Step 4: Update references**

Update composition root and `MainWindowDependencies`.

- [ ] **Step 5: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

## Chunk 4: Presentation Shared Boundaries

### Task 9: Add Shell And Feature Contracts

**Files:**
- Create: `src/AiCleanVolume.Desktop/Presentation/Shared/IMainWindowShell.cs`
- Create: `src/AiCleanVolume.Desktop/Presentation/Shared/IFeaturePage.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.Operations.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.Navigation.cs`

- [ ] **Step 1: Create `IMainWindowShell`**

Expose busy state, notices, logging, and UI invocation helpers needed by feature controllers.

- [ ] **Step 2: Create `IFeaturePage`**

Expose page id, view control, and activation hook.

- [ ] **Step 3: Implement shell on `MainWindow`**

Add the interface to `MainWindow` and route methods to existing operations.

- [ ] **Step 4: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

### Task 10: Move Shared Presentation Utilities

**Files:**
- Move from: `src/AiCleanVolume.Desktop/MainWindow.UiFactory.cs`
- Create: `src/AiCleanVolume.Desktop/Presentation/Shared/Antd/AntdControlFactory.cs`
- Create: `src/AiCleanVolume.Desktop/Presentation/Shared/BackgroundOperationRunner.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.Operations.cs`

- [ ] **Step 1: Extract stateless AntdUI factories**

Move generic control factory methods that do not need `MainWindow` fields into `AntdControlFactory`.

- [ ] **Step 2: Keep window-specific helpers in `MainWindow` temporarily**

Leave helpers that depend on page fields or active settings in `MainWindow` until feature extraction.

- [ ] **Step 3: Extract background runner**

Move reusable background execution pattern into `BackgroundOperationRunner`, but keep UI notifications in `IMainWindowShell`.

- [ ] **Step 4: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds and no UI behavior changes.

## Chunk 5: Feature Extraction First Pass

### Task 11: Extract Settings Feature

**Files:**
- Create under: `src/AiCleanVolume.Desktop/Presentation/Features/Settings/`
- Move/split from: `src/AiCleanVolume.Desktop/MainWindow.Settings.cs`
- Move/split from: `src/AiCleanVolume.Desktop/MainWindow.AiProfiles.cs`
- Move/split from: `src/AiCleanVolume.Desktop/MainWindow.AiProfilePage.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.Layout.cs`

- [ ] **Step 1: Create settings state**

Create `SettingsPageState` for selected profile, editing index, preset-sync flags, and pending prompt state.

- [ ] **Step 2: Create settings view**

Move AntdUI settings panel and AI profile page construction into view classes while keeping existing controls and text.

- [ ] **Step 3: Create settings controller**

Move load/save, access mode, provider preset, prompt preset, and profile card actions into the controller.

- [ ] **Step 4: Wire through main window**

`MainWindow` creates or receives the settings feature and adds its view to the page host.

- [ ] **Step 5: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: settings UI compiles and keeps existing behavior.

### Task 12: Extract Scan Feature

**Files:**
- Create under: `src/AiCleanVolume.Desktop/Presentation/Features/Scan/`
- Move/split from: `src/AiCleanVolume.Desktop/MainWindow.Scan.cs`
- Move/split scan layout from: `src/AiCleanVolume.Desktop/MainWindow.Layout.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: Create scan state**

Move current root, current request, tree version, expanded paths, context row, and scan progress state into `ScanPageState`.

- [ ] **Step 2: Create scan view**

Move scan toolbar, filters, drive summary, status panel, and storage table creation.

- [ ] **Step 3: Create scan controller**

Move drive loading, scan request building, scan execution, storage expand, progress, and summary updates.

- [ ] **Step 4: Keep deletion hook explicit**

Expose a controller callback for storage-row delete instead of making scan feature own deletion services directly.

- [ ] **Step 5: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: scan UI compiles and scanning behavior remains unchanged.

### Task 13: Extract Suggestions And Deletion Feature

**Files:**
- Create under: `src/AiCleanVolume.Desktop/Presentation/Features/Suggestions/`
- Move/split from: `src/AiCleanVolume.Desktop/MainWindow.Suggestions.cs`
- Move/split from: `src/AiCleanVolume.Desktop/MainWindow.Deletion.cs`
- Move/split suggestion layout from: `src/AiCleanVolume.Desktop/MainWindow.Layout.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: Create suggestions state**

Move suggestion rows and selected suggestion scan context into `SuggestionsPageState`.

- [ ] **Step 2: Create suggestions view**

Move suggestion toolbar/table layout while preserving AntdUI controls.

- [ ] **Step 3: Create suggestions controller**

Move AI analysis, regular analysis, super analysis, selection toggles, table button clicks, and sandbox refresh.

- [ ] **Step 4: Integrate deletion workflow**

Use `CleanupDeletionWorkflow` for non-UI deletion orchestration. Keep confirmation and notifications in the controller through `IMainWindowShell`.

- [ ] **Step 5: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: suggestions and deletion behavior compile with existing messages and statuses.

### Task 14: Move Logs And MainWindow Shell Files

**Files:**
- Create under: `src/AiCleanVolume.Desktop/Presentation/Features/Logs/`
- Create under: `src/AiCleanVolume.Desktop/Presentation/MainWindow/`
- Move/split from: `src/AiCleanVolume.Desktop/MainWindow.Layout.cs`
- Move/split from: `src/AiCleanVolume.Desktop/MainWindow.Navigation.cs`
- Move/split from: `src/AiCleanVolume.Desktop/MainWindow.Windowing.cs`
- Move/split from: `src/AiCleanVolume.Desktop/MainWindow.Operations.cs`

- [ ] **Step 1: Move log panel**

Move log view and log append behavior into the logs feature or shell shared service.

- [ ] **Step 2: Move main window shell files**

Move window shell partial files into `Presentation/MainWindow` and update namespace only if all references remain coherent.

- [ ] **Step 3: Keep `MainWindow` small**

After extraction, `MainWindow` should mostly contain shell fields, constructor, page registration, navigation, and window lifecycle.

- [ ] **Step 4: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

## Chunk 6: Final Engineering Pass

### Task 15: Remove Empty Directories And Update Documentation

**Files:**
- Modify: `README.md`
- Modify: `docs/plans/2026-05-25-project-architecture-reorganization-design.md` if implementation choices changed.
- Remove empty source directories if any.

- [ ] **Step 1: Update README directory section**

Document the new Core and Desktop structure.

- [ ] **Step 2: Check empty directories**

Run:

```pwsh
Get-ChildItem -Path .\src -Directory -Recurse | Where-Object { -not (Get-ChildItem -LiteralPath $_.FullName -Force) } | Select-Object FullName
```

Expected: no unexpected empty source directories remain.

- [ ] **Step 3: Measure source file sizes**

Run:

```pwsh
$files = Get-ChildItem -Path .\src -Recurse -File -Filter *.cs | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
$files | ForEach-Object {
    [pscustomobject]@{
        Lines = (Get-Content -LiteralPath $_.FullName | Measure-Object -Line).Lines
        Path = $_.FullName.Replace((Get-Location).Path + [System.IO.Path]::DirectorySeparatorChar, '')
    }
} | Sort-Object Lines -Descending | Select-Object -First 25 | Format-Table -AutoSize
```

Expected: remaining large files are justified shell/view files or next-step candidates.

### Task 16: Final Verification

**Files:**
- Inspect all changed files.

- [ ] **Step 1: Build Debug**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

- [ ] **Step 2: Inspect diff**

Run:

```pwsh
git status --short
git diff --stat
```

Expected: changes are limited to planned source structure and documentation.

- [ ] **Step 3: Stop without commit**

Do not run `git commit`. Report changed files, build result, and any residual risk to the user.
