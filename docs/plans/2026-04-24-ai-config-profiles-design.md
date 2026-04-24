# AI 配置历史方案设计

## 目标
在 AI 设置中支持自动记录最近保存过的配置，并允许用户手动命名保存，方便在不同模型、接口和访问方式之间切换。

## 已确认需求
- 自动记录每次保存的 AI 配置。
- 自动记录名称使用“模型名 · yyyy-MM-dd HH:mm”，不要使用“最近配置”。
- 用户可以手动输入名称保存配置。
- 切换历史配置时填充 UI，用户仍通过保存设置确认写入当前配置。
- 顺便把内置 OpenAI/GPT 默认模型从 `gpt-4o-mini` 调整为 `gpt-5.4`。

## 设计
- 在 `AiSettings` 新增 `Profiles` 列表，元素为 `AiProfile`。
- `AiProfile` 保存名称、保存时间、访问模式、Endpoint、ApiKey、Model、MaxSuggestions、SystemPrompt、ModelCookieMappings。
- 保存设置时生成当前 AI 配置快照，按配置指纹去重，新增或更新到列表头部，最多保留 10 条。
- 手动保存时弹窗输入名称，同名覆盖或新增到列表头部。
- 设置页新增配置下拉与“应用配置”“保存为配置”控件。

## 文件影响
- `src/AiCleanVolume.Core/Models/ApplicationSettings.cs`：新增配置方案模型、规范化和默认模型调整。
- `src/AiCleanVolume.Desktop/MainWindow.cs`：新增 UI 控件、配置快照保存/应用逻辑、内置模型调整。
- `src/AiCleanVolume.Desktop/appsettings.json`：默认模型调整并新增空 Profiles。
