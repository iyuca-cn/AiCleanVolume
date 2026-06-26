# 原生懒加载扫描设计

## 背景

`AiCleanVolume` 当前通过 `folder-size-ranker-cli.exe` 扫描磁盘。桌面端启动外部进程，读取 CLI 的 JSON 输出，再在 C# 中解析并构建完整 `ScanSession`。这个路径带来三类开销：

- 外部进程启动和标准输出管道开销。
- 原生侧 JSON 序列化与 C# 侧 JSON 解析开销。
- C# 侧为完整目录树、文件节点、字符串和 `List<T>` 分配大量托管对象，增加内存占用和 GC 压力。

目标是让 C# 层直接使用扫描能力，同时去除 JSON 转换和全量托管树构建带来的性能与内存损耗。这个目标不能通过把 JSON 生成逻辑塞进静态库完成；JSON/table 输出应属于 `folder-size-ranker-cli.exe`。

## 目标

- `mftscan-core.lib` 只提供扫描、聚合、排序索引和查询接口，不包含 JSON/table 输出逻辑。
- `folder-size-ranker-cli.exe` 保留为命令行本体，基于 core 查询接口自行输出 table/json。
- `AiCleanVolume.exe` 不再调用 `folder-size-ranker-cli.exe`，也不再依赖额外自家 exe/native dll。
- C# 不再全量保存目录树和文件树，只保存 native session 句柄与可见 UI 节点。
- 展开目录使用懒加载，不写磁盘，不重扫整盘，不一次性 materialize 超大目录的全部子项。
- 高性能和低内存并重，避免用“临时兜底”或隐藏式外部工具替代正式架构。

## 非目标

- 不把原生 DLL 嵌入资源后运行时释放。
- 不在 `mftscan-core.lib` 中保留 JSON 输出。
- 不把扫描结果写入磁盘换取低内存。
- 不把所有节点从 native 全量复制到 C# 后再懒加载 UI。
- 不修改 `third_party` 下第三方库代码。

## 架构

整体拆成三层：

1. `mftscan-core.lib`
   原生 C 静态库，负责扫描、聚合、排序、维护紧凑 native session。它不依赖 `yyjson`，不写 `stdout`。

2. `folder-size-ranker-cli.exe`
   命令行本体，负责参数解析、帮助信息、错误输出、table/json 输出。它链接 `mftscan-core.lib`，并在 CLI 层使用 `yyjson` 输出 JSON。

3. `AiCleanVolume.exe`
   混合模式 .NET Framework 程序集。C# 源码编译为 MSIL module，C++/CLI 桥接代码静态链接 `mftscan-core.lib`，最终由 MSVC linker 链成单个 exe。运行时不再携带 `folder-size-ranker-cli.exe`。

## Native Session 模型

`mftscan-core.lib` 扫描完成后返回一个 opaque session handle：

```c
typedef struct MftscanSession MftscanSession;

MftscanError mftscan_session_scan(
    const MftscanSessionOptions *options,
    MftscanSession **session);

void mftscan_session_free(MftscanSession *session);
```

session 内部使用紧凑结构，不保存 C# 式对象树：

- `DirectoryRecord[]`：目录扁平数组。
- `FileRecord[]`：文件扁平数组。
- `uint32_t[] childDirectoryOrder`：每个目录的直接子目录索引区间。
- `uint32_t[] childFileOrder`：每个目录的直接文件索引区间。
- UTF-16 名称池：节点只存 `offset/length`。

目录记录保存：

- `id`
- `parent_id`
- `name_offset/name_length`
- `logical_bytes/allocated_bytes`
- `direct_file_count`
- `total_file_count`
- `total_directory_count`
- `child_dir_start/child_dir_count`
- `child_file_start/child_file_count`

这些数据足够支持快速查询，但不会形成嵌套对象树，也不会让 C# 为未显示节点分配对象。

## 查询接口

桥接层需要的核心接口：

```c
MftscanError mftscan_session_get_node(
    const MftscanSession *session,
    uint32_t node_id,
    MftscanNodeInfo *node);

MftscanError mftscan_session_get_children(
    const MftscanSession *session,
    uint32_t node_id,
    uint32_t start,
    uint32_t count,
    MftscanChildBuffer *children);

void mftscan_child_buffer_free(MftscanChildBuffer *children);
```

`start/count` 是 UI 懒加载窗口，不是用户可见的分页功能。用户看到的是展开目录，内部只 materialize 当前需要显示的节点。对于 20 万个直接文件的目录，首次展开只返回可见窗口或预取窗口，滚动时继续拉取后续窗口。

普通展开的复杂度接近 `O(本次返回节点数)`。排序在 session 建立时完成，展开时只切片返回，不重新排序、不重扫、不解析 JSON。

## C# 集成

当前 `IScanProvider` 边界保留。`FolderSizeRankerScanProvider` 或新的 native provider 负责：

- 创建 native session。
- 持有 `IntPtr` session handle。
- 把 root/current visible window 转成少量 `StorageItem`。
- 在 UI 展开或滚动时按需调用桥接查询接口。
- 清理 session 时释放 native handle。

当前 C# `ScanSession.Directories`、`DirectoryNodeState.DirectFiles`、完整 JSON 解析路径会被移除或仅作为旧实现参考。C# 不再维护完整文件树。

## 构建约束

混合模式程序集不能继续使用 `Any CPU`。主线先支持 `x64`：

- `mftscan-core.lib` 构建 x64 静态库。
- C++/CLI 桥接代码使用 `/clr`，静态链接 x64 core。
- C# 项目输出 MSIL module。
- MSVC linker 生成最终 `AiCleanVolume.exe`。

`net48 x64` 作为第一阶段验证目标；`net40 x64` 在构建链稳定后补齐。

## 性能预期

该设计去掉：

- 外部进程启动。
- JSON 生成。
- JSON 解析。
- 全量托管树对象分配。
- 大量未显示节点的托管字符串复制。

首次扫描仍然需要读取 MFT 或平台 API，这是核心耗时。扫描完成后的展开、滚动和局部显示应主要受“本次 materialize 的节点数量”影响，而不是整棵树规模。

## 风险

- 构建链比纯 C# 复杂，需要 MSVC/C++ Build Tools。
- UI 层需要支持懒加载窗口，不再依赖一次性完整 `Children` 列表。
- native session 生命周期必须严格由 provider 管理，避免句柄泄漏或释放后访问。
- x86/x64 不能混用，发布产物需要明确架构。
