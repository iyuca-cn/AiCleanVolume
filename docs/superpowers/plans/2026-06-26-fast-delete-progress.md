# 极速删除状态显示 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将文件夹删除改为非递归极速迭代删除，并显示当前正在删除的路径。

**Architecture:** Core 定义删除状态快照与删除接口参数；Windows 删除实现只更新内存状态，不触碰 UI；MainWindow 用高频 Timer 拉取最新状态并用 AntdUI 文本显示。

**Tech Stack:** .NET Framework 4.8, WinForms, AntdUI, Win32 `FindFirstFileW` / `DeleteFileW` / `RemoveDirectoryW`.

---

## Chunk 1: 删除状态契约

### Task 1: Core 删除进度模型

**Files:**
- Create: `src/AiCleanVolume.Core/Application/Deletion/DeletionProgressState.cs`
- Modify: `src/AiCleanVolume.Core/Kernel/Ports/IDeletionService.cs`
- Modify: `src/AiCleanVolume.Core/Application/Deletion/CleanupDeletionWorkflow.cs`

- [ ] 新增 `DeletionProgressState`，只保存最新路径、阶段文本和版本号。
- [ ] `IDeletionService.Delete` 增加可空 `DeletionProgressState` 参数。
- [ ] `CleanupDeletionWorkflow.Delete` 增加可空 `DeletionProgressState` 参数并透传。

## Chunk 2: 极速迭代删除

### Task 2: 改造 Windows 删除实现

**Files:**
- Modify: `src/AiCleanVolume.Desktop/Infrastructure/Windows/RecycleBinDeletionService.cs`

- [ ] 删除目录删除函数中的递归调用。
- [ ] 用显式栈枚举目录，删除文件，记录待删除目录。
- [ ] 后序删除目录，保留重解析点保护。
- [ ] 删除线程只调用 `progress.Update(...)` 写入内存状态。

## Chunk 3: UI 高频拉取显示

### Task 3: MainWindow 接入删除状态

**Files:**
- Modify: `src/AiCleanVolume.Desktop/Presentation/MainWindow/MainWindow.cs`
- Modify: `src/AiCleanVolume.Desktop/Presentation/MainWindow/MainWindow.Deletion.cs`
- Modify: `src/AiCleanVolume.Desktop/Presentation/MainWindow/MainWindow.Operations.cs`
- Modify as needed: `src/AiCleanVolume.Desktop/Presentation/Features/Suggestions/SuggestionsPageView.cs`

- [ ] MainWindow 持有当前删除状态和高频 Timer。
- [ ] 删除开始时启动 Timer，删除结束时停止并恢复状态文本。
- [ ] Timer 只在 UI 线程读取最新快照，不阻塞删除线程。
- [ ] 建议页批量删除和扫描页文件树删除都显示当前路径。

## Chunk 4: 验证

### Task 4: 构建与检查

**Files:**
- No planned code changes.

- [ ] `rg -n "DeleteDirectoryByWinApi\\(|ScanDirectory\\(" src/AiCleanVolume.Desktop/Infrastructure/Windows/RecycleBinDeletionService.cs` 检查删除函数无递归自调用。
- [ ] `dotnet build src/AiCleanVolume.Desktop/AiCleanVolume.Desktop.csproj -f net48`
- [ ] 记录全量多目标构建若失败，是否为既有 `net40` NativeBridge 条件引用问题。
- [ ] 汇总验证结果。
