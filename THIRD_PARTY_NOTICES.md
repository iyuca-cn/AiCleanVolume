# Third-Party Notices

本文件用于归档本仓库及其发布产物涉及的第三方依赖与许可证信息，便于满足常见的 MIT / Apache 2.0 许可证保留义务。

项目自身采用 MIT 许可证；第三方组件仍分别适用其各自许可证。

## 随仓库或程序分发的第三方组件

| 组件 | 版本 / 快照 | 用途 | 来源 | 许可证 | 本仓库中的许可证文件 |
| --- | --- | --- | --- | --- | --- |
| AntdUI | 2.3.0 | WinForms UI 组件库，编译产出 `AntdUI.dll` | https://github.com/AntdUI/AntdUI | Apache License 2.0 | `third_party/AntdUI-v2.3.0/LICENSE` |
| folder-size-ranker-cli | vendored executable snapshot | 扫描磁盘目录大小的 CLI，构建后复制到桌面程序输出目录 | https://github.com/iyuca-cn/folder-size-ranker-cli | MIT | `third_party/folder-size-ranker-cli/LICENSE` |
| yyjson | vendored in `folder-size-ranker-cli` | `folder-size-ranker-cli` 的 JSON 输出依赖 | https://github.com/ibireme/yyjson | MIT | `third_party/folder-size-ranker-cli/YYJSON-LICENSE` |
| Newtonsoft.Json | 13.0.3 | 运行时 JSON 序列化 / 反序列化 | https://www.newtonsoft.com/json | MIT | `licenses/Newtonsoft.Json-13.0.3-LICENSE.txt` |
| RestSharp | 105.2.3 | 运行时 HTTP / REST 客户端 | https://github.com/restsharp/RestSharp | Apache License 2.0 | `licenses/RestSharp-105.2.3-LICENSE.txt` |

## 仅构建期依赖

| 组件 | 版本 | 用途 | 来源 | 许可证 | 本仓库中的许可证文件 |
| --- | --- | --- | --- | --- | --- |
| Microsoft.NETFramework.ReferenceAssemblies.net40 | 1.0.3 | 仅构建期引用，`PrivateAssets="all"`，不随应用运行时再次分发 | https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies.net40/1.0.3 | MIT | `licenses/Microsoft.NETFramework.ReferenceAssemblies.net40-1.0.3-LICENSE.txt` |

## 分发要求

- 分发源代码或二进制时，请一并保留本文件与上表列出的许可证文件。
- Apache 2.0 组件除许可证文本外，如上游后续增加 `NOTICE` 文件，也应一并保留。
- `folder-size-ranker-cli.exe` 在其上游仓库中静态纳入了 `yyjson` 源码实现，因此单独分发该 exe 时，也应一并分发 `YYJSON-LICENSE`。
- 本文件是归档索引，不替代各上游项目的原始许可证文本；具体权利义务以上游许可证原文为准。

## 核对依据

- `src/AiCleanVolume.Core/AiCleanVolume.Core.csproj`
- `src/AiCleanVolume.Desktop/AiCleanVolume.Desktop.csproj`
- `third_party/AntdUI-v2.3.0/LICENSE`
- `third_party/folder-size-ranker-cli/README.md`
- 本机 NuGet 缓存中的 `*.nuspec` 与许可证文件
- `folder-size-ranker-cli` 上游仓库中的 `LICENSE` 与 `third_party/yyjson/LICENSE`
