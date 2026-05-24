# 清理建议页提示词编辑 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 AI 提示词编辑从设置页迁移到清理建议页，通过 AntdUI 弹窗让用户在执行 AI 识别前修改和保存提示词。

**Architecture:** 保持 `AiSettings.SystemPrompt` 作为单一提示词配置来源。清理建议页新增提示词按钮和 AntdUI 模态编辑器，复用现有提示词预设、盘符范围提示和设置持久化逻辑；设置页移除原提示词编辑控件。

**Tech Stack:** C# 7.3, WinForms, AntdUI, .NET Framework net40/net48.

---

## Chunk 1: UI 字段和布局

### Task 1: 增加建议页提示词按钮

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.Layout.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.Operations.cs`

- [x] 在 `MainWindow.cs` 增加 `suggestionPromptButton` 字段。
- [x] 在 `CreateSuggestionPanel()` 中创建 `提示词` 按钮，图标使用 `EditOutlined`，点击调用提示词编辑弹窗。
- [x] 调整建议页按钮行网格，让 `提示词` 位于 `AI 识别` 与 `删除勾选` 附近。
- [x] 在 `SetBusy()` 中禁用/恢复 `suggestionPromptButton`。

## Chunk 2: 提示词弹窗行为

### Task 2: 实现提示词编辑弹窗

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.Settings.cs`

- [x] 新增 `ShowSuggestionPromptEditor()`，用 AntdUI `Modal.Config` 构建弹窗。
- [x] 弹窗内容包含 AntdUI `Select` 和 AntdUI `Input`。
- [x] 下拉选择预设时使用 `preset.BuildPrompt(GetPromptDriveRoot())` 写入编辑框。
- [x] 编辑框手动变化时复用 `SelectAiPromptPresetForPrompt` 同等匹配逻辑。
- [x] 点击保存时把编辑框内容写入 `settings.Ai.SystemPrompt`，调用 `settings.EnsureDefaults()` 并 `settingsStore.Save(settings)`。

## Chunk 3: 设置页迁移

### Task 3: 移除设置页原提示词编辑区

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.Layout.cs`
- Modify: `src/AiCleanVolume.Desktop/MainWindow.Settings.cs`

- [x] 从设置页初始化中移除 `aiPromptPresetSelect` 和 `systemPromptInput` 的控件创建。
- [x] 保留字段给弹窗逻辑或改为弹窗局部控件，避免设置页继续持有隐藏编辑区。
- [x] `LoadSettingsToUi()` 不再直接写设置页提示词输入框，但仍要同步提示词预设状态。
- [x] `SaveSettingsFromUi()` 不覆盖当前 `settings.Ai.SystemPrompt`。

## Chunk 4: 验证

### Task 4: 构建验证

**Files:**
- No source changes.

- [x] 执行 `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug`。
- [x] 如构建失败，修复编译错误后重新构建。
- [x] 检查 `git diff --stat` 和关键差异，确认没有修改第三方库。
