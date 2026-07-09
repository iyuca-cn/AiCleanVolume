# WebView2 直接复用设计稿 UI

## 背景

WinForms 逐控件模仿设计稿只能形似。设计稿（`src/AiCleanVolume.Desktop/WebUi/index.html`）
是 claude.ai/design 的 x-dc 组件：HTML 模板（sc-if/sc-for/{{ }}）+ `DCLogic` 组件类，
由 `support.js`（dc-runtime，依赖 window.React/ReactDOM，已本地化 UMD）渲染。
改用 WebView2 承载该页面，像素级还原；C# 退为后端服务。

## 架构

- **窗体**：`WebShellWindow : Form`，FormBorderStyle.None，WebView2 Dock=Fill。
  `SetVirtualHostNameToFolderMapping("app.local", <输出目录>\WebUi, Allow)`，
  导航 `https://app.local/index.html`。
- **前端**：index.html 头部依次加载 react / react-dom UMD、support.js。
  DCLogic 组件保留模板与 renderVals 结构，state 改由桥驱动。
- **桥协议**：JS→C# `window.chrome.webview.postMessage({id, method, params})`；
  C# 处理完 `PostWebMessageAsJson({id, ok, result|error})`；
  C# 主动推送 `{event, data}`（扫描进度、删除进度、AI 流式完成等）。
- **后端服务复用**：DesktopCompositionRoot 全套（scanProvider / aiAdvisor(含 IAiChatService) /
  deletionWorkflow / settingsStore / explorerService / 各 planner）。

## 桥方法

| method | params | result |
|---|---|---|
| window.minimize / maximize / close / dragMove | - | - (dragMove 用 ReleaseCapture + WM_NCLBUTTONDOWN) |
| env.info | - | { elevated, version, drives:[{name,fs,used,total,scanned}] } |
| env.restartElevated / env.openPath | path | - |
| scan.start | { location, sortMode } | 完成事件 scan.done → { rootNodeId, path, bytes, files, dirs }；失败 scan.error |
| scan.children | { nodeId, start, count } | { total, items:[{nodeId,name,path,bytes,isDir,files,hasChildren}] } |
| ai.chat | { messages } | { content, tokens } （错误走 error） |
| ai.report | { } | 后端组目录摘要→严格 JSON 报告 { safeBytes, confirmBytes, systemBytes, summary, classified } |
| suggest.analyze | { location } | { items:[{path,name,bytes,risk,source,reason,sandbox}] } |
| del.run | { paths, useRecycleBin } | 逐项评估+删除；进度事件 del.progress，完成 { results:[{path,ok,message}] } |
| settings.get / settings.save / settings.testAi | 按 ApplicationSettings JSON 子集 | - |

## 构建

- TargetFrameworks 收敛为 net48（net40 随 WinForms UI 一并移除）。
- `Microsoft.Web.WebView2` NuGet（net48 兼容版本）。
- `WebUi\**` 为 Content + PreserveNewest。
- Presentation/MainWindow 全部 partial、Features、AntdUI 引用在前端接线完成后删除。

## 分工

- 阶段 A：宿主 + 桥 + 构建收敛，设计稿以演示数据先跑通（support.js 渲染链路验证）。
- 阶段 B：DCLogic 假数据换桥数据（树懒加载 nodeId 映射、聊天、报告、推荐、设置、删除确认双阶段）。
- 阶段 C：删除 WinForms UI 与 AntdUI 引用，回归。
