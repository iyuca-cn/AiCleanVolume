# folder-size-ranker-cli

`folder-size-ranker-cli` 是一个 Windows 命令行工具，用 C 语言实现，用于统计文件夹大小。NTFS 卷会直接读取 MFT，其他文件系统会降级为平台系统 API 扫描。

它的目标是做类似 WizTree 的快速统计。默认输出“没有子文件夹的文件夹”，也就是叶子目录，并按大小从大到小排序；指定 `--all` 时会输出指定位置下所有层级目录的 JSON 树。

## 作用

- NTFS 卷直接读取目标所在盘符的 MFT，不递归遍历目录。
- 非 NTFS 卷使用 Win32 平台 API 降级扫描，通过显式栈迭代遍历目录树，不使用递归函数。
- 统计叶子目录的文件大小。
- 支持输出指定位置下所有层级目录的紧凑 JSON 树。
- 支持按逻辑大小或分配大小排序。
- 支持设置最小输出大小。
- 支持表格输出和 JSON 输出。

## 统计口径

默认模式只输出叶子目录：

- 叶子目录：目录下面没有任何子文件夹。
- 非叶子目录：目录下面还有子文件夹，不会出现在输出结果里。
- 目录大小：该叶子目录直属文件的大小总和。
- 硬链接：按文件 MFT 记录去重，同一个文件实体只统计一次。

指定 `--all` 时：

- 输出根节点为 `--location` 指定的目录或盘符根目录。
- `files` 包含当前目录下的直接文件。
- `children` 包含直接子目录，子目录下继续递归包含更深层目录。
- 每个目录的 `bytes` 是该目录自身及所有后代目录中文件大小的递归汇总。
- 每个文件的 `bytes` 是该文件自身大小。
- 每一层 `files` 都按 `bytes` 从大到小排序。
- 每一层 `children` 都按 `bytes` 从大到小排序。
- `--min-size` 会过滤每层中小于指定大小的文件和子目录；根节点始终输出。
- `--limit` 表示每层最多输出多少个直接文件，以及多少个直接子目录。
- `--all` 固定输出紧凑 JSON，不接受 `--format`。

输出结果始终只有一个大小字段：

- 表格输出列名固定为 `Bytes`
- JSON 输出字段固定为 `bytes`
- 这个字段的含义由 `--sort` 决定：
  - `--sort logical` 时表示逻辑大小
  - `--sort allocated` 时表示分配大小

## 运行要求

- Windows
- NTFS 卷使用 MFT 快路径；其他文件系统使用平台 API 降级路径。
- NTFS MFT 快路径需要管理员权限；平台 API 降级路径不做管理员前置要求。
- 支持 x86 / x64 构建

程序清单 `app.manifest` 使用 `asInvoker`。扫描 NTFS 卷时，如果当前进程未提权，程序会提示需要管理员权限；扫描非 NTFS 卷时会直接使用平台 API 降级路径。

## 构建

建议在 **Visual Studio Developer PowerShell** 或 **Developer Command Prompt** 中构建：

```powershell
msbuild .\folder-size-ranker-cli.vcxproj /t:Build /p:Configuration=Release /p:Platform=x64
```

```powershell
msbuild .\folder-size-ranker-cli.vcxproj /t:Build /p:Configuration=Release /p:Platform=Win32
```

构建产物：

```text
x64\Release\folder-size-ranker-cli.exe
x86\Release\folder-size-ranker-cli.exe
```

如果当前终端没有 `msbuild`，请先打开 Visual Studio 自带的开发者终端再执行。

Release 配置显式静态链接 CRT，因此发版产物保持为单个 exe 文件。

## CLI 用法

```text
folder-size-ranker-cli.exe --location <path> --sort <logical|allocated> [--min-size expr] [--format <table|json>] [--limit N]
folder-size-ranker-cli.exe --location <path> --sort <logical|allocated> --all [--min-size expr] [--limit N]
```

参数说明：

