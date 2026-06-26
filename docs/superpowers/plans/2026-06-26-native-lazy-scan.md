# Native Lazy Scan Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 `AiCleanVolume` 从调用 `folder-size-ranker-cli.exe` 改为单进程混合模式集成，并通过 native lazy session 去除 JSON 转换和全量托管树内存开销。

**Architecture:** `mftscan-core.lib` 只保留扫描、聚合、排序索引和查询接口；CLI 链接 core 后自行负责 table/json 输出；`AiCleanVolume.exe` 通过 C++/CLI 静态链接 core，并让 C# provider 只持有 native session 句柄和可见 UI 节点。

**Tech Stack:** C17、MSVC v143、MSBuild、C++/CLI `/clr`、.NET Framework 4.8/4.0、C# 7.3、WinForms、AntdUI。

---

## Chunk 1: 原生 core 边界

### Task 1: 拆分 `mftscan-core.lib`

**Files:**
- Create: `E:\work\mft\mftscan-core.vcxproj`
- Create: `E:\work\mft\mftscan-core.vcxproj.filters`
- Modify: `E:\work\mft\folder-size-ranker-cli.vcxproj`
- Modify: `E:\work\mft\folder-size-ranker-cli.vcxproj.filters`
- Modify: `E:\work\mft\folder-size-ranker-cli.sln`

- [ ] **Step 1: 建立静态库项目**

  创建 `mftscan-core.vcxproj`，配置 `ConfigurationType=StaticLibrary`，保留 Win32/x64 Debug/Release 四组配置，沿用现有 include、C17、`CompileAsC`、`WIN32_LEAN_AND_MEAN`、`NOMINMAX` 等编译选项。

- [ ] **Step 2: 把 core 源文件放入静态库**

  core 项目包含：

  ```text
  src\admin.c
  src\aggregate.c
  src\ntfs_mft.c
  src\ntfs_record.c
  src\ntfs_stream.c
  src\ntfs_volume.c
  src\path_resolver.c
  src\platform_scan.c
  src\scan.c
  src\util.c
  ```

  暂时不包含：

  ```text
  src\main.c
  src\output_json.c
  src\output_table.c
  third_party\yyjson\yyjson.c
  ```

- [ ] **Step 3: 改 CLI 项目为薄本体**

  `folder-size-ranker-cli.vcxproj` 继续是 `Application`，只编译 CLI 相关源文件，并通过 project reference 链接 `mftscan-core.lib`。

- [ ] **Step 4: 处理 linker 依赖**

  CLI 仍链接 `Advapi32.lib`，core 项目不直接设置 CLI 输出相关依赖。

- [ ] **Step 5: 构建验证**

  Run:

  ```powershell
  msbuild E:\work\mft\folder-size-ranker-cli.sln /t:Build /p:Configuration=Release /p:Platform=x64
  ```

  Expected: 生成 `E:\work\mft\x64\Release\mftscan-core.lib` 和 `E:\work\mft\x64\Release\folder-size-ranker-cli.exe`。

### Task 2: 从公开 core API 中移除输出职责

**Files:**
- Modify: `E:\work\mft\include\mftscan.h`
- Modify: `E:\work\mft\src\model.h`
- Modify: `E:\work\mft\src\main.c`
- Modify: `E:\work\mft\src\output_json.c`
- Modify: `E:\work\mft\src\output_table.c`

- [ ] **Step 1: 调整公开头文件职责**

  `include\mftscan.h` 保留扫描选项、错误码、基础结果结构和 session API。将 table/json 输出函数从 core 公共 API 中移出，或标记为 CLI-only 私有声明。

- [ ] **Step 2: 新增 CLI 私有头**

  创建 `src\cli_output.h` 或等价文件，声明：

  ```c
  MftscanError mftscan_cli_output_table(...);
  MftscanError mftscan_cli_output_json(...);
  ```

  `output_json.c` 和 `output_table.c` 只被 CLI 项目编译。

- [ ] **Step 3: main 调用 CLI 输出函数**

  `src\main.c` 不再调用 core 公开输出函数，而是调用 CLI 私有输出函数。

- [ ] **Step 4: 构建验证**

  Run:

  ```powershell
  msbuild E:\work\mft\folder-size-ranker-cli.sln /t:Build /p:Configuration=Debug /p:Platform=x64
  ```

  Expected: CLI 行为保持兼容，core lib 不再依赖 `yyjson`。

