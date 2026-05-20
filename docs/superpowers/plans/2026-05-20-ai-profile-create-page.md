# AI Profile Create Page Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将设置页“新增 AI 配置”从右侧抽屉改为应用内独立页面。

**Architecture:** 继续使用 `MainWindow` 单窗体和现有 `pageHost` 多页面结构，新增一个不出现在侧边栏菜单里的配置创建页。保存逻辑复用现有 AI Profile 创建、归一化、持久化和卡片刷新能力。

**Tech Stack:** C# 7.3、.NET Framework 4.0、WinForms、AntdUI 2.3.0。

---

## File Structure

- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
  - 新增 `PageAiProfileCreate` 页面常量和页面字段。
  - 将加号按钮从打开 Drawer 改为切换到独立页面。
  - 用 AntdUI 面板实现新增 AI 配置页面布局。
  - 移除 Drawer 相关状态和打开/关闭逻辑。
  - 调整页面切换、标题、按钮可见性和忙碌态。

## Chunk 1: 页面入口和导航状态

- [ ] 添加 `PageAiProfileCreate` 常量和 `aiProfileCreatePage` 字段。
- [ ] 在主布局初始化时创建新增配置页并加入 `pageHost`。
- [ ] 将加号按钮事件改为进入新增配置页。
- [ ] 更新 `SetActivePage`、`GetPageControl`、`GetPageTitle`、`GetPageDescription`，让新增页拥有独立标题和描述。

## Chunk 2: 新增配置页面布局

- [ ] 新建页面容器，包含返回按钮、标题说明、三段配置区域和底部操作区。
- [ ] 普通字段采用两列布局，`模型 Cookie`、`系统提示词` 使用整行大输入框。
- [ ] 初始化字段时复用当前设置默认值和现有预设联动事件。

## Chunk 3: 保存和清理逻辑

- [ ] 将 `SaveAiProfileFromDrawer` 调整为页面保存方法。
- [ ] 将 `CreateAiProfileFromDrawer` 调整为从页面字段创建配置。
- [ ] 保存成功后刷新配置列表、选中新配置并返回设置页。
- [ ] 取消和返回时清空新增页临时控件引用并回到设置页。

## Chunk 4: 验证

- [ ] 执行 `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug`。
- [ ] 检查没有修改第三方库代码。
- [ ] 检查未提交计划文档和代码，等待用户明确要求提交。
