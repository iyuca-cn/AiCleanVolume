# Large File Split Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split oversized source files into focused files while preserving existing behavior.

**Architecture:** Use behavior-preserving C# file decomposition. `MainWindow` becomes a partial class split by UI and workflow responsibility; Core models and Desktop services are split by domain object or internal helper responsibility without changing public type names or JSON shape.

**Tech Stack:** .NET Framework 4.0, C# 7.3, WinForms, AntdUI, Newtonsoft.Json, RestSharp 105.2.3.

---

## File Structure

- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
  - Keep fields, constructor, `InitializeComponent`, table setup, initial placeholder binding, and shared constants.
  - Change `public sealed class MainWindow` to `public sealed partial class MainWindow`.
- Create: `src/AiCleanVolume.Desktop/MainWindow.Layout.cs`
  - Move page and section creation methods.
- Create: `src/AiCleanVolume.Desktop/MainWindow.Navigation.cs`
  - Move navigation, sidebar resize, sidebar collapse, page switching, page title/description helpers.
- Create: `src/AiCleanVolume.Desktop/MainWindow.Settings.cs`
  - Move settings load/save, AI access mode, preset selection, sandbox and privilege UI state.
- Create: `src/AiCleanVolume.Desktop/MainWindow.AiProfiles.cs`
  - Move AI profile card UI, profile apply/save logic and profile formatting helpers.
- Create: `src/AiCleanVolume.Desktop/MainWindow.AiProfilePage.cs`
  - Move AI profile create/edit page construction methods.
- Create: `src/AiCleanVolume.Desktop/MainWindow.Scan.cs`
  - Move drive loading, scan request creation, scanning, storage table expansion, scan summary/progress, path parsing.
- Create: `src/AiCleanVolume.Desktop/MainWindow.Suggestions.cs`
  - Move suggestion analysis, selection actions, suggestion binding and sandbox refresh.
- Create: `src/AiCleanVolume.Desktop/MainWindow.Deletion.cs`
  - Move suggestion deletion, storage row deletion, sandbox confirmation and tree removal helpers.
- Create: `src/AiCleanVolume.Desktop/MainWindow.Windowing.cs`
  - Move keyboard/window lifecycle, Win32 methods, restore repaint, startup reveal, bounds helpers.
- Create: `src/AiCleanVolume.Desktop/MainWindow.UiFactory.cs`
  - Move generic AntdUI factory helpers and surface/table styling.
- Create: `src/AiCleanVolume.Desktop/MainWindow.Presets.cs`
  - Move AI preset arrays, preset classes and prompt formatting helpers.
- Create: `src/AiCleanVolume.Desktop/MainWindow.Operations.cs`
  - Move shared background operation, busy state, notice and log helpers.
- Modify/Create: `src/AiCleanVolume.Core/Models/*.cs`
  - Split `ApplicationSettings.cs` into one file per settings model.