## Chunk 2: Native Lazy Session

### Task 3: 设计并实现紧凑 session 数据结构

**Files:**
- Create: `E:\work\mft\src\session.h`
- Create: `E:\work\mft\src\session.c`
- Modify: `E:\work\mft\include\mftscan.h`
- Modify: `E:\work\mft\src\model.h`
- Modify: `E:\work\mft\mftscan-core.vcxproj`
- Modify: `E:\work\mft\mftscan-core.vcxproj.filters`

- [ ] **Step 1: 定义 opaque session**

  在 `include\mftscan.h` 声明：

  ```c
  typedef struct MftscanSession MftscanSession;
  ```

- [ ] **Step 2: 定义查询 DTO**

  增加 `MftscanSessionOptions`、`MftscanNodeInfo`、`MftscanChildInfo`、`MftscanChildBuffer`。DTO 使用固定宽度整数，不暴露内部数组指针生命周期。

- [ ] **Step 3: 实现名称池**

  在 native session 内使用 UTF-16 名称池，节点只保存 offset/length。避免为每个节点单独 malloc 字符串。

- [ ] **Step 4: 实现扁平目录和文件记录**

  session 内部保存 `DirectoryRecord[]`、`FileRecord[]`、`childDirectoryOrder[]`、`childFileOrder[]`，不保存嵌套 children 对象树。

- [ ] **Step 5: 构建验证**

  Run:

  ```powershell
  msbuild E:\work\mft\mftscan-core.vcxproj /t:Build /p:Configuration=Debug /p:Platform=x64
  ```

  Expected: core 静态库可独立构建。

### Task 4: 实现 session 扫描与懒查询 API

**Files:**
- Modify: `E:\work\mft\src\session.c`
- Modify: `E:\work\mft\src\aggregate.c`
- Modify: `E:\work\mft\src\scan.c`
- Modify: `E:\work\mft\include\mftscan.h`

- [ ] **Step 1: 实现 `mftscan_session_scan`**

  扫描完成后，将现有 `MftscanContext` 转换为紧凑 session。session 持有所有查询所需数据，释放时统一释放。

- [ ] **Step 2: 实现排序索引**

  在 session 创建阶段根据 `sort_mode` 对每个目录的直接子目录和直接文件建立排序索引。展开时不重新排序。

- [ ] **Step 3: 实现 `mftscan_session_get_node`**

  按 `node_id` 返回目录摘要：路径显示所需名称、大小、直接文件数、总文件数、总目录数、是否有子项。

- [ ] **Step 4: 实现 `mftscan_session_get_children`**

  支持 `start/count` 懒加载窗口，按目录和文件的排序顺序返回当前窗口。该窗口是 UI 内部懒加载，不是用户可见分页。

- [ ] **Step 5: 实现释放函数**

  `mftscan_session_free` 释放 session，`mftscan_child_buffer_free` 释放单次查询分配的返回缓冲。

- [ ] **Step 6: 添加最小 native 验证程序或 CLI 临时验证路径**

  使用 root 查询、首屏 children 查询、超出范围查询验证 API 行为。

- [ ] **Step 7: 构建验证**

  Run:

  ```powershell
  msbuild E:\work\mft\folder-size-ranker-cli.sln /t:Build /p:Configuration=Debug /p:Platform=x64
  ```

  Expected: core 和 CLI 均通过构建。

### Task 5: 让 CLI 基于 session 输出 table/json

**Files:**
- Modify: `E:\work\mft\src\main.c`
- Modify: `E:\work\mft\src\output_json.c`
- Modify: `E:\work\mft\src\output_table.c`
- Modify: `E:\work\mft\src\cli_output.h`

- [ ] **Step 1: main 改为创建 session**

  CLI 解析参数后调用 `mftscan_session_scan`。

- [ ] **Step 2: table 输出使用 session 查询**

  table 输出通过 session 遍历所需节点，并保持原有排序与 limit 行为。

- [ ] **Step 3: JSON 输出使用 session 查询**

  JSON 输出仍属于 CLI 层，`yyjson` 只在 CLI 项目内使用。

- [ ] **Step 4: 兼容性验证**

  Run:

  ```powershell
  E:\work\mft\x64\Debug\folder-size-ranker-cli.exe --location C:\ --sort allocated --all --limit 10
  ```

  Expected: 输出结构与当前 C# 解析逻辑兼容，直到 C# provider 替换完成。

