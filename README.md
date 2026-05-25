# AI Clean Volume

基于 `.NET Framework 4.0 / 4.8 + AntdUI` 的 Windows 磁盘清理桌面工具原型。

## 已实现

- 使用 `folder-size-ranker-cli` 扫描指定盘符或目录，并以树表方式展示空间占用
- 使用 `AntdUI.PageHeader` 作为自定义标题栏
- 清理建议页同时支持 `常规清理`（本地规则）和 `AI 识别`
- 支持 `OpenAI 兼容` 接口做清理建议；未启用 AI 时自动回退到本地启发式规则
- 支持 `标准 API` 与 `2API` 两种 AI 接入模式；`2API` 按模型匹配 Cookie 发送 `X-Provider-Cookie`
- 清理列表采用类似清理软件的列表风格，默认勾选，支持双击或按钮打开对应路径
- 删除前经过沙盒评估：命中允许位置直接放行，否则要求用户确认
- 支持“完全权限模式”复选框；仅在当前进程管理员运行时真正绕过沙盒
- 支持回收站删除与永久删除切换
- 支持 `ChatGPT / OpenAI`、`DeepSeek` 接口预设，以及 AI 系统提示词预设和自定义提示词
- 支持 `appsettings.json` 持久化 AI 与沙盒配置

## 目录

- `src/AiCleanVolume.Core/Domain`：存放存储树、清理建议、沙盒评估和设置等纯领域对象
- `src/AiCleanVolume.Core/Kernel/Ports`：微内核端口，定义扫描、AI 建议、删除、资源管理器、权限和设置存储接口
- `src/AiCleanVolume.Core/Application`：扫描格式化、候选规划、本地启发式、沙盒评估和删除用例编排
- `src/AiCleanVolume.Desktop/Composition`：桌面组合根，集中装配主窗口和基础设施依赖
- `src/AiCleanVolume.Desktop/Infrastructure`：扫描 CLI、OpenAI 兼容接口、JSON 设置存储和 Windows 删除/资源管理器/权限适配
- `src/AiCleanVolume.Desktop/Presentation`：WinForms + AntdUI 主窗口、共享 UI 工具和后台操作辅助
- `third_party/folder-size-ranker-cli`：扫描 CLI
- `third_party/AntdUI-v2.3.0`：AntdUI 源码

## 运行

1. 编译：

   ```pwsh
   dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug
   ```

2. 运行 `.NET Framework 4.0` 版本：

   ```pwsh
   .\src\AiCleanVolume.Desktop\bin\Debug\net40\AiCleanVolume.exe
   ```

   运行 `.NET Framework 4.8` 版本：

   ```pwsh
   .\src\AiCleanVolume.Desktop\bin\Debug\net48\AiCleanVolume.exe
   ```

3. 如需 AI：
   - 打开右侧配置区
   - 启用 `AI`
   - 选择 `接入类型`
  - `标准 API`：填入 `接口地址 / API Key / 模型`
  - `2API`：填入 `接口地址 / 模型`，并在 `模型 Cookie` 中按 `模型=完整 Cookie` 每行配置一条映射
  - `接口地址` 兼容三种写法：根地址（如 `http://127.0.0.1:3000`）、`.../v1`、完整 `.../v1/chat/completions`
  - 点击 `保存配置`

## 说明

- 扫描 NTFS 盘时，`folder-size-ranker-cli` 可能需要管理员权限。
- 当前 OpenAI 兼容实现走 `/v1/chat/completions`。
- `2API` 模式不会发送 `Authorization`，而是根据当前模型精确匹配 `模型 Cookie` 配置并发送 `X-Provider-Cookie`。
- 项目会同时生成 `.NET Framework 4.0` 与 `.NET Framework 4.8` 两套产物；`net40` 版本使用框架自带 `HttpWebRequest`，`net48` 版本使用 `RestSharp 106.15.0`，避免继续引用存在高危漏洞告警的 `RestSharp 105.2.3`。
- 本地为兼容当前 SDK，对 `third_party/AntdUI-v2.3.0/src/AntdUI/AntdUI.csproj` 去掉了 `net10.0-windows` 目标框架。