- Modify/Create: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.*.cs`
  - Split scan provider internals into partial files.
- Modify/Create: `src/AiCleanVolume.Desktop/Services/OpenAiCompatibleAdvisor.*.cs`
  - Split AI advisor internals into partial files.

## Chunk 1: MainWindow Partial Split

### Task 1: Prepare `MainWindow` for partial files

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [x] **Step 1: Change the class declaration**

Change:

```csharp
public sealed class MainWindow : AntdUI.Window
```

to:

```csharp
public sealed partial class MainWindow : AntdUI.Window
```

- [x] **Step 2: Keep the first compile boundary small**

Do not move methods yet. Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds with existing warnings.

### Task 2: Move preset definitions

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Create: `src/AiCleanVolume.Desktop/MainWindow.Presets.cs`

- [x] **Step 1: Move these members to `MainWindow.Presets.cs`**

Move:
- `DefaultAiSystemPrompt`
- `AiPromptPresets`
- `AiProviderPresets`
- `AiPromptPreset`
- `AiProviderPreset`
- `BuildDriveScopedPrompt`
- `NormalizeDriveRootText`
- `FormatDriveLabel`

Use the same namespace and class declaration:

```csharp
namespace AiCleanVolume.Desktop
{
    public sealed partial class MainWindow : AntdUI.Window
    {
        // moved members
    }
}
```

- [x] **Step 2: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

### Task 3: Move generic UI factories

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Create: `src/AiCleanVolume.Desktop/MainWindow.UiFactory.cs`

- [x] **Step 1: Move generic UI helpers**

Move methods from `CreateToolbarCaption` through `CreateInfoCard`, plus:
- `CreateSettingsSurfacePanel`
- `CreateSettingsGroupPanel`
- `CreateSettingsGroupTitle`
- `CreateSmallMutedLabel`

Keep `using System.Drawing;` and `using System.Windows.Forms;` in the new file.

- [x] **Step 2: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

### Task 4: Move layout creation methods

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Create: `src/AiCleanVolume.Desktop/MainWindow.Layout.cs`

- [x] **Step 1: Move page and panel construction**

Move methods from `CreatePageContainer` through `CreateLogPanel`, excluding helpers already moved to `MainWindow.UiFactory.cs`.

- [x] **Step 2: Move `CreateNavigationItem` only if it remains layout-specific**

Prefer keeping `CreateNavigationItem` with layout creation because it builds menu items.

- [x] **Step 3: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

### Task 5: Move navigation and settings workflows

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Create: `src/AiCleanVolume.Desktop/MainWindow.Navigation.cs`
- Create: `src/AiCleanVolume.Desktop/MainWindow.Settings.cs`

- [x] **Step 1: Move navigation methods**

Move:
- sidebar resize handlers
- `FinishSidebarResize`
- `SettingsNavButton_Click`
- `ApplySidebarWidth`
- `ToggleSidebarCollapsed`
- `SetSidebarCollapsed`
- `ApplySidebarVisualState`
- `SetNavigationText`
- `NavigationMenu_SelectChanged`
- `SyncNavigationSelection`
- `UpdateSettingsNavigationState`
- `SetActivePage`
- `CompactStorageTreeRowsForNavigation`
- `GetPageControl`
- `SuspendPageSwitchLayout`
- `ResumePageSwitchLayout`
- `GetPageTitle`
- `GetPageDescription`
- `GetActivePageDescription`
- sidebar width helpers at the bottom of the file

- [x] **Step 2: Move settings methods**

Move:
- `LoadSettingsToUi`
- AI access mode and provider/prompt preset population and selection helpers
- privilege checkbox handlers
- `SaveSettings`
- `TestAiSettings`
- `SaveSettingsFromUi`
- AI configured and model cookie mapping helpers
- parse helpers used only by settings

- [x] **Step 3: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

### Task 6: Move AI profile workflow

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Create: `src/AiCleanVolume.Desktop/MainWindow.AiProfiles.cs`

- [x] **Step 1: Move AI profile methods**

Move methods from `PopulateAiProfiles` through `PromptForAiProfileName`, excluding `TestAiSettings` if it was already moved with settings.

- [x] **Step 2: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

### Task 7: Move scan, suggestions, deletion and windowing

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Create: `src/AiCleanVolume.Desktop/MainWindow.Scan.cs`
- Create: `src/AiCleanVolume.Desktop/MainWindow.Suggestions.cs`
- Create: `src/AiCleanVolume.Desktop/MainWindow.Deletion.cs`
- Create: `src/AiCleanVolume.Desktop/MainWindow.Windowing.cs`

- [x] **Step 1: Move scan methods**

Move drive loading, location resolving, scan request builders, scan execution, storage table handlers, storage tree expansion and scan progress/summary helpers to `MainWindow.Scan.cs`.

- [x] **Step 2: Move suggestion methods**

Move suggestion analysis, regular/super/configured path analysis, suggestion selection, suggestion table click handlers, binding and sandbox refresh to `MainWindow.Suggestions.cs`.

- [x] **Step 3: Move deletion methods**

Move selected/single suggestion delete, storage row delete, sandbox validation/confirmation, delete outcome type and storage tree removal helpers to `MainWindow.Deletion.cs`.

- [x] **Step 4: Move windowing methods**

Move keyboard overrides, load/handle/shown/dispose, redraw helpers, Win32 imports and structs, WndProc, restore repaint, startup reveal and bounds helpers to `MainWindow.Windowing.cs`.

- [x] **Step 5: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

## Chunk 2: Core Model Split

### Task 8: Split settings models

**Files:**
- Modify: `src/AiCleanVolume.Core/Models/ApplicationSettings.cs`
- Create: `src/AiCleanVolume.Core/Models/AiSettings.cs`
- Create: `src/AiCleanVolume.Core/Models/AiModelCookieMapping.cs`
- Create: `src/AiCleanVolume.Core/Models/AiProfile.cs`
- Create: `src/AiCleanVolume.Core/Models/SandboxSettings.cs`
- Create: `src/AiCleanVolume.Core/Models/ScanSettings.cs`
- Create: `src/AiCleanVolume.Core/Models/UiSettings.cs`

- [x] **Step 1: Move one model per file**

Keep namespace `AiCleanVolume.Core.Models` and public type names unchanged.

- [x] **Step 2: Preserve private helper access**

If a moved type needs `NormalizeValue`, duplicate a private helper only inside that type or make an `internal static` helper only if multiple files need it. Prefer minimal duplication over new architecture.

- [x] **Step 3: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

## Chunk 3: Desktop Service Split

### Task 9: Split scan provider internals

**Files:**
- Modify: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.cs`
- Create: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.Session.cs`
- Create: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.Json.cs`
- Create: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.Platform.cs`
- Create: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.Paths.cs`

