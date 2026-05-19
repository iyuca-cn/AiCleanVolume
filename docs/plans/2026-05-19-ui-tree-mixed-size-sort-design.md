# UI 文件树混合大小排序设计

## 背景

当前桌面端只是 `folder-size-ranker-cli` 的 UI 壳，扫描与大小统计由 CLI 独立完成。UI 文件树当前在子节点展示时会保持数据源的原始顺序，导致同一层里文件与文件夹没有按大小混合排列。

## 目标

- 同一父节点下的文件和文件夹混合显示。
- 按大小从大到小排列；文件使用自身大小，文件夹使用 CLI 提供的目录总大小。
- 不修改 CLI、不修改扫描 Provider 的数据结构和输出顺序。
- 避免增加长期内存占用。

## 方案

排序只放在 UI 行物化阶段：`StorageEntryRow` 从 `StorageItem.Children` 生成子行时，仍按原逻辑创建 UI 子行，随后只对当前 `Children` 子行列表做原地排序。

这样可以保持 `StorageItem.Children` 原始列表不变，不复制 `StorageItem` 集合，也不增加额外索引数组；排序只重排 UI 已经需要存在的子行引用。排序比较器读取行内 `StorageItem` 的 `Bytes`、`Name` 和 `Path`，不创建额外节点对象。

## 数据流

1. CLI 输出扫描结果。
2. Provider 解析为 `StorageItem.Children`，保持原始数据含义。
3. `StorageEntryRow.MaterializeLoadedChildren()` 展开节点时，按大小降序遍历子项。
4. UI 表格显示混合后的文件/文件夹顺序。

## 内存约束

- 不复制 `StorageItem` 对象。
- 不修改原始 `StorageItem.Children` 列表，也不创建排序后的 `List<StorageItem>`。
- 不创建索引数组或额外节点集合；只对 UI 已有 `Children` 列表做原地排序。
- 懒加载释放子行逻辑保持不变，收起后仍可释放 UI 行。

## 测试

- 构造同一层包含文件与文件夹的 `StorageItem`，验证 `StorageEntryRow.Children` 按 `Bytes` 降序混排。
- 验证大小相同时按名称稳定排序。
- 验证原始 `StorageItem.Children` 顺序不被修改。

