# WizTree Compact Layout Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将桌面端右侧内容区改为 WizTree 式紧凑工具布局，并让左侧导航支持 TroveKit 式展开/折叠。

**Architecture:** 保留现有 WinForms + AntdUI 主窗口、页面切换和微内核服务。只重组 `MainWindow` 中的 UI 容器和按钮归属，扫描、建议、删除、日志、设置服务逻辑不变。第三方 AntdUI 代码不修改。

**Tech Stack:** C# 7.3、.NET Framework 4.0、WinForms、AntdUI 2.3.0。

---

## File Structure

- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
  - 移除右侧全局 `titleBar` 的创建、加入和页面标题更新。
  - 将原本挂在 `titleBar` 的页面操作按钮迁移到对应页面工具条。
  - 压缩 `pageHost` 外边距。
  - 将扫描页工具卡片改为紧凑工具条。
  - 将清理建议页顶部标题说明改为紧凑工具条。
  - 保持日志页和设置页使用 AntdUI 控件，只减少顶部空白。
  - 增加侧栏折叠按钮、折叠状态和导航文字显隐逻辑。
- Modify: `src/AiCleanVolume.Core/Models/ApplicationSettings.cs` only if persistent sidebar collapsed state is needed.
  - 增加 UI 设置字段时要保持默认展开。
- Modify: `src/AiCleanVolume.Desktop/appsettings.json` only if settings model is extended.
  - 同步默认 UI 设置。

## Chunk 1: 移除右侧页面大标题栏

- [ ] 删除或停用 `titleBar` 字段的创建和 `contentHost.Controls.Add(titleBar)`。
- [ ] 从 `SetActivePage()` 移除 `titleBar.Text` 和 `titleBar.Description` 更新。
- [ ] 调整 `SuspendPageSwitchLayout()` 和 `ResumePageSwitchLayout()`，不要依赖已移除的 `titleBar`。
- [ ] 将 `pageHost.Padding` 从当前大边距缩小为紧凑边距，例如 `new Padding(8, 6, 8, 8)`。
- [ ] 保留顶部 `appBar`，只显示应用名和窗口按钮。

## Chunk 2: 迁移页面级操作按钮

- [ ] 扫描按钮继续由扫描页工具条持有，不放到全局标题栏。
- [ ] 在清理建议页顶部工具条中加入 `regularCleanButton`、`superCleanButton`、`analyzeButton`、`deleteButton`。
- [ ] 在设置页顶部或底部固定操作区中加入 `saveSettingsButton`。
- [ ] 更新 `SetActivePage()` 中按钮可见性逻辑，避免操作按钮因全局标题栏移除后失效。
- [ ] 更新 `SetBusy()`，确认忙碌态仍控制扫描、AI、常规清理、超级清理、删除、保存配置按钮。

## Chunk 3: 扫描页紧凑化

- [ ] 将 `CreateScanToolbarPanel()` 高度改为约 104 到 124 像素。
- [ ] 将 `CreateCardPanel(16)` 或明显阴影替换为浅色平面工具条容器。
- [ ] 第一行布局包含 `选择`、盘符下拉、`扫描`、`位置`、路径输入和容量摘要。
- [ ] 第二行布局包含 `最小`、`限制`、`排序`、扫描状态和进度。
- [ ] 删除大面积卡片内边距和阴影，让表格直接贴近工具条。
- [ ] 将 `CreateStoragePanel()` 改为轻边框表格容器，避免大圆角大阴影卡片。

## Chunk 4: 清理建议页紧凑化

- [ ] 删除页内 `CreateSectionTitle("清理建议")` 和描述文本。
- [ ] 组合盘符、最小值、数量限制、权限开关、全选、全不选、反选和清理操作按钮到顶部工具条。
- [ ] 将 `CreateSuggestionPanel()` 主容器改为轻边框或平面容器。
- [ ] 保持 `suggestionTable` 列定义、双击定位、按钮点击和选择逻辑不变。

## Chunk 5: 日志页和设置页顶部留白处理

- [ ] 检查 `CreateLogPanel()`，移除多余标题说明或顶部空白。
- [ ] 检查 `CreateSettingsPanel()`，去掉“设置”大标题和描述，保留配置分组。
- [ ] 缩小设置页 `Padding` 和分组间距，但不破坏现有滚动体验。
- [ ] 检查新增 AI 配置页标题区域，保留返回按钮和必要名称，不恢复全局大标题。

## Chunk 6: TroveKit 式侧栏折叠

- [ ] 新增侧栏折叠状态字段，例如 `sidebarCollapsed`。
- [ ] 新增圆形折叠按钮，放在侧栏右缘中上部。
- [ ] 更新 `ApplySidebarWidth()` 或新增方法，使折叠态宽度约 72 像素，展开态使用当前宽度。
- [ ] 折叠时隐藏品牌文字和导航文字，只保留图标。
- [ ] 展开时恢复导航文字和原宽度。
- [ ] 确认底部设置按钮在折叠态仍可点击并显示选中态。

## Chunk 7: 验证

- [ ] 执行 `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug`。
- [ ] 启动桌面程序，检查扫描页、清理建议页、日志页、设置页布局。
- [ ] 手动切换侧栏展开/折叠，确认导航和设置入口可用。
- [ ] 手动触发扫描，确认状态和进度显示正常。
- [ ] 手动进入清理建议页，确认 AI 识别、常规清理、超级清理、删除勾选按钮可用。
- [ ] 检查 `git status --short`，确认未修改 `third_party`。
- [ ] 等待用户明确要求后再提交。
