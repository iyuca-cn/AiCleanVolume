# Scan Header Summary Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将扫描页顶部改成更接近磁盘工具的摘要头部，同时保留高级筛选项并统一默认值为 `-1`。

**Architecture:** 扫描页顶部改为左侧筛选区、右侧磁盘容量摘要区和底部扫描状态区。`MainWindow` 负责布局重排、扫描状态更新和卷摘要绑定；扫描参数默认值由核心模型、桌面端回退值和仓库默认配置统一收敛到 `-1`。

**Tech Stack:** C#、.NET Framework 4.0、WinForms、AntdUI 2.3.0、现有 `SettingsStore` 与 `FolderSizeRankerScanProvider`。

---

### Task 1: 重排扫描页顶部布局

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] 用新的顶部卡片替换旧的双行工具栏和三张信息卡。
- [ ] 将扫描按钮移到扫描页顶部左侧，保留其他页面操作按钮在标题栏。
- [ ] 新增右侧容量摘要区域和底部扫描状态区域。

### Task 2: 接入磁盘容量摘要与扫描状态

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] 根据盘符和路径所属卷刷新总空间、已用、可用、预留空间。
- [ ] 扫描开始时显示加载态，完成时显示耗时和成功态，失败时显示错误态。
- [ ] 保持现有扫描、AI 建议和删除流程的主逻辑不变。

### Task 3: 统一默认值为 -1

**Files:**
- Modify: `src/AiCleanVolume.Core/Models/ApplicationSettings.cs`
- Modify: `src/AiCleanVolume.Core/Models/CoreModels.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Modify: `src/AiCleanVolume.Desktop/appsettings.json`

- [ ] 将扫描设置默认值从 `128 / 80` 改为 `-1 / -1`。
- [ ] 将桌面端解析回退值同步改为 `-1 / -1`。
- [ ] 保证负数继续按“不限”语义传递给扫描请求。

### Task 4: 验证

**Files:**
- No source changes beyond the files above.

- [ ] 执行 `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug`。
- [ ] 检查扫描页顶部编译通过，且新的状态字段、布局辅助方法没有命名冲突。
