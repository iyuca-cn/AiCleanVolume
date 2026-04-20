# 侧边栏与普通态窗口行为修复 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将左栏重构为接近参考图的品牌区 + `AntdUI.Menu` 主导航 + 独立日志入口 + 底部齿轮设置入口，同时修复普通态窗口超出屏幕可用范围的问题。

**Architecture:** 保持 `MainWindow` 为单窗体入口，但将布局改为“顶部细栏 + 左侧品牌/导航栏 + 右侧内容区”。左栏使用 `AntdUI.Menu` 承担主导航，`日志` 作为独立入口，`设置` 通过底部齿轮按钮进入。窗口行为上保留 `WM_GETMINMAXINFO` 的最大化处理，并补充普通态尺寸与位置的统一约束逻辑。

**Tech Stack:** C#、.NET Framework 4.0、WinForms、AntdUI 2.3.0。

---

### Task 1: 重做窄侧边栏

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] 增加顶部细栏和左栏品牌区。
- [ ] 使用 `AntdUI.Menu` 构建主导航和日志入口分组。
- [ ] 添加底部齿轮设置入口和右边界收起按钮。
- [ ] 保留现有页面切换与页头标题更新逻辑。

### Task 2: 拆出独立日志页

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] 将日志从建议页中拆出为独立页面。
- [ ] 左栏 `日志` 入口切换到独立日志页。

### Task 3: 修复普通态窗口越界

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] 抽出普通态尺寸与位置夹紧逻辑。
- [ ] 在初始化后应用当前屏幕工作区约束。
- [ ] 在从最大化还原到普通态时重新校正窗口范围。

### Task 4: 验证

**Files:**
- No source changes beyond `src/AiCleanVolume.Desktop/MainWindow.cs`.

- [ ] 执行 `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug`。
- [ ] 如仅出现既有依赖告警，则记录不扩展处理。
