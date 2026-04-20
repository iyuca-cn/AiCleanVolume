# 目录树会话级 JSON 缓存 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让目录树在一次扫描后复用同一份 CLI JSON 会话缓存，展开和预取都不再重复调用 CLI，同时把内存占用控制在小型节点缓存范围内。

**Architecture:** 首次目录树扫描时把 CLI JSON 流式写入会话临时文件，并构建目录路径索引。目录展开和后台预热改为通过路径索引读取直接子目录摘要和 `files` 切片来物化节点，只保留小型内存节点缓存而不常驻整棵树。

**Tech Stack:** C#、.NET Framework 4.0、WinForms、Newtonsoft.Json（仅保留在全量扫描路径）、自定义轻量 JSON 索引/切片解析器。

---

### Task 1: 重写目录树部分扫描链路

**Files:**
- Modify: `src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.cs`

- [ ] 为 `LoadDepth >= 0` 的目录树扫描引入会话级 JSON 临时文件缓存。
- [ ] 构建按路径定位的目录索引，而不是每次从头解析整段 JSON。
- [ ] 让子目录展开命中同一会话缓存，不再重复调用 CLI。
- [ ] 保留 `LoadDepth < 0` 的全量扫描路径，避免影响 AI 分析功能。

### Task 2: 收紧内存缓存职责

**Files:**
- Modify: `src/AiCleanVolume.Desktop/Services/StorageTreePrefetchCoordinator.cs`

- [ ] 保持预取职责，但让预取只预热节点结果，不再承担原始数据缓存。
- [ ] 控制物化节点缓存上限，避免整棵树回流到内存。
- [ ] 修复缓存对象复用导致的可变共享风险。

### Task 3: 接入主窗体展开链路

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] 让展开逻辑优先命中预热缓存。
- [ ] 未命中预热时继续调用 `scanProvider.Scan`，但该调用必须只走会话缓存解析路径。
- [ ] 维持重新扫描时的会话失效行为。

### Task 4: 构建验证

**Files:**
- No source changes beyond the files above.

- [ ] 执行 `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug`。
- [ ] 检查目录树首次展开是否不再重复触发 CLI 扫描。
- [ ] 检查重新扫描后旧会话是否被清理。
