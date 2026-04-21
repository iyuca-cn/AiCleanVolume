# Storage Tree Memory Del Fix Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce storage tree session memory and make `Del` reliably trigger file-tree deletion.

**Architecture:** Replace per-file cached `StorageItem` objects in the parsed session with a minimal file state and materialize transient `StorageItem` objects only when a node is expanded. Simplify the Delete shortcut path so scan-page tree rows can be deleted even when focus bookkeeping is imperfect.

**Tech Stack:** .NET Framework 4.0, WinForms, AntdUI, Newtonsoft.Json streaming parser.

---

### Task 1: Shrink session file storage

**Files:**
- Modify: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.cs`

- [ ] **Step 1: Replace `DirectFiles` item type**
- [ ] **Step 2: Store only file path and bytes in session**
- [ ] **Step 3: Materialize transient `StorageItem` objects on expand**
- [ ] **Step 4: Remove unused slice/temp-file session fields and helpers**

### Task 2: Make `Del` deterministic

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: Update tree click bookkeeping for left/right click**
- [ ] **Step 2: Remove text-input focus guard from `Del` path**
- [ ] **Step 3: Fallback to last active tree row when selection lookup misses**

### Task 3: Validate

**Files:**
- Modify: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: Build**

Run: `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug`

Expected: build succeeds; existing `RestSharp` vulnerability warnings may remain.
