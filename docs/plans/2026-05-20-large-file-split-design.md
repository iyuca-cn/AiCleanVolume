# 大文件拆分设计

## 背景

当前项目已经形成清晰的 Core / Desktop 分层，但部分文件承担了过多职责。`MainWindow.cs` 约 4189 行，集中包含 AntdUI 布局、导航、设置、AI 配置档案、扫描、清理建议、删除、窗口消息和通用控件工厂。`FolderSizeRankerScanProvider.cs`、`OpenAiCompatibleAdvisor.cs`、`ApplicationSettings.cs` 也存在模型、DTO、解析和流程代码集中在单文件的问题。

本次目标是按工程标准拆分大文件，提高可读性和后续维护效率。拆分应保持现有行为，不引入临时兜底设计，不修改第三方库，不改变持久化格式。

## 方案

采用“职责拆分，不改行为”的方式。

`MainWindow` 使用 C# `partial class` 拆分。原 `MainWindow.cs` 保留字段、构造、核心入口和少量跨职责共享常量，其余方法按职责迁移到同命名空间下的 `MainWindow.*.cs` 文件。这样可以降低重构风险，同时让每个文件只承载一个明确界面职责。

Core 模型按领域对象拆分。`ApplicationSettings.cs` 只保留根设置对象，其它设置类型迁移到独立文件，保持命名空间、类型名和 JSON 结构不变。

Desktop 服务按内部职责拆分。扫描适配器保留公共入口，把扫描会话状态、JSON 解析、平台 API 回退和路径工具拆出；AI 适配器保留公共入口，把请求辅助、响应 DTO 和连接测试结果拆出。

## 文件边界

`src/AiCleanVolume.Desktop/MainWindow.cs`：
保留 `MainWindow` 字段、构造函数、`InitializeComponent` 等入口型代码，并声明为 `partial`。

`src/AiCleanVolume.Desktop/MainWindow.Layout.cs`：
页面、侧栏、设置面板、扫描区、建议区、日志区等 AntdUI 控件创建。

`src/AiCleanVolume.Desktop/MainWindow.Navigation.cs`：
页面切换、导航菜单状态、侧栏折叠与宽度持久化。

`src/AiCleanVolume.Desktop/MainWindow.Settings.cs`：
设置加载保存、AI 接入模式、预设同步、沙盒与权限复选框状态。

`src/AiCleanVolume.Desktop/MainWindow.AiProfiles.cs`：
AI 配置档案页面、卡片、创建、应用、保存和格式化辅助。

`src/AiCleanVolume.Desktop/MainWindow.Scan.cs`：
扫描入口、扫描请求构造、树加载、展开刷新、扫描摘要和进度。

`src/AiCleanVolume.Desktop/MainWindow.Suggestions.cs`：
清理建议分析、选择操作、建议绑定、沙盒状态刷新。

`src/AiCleanVolume.Desktop/MainWindow.Deletion.cs`：
建议删除、存储树节点删除、沙盒确认、删除后树更新。

`src/AiCleanVolume.Desktop/MainWindow.Windowing.cs`：
窗口生命周期、消息处理、恢复重绘、启动首帧和 Win32 结构。

`src/AiCleanVolume.Desktop/MainWindow.UiFactory.cs`：
通用 AntdUI 控件工厂、表格表面样式、输入和按钮创建辅助。

`src/AiCleanVolume.Desktop/MainWindow.Presets.cs`：
AI 提示词预设、供应商预设及其内部类型。

`src/AiCleanVolume.Desktop/Services/FolderSizeRankerScanProvider.*.cs`：
保留扫描公共入口，拆分会话状态、JSON 解析、路径工具、平台 API 回退和 CLI 参数构造。

`src/AiCleanVolume.Desktop/Services/OpenAiCompatibleAdvisor.*.cs`：
保留 AI 分析与连接测试入口，拆分请求构造、响应映射、DTO 和连接测试结果。

`src/AiCleanVolume.Core/Models/*.cs`：
按 `ApplicationSettings`、`AiSettings`、`AiProfile`、`SandboxSettings`、`ScanSettings`、`UiSettings` 拆分模型文件。

## 约束

- 不修改 `third_party` 下任何第三方库代码。
- 界面仍全部使用 AntdUI。
- 不改变配置 JSON 字段和默认值语义。
- 不改变扫描、AI 建议、沙盒删除和窗口行为。
- 不引入“兜底设计”作为正式架构手段。
- 计划文档和设计文档写入后不自动 git 提交。

## 验证

先记录当前构建基线。拆分后运行：

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

预期构建通过。现有 RestSharp 漏洞告警和 AntdUI 第三方告警属于拆分前既有输出，不在本次变更中处理。

## 风险

`MainWindow` 私有方法之间调用密集，拆分时需要确保全部 partial 文件处于同一命名空间和同一类名下，避免访问级别变化。

嵌套类型迁移时若改成顶层类型，应优先保持 `private` 嵌套或同文件 partial 内部类型，减少公开面变化。

服务拆分时不要改变异常消息、缓存会话生命周期、路径比较规则或 AI 响应映射规则。
