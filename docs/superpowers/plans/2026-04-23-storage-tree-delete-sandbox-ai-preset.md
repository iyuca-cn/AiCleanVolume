# Storage Tree Delete Sandbox AI Preset Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复扫描树删除和展开交互，升级删除确认框，并为设置页增加 AI 提示词预设。

**Architecture:** 保持现有 WinForms + AntdUI 单窗体架构，集中修改 `MainWindow` 的交互逻辑和设置绑定。树删除只更新现有 `StorageItem` / `StorageEntryRow` 对象，设置去重放在模型默认值与 UI 保存入口中。

**Tech Stack:** .NET Framework 4.0、WinForms、AntdUI、C# 7.3。

---

## Chunk 1: 扫描树删除与展开

### Task 1: 保留文件树展开状态

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Modify: `src/AiCleanVolume.Desktop/ViewModels/StorageEntryRow.cs`

- [ ] **Step 1: 增加展开路径跟踪**

在 `MainWindow` 中维护已展开路径集合，`StorageTable_ExpandChanged` 展开时添加路径，收起时移除路径。

- [ ] **Step 2: 删除后只更新当前树**

替换 `RemoveDeletedStorageRow` 的 `RebindStorageTree()` 路径，只移除当前 `StorageEntryRow` 和对应 `StorageItem`，然后刷新表格。

- [ ] **Step 3: 增加轻量行刷新**

在 `StorageEntryRow` 中增加只刷新显示列、不重建子节点的方法，用于更新祖先大小与统计。

## Chunk 2: 删除确认与打开行为

### Task 2: AntdUI 删除确认框

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: 替换 MessageBox 确认**

将文件树删除确认改为 `AntdUI.Modal`，正文展示确认文案、路径和大小。

- [ ] **Step 2: 增加资源管理器菜单**

右键菜单新增“在文件资源管理器打开”，目录打开目录，文件定位文件。

- [ ] **Step 3: 双击复用展开逻辑**

目录双击调用 `storageTable.Expand(row, nextState)`，文件双击保持资源管理器打开。

## Chunk 3: 设置去重与 AI 预设

### Task 3: 设置页体验

**Files:**
- Modify: `src/AiCleanVolume.Core/Models/ApplicationSettings.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Modify: `README.md`

- [ ] **Step 1: 规范化允许位置**

在 `SandboxSettings` 中集中规范化和去重允许位置，保存 UI 文本时也使用该逻辑。

- [ ] **Step 2: 添加 AI 预设和提示词输入**

设置页新增预设下拉和系统提示词输入框，内置保守、标准、激进缓存、开发环境、系统临时、日志优先等模板。

- [ ] **Step 3: 构建验证**

Run: `dotnet build .\AiCleanVolume.sln -c Debug`
Expected: 构建成功；允许既有第三方库警告和 RestSharp 漏洞警告。
