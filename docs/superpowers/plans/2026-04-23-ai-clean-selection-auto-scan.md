# AI Clean Selection Auto Scan Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为清理建议页补充全选、全不选、反选操作，并在未扫描当前所选盘符时自动扫描后继续生成建议，同时让 AI 提示词随盘符变化自动更新。

**Architecture:** 保持现有 WinForms + AntdUI 单窗体结构，集中修改 `MainWindow` 的建议页工具栏、AI 分析入口和提示词预设渲染逻辑。批量勾选只操作现有 `CleanupSuggestionRow` 集合，不改动核心删除/分析服务；自动扫描复用现有后台任务封装，在扫描成功回调后继续分析。

**Tech Stack:** .NET Framework 4.0、WinForms、AntdUI、C# 7.3。

---

## Chunk 1: 建议列表批量勾选

### Task 1: 增加快捷选择按钮

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: 扩展建议页工具栏**

在建议页顶部工具栏中新增“全选”“全不选”“反选”三个按钮，与现有完全权限开关并排显示。

- [ ] **Step 2: 复用现有行模型切换选中状态**

为 `suggestionRows` 增加统一批量更新入口，使用 `CleanupSuggestionRow.selected` 触发表格刷新。

## Chunk 2: 自动扫描后继续分析

### Task 2: 分析入口兜底扫描

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: 统一解析当前目标位置**

从路径输入框和盘符下拉中解析当前要分析的位置，默认回退到系统盘。

- [ ] **Step 2: 未扫描或盘符切换时自动扫描**

在 AI/常规分析入口判断当前扫描结果是否匹配所选位置；若不匹配，则先自动扫描，再在扫描完成回调中继续分析。

## Chunk 3: 提示词随盘符变化

### Task 3: 让 AI 预设按盘符渲染

**Files:**
- Modify: `src/AiCleanVolume.Desktop/MainWindow.cs`

- [ ] **Step 1: 给预设提示词增加盘符上下文**

把提示词预设改为按当前盘符渲染，统一加上“当前重点分析某盘”的上下文。

- [ ] **Step 2: 保留自定义提示词**

只有当前提示词仍可识别为内置预设时，才随盘符自动刷新；自定义文本保持不变。

- [ ] **Step 3: 构建验证**

Run: `dotnet build .\AiCleanVolume.sln -c Debug`
Expected: 构建成功；允许保留既有第三方依赖告警。
