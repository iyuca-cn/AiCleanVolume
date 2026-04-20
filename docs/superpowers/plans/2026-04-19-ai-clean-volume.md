# AI Clean Volume Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一个 .NET Framework 4.0 + AntdUI 的 C 盘空间分析与 AI 清理建议桌面工具。

**Architecture:** 使用微内核边界：扫描、AI 判断、沙盒评估、删除执行、Explorer 预览分别通过接口解耦。桌面层负责 AntdUI 界面、folder-size-ranker-cli 适配、OpenAI 兼容接口适配和本地设置持久化。

**Tech Stack:** C#、.NET Framework 4.0、WinForms、AntdUI v2.3.0、RestSharp、Newtonsoft.Json、folder-size-ranker-cli。

---

## Chunk 1: Project Skeleton

### Task 1: Create solution and projects

**Files:**
- Create: `AiCleanVolume.sln`
- Create: `src/AiCleanVolume.Core/AiCleanVolume.Core.csproj`
- Create: `src/AiCleanVolume.Desktop/AiCleanVolume.Desktop.csproj`

- [ ] **Step 1: Add net40 core library**
- [ ] **Step 2: Add net40 WinForms desktop app**
- [ ] **Step 3: Reference AntdUI source project and third-party CLI**
- [ ] **Step 4: Restore packages**

## Chunk 2: Kernel Boundaries

### Task 2: Implement domain contracts

**Files:**
- Create: `src/AiCleanVolume.Core/Models/CoreModels.cs`
- Create: `src/AiCleanVolume.Core/Models/ApplicationSettings.cs`
- Create: `src/AiCleanVolume.Core/Services/Interfaces.cs`
- Create: `src/AiCleanVolume.Core/Services/DeletionSandbox.cs`
- Create: `src/AiCleanVolume.Core/Services/CandidatePlanner.cs`
- Create: `src/AiCleanVolume.Core/Services/HeuristicCleanupAdvisor.cs`

- [ ] **Step 1: Model scan tree and cleanup suggestions**
- [ ] **Step 2: Define scanning, AI, sandbox, deletion interfaces**
- [ ] **Step 3: Add allow-list sandbox behavior**
- [ ] **Step 4: Add local heuristic fallback advisor**

## Chunk 3: Desktop Adapters

### Task 3: Implement infrastructure

**Files:**
- Create: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.cs`
- Create: `src/AiCleanVolume.Desktop/Services/OpenAiCompatibleAdvisor.cs`
- Create: `src/AiCleanVolume.Desktop/Services/SettingsStore.cs`
- Create: `src/AiCleanVolume.Desktop/Services/RecycleBinDeletionService.cs`
- Create: `src/AiCleanVolume.Desktop/Services/ShellExplorerService.cs`
- Create: `src/AiCleanVolume.Desktop/Services/WindowsPrivilegeService.cs`

- [ ] **Step 1: Invoke folder-size-ranker-cli with `--all`**
- [ ] **Step 2: Parse JSON into scan tree**
- [ ] **Step 3: Call OpenAI-compatible chat completions endpoint**
- [ ] **Step 4: Persist AI and sandbox settings**
- [ ] **Step 5: Delete through recycle bin when enabled**

## Chunk 4: UI

### Task 4: Build AntdUI screen

**Files:**
- Create: `src/AiCleanVolume.Desktop/Program.cs`
- Create: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Create: `src/AiCleanVolume.Desktop/ViewModels/StorageEntryRow.cs`
- Create: `src/AiCleanVolume.Desktop/ViewModels/CleanupSuggestionRow.cs`
- Create: `README.md`

- [ ] **Step 1: Add PageHeader custom title bar**
- [ ] **Step 2: Add drive selector and scan controls**
- [ ] **Step 3: Add WizTree-like storage tree table**
- [ ] **Step 4: Add AI suggestion table with checked rows**
- [ ] **Step 5: Add double-click Explorer preview**
- [ ] **Step 6: Add sandbox confirmation delete flow**
- [ ] **Step 7: Build solution and fix compile errors**
