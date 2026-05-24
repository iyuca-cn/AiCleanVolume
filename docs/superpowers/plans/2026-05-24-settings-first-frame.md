# Settings First Frame Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让启动和设置页首帧一次成型，不暴露控件逐个出现或逐个变值。

**Architecture:** 保留现有 AntdUI 窗体和页面结构，把真实配置绑定前移到窗口可见前，并在绑定期间暂停布局和事件副作用。`OnShown` 只恢复重绘并执行一次稳定刷新，不再二次填充设置页。

**Tech Stack:** C# 7.3、.NET Framework 4.0、WinForms、AntdUI 2.3.0。

---

## File Structure

- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
  - 删除构造阶段占位 UI 绑定。
  - 新增首帧前真实绑定入口。
  - 移除不再需要的 startup UI binding 状态字段。
- Modify: `src/AiCleanVolume.Desktop/MainWindow.Windowing.cs`
  - `OnShown` 不再排队二次绑定设置页。
  - 保留一次首帧恢复和一次启动后摘要刷新。
- Modify: `src/AiCleanVolume.Desktop/MainWindow.Scan.cs`
  - 让 `LoadDrives()` 只负责批量绑定盘符和选择默认位置。
  - 避免绑定过程中触发多次摘要和提示词刷新。

## Chunk 1: 首帧前真实绑定

- [ ] 在 `MainWindow` 构造完成控件和表格列后调用新方法 `BindInitialUiBeforeFirstFrame()`。
- [ ] 新方法内部设置 `loadingStartupUi = true`，暂停窗体布局，执行 `LoadSettingsToUi()` 和 `LoadDrives()`。
- [ ] 新方法结束后恢复布局，但不强制立即重绘窗口。
- [ ] 删除 `ApplyInitialUiPlaceholders()`，避免默认值先显示再被真实值替换。

## Chunk 2: 移除 Shown 后二次绑定

- [ ] `OnShown` 保留 `QueueStartupReveal()`。
- [ ] 删除 `QueueStartupUiBinding()` 调用。
- [ ] 将 `CompleteStartupUiBinding()` 收敛为 `CompleteStartupPostShowRefresh()`，只更新磁盘摘要、提示词和日志。
- [ ] 删除 `startupUiBindingQueued` 和 `startupUiBindingCompleted` 字段。

## Chunk 3: 批量盘符绑定

- [ ] 调整 `LoadDrives()`：清空并填充 `driveSelect` / `suggestionDriveSelect` 后只设置一次默认选择和路径。
- [ ] 删除 `LoadDrives()` 内部的 `UpdateDriveSummaryForLocation()` 和 `RefreshPromptForCurrentLocation()`，统一由 post-show 刷新调用。

## Chunk 4: 验证

- [ ] 运行 `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug`。
- [ ] 运行 `git status --short`，确认没有修改第三方库。
- [ ] 人工启动检查：启动后设置页进入时应一次成型，没有明显逐项绘制。

## Commit

本计划不执行提交。只有用户明确要求时才提交，提交信息遵守项目约定。
