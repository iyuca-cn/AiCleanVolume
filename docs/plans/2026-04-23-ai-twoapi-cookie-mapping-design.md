# AI 2API Cookie 映射设计

## 背景

当前桌面端只支持 OpenAI 兼容的标准 API 鉴权：配置接口地址、API Key 和模型后，请求 `/v1/chat/completions` 并发送 `Authorization: Bearer <key>`。

本次新增 `2API` 通道，用于对接本地 `ai2api` 网关。`2API` 仍使用 OpenAI 兼容的 `/v1/chat/completions` 响应格式，但鉴权改为按当前模型匹配 Cookie，并通过 `X-Provider-Cookie` 请求头发送。

## 目标

- AI 设置支持 `标准 API` 和 `2API` 两种接入类型。
- `标准 API` 继续使用现有 `API Key`。
- `2API` 使用“模型=Cookie”映射替代 `API Key`。
- 模型名不限定 provider，例如 `yuanbao/deep_seek_v3` 或 `other-provider/model-a` 都可使用。
- 旧配置默认保持 `标准 API`，现有用户无需迁移。

## 配置模型

在 `AiSettings` 中新增：

- `AccessMode`：字符串，默认 `standard_api`。
- `ModelCookieMappings`：结构化数组，每项包含：
  - `Model`
  - `Cookie`

保留现有字段：

- `Endpoint`
- `ApiKey`
- `Model`
- `MaxSuggestions`
- `SystemPrompt`

## 设置页

在现有设置页新增：

- `接入类型` 下拉框：`标准 API` / `2API`。
- `模型 Cookie` 多行输入框，每行一条映射：

```text
provider/model=完整 Cookie 字符串
```

示例：

```text
yuanbao/deep_seek_v3=pgv_pvid=xxx; hy_user=yyy; token=zzz
other-provider/model-a=session=abc; token=def
```

Cookie 本身是一整串字符串，不支持跨行 Cookie。重复模型以后面的配置为准。

## 请求行为

- `标准 API`：
  - 请求路径保持 `/v1/chat/completions`。
  - 发送 `Authorization: Bearer <ApiKey>`。
- `2API`：
  - 请求路径保持 `/v1/chat/completions`。
  - 不发送 `Authorization`。
  - 用当前 `Model` 精确匹配 `ModelCookieMappings`。
  - 命中后发送 `X-Provider-Cookie: <Cookie>`。
  - 未命中时回退本地规则，不记录 Cookie 内容。

## 兼容与安全

- 旧版 `appsettings.json` 没有 `AccessMode` 时默认 `standard_api`。
- 旧版 `ApiKey` 保留，不因切换 `2API` 被清空。
- Cookie 会持久化到 `appsettings.json`，但不会写入日志。
- 请求失败、模型无 Cookie、响应不可解析时沿用现有本地回退策略。
