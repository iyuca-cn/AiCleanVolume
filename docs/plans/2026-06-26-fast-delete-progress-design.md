# 极速删除状态显示设计

## 目标

文件夹删除必须更快，并且删除过程中能看到当前正在处理的路径。

## 约束

- 禁止使用递归函数删除目录。
- 不做删除前统计，不计算百分比，不为了进度额外扫描磁盘。
- 删除线程不调用 UI，不等待 UI，不被 UI 刷新频率拖慢。
- 保持现有 WinAPI 删除、长路径、重解析点保护和 best-effort 删除语义。

## 设计

删除服务使用显式栈迭代处理目录。遍历阶段删除文件并记录待删除目录，目录本身在后序阶段删除，避免递归调用。重解析点目录只删除链接本身，不进入目标。

进度只表示“当前正在处理的路径”。删除线程把最新路径写入一个轻量状态对象，不触发 UI 调度；UI 使用高频 WinForms Timer 主动读取最新路径并刷新 AntdUI 状态文本。UI 卡顿只会影响显示实时性，不影响删除速度。

建议页批量删除显示当前建议项和当前路径；扫描页文件树删除显示当前路径。删除完成后恢复原页面状态。

## 影响范围

- `src/AiCleanVolume.Core/Kernel/Ports/IDeletionService.cs`
- `src/AiCleanVolume.Core/Application/Deletion/CleanupDeletionWorkflow.cs`
- 新增删除进度状态模型。
- `src/AiCleanVolume.Desktop/Infrastructure/Windows/RecycleBinDeletionService.cs`
- `src/AiCleanVolume.Desktop/Presentation/MainWindow/MainWindow.Deletion.cs`
- `src/AiCleanVolume.Desktop/Presentation/MainWindow/MainWindow.Operations.cs`
- 需要时微调建议页 AntdUI 状态显示。

## 验证

- 删除深层目录时无递归函数调用。
- 删除过程中状态文本持续显示当前路径。
- 删除线程不直接调用 UI。
- 构建通过。
