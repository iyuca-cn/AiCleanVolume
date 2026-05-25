# 项目架构重组设计

## 背景

项目已经形成 `AiCleanVolume.Core` / `AiCleanVolume.Desktop` 两层，并完成过一次大文件拆分；但当前 `MainWindow` 仍然承担依赖创建、页面状态、AntdUI 控件、扫描、AI 建议、删除、设置和窗口消息处理等多种职责。Core 层也还存在模型、端口、应用服务边界不够清晰的问题。

本次选择“架构升级优先”的路线：允许调整目录、命名空间、类边界和内部调用方式，但保持现有 UI 表现、用户功能、配置格式和第三方库不变。

## 目标

- 将微内核边界显式化，让 Core 只包含领域对象、端口和应用用例。
- 将 Desktop 拆成组合根、基础设施适配、Presentation feature 和共享 UI 工具。
- 让 `MainWindow` 退回单窗体外壳职责，不再直接创建和编排全部业务服务。
- 让扫描、建议、设置、AI Profile、删除等功能可以在各自 feature 内维护。
- 每个阶段都能独立编译验证，避免一次性爆炸式重构。

## 非目标

- 不修改 `third_party` 下任何第三方库代码。
- 不改变 `appsettings.json` 字段、默认值和持久化语义。
- 不改变现有 AntdUI 界面表现和用户可见工作流。
- 不引入“临时兜底”作为正式架构手段。
- 不自动 git commit，除非用户明确要求。

## 目标目录

```text
src/
  AiCleanVolume.Core/
    Domain/
      Storage/
      Cleanup/
      Sandbox/
      Settings/
    Kernel/
      Ports/
    Application/
      Scanning/
      CleanupPlanning/
      Deletion/

  AiCleanVolume.Desktop/
    Composition/
    Infrastructure/
      Scanning/
      Ai/
      Settings/
      Windows/
    Presentation/
      MainWindow/
      Features/
        Scan/
        Suggestions/
        Settings/
        Logs/
      Shared/
        Antd/
```

## Core 边界

`Core` 不引用 `Desktop`、AntdUI、WinForms、RestSharp 或任何桌面平台实现。

`Domain` 存放纯领域对象：

- `Storage`：`ScanRequest`、`StorageItem`、`ScanSortMode`
- `Cleanup`：`CleanupCandidate`、`CleanupSuggestion`、`CleanupRisk`、`CleanupStatus`、`CleanupResult`
- `Sandbox`：`SandboxEvaluation`、`SandboxAction`
- `Settings`：`ApplicationSettings`、`AiSettings`、`AiProfile`、`SandboxSettings`、`ScanSettings`、`UiSettings`

`Kernel/Ports` 存放稳定端口：

- `IScanProvider`
- `IAiCleanupAdvisor`
- `IDeletionSandbox`
- `IDeletionService`
- `IExplorerService`
- `IPrivilegeService`
- `ISettingsStore`

`Application` 存放应用用例和纯业务编排：

- `Scanning`：扫描请求工厂、容量格式化等扫描相关应用逻辑。
- `CleanupPlanning`：候选规划、配置路径清理规划、本地启发式建议。
- `Deletion`：删除工作流，集中处理沙盒评估、权限判断、删除执行和结果映射。

## Desktop 边界

`Composition` 是桌面端组合根，集中创建服务和窗口依赖。`Program.cs` 只负责启动 AntdUI/WinForms 环境并调用组合根创建主窗口。

`Infrastructure` 实现 Core 端口：

- `Scanning`：`folder-size-ranker-cli` 适配器、JSON 解析、平台 API 扫描补充、扫描会话状态。
- `Ai`：OpenAI 兼容接口适配、请求构造、响应映射、DTO、连接测试结果。
- `Settings`：JSON 设置存储实现。
- `Windows`：回收站删除、Explorer 打开、管理员权限判断。

`Presentation` 存放 AntdUI 桌面界面：

- `MainWindow`：主窗口壳、导航、窗口生命周期、页面宿主。
- `Features/Scan`：扫描页视图、状态、控制器。
- `Features/Suggestions`：建议页视图、状态、控制器。
- `Features/Settings`：设置页、AI Profile 列表和编辑页。
- `Features/Logs`：日志展示。
- `Shared`：AntdUI 控件工厂、后台操作、通知、主窗口 shell 接口。

## MainWindow 瘦身

`MainWindow` 最终只保留：

- 创建主窗口壳：标题栏、侧栏、页面宿主。
- 注册 feature 页面并处理页面切换。
- 处理窗口生命周期、尺寸、重绘和键盘入口。
- 提供 `IMainWindowShell`，让 feature 使用通知、日志、忙碌状态和 UI 调度能力。

建议新增轻量接口：

```csharp
public interface IMainWindowShell
{
    void SetBusy(bool busy, string description);
    void ShowInfo(string title, string message);
    void ShowWarning(string title, string message);
    void ShowError(string title, string message);
    void Log(string message);
}

public interface IFeaturePage
{
    string PageId { get; }
    Control View { get; }
    void OnActivated();
}
```

页面 feature 使用 `View + Controller + State` 的小型结构，不引入大型框架：

```text
ScanPageView.cs
ScanPageController.cs
ScanPageState.cs
```

设置、建议和日志 feature 采用相同模式。

## 迁移策略

1. 建立组合根和 `MainWindowDependencies`，先把服务创建从 `MainWindow` 移走。
2. 重排 Core 目录和命名空间，保持 public 类型名、行为和 JSON 结构不变。
3. 重排 Desktop Infrastructure，实现类迁到对应适配目录。
4. 将 `MainWindow` 的通用 UI 工厂和操作辅助迁到 `Presentation/Shared`。
5. 分 feature 抽离设置、扫描、建议、删除和日志，不一次性抽空 `MainWindow`。
6. 每阶段执行 Debug 编译，最终检查 diff 和源文件体量。

## 验证

每个阶段运行：

```pwsh
dotnet build .\AiCleanVolume.sln -c Debug --no-restore
```

最终补充：

```pwsh
git status --short
git diff --stat
```

预期 Debug 编译通过，变更集中在计划内目录和文档，不修改第三方库。

## 风险

- 命名空间迁移会影响大量 `using`，必须分阶段编译修正。
- `MainWindow` 私有方法之间调用密集，feature 抽离时要先迁状态和依赖，再迁事件处理。
- 删除流程涉及真实文件操作，迁移时必须保持现有沙盒、权限和确认语义。
- 设置持久化不能改变 JSON 字段名、默认值或配置文件路径。