## Chunk 3: C++/CLI 桥接

### Task 6: 新增 bridge 项目

**Files:**
- Create: `E:\work\ai-clean-volume\src\AiCleanVolume.NativeBridge\AiCleanVolume.NativeBridge.vcxproj`
- Create: `E:\work\ai-clean-volume\src\AiCleanVolume.NativeBridge\NativeMftScanBridge.h`
- Create: `E:\work\ai-clean-volume\src\AiCleanVolume.NativeBridge\NativeMftScanBridge.cpp`
- Modify: `E:\work\ai-clean-volume\AiCleanVolume.sln`

- [ ] **Step 1: 创建 C++/CLI 项目**

  使用 `/clr`，目标 x64，引用 `E:\work\mft\include`，链接 `E:\work\mft\x64\Release\mftscan-core.lib` 或对应配置输出。

- [ ] **Step 2: 定义托管 DTO**

  在 bridge 中定义 `NativeScanOptions`、`NativeNodeInfo`、`NativeChildInfo`、`NativeChildPage`。

- [ ] **Step 3: 封装 session 生命周期**

  提供托管类 `NativeMftScanSession : IDisposable`，内部持有 `MftscanSession*`。

- [ ] **Step 4: 封装懒查询**

  暴露：

  ```csharp
  NativeNodeInfo GetNode(int nodeId)
  NativeChildPage GetChildren(int nodeId, int start, int count)
  ```

- [ ] **Step 5: 构建验证**

  Run:

  ```powershell
  msbuild E:\work\ai-clean-volume\src\AiCleanVolume.NativeBridge\AiCleanVolume.NativeBridge.vcxproj /t:Build /p:Configuration=Debug /p:Platform=x64
  ```

  Expected: bridge 编译并静态链接 core。

## Chunk 4: C# Provider 懒加载

### Task 7: 替换扫描 provider 的数据源

**Files:**
- Modify: `E:\work\ai-clean-volume\src\AiCleanVolume.Desktop\Infrastructure\Scanning\FolderSizeRankerScanProvider.cs`
- Modify: `E:\work\ai-clean-volume\src\AiCleanVolume.Desktop\Infrastructure\Scanning\FolderSizeRankerScanProvider.Session.cs`
- Modify: `E:\work\ai-clean-volume\src\AiCleanVolume.Desktop\Infrastructure\Scanning\FolderSizeRankerScanProvider.Paths.cs`
- Modify: `E:\work\ai-clean-volume\src\AiCleanVolume.Desktop\Infrastructure\Scanning\FolderSizeRankerScanProvider.Json.cs`
- Modify: `E:\work\ai-clean-volume\src\AiCleanVolume.Desktop\Infrastructure\Scanning\FolderSizeRankerScanProvider.Platform.cs`

- [ ] **Step 1: 移除 exe 路径依赖**

  删除 `executablePath`、`File.Exists(folder-size-ranker-cli.exe)`、`ProcessStartInfo` 和 `Process.Start` 路径。

- [ ] **Step 2: 引入 native session handle**

  provider 持有当前 `NativeMftScanSession`，`ClearCache` 释放 session。

- [ ] **Step 3: 保留 `IScanProvider.Scan` 协议**

  `LoadDepth < 0` 创建新 session 并返回 root 可见窗口；`LoadDepth >= 0` 按 `SessionNodeId` 查询子窗口。

- [ ] **Step 4: 移除 JSON 解析路径**

  删除或废弃 `BuildCompactSession(JsonTextReader...)` 等完整 JSON session 构建逻辑。

- [ ] **Step 5: 保留平台 API 降级判断**

  平台 API 降级只能作为明确错误路径的用户可见行为，不作为正式架构的隐藏兜底。若保留，必须标明仅用于不支持文件系统或权限失败时的降级扫描。

- [ ] **Step 6: 构建验证**

  Run:

  ```powershell
  dotnet build E:\work\ai-clean-volume\src\AiCleanVolume.Desktop\AiCleanVolume.Desktop.csproj -c Debug -f net48
  ```

  Expected: C# 编译通过。最终混合链接在后续任务验证。

### Task 8: UI 懒加载窗口适配

