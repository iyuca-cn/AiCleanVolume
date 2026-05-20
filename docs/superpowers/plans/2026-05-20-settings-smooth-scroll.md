# Settings Smooth Scroll Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 改善设置页和新增 AI 配置页鼠标滚轮滚动的卡顿体感。

**Architecture:** 在桌面项目内新增一个专用 `SmoothScrollStackPanel`，继承 AntdUI 的 `StackPanel` 并仅重写滚轮处理。页面仍使用 AntdUI 控件和原有布局，滚动位置继续交给 AntdUI `ScrollBar` 管理，避免修改第三方库和动画延迟。

**Tech Stack:** C# 7.3、.NET Framework 4.0、WinForms、AntdUI 2.3.0。

---

## File Structure

- Create: `src/AiCleanVolume.Desktop/Controls/SmoothScrollStackPanel.cs`
  - 封装 50 像素步长的跟手式滚轮逻辑。
  - 同步更新 `ScrollBar.ValueY`，避免滚轮动画造成延迟。
  - 滚动布局期间暂停宿主重绘，降低滚轮刷新闪烁。
  - 保留 AntdUI `ScrollBar` 的边界裁剪和重绘行为。
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
  - 引入 `AiCleanVolume.Desktop.Controls` 命名空间。
  - 将 `CreateVerticalScrollPanel()` 返回的滚动宿主替换为 `SmoothScrollStackPanel`。

## Chunk 1: 新增平滑滚动控件

- [ ] 创建 `SmoothScrollStackPanel.cs`。
- [ ] 继承 `AntdUI.StackPanel`。
- [ ] 重写 `OnMouseWheel`，仅在纵向滚动条可用时处理滚轮。
- [ ] 将滚轮位移按更细步长同步写入 `ScrollBar.ValueY`。

## Chunk 2: 接入设置页滚动容器

- [ ] 在 `MainWindow.cs` 添加控件命名空间。
- [ ] 修改 `CreateVerticalScrollPanel()` 使用 `SmoothScrollStackPanel`。
- [ ] 确认返回类型保持 `AntdUI.StackPanel`，减少调用点改动。

## Chunk 3: 验证

- [ ] 执行 `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug`。
- [ ] 检查 `git status --short`，确认没有修改第三方库。
- [ ] 等待用户明确要求后再提交。
