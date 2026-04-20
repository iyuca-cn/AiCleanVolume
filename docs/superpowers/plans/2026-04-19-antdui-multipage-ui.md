# AntdUI 多页面界面 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将单页桌面界面改为 Ant Design 风格的侧边栏多页面，并修复最大化超出屏幕。

**Architecture:** 保持 `MainWindow` 作为单窗体入口，使用 `AntdUI.PageHeader` 作为标题栏，左侧 `AntdUI.Menu` 负责页面切换，右侧 `Panel` 承载扫描、AI 建议与设置三个页面。业务服务与表格绑定逻辑不变，只调整控件归属和页面显示。最大化通过 `WM_GETMINMAXINFO` 约束到当前屏幕工作区。

**Tech Stack:** .NET Framework 4.0、WinForms、AntdUI 2.3.0。

---

### Task 1: 重构 MainWindow 布局

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] 添加侧边栏菜单字段、页面容器字段、页面常量。
- [ ] 将扫描控件放入扫描页。
- [ ] 将建议表和日志放入 AI 建议页。
- [ ] 将 AI/沙盒配置放入设置页。
- [ ] 通过菜单切换页面可见性，并更新页头说明。

### Task 2: 修复最大化

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] 重写 `WndProc` 处理 `WM_GETMINMAXINFO`。
- [ ] 使用当前屏幕 `WorkingArea` 设置最大化位置和大小。

### Task 3: 验证

**Files:**
- No source changes beyond MainWindow.

- [ ] 执行 `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug`。
- [ ] 如存在无关 NuGet 漏洞告警，只记录不扩展修复。