**Files:**
- Modify: `E:\work\ai-clean-volume\src\AiCleanVolume.Desktop\Presentation\MainWindow\MainWindow.Scan.cs`
- Modify: `E:\work\ai-clean-volume\src\AiCleanVolume.Desktop\Infrastructure\Scanning\StorageTreePrefetchCoordinator.cs`
- Modify: `E:\work\ai-clean-volume\src\AiCleanVolume.Core\Domain\Storage\ScanRequest.cs`
- Modify: `E:\work\ai-clean-volume\src\AiCleanVolume.Core\Domain\Storage\StorageItem.cs`

- [ ] **Step 1: 明确懒加载窗口字段**

  在 `ScanRequest` 增加内部使用字段，例如 `ChildStart`、`ChildCount`，默认首屏窗口大小使用 UI 常量。

- [ ] **Step 2: 展开目录时只请求窗口**

  UI 展开节点时根据 `SessionNodeId` 请求首个窗口，不绑定完整 children。

- [ ] **Step 3: 滚动或继续展开时追加窗口**

  保证用户体验是懒加载，不显示“分页”概念。

- [ ] **Step 4: 防止重复 materialize**

  每个 UI 节点记录已加载范围，避免重复创建相同 `StorageItem`。

- [ ] **Step 5: 验证大目录体验**

  使用包含大量直接文件的目录测试展开首屏响应，观察 UI 是否明显卡顿。

## Chunk 5: 单 exe 混合模式构建

### Task 9: 调整构建平台

**Files:**
- Modify: `E:\work\ai-clean-volume\AiCleanVolume.sln`
- Modify: `E:\work\ai-clean-volume\src\AiCleanVolume.Core\AiCleanVolume.Core.csproj`
- Modify: `E:\work\ai-clean-volume\src\AiCleanVolume.Desktop\AiCleanVolume.Desktop.csproj`

- [ ] **Step 1: 增加 x64 配置**

  将解决方案从仅 `Any CPU` 扩展到 `x64`。混合模式程序集主线使用 x64。

- [ ] **Step 2: 移除 CLI 内容复制**

  从 Desktop csproj 删除 `folder-size-ranker-cli.exe` 的 `None Include` 和 `CopyToOutputDirectory`。

- [ ] **Step 3: 配置 C# module 输出**

  为混合链接引入单独构建目标：C# 源码先输出 `.netmodule`，再交给 MSVC linker。

- [ ] **Step 4: 配置最终链接**

  MSVC linker 输入包括 C# `.netmodule`、C++/CLI obj、native core lib，输出 `AiCleanVolume.exe`。

- [ ] **Step 5: 构建验证**

  Run:

  ```powershell
  msbuild E:\work\ai-clean-volume\AiCleanVolume.sln /t:Build /p:Configuration=Debug /p:Platform=x64
  ```

  Expected: 输出单个 `AiCleanVolume.exe`，输出目录不包含 `folder-size-ranker-cli.exe`。

### Task 10: 验证运行和内存行为

**Files:**
- Modify: `E:\work\ai-clean-volume\README.md`
- Modify: `E:\work\ai-clean-volume\THIRD_PARTY_NOTICES.md`

- [ ] **Step 1: 运行 net48 x64**

  Run:

  ```powershell
  E:\work\ai-clean-volume\src\AiCleanVolume.Desktop\bin\x64\Debug\net48\AiCleanVolume.exe
  ```

  Expected: 应用启动，扫描入口可用。

- [ ] **Step 2: 验证无外部 CLI**

  删除输出目录中的 `folder-size-ranker-cli.exe` 后运行扫描。

  Expected: 扫描不依赖该 exe。

- [ ] **Step 3: 验证懒加载**

  展开大目录时只 materialize 当前窗口，托管对象数量不随目录完整规模一次性增长。

- [ ] **Step 4: 验证内存**

  使用 Windows 任务管理器或性能工具观察扫描后展开前后的 private bytes 和托管 GC 行为。

- [ ] **Step 5: 更新文档**

  README 删除外部 CLI 运行时依赖说明，改为说明 x64 构建和管理员权限要求。第三方声明保留 `yyjson` 用于 CLI，不再描述 Desktop 分发 CLI exe。

## 执行说明

- 本计划不包含 git commit 步骤；需要提交时由用户明确发起。
- 先实现 `net48 x64`，验证稳定后再补 `net40 x64`。
- 不修改 `E:\work\ai-clean-volume\third_party\AntdUI-v2.3.0` 下任何文件。
- 若工作区已有用户改动，实施时必须避开或与其兼容，不能回退用户改动。
