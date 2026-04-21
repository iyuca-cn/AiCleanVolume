# Storage Tree Session Worker Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace storage tree prefetch/cache behavior with a parsed in-memory session and run scan/expand work off the UI thread.

**Architecture:** `FolderSizeRankerScanProvider` owns a parsed `StorageTreeSession` indexed by directory path. `MainWindow` uses a reusable background worker for scan, expand, and deletion work, while the UI thread only binds results. The storage card only renders the table, and Delete is handled at window level.

**Tech Stack:** .NET Framework 4.0, WinForms, AntdUI, Newtonsoft.Json streaming reader, `folder-size-ranker-cli.exe`.

---

## Files

- Modify: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.cs`
- Create: `src/AiCleanVolume.Desktop/Services/ReusableBackgroundWorker.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Modify: `src/AiCleanVolume.Desktop/AiCleanVolume.Desktop.csproj`
- Remove or orphan from use: `src/AiCleanVolume.Desktop/Services/StorageTreePrefetchCoordinator.cs`
- Validate: `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug`

## Chunk 1: Parsed Session Provider

### Task 1: Replace JSON slice session with parsed state

**Files:**
- Modify: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.cs`

- [ ] **Step 1: Add parsed session types**

Create private nested types:

```csharp
private sealed class StorageTreeSession
{
    public string RootPath { get; set; }
    public string TemplateKey { get; set; }
    public Dictionary<string, DirectoryNodeState> DirectoryIndex { get; set; }
}

private sealed class DirectoryNodeState
{
    public string Path { get; set; }
    public long Bytes { get; set; }
    public int DirectFileCount { get; set; }
    public int TotalFileCount { get; set; }
    public int TotalDirectoryCount { get; set; }
    public List<StorageItem> DirectFiles { get; set; }
    public List<string> DirectDirectoryPaths { get; set; }
}
```

- [ ] **Step 2: Parse CLI output directly into session**

Replace temp-file indexing with streaming JSON parsing that builds `DirectoryNodeState` recursively and registers each directory in `DirectoryIndex`.

- [ ] **Step 3: Materialize directory from parsed state**

Update materialization to clone `DirectFiles` and create direct child directory `StorageItem` nodes from indexed `DirectoryNodeState`.

- [ ] **Step 4: Expose path materialization through `Scan`**

Keep `IScanProvider.Scan(ScanRequest)` unchanged. For `LoadDepth >= 0`, reuse the compatible current session and return `MaterializeDirectory(session, entry, request.LoadDepth, isRoot)`.

- [ ] **Step 5: Set CLI path to exe directory**

Change executable resolution to `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "folder-size-ranker-cli.exe")`.

## Chunk 2: Reusable Worker and Main Window Wiring

### Task 2: Add reusable background worker

**Files:**
- Create: `src/AiCleanVolume.Desktop/Services/ReusableBackgroundWorker.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: Create worker queue**

Implement a disposable class with one background thread, `Queue(Action action)`, and a blocking queue based on `Queue<Action>` + `Monitor`.

- [ ] **Step 2: Replace `ThreadPool.QueueUserWorkItem` in `RunBackground`**

Use the reusable worker for generic background operations and marshal completion through `BeginInvoke`.

- [ ] **Step 3: Replace expand work scheduling**

Use the reusable worker for `StorageTable_ExpandChanged`, keep `treeVersion` checks before applying UI results.

- [ ] **Step 4: Dispose worker on window dispose**

Override or extend dispose path so the worker thread exits when the form is closed.

## Chunk 3: Remove Prefetch Cache and Clean UI

### Task 3: Remove prefetch integration

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: Remove `StorageTreePrefetchCoordinator` field and constructor initialization**

Delete references to `storageTreePrefetch`.

- [ ] **Step 2: Simplify scan success path**

Do not call `BeginSession` or `PredictFrom`; provider session owns parsed results.

- [ ] **Step 3: Simplify expand path**

Remove cache lookup and remember/predict calls; only call `scanProvider.Scan(CreateScanRequest(row.Item.Path, 1, currentTreeRequest))` on the worker.

- [ ] **Step 4: Simplify delete refresh path**

After local UI tree removal, do not restart prefetch. Increment `currentTreeVersion` only if needed to reject stale expand callbacks.

### Task 4: Remove storage card title and description

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: Delete heading/description controls from `CreateStoragePanel`**

Only add `storageTable` to the panel.

## Chunk 4: Delete Key and Project Output

### Task 5: Fix Delete key handling

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: Set `KeyPreview = true` during initialization**

- [ ] **Step 2: Override `ProcessCmdKey` or `OnKeyDown`**

When `Keys.Delete`, active page is scan, and a storage row is active, call `DeleteStorageRow`.

- [ ] **Step 3: Keep table-level handler as fallback**

Leave `storageTable.KeyDown` in place or route both through a shared helper.

### Task 6: Copy CLI beside exe

**Files:**
- Modify: `src/AiCleanVolume.Desktop/AiCleanVolume.Desktop.csproj`

- [ ] **Step 1: Change linked output path**

Link CLI as `folder-size-ranker-cli.exe`, not `tools\folder-size-ranker-cli.exe`.

- [ ] **Step 2: Build**

Run: `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug`

Expected: build succeeds; existing RestSharp and AntdUI warnings may remain.
