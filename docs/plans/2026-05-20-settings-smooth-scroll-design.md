# 设置页平滑滚动设计

## 背景

设置页和新增 AI 配置页都使用 `AntdUI.StackPanel` 作为纵向滚动容器。当前滚轮事件直接按 `MouseEventArgs.Delta` 写入 `ScrollBar.ValueY`，常见鼠标一次滚动会跳 120 像素，页面内容又以较高表单区块为主，用户会感到滚动一顿一顿。

## 目标

优化设置页和新增 AI 配置页的鼠标滚轮体感，让页面滚动从大幅跳动变成更细的即时滚动。修复只影响这两个表单滚动页，不改变扫描结果表格、清理建议表格和第三方 AntdUI 源码。

## 方案

- 在桌面项目内新增 `SmoothScrollStackPanel`，继承 `AntdUI.StackPanel`。
- 拦截纵向滚轮事件，把 AntdUI 默认的 120 像素位移改为更细的 50 像素位移。
- 滚轮事件同步写入 `ScrollBar.ValueY`，避免动画追赶造成延迟。
- 写入滚动值期间通过 `WM_SETREDRAW` 暂停滚动宿主重绘，布局更新完成后只刷新一次。
- 若没有纵向滚动条或滚轮事件不能处理，则回到基类默认行为。
- 将 `MainWindow.CreateVerticalScrollPanel()` 返回值替换为 `SmoothScrollStackPanel`，让设置页和新增 AI 配置页复用更细的跟手滚动能力。

## 数据流

滚轮输入进入 `SmoothScrollStackPanel.OnMouseWheel` 后，根据当前 `ScrollBar.ValueY` 计算下一帧位置，并立即写回 AntdUI 自带滚动条。滚动条仍负责边界裁剪、重绘和布局通知。

## 错误处理

当控件尚未显示滚动条、滚动条不可用或滚动位置已到达边界时，交由原有逻辑处理或保持静默。不会引入回退架构，也不会修改第三方库。

## 验证

执行 Debug 编译，确认新增控件和 `MainWindow` 引用可编译。人工验证设置页和新增 AI 配置页鼠标滚轮滚动更跟手，扫描结果和清理建议表格滚动行为不变。
