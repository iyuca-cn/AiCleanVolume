# 建议项删除操作与 WinAPI 删除优化设计

## 目标
在常规清理与 AI 建议列表中同时保留“查看”和“删除”操作，并将删除实现从 Shell/VisualBasic 删除改为直接 WinAPI 删除以提升速度。

## 需求
- 建议列表操作列保留“查看”，新增“删除”。
- 删除文件直接调用 WinAPI `DeleteFile`。
- 删除文件夹递归删除内容后调用 WinAPI `RemoveDirectory`。
- UI 交互统一使用 AntdUI 的 Modal/按钮样式。
- 批量删除继续走后台线程，避免阻塞 UI。

## 设计
- `CleanupSuggestionRow` 的 `actions` 改为两个 `CellButton`：`open` 和 `delete`。
- `SuggestionTable_CellButtonClick` 根据按钮 key 分流：查看打开资源管理器，删除执行单项删除。
- 抽取共享确认与删除执行逻辑，批量删除和单项删除复用。
- `RecycleBinDeletionService` 改为 WinAPI 删除服务：文件调用 `DeleteFileW`，目录递归调用 `DeleteFileW` 删除文件，再调用 `RemoveDirectoryW` 删除目录。
- 使用 `GetLastWin32Error` 转换错误信息，保留失败状态显示。

## 文件影响
- `src/AiCleanVolume.Desktop/ViewModels/CleanupSuggestionRow.cs`
- `src/AiCleanVolume.Desktop/MainWindow.cs`
- `src/AiCleanVolume.Desktop/Services/RecycleBinDeletionService.cs`
