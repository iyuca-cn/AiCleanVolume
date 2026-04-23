# AI Clean Volume

基于 `.NET Framework 4.0 + AntdUI` 的 Windows 磁盘清理桌面工具原型。

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

- `src/AiCleanVolume.Core`：微内核接口、领域模型、候选规划、沙盒规则、本地启发式
- `src/AiCleanVolume.Desktop`：WinForms + AntdUI 界面、扫描器适配、AI 适配、设置存储
- `third_party/folder-size-ranker-cli`：扫描 CLI
- `third_party/AntdUI-v2.3.0`：AntdUI 源码

## 运行

1. 编译：

   ```pwsh
   dotnet build E:\work\ai-clean-volume\AiCleanVolume.sln -c Debug
   ```

2. 运行：

   ```pwsh
   .\src\AiCleanVolume.Desktop\bin\Debug\net40\AiCleanVolume.exe
   ```

3. 如需 AI：
   - 打开右侧配置区
   - 启用 `AI`
   - 选择 `接入类型`
   - `标准 API`：填入 `接口地址 / API Key / 模型`
   - `2API`：填入 `接口地址 / 模型`，并在 `模型 Cookie` 中按 `模型=完整 Cookie` 每行配置一条映射
   - 点击 `保存配置`

## 说明

- 扫描 NTFS 盘时，`folder-size-ranker-cli` 可能需要管理员权限。
- 当前 OpenAI 兼容实现走 `/v1/chat/completions`。
- `2API` 模式不会发送 `Authorization`，而是根据当前模型精确匹配 `模型 Cookie` 配置并发送 `X-Provider-Cookie`。
- 为满足 `.NET Framework 4.0` 约束，当前只能使用 `RestSharp 105.2.3`，构建时会出现已知漏洞告警；如果你允许把目标框架提升到 `net452+`，我建议再升级 RestSharp 版本。
- 本地为兼容当前 SDK，对 `third_party/AntdUI-v2.3.0/src/AntdUI/AntdUI.csproj` 去掉了 `net10.0-windows` 目标框架。
