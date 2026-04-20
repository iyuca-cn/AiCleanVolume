# 目录树后台预取秒开 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在保持目录树懒加载的前提下，为扫描完成后的热点目录增加 5 层后台预取、点击预测和会话内缓存，让大多数首次展开做到即点击即查看，并避免同一路径重复从头解析。

**Architecture:** 主扫描继续只返回根和首层，但扫描器必须真正截断深层 JSON。`MainWindow` 在扫描成功后把热点目录提交给后台预取协调器，协调器在当前会话内缓存一层扫描结果，后台预取到 5 层，并在用户点击展开后对当前路径做预测预取；展开事件优先返回缓存结果，仅对未命中的冷门目录走兜底加载。

**Tech Stack:** C#、.NET Framework 4.0、WinForms、AntdUI 2.3.0、现有 `FolderSizeRankerScanProvider`。

---

### Task 1: 修复浅层扫描的真实深度截断

**Files:**
- Modify: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.cs`

- [ ] 当 `LoadDepth` 到达阈值时，深层 `files` 与 `children` 不再递归解析，而是直接跳过。
- [ ] 保证浅层节点仍然能得到路径、大小、直接文件数和直接子目录数等最小必要摘要。
- [ ] 避免“少挂对象但仍完整递归读 JSON”的伪懒加载行为。

### Task 2: 重构目录树预取协调器

**Files:**
- Create: `src/AiCleanVolume.Desktop/Services/StorageTreePrefetchCoordinator.cs`
- [ ] 用会话内缓存替代当前薄弱的路径缓存，缓存键至少覆盖路径与当前扫描会话。
- [ ] 将预取深度提升到 5 层，并限制每层热点目录数与总缓存条目数。
- [ ] 让协调器支持动态入队，而不是只在 `BeginSession` 时生成一次静态队列。
- [ ] 预取失败时仅记录日志或吞掉异常，不中断后续任务。

### Task 3: 接入主窗体扫描生命周期与点击预测

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Create: `src/AiCleanVolume.Desktop/Services/StorageTreePrefetchCoordinator.cs`

- [ ] 在 `MainWindow` 中持有协调器实例。
- [ ] 在 `ScanCurrentLocation` 成功后，用当前根节点和扫描参数启动新一轮预取。
- [ ] 在用户重新扫描时，先使上一轮扫描的预取任务失效。
- [ ] 在用户展开某个节点后，触发对该节点热点子目录的预测预取。
- [ ] 保持 AI 分析的全量扫描逻辑不受目录树预取影响。

### Task 4: 将展开逻辑改为优先命中缓存并复用结果

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Modify: `src/AiCleanVolume.Desktop/ViewModels/StorageEntryRow.cs`

- [ ] 在 `StorageTable_ExpandChanged` 中先查询预取缓存，再决定是否需要兜底扫描。
- [ ] 缓存命中时直接把子节点应用到对应 `StorageItem` 与 `StorageEntryRow`。
- [ ] 未命中时继续保留兜底扫描逻辑，保证冷门目录也能展开。
- [ ] 兜底扫描成功后立即回填缓存，避免同一路径再次从头扫描。
- [ ] 去掉“扫描完成后常见首次点击显示 `正在加载...`”这条常态路径。
- [ ] 仅在真正兜底加载且确有必要时才保留临时占位或最小等待反馈。

### Task 5: 收紧行模型并保留点击深度信息

**Files:**
- Modify: `src/AiCleanVolume.Desktop/ViewModels/StorageEntryRow.cs`

- [ ] 让未预取但可展开的目录继续保留箭头能力，不依赖明显的文本占位。
- [ ] 为行模型补充层级信息，供点击预测预取使用。
- [ ] 区分“尚未预取”“命中缓存”“正在兜底加载”三种状态。
- [ ] 避免每次刷新都递归重建过多行对象。

### Task 6: 验证与回归检查

**Files:**
- No source changes beyond the files above.

- [ ] 执行 `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug`。
- [ ] 手工验证：扫描完成后等待后台预取，再首次展开根下大目录，应直接显示子节点。
- [ ] 手工验证：快速重新扫描到其他路径，不应出现旧路径的预取结果串入。
- [ ] 手工验证：点击未命中缓存的冷门目录，仍能正常展开。
- [ ] 若只剩既有 `RestSharp 105.2.3` 漏洞告警，则记录为已知问题，不在本任务扩展处理。
