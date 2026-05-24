# Settings First Frame Design

## Goal

设置页和应用启动首帧必须一次成型。用户不应看到空壳、占位值、AI 配置卡片、输入框内容或磁盘摘要逐个出现、逐个变值。

## Current Problem

当前 `MainWindow` 构造阶段调用 `ApplyInitialUiPlaceholders()` 写入默认盘符和占位摘要；窗口 `Shown` 后再通过 `CompleteStartupUiBinding()` 调用 `LoadSettingsToUi()` 和 `LoadDrives()`。这会让设置页先显示默认/空状态，再在窗口已经可见后逐项绑定真实配置和 AI 配置卡片，肉眼可见地形成二次绘制。

## Recommended Approach

首帧前完成真实 UI 绑定。构造阶段创建所有 AntdUI 控件后，立即在暂停布局的状态下执行 `LoadSettingsToUi()` 和 `LoadDrives()`，让开关、输入框、下拉框、AI 配置卡片和扫描页默认盘符在第一次可见前就稳定下来。`OnShown` 只负责恢复首帧重绘、刷新一次磁盘摘要、提示词和启动日志。

这个方案不引入临时兜底，不修改第三方 AntdUI 代码，也不改变微内核服务边界。磁盘列表枚举保留在 UI 层，但只做一次批量绑定，避免暴露控件逐个更新。

## Alternatives Considered

- 延后显示设置页直到点击时再构建：可减少启动工作，但第一次进入设置页仍可能逐块出现。
- 保留占位值并加遮罩：遮罩会掩盖症状，但正式界面仍在可见后变更，不符合“一次成型”的目标。
- 当前推荐方案：首帧前真实绑定，显示后只做一次必要刷新，改动小且直接消除二次绘制来源。

## Verification

- 启动应用后首帧不出现空白设置状态或明显逐项填值。
- 进入设置页时 AI 配置卡片和设置控件已经稳定。
- 构建通过，且不修改 `third_party`。
