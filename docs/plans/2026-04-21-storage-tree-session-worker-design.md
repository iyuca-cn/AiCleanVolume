# 文件树会话解析与后台工作线程设计

## 背景

当前空间树已经支持会话级 JSON 索引和节点展开，但仍存在几个问题：

1. 空间树卡片顶部仍有标题和描述，用户希望只保留树本身。
2. 文件树展开链路仍带有预取缓存协调器，并且会从临时 JSON 切片再次解析文件列表。
3. 扫描和展开调度分散在 `ThreadPool` 上，用户希望改为不会阻塞 UI 的可复用后台线程。
4. `Del` 删除依赖表格焦点，当前在部分焦点状态下无效。
5. `folder-size-ranker-cli.exe` 必须从主程序 exe 同目录调用，而不是 `tools` 子目录。

## 目标

1. 空间树视图只显示树表格，不显示标题和描述。
2. 首次扫描时一次性调用同目录下的 `folder-size-ranker-cli.exe`，流式解析 JSON 并保存已解析结果。
3. 展开节点时只从当前会话的内存索引中取直接子项，不再读取临时文件、不再反序列化、不再预取缓存。
4. 扫描、展开和删除耗时操作都放到可复用后台工作线程执行，UI 线程只做控件更新。
5. 修复 `Del` 无效：窗口级捕获 Delete，当前页为扫描页且空间树有活动行时触发删除。

## 方案

### 会话数据结构

新增会话结构保存已解析结果：

- `StorageTreeSession`
  - `RootPath`
  - `TemplateKey`
  - `DirectoryIndex`
- `DirectoryNodeState`
  - `Path`
  - `Bytes`
  - `DirectFileCount`
  - `TotalFileCount`
  - `TotalDirectoryCount`
  - `DirectFiles`
  - `DirectDirectoryPaths`

扫描时构建 `path -> DirectoryNodeState` 映射。`DirectFiles` 在扫描解析时已经转换成 `StorageItem` 文件对象；目录子级通过路径索引关联。

### 扫描流程

1. UI 线程读取扫描参数并更新忙碌状态。
2. 后台工作线程调用同目录下的 `folder-size-ranker-cli.exe`。
3. 后台线程流式解析 CLI JSON，构建 `StorageTreeSession`。
4. 后台线程从会话物化根节点的一层子项。
5. UI 线程绑定根节点、更新摘要和日志。

### 展开流程

1. `ExpandChanged` 同步位置输入框为展开节点路径。
2. 如果节点已加载，直接返回。
3. 后台工作线程按路径从当前会话取 `DirectoryNodeState` 并物化直接子项。
4. UI 线程按版本号确认结果仍有效后刷新行。

展开不再调用 CLI，不读取临时文件，不解析 JSON 切片，也不走预取缓存。

### 后台线程

新增可复用后台工作队列：

- 单个后台线程循环执行任务。
- 支持投递 `Action`。
- 捕获异常并通过 UI 回调处理。
- 窗口关闭时停止。

`RunBackground` 改为使用该工作队列，避免每次创建新的 `ThreadPool` 工作项，并保证扫描/展开不会阻塞 UI。

### CLI 路径

`FolderSizeRankerScanProvider` 改为查找：

```text
AppDomain.CurrentDomain.BaseDirectory\folder-size-ranker-cli.exe
```

项目文件把第三方 CLI 复制到输出目录根部。

### Del 删除

启用 `KeyPreview` 并重写窗口级按键处理：

- 当前页必须是扫描页。
- 当前没有忙碌操作。
- 空间树存在聚焦或选中行。
- 按下 `Delete` 时调用现有文件树删除流程。

保留删除确认和根路径保护逻辑。

## 验收

1. 空间树卡片中只显示树表格。
2. 构建输出目录根部存在 `folder-size-ranker-cli.exe`。
3. 扫描时 UI 不冻结，窗口仍可响应。
4. 展开节点不再调用 CLI，也不读取临时 JSON 切片。
5. 重复展开同一节点直接使用会话中的已解析结果。
6. `Del` 在扫描页选中文件树行后有效，并且仍有删除确认。
7. `dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug` 通过。