| 参数 | 必填 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `--location <path>` | 是 | 无 | 指定扫描位置，可以是 `C:`、`C:\` 或某个文件夹 |
| `--sort <logical|allocated>` | 是 | 无 | 指定排序字段 |
| `--min-size <expr>` | 否 | `0` | 只输出大小大于等于该值的目录，支持表达式 |
| `--format <table|json>` | 否 | `table` | 输出表格或 JSON |
| `--limit <N>` | 否 | 不限制 | 只输出前 N 条 |
| `--all` | 否 | 关闭 | 输出所有层级目录的紧凑 JSON 树 |
| `--help` | 否 | 无 | 显示帮助 |

说明：

- `--location` 指向 NTFS 子目录时，程序仍会扫描整卷 MFT，再只输出该目录子树内的叶子目录。
- `--location` 指向非 NTFS 子目录时，程序仍走平台 API 降级路径，并在输出阶段按该目录子树过滤。
- `--volume` 已废弃，不再支持。
- `--all` 与 `--format` 互斥；指定 `--all` 时输出固定为紧凑 JSON。
- 默认模式下 `--limit` 表示输出前 N 条；`--all` 模式下 `--limit` 表示每层最多输出 N 个直接文件，以及 N 个直接子目录。

`--min-size` 使用的字段和 `--sort` 一致：

- `--sort logical`：按逻辑大小过滤和排序。
- `--sort allocated`：按分配大小过滤和排序。

`--min-size` 支持：

- `+ - * /`
- 括号 `()`
- 小数
- 空格

最终结果会**向上取整**为字节数，例如：

- `10/3` => `4`
- `1.5*1024*1024` => `1572864`

如果表达式里包含空格或括号，建议加引号，例如：

```powershell
.\x64\Release\folder-size-ranker-cli.exe --location C: --sort logical --min-size "(1.5 + 0.5) * 1024 * 1024"
```

## 示例

输出 `C:` 盘最大的 20 个叶子目录，按分配大小排序：

```powershell
.\x64\Release\folder-size-ranker-cli.exe --location C: --sort allocated --format table --limit 20
```

输出 `C:` 盘逻辑大小至少 1 GiB 的叶子目录：

```powershell
.\x64\Release\folder-size-ranker-cli.exe --location C: --sort logical --min-size 1073741824 --format table
```

使用表达式指定最小值：

```powershell
.\x64\Release\folder-size-ranker-cli.exe --location C: --sort allocated --min-size "(1.5+0.5)*1024*1024" --format table
```

输出 JSON，方便脚本处理：

```powershell
.\x64\Release\folder-size-ranker-cli.exe --location C: --sort allocated --min-size 104857600 --format json --limit 50
```

只看某个子目录：

```powershell
.\x64\Release\folder-size-ranker-cli.exe --location C:\Windows --sort allocated --format table --limit 20
```

输出 `C:\Users` 下所有层级目录与直接文件的紧凑 JSON 树，每层最多 10 个直接文件和 10 个直接子目录：

```powershell
.\x64\Release\folder-size-ranker-cli.exe --location C:\Users --sort allocated --all --min-size 104857600 --limit 10
```

## 表格输出

表格输出包含两列：

```text
Bytes                Path
1416950032           C:\Windows\System32\DriverStore\FileRepository\...
```

## JSON 输出

JSON 输出为 UTF-8，结构如下：

```json
{
  "volume": "C:",
  "location": "C:\\Windows",
  "sort_by": "allocated",
  "min_size": 104857600,
  "total_leaf_dirs": 2,
  "items": [
    {
      "path": "C:\\Example\\LeafFolder",
      "bytes": 123469824
    }
  ]
}
```

字段说明：

- `volume`：程序内部实际扫描的盘符。
- `location`：用户请求的扫描位置。
- `sort_by`：排序字段。
- `min_size`：最小大小过滤值。
- `total_leaf_dirs`：输出结果条数。
- `items`：已经按指定字段降序排序的叶子目录列表，每项只包含 `bytes` 和 `path`。

## `--all` JSON 输出

`--all` 输出为紧凑 JSON，结构如下：

```json
{"path":"C:\\Users","bytes":123469824,"files":[{"path":"C:\\Users\\desktop.ini","bytes":174}],"children":[{"path":"C:\\Users\\Alice","bytes":123469824,"files":[],"children":[]}]}
```

字段说明：

- `path`：当前目录路径。
- `bytes`：目录节点表示当前目录自身及所有后代目录中文件大小的递归汇总；文件节点表示单文件大小，字段含义都由 `--sort` 决定。
- `files`：当前目录的直接文件，已按 `bytes` 降序排序。
- `children`：当前目录的直接子目录，已按 `bytes` 降序排序，并递归包含更深层目录。

## 注意事项

- NTFS 快路径读取底层结构，必须管理员权限运行。
- 非 NTFS 降级路径使用平台 API 迭代遍历，速度通常慢于 NTFS MFT 快路径。
- 非 NTFS 降级路径会跳过目录型 reparse point，避免 junction、符号链接等导致循环或重复扫描。
- 当前一次只会解析一个 `--location`，其所属位置必须位于类似 `C:` 的本地盘符上。
- 若部分本地虚拟盘 / RamDisk 对文件夹路径返回异常卷根，程序会回退到所属盘符继续扫描；真正挂载到文件夹的卷仍不支持。
- 当前只输出叶子目录，不输出父目录汇总。
- 当前第一版优先保证正确性和结构清晰，后续可以继续优化性能和输出格式。

## GitHub Actions

仓库配置 GitHub Actions 后：

- 普通 `push` / `pull_request` 会自动执行 Windows 编译检查
- 推送 `v*` annotated tag 会自动创建 GitHub Release
- Release 会上传由 Action 构建出的：
  - `folder-size-ranker-cli-x86.exe`
  - `folder-size-ranker-cli-x64.exe`
- GitHub Release 正文严格使用 annotated tag 的完整多行描述
- 如果使用 lightweight tag，Release 工作流会直接失败

推荐这样发版：

```powershell
git tag -a v1.0.0
git push origin v1.0.0
```

然后在 Git 打开的编辑器里写多行版本说明。