- [x] **Step 1: Mark provider partial**

Change:

```csharp
public sealed class FolderSizeRankerScanProvider : IScanProvider
```

to:

```csharp
public sealed partial class FolderSizeRankerScanProvider : IScanProvider
```

- [x] **Step 2: Move nested session/state types**

Move `ScanSession`, `DirectoryNodeState`, `FileNodeState`, `CliExecutionException` to `FolderSizeRankerScanProvider.Session.cs` as nested members inside the partial class.

- [x] **Step 3: Move JSON parsing methods**

Move compact JSON parsing methods to `FolderSizeRankerScanProvider.Json.cs`.

- [x] **Step 4: Move platform fallback methods**

Move `TryScanWithPlatformApi`, `ScanDirectory`, safe filesystem helpers and compare/limit helpers to `FolderSizeRankerScanProvider.Platform.cs`.

- [x] **Step 5: Move path and CLI argument helpers**

Move normalize/path compare/template key/argument quoting helpers to `FolderSizeRankerScanProvider.Paths.cs`.

- [x] **Step 6: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

### Task 10: Split AI advisor internals

**Files:**
- Modify: `src/AiCleanVolume.Desktop/Services/OpenAiCompatibleAdvisor.cs`
- Create: `src/AiCleanVolume.Desktop/Services/OpenAiCompatibleAdvisor.Requests.cs`
- Create: `src/AiCleanVolume.Desktop/Services/OpenAiCompatibleAdvisor.Mapping.cs`
- Create: `src/AiCleanVolume.Desktop/Services/OpenAiCompatibleAdvisor.Dto.cs`
- Create: `src/AiCleanVolume.Desktop/Services/AiConnectionTestResult.cs`

- [x] **Step 1: Mark advisor partial**

Change:

```csharp
public sealed class OpenAiCompatibleAdvisor : IAiCleanupAdvisor
```

to:

```csharp
public sealed partial class OpenAiCompatibleAdvisor : IAiCleanupAdvisor
```

- [x] **Step 2: Move request/auth/endpoint helpers**

Move auth header, endpoint normalization, response summary, prompt building, secret masking and preview helpers to `OpenAiCompatibleAdvisor.Requests.cs`.

- [x] **Step 3: Move response mapping helpers**

Move JSON extraction, suggestion mapping, provider cookie resolution, risk parsing and path normalization to `OpenAiCompatibleAdvisor.Mapping.cs`.

- [x] **Step 4: Move DTO types**

Move `ChatCompletionResponse`, `ChatChoice`, `ChatMessage`, `AiSuggestionEnvelope`, `AiSuggestionDto` to `OpenAiCompatibleAdvisor.Dto.cs` as nested private types inside the partial class.

- [x] **Step 5: Move `AiConnectionTestResult`**

Move the public result type to its own top-level file with namespace `AiCleanVolume.Desktop.Services`.

- [x] **Step 6: Build**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds.

## Chunk 4: Final Verification

### Task 11: Check file sizes and git diff

**Files:**
- Inspect all changed files.

- [x] **Step 1: Measure source file sizes**

Run:

```pwsh
$files = Get-ChildItem -Path .\src -Recurse -File -Filter *.cs | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
$files | ForEach-Object {
    [pscustomobject]@{
        Lines = (Get-Content -LiteralPath $_.FullName | Measure-Object -Line).Lines
        Path = $_.FullName.Replace((Get-Location).Path + [System.IO.Path]::DirectorySeparatorChar, '')
    }
} | Sort-Object Lines -Descending | Select-Object -First 20 | Format-Table -AutoSize
```

Expected: no project-owned source file remains above roughly 800 lines unless there is a justified reason.

- [x] **Step 2: Build final Debug**

Run:

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

Expected: build succeeds with existing warnings only.

- [x] **Step 3: Review changed files**

Run:

```pwsh
git status --short
git diff --stat
```

Expected: only planned source files and the two plan/design documents changed.

- [x] **Step 4: Do not commit**

Stop after verification. The user explicitly asked that plans are not committed automatically, and no commit should be created unless requested.
