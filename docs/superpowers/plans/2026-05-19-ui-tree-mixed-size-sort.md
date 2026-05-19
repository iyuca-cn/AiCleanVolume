# UI Tree Mixed Size Sort Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让桌面端文件树在 UI 展示层按大小降序混合显示文件和文件夹，且不增加长期内存占用。

**Architecture:** 仅修改 `StorageEntryRow` 的子行物化逻辑，不修改 CLI、Provider 或 `StorageItem.Children` 原始顺序。展开节点时创建原本就需要的 UI 子行，然后对当前 UI 子行列表原地排序，避免复制 `StorageItem` 集合或创建索引数组。

**Tech Stack:** C# 7.3、.NET Framework 4.0、WinForms、AntdUI 表格树。

---

## Chunk 1: UI 展示排序

### Task 1: 修改子行展示顺序

**Files:**
- Modify: `src/AiCleanVolume.Desktop/ViewModels/StorageEntryRow.cs`

- [ ] **Step 1: 保留原始数据顺序**

确认不调用 `Item.Children.Sort`，不创建 `List<StorageItem>` 副本，只在生成 `StorageEntryRow` 后改变 UI 子行列表顺序。

- [ ] **Step 2: 添加 UI 子行原地排序**

在 `MaterializeLoadedChildren()` 中：

```csharp
Children.Clear();
AddSortedChildRows();
```

生成子行后执行：

```csharp
if (Children.Count > 1) Children.Sort(CompareChildRowsByDisplayOrder);
```

空列表或单个子项不执行排序。

- [ ] **Step 3: 实现轻量排序比较器**

新增比较逻辑：

```csharp
private static int CompareChildRowsByDisplayOrder(object leftValue, object rightValue)
```

比较规则：先按 `Bytes` 降序，再按 `Name` 升序，最后按 `Path` 升序保证稳定显示。

- [ ] **Step 4: 检查内存约束**

确认实现不分配 `int[]`，不复制 `StorageItem`，不缓存排序结果，收起节点时现有释放逻辑不变。

### Task 2: 验证构建

**Files:**
- Validate: `AiCleanVolume.sln`

- [ ] **Step 1: 运行 Debug 构建**

Run: `dotnet build AiCleanVolume.sln -c Debug`

Expected: 构建成功，无新增编译错误。

- [ ] **Step 2: 检查 git diff**

Run: `git diff -- src/AiCleanVolume.Desktop/ViewModels/StorageEntryRow.cs docs/plans/2026-05-19-ui-tree-mixed-size-sort-design.md docs/superpowers/plans/2026-05-19-ui-tree-mixed-size-sort.md`

Expected: 仅包含 UI 排序实现和两份文档。

- [ ] **Step 3: 不提交**

按用户要求，本次只写文件与验证，不执行 `git commit`。

