# AiCleanVolume 竞品分析报告

> 分析日期：2026-09
> 项目：[AiCleanVolume](https://github.com/iyuca-cn/AiCleanVolume) —— Windows 桌面磁盘清理工具：NTFS MFT 原生扫描（树状展示大文件）+ OpenAI 兼容 LLM 接口（AI 判断可安全删除的文件）+ 回收站/永久删除 + 完全权限模式，.NET Framework 4.8 + WebView2 + C++/CLI，MIT 开源。

---

## 1. 项目定位概述

AiCleanVolume 的核心差异化主张可以概括为三句话：

1. **扫得快**：直接读 NTFS MFT 做原生扫描，而不是传统文件系统遍历；
2. **判断准**：接入 ChatGPT / DeepSeek 等 OpenAI 兼容接口，由 AI 判断哪些文件可以安全删除；
3. **删得稳**：删除前做安全评估，默认移到回收站，可选永久删除，支持完全权限模式处理系统文件。

本报告从「空间分析型」「规则清理型」「管家全家桶型」「AI/LLM 探索型」四类竞品出发，逐类对比，最后给出差异化机会与产品路线建议。

---

## 2. 竞品全景分类

| 类型 | 代表产品 | 与本项目的关系 |
| --- | --- | --- |
| **A. 空间分析型**（只分析不清理） | WinDirStat、WizTree、TreeSize、SpaceSniffer、JDiskReport、QDirStat、ncdu、DaisyDisk、GrandPerspective、OmniDiskSweeper | 扫描/展示能力直接对标，其中 WizTree、WinDirStat 与 MFT 扫描技术同赛道 |
| **B. 规则清理型**（按预定义规则清垃圾） | CCleaner、BleachBit、Windows 存储感知/清理建议 | 覆盖"一键清理"心智，但无空间分析、无 AI 判断 |
| **C. 管家全家桶型**（国产生态） | 360 清理 Pro、腾讯电脑管家、火绒、联想电脑管家、华为电脑管家、Dism++ | 解决"C 盘红了"的明确痛点，但普遍存在信任/捆绑/弹窗问题 |
| **D. AI/LLM 探索型** | unix-disk-mcp、ai-disk-cleaner、FilesMagic AI、SweepDisk、腾讯 QClaw、Copilot+ PC 存储建议 | 与本项目定位最接近，但多为"建议层"探索，尚未形成 Windows 桌面成熟产品 |

---

## 3. 核心竞品对比表

### 3.1 国外与开源竞品

| 竞品 | 扫描技术 | 展示形式 | 删除/清理能力 | AI/智能 | 平台 | 定价/协议 |
| --- | --- | --- | --- | --- | --- | --- |
| WizTree | 直接读 NTFS MFT（宣称 1TB SSD 约 5 秒） | 树状列表 | 无 | 无 | Windows | 免费、闭源 |
| WinDirStat | 传统遍历；新版加入 direct NTFS MFT 扫描选项 | 树状 + Treemap | 无 | 无 | Windows | 免费开源 GPL-2.0 |
| TreeSize Free / Pro | 常规遍历（支持网络盘） | 树状 + Treemap | 无 | 无 | Windows | Free 免费；Pro 订阅（中文官网约 ¥318/年） |
| SpaceSniffer | 常规遍历 | 交互式 Treemap | 右键删除，无回收站 | 无 | Windows | 免费 |
| JDiskReport | Java 常规遍历 | 饼图/环形图/柱状图 | 无 | 无 | Win/Mac/Linux | 免费、专有 |
| QDirStat | 常规遍历 | 树状 + Treemap | 支持回收站删除、永久删除、脚本化清理 | 无 | Linux | 免费开源 |
| ncdu | du 命令 | 命令行交互列表 | 直接删除（带确认），无回收站 | 无 | Linux/Mac | 免费开源 |
| BleachBit | 预定义规则扫描 | 分类列表 | 删除、碎纸机模式；无回收站 | 无 | Win/Linux | 免费开源 GPLv3 |
| CCleaner | 预定义规则扫描 | 分类列表 | 一键清理、系统文件清理；无回收站 | 无 | Windows | 免费版 + Pro（约 $29.95/首年） |
| DaisyDisk | 常规遍历（宣称 15 秒极速扫描） | Treemap + 雷达图 | 拖拽删除，无回收站/权限提升 | 无 | macOS | $9.99 买断 |
| GrandPerspective | 常规遍历 | Treemap | 删除、Finder 定位 | 无 | macOS | 免费开源 |
| OmniDiskSweeper | 常规遍历 | 列表（按大小降序） | 删除、打开文件 | 无 | macOS | 免费 |

### 3.2 国产竞品

| 竞品 | 核心能力 | 智能/AI 程度 | 商业模式 | 主要痛点 |
| --- | --- | --- | --- | --- |
| 360 清理 Pro | 大文件查找、重复文件删除、C 盘无损搬家 | 规则型，无 AI 删除判断 | 免费 | 历史捆绑、诱导清理、扣费投诉，信任成本高 |
| 腾讯电脑管家 | 垃圾清理、社交缓存、大文件智能扫描、重复文件 | 规则型扫描 | 免费 | 广告弹窗、深度清理权限争议 |
| 火绒安全 | 垃圾清理、广告拦截 | 无 AI | 免费 | 清理能力相对轻量 |
| 联想电脑管家 | 垃圾清理、系统加速、驱动管理 | 无 AI | 免费（预装生态） | 依赖联想设备生态 |
| 华为电脑管家 | 电脑清理、系统盘瘦身、大文件清理、云查搬移 | 云查规则/白名单，非 AI 大模型 | 免费（预装生态） | 部分功能需管理员权限，门槛较高 |
| Dism++ | 系统清理、优化、备份还原、驱动/更新管理 | 无 AI | 免费开源 MIT | 学习曲线陡峭，误操作有风险 |
| Windows 存储感知 / 清理建议 | 自动清理临时文件、旧 Windows 安装、未使用应用建议 | 系统规则型建议 | 免费内置 | 覆盖范围有限，不扫应用缓存/重复大文件 |

### 3.3 AI/LLM 探索型（与本项目最接近）

| 产品 | 形态 | AI 参与方式 | 说明 |
| --- | --- | --- | --- |
| unix-disk-mcp | MCP 服务 | LLM 探索文件系统并给出删除建议 | AI 只能建议/暂存，不能直接删除 |
| ai-disk-cleaner 等 | 脚本/助手 | LLM 扫描文件并输出建议 | 同为"建议层"探索 |
| FilesMagic AI | macOS 商业工具 | 识别重复文件、未使用应用、开发者缓存（node_modules、Xcode derived data）并一键清理 | 定价约 $4.99/月、$29/年或 $49.99 终身 |
| SweepDisk | macOS 工具 | AI 文件整理，强调 100% 本地、不上传字节到云端 | 本地处理是核心卖点 |
| 腾讯 QClaw（OpenClaw 国内版） | 开源 Agent 框架 | 通过 file-skill 做 C 盘清理与文件整理，强调"零删除、零篡改、一键回撤"、AI 语义自动归类 | 面向开发者的 Agent 能力，非独立桌面产品 |
| Microsoft Copilot+ PC | 系统级 | AI 管理资源并给出存储组织建议 | 更偏系统级建议，不是独立清理工具 |

---

## 4. 分维度对比与关键洞察

### 4.1 扫描速度：MFT 原生扫描是分水岭

- WizTree 直接读 NTFS MFT，宣称 1TB SSD 约 5 秒完成扫描，是该领域的速度标杆；WinDirStat 也在新版加入了 direct NTFS MFT 扫描选项。AiCleanVolume 通过 mftscan-core 静态库做 native 扫描，技术路线与 WizTree/WinDirStat 一致，这是硬核性能卖点。
- 传统遍历型工具（TreeSize、SpaceSniffer、JDiskReport 等）在大型 SSD 上明显偏慢，属于体验短板。

### 4.2 删除安全机制：信任的核心

- 绝大多数空间分析工具（WizTree、TreeSize、WinDirStat 等）**只分析不删除**；能删除的（SpaceSniffer、ncdu）大多没有回收站机制，误删不可恢复。
- QDirStat 是少数支持"移入回收站"的桌面分析工具，值得参考。
- AiCleanVolume 的"先评估 → 默认回收站 → 可选永久删除 → 完全权限模式"设计，安全层级高于多数同类，是建立信任的关键。

### 4.3 AI 判断可删性：仍是蓝海

- 主流清理工具的"智能"普遍停留在**启发式规则**（BleachBit 规则库、CleanMyMac 智能扫描数据库、Windows 清理建议、华为云查白名单），没有 LLM 判断。
- 已有 AI 探索（unix-disk-mcp、ai-disk-cleaner、FilesMagic AI、QClaw）的共同特点是：**AI 只负责"建议"，删除仍需用户确认**。
- **Windows 桌面端 + MFT 原生扫描 + LLM 可删性判断 + 开源**的组合，目前没有成熟竞品，属于明确蓝海。

### 4.4 商业模式：免费工具与付费订阅并存

| 模式 | 代表 | 定价参考 |
| --- | --- | --- |
| 免费 + 广告/内购 | CCleaner 免费版 | 广告弹窗被大量吐槽 |
| Pro 订阅 | CCleaner Pro、TreeSize Pro、CleanMyMac X | 约 $30-44/年 起 |
| 一次性买断 | DaisyDisk、FilesMagic AI | $9.99 / $49.99 |
| 开源免费/捐赠 | BleachBit、WinDirStat、Dism++、AiCleanVolume | 无广告无订阅，靠社区信任 |

---

## 5. 市场概况与趋势

- 全球磁盘清理软件市场持续增长：不同机构口径为 2025 年约 12-28 亿美元，预计 2034 年达 25-52 亿美元（CAGR 约 6.8%-9.6%）；Windows 占最大份额约 52.3%。
- 中国市场 2024 年电脑清理软件市场规模约 35 亿元人民币，同比增长 18.6%，猎豹清理大师（约 1.2 亿用户）与 360 安全卫士（约 1.1 亿用户）占据头部。
- 系统级 AI 化正在推进：微软 Copilot+ PC 引入 AI 存储组织建议；Windows 内置"清理建议"是"安全删除"的信任标杆，但覆盖有限——第三方 AI 清理必须比系统建议更透明、更可解释才能建立信任。

---

## 6. 用户痛点与信任问题（为什么用户不敢用清理工具）

1. **误删恐惧**：用户普遍不敢删大文件/系统目录（System32、WinSxS、Program Files 被视为高风险区），担心误删后恢复成本高、甚至系统异常。
2. **捆绑与弹窗**：CCleaner 曾捆绑 Avast 被微软标记为 PUA；360 等国产产品历史上有捆绑安装、诱导清理、自动续费投诉。
3. **供应链与隐私信任**：CCleaner 2017 年供应链攻击影响 227 万用户；Avast 曾因数据实践被 FTC 罚款 1650 万美元——清理软件"既要读你的文件又要联网"的天然矛盾加深了用户疑虑。
4. **AI 隐私顾虑**：把本地文件内容/元数据发送给云端 LLM 存在数据留存与泄露风险；第三方报告指出上传到 GenAI 工具的数据中超过 40% 可能包含 PII/PCI 信息。**"文件不出本机"正成为 AI 清理类工具的重要信任点**（如 SweepDisk 主打 100% 本地处理）。

---

## 7. 差异化机会与产品路线建议

### 7.1 差异化定位

> **开源、透明、可解释的 AI 磁盘清理工具**：MFT 原生扫描的速度优势 + LLM 可删性判断 + 本地规则兜底 + 回收站优先的安全模型。

对照竞品：BleachBit 开源但无 AI；FilesMagic AI 有 AI 但闭源且仅 macOS；WizTree 快但只分析不删除。AiCleanVolume 的组合在当前市场是稀缺的。

### 7.2 目标用户

- **开发者/高级用户**（首选）：关注缓存、旧文件、大文件，接受开源与透明；
- **普通 Windows 用户**：解决"C 盘红了"、不知道哪些能删的问题；
- **企业 IT 管理员**（远期）：批量扫描、规则一致、审计与合规。

### 7.3 功能与体验路线建议

| 优先级 | 建议 | 理由 |
| --- | --- | --- |
| P0 | **隐私默认关闭 LLM**：默认走本地规则，AI 作为显式开启的高级能力；明确展示会发送哪些文件元数据、为何发送、如何关闭 | 云端 LLM 发送文件内容是最大信任风险 |
| P0 | **支持本地模型**（Ollama 等 OpenAI 兼容端点） | 满足隐私导向用户，"文件不出本机" |
| P0 | **回收站优先 + 风险分级**：系统目录/白名单外文件标注风险等级，展示 AI 判断依据（为什么可删） | 对应"误删恐惧"第一痛点；"能解释"胜过"能判断" |
| P1 | **一键清理建议 + 回撤**：批量采纳 AI 建议、一键回撤（参照 QClaw 的"零删除、零篡改、一键回撤"） | 降低决策负担，建立试错安全感 |
| P1 | **分类统计视图**：按"临时文件/缓存/旧安装包/重复文件/未使用应用"分类汇总 | 对齐国产管家心智，且比"大文件列表"更直接 |
| P2 | 商业化路径：开源 + 捐赠 → 核心开源 + Pro（批量处理/企业部署/高级 AI）订阅 → 企业版 | 参照 BleachBit → CCleaner Pro 的演进 |

### 7.4 风险提示

- AI 判断"可删除"属高风险功能，**必须保留本地规则兜底与人工确认**，否则任何一次误删都会摧毁信任；
- 广告/捆绑是清理工具行业的信誉坟墓，开源免费 + 明确隐私政策是更稳妥的起步方式；
- 需在 README 与 UI 中明确区分"规则判断"与"AI 判断"，避免合规与信任混淆。

---

## 8. 结论

1. **扫描层**：MFT 原生扫描使 AiCleanVolume 与 WizTree 同属速度第一梯队，是明确的性能卖点。
2. **判断层**：LLM 可删性判断在 Windows 桌面清理领域仍是蓝海，但必须配套本地规则兜底、可解释判断与隐私开关。
3. **删除层**：回收站优先 + 风险分级 + 完全权限模式的安全模型高于多数竞品，是差异化信任资产。
4. **建议**：优先打磨"快、准、稳、透明"四要素，以开发者社区为起点建立信任，再向普通用户与商业化延伸。

---

## 附：主要参考来源

- WizTree 官网：https://wiztree.app/
- WinDirStat 更新日志（MFT 选项）：https://github.com/windirstat/windirstat/blob/master/CHANGELOG.md
- TreeSize Free：https://www.jam-software.com/treesize_free ；中文定价页：https://treesize.cn/download.html
- SpaceSniffer：https://www.techspot.com/downloads/5365-space-sniffer.html
- QDirStat：https://ubunlog.com/en/qdirstat-detects-clean-hard-drive/
- BleachBit：https://www.bleachbit.org/features
- CCleaner：https://www.ccleaner.com/ccleaner ；2017 供应链事件：https://blog.avast.com/update-ccleaner-attackers-entered-via-teamviewer
- DaisyDisk 定价：https://daisydiskapp.com/support/pricing/
- 360 清理 Pro：https://weishi.360.cn/qlPro.html
- 腾讯电脑管家：https://guanjia.qq.com/help/ljql.html
- 火绒垃圾清理：https://www.huorong.cn/document/info/productions/120
- 联想电脑管家：https://guanjia.lenovo.com.cn/help2.html
- 华为系统盘瘦身：https://consumer.huawei.com/cn/support/content/zh-cn15962667/
- Dism++：http://www.dismplus.com/
- Windows 存储感知：https://learn.microsoft.com/zh-cn/windows/configuration/storage/storage-sense ；清理建议：https://support.microsoft.com/zh-cn/windows/experience/storage/filemanagement/free-up-drive-space-in-windows
- unix-disk-mcp：https://www.npmjs.com/package/unix-disk-mcp
- FilesMagic AI：https://1dot.ai/files-magic-ai
- SweepDisk（本地 AI 文件整理）：https://www.producthunt.com/products/sweepdisk
- 腾讯 QClaw C 盘清理实践：https://cloud.tencent.com/developer/article/2670037
- Copilot+ PC 存储管理：https://www.microsoft.com/en-us/windows/learning-center/how-to-delete-temporary-files
- 全球磁盘清理软件市场（DataIntelo）：https://dataintelo.com/report/global-disk-cleanup-software-market
- 中国市场数据（猎豹/360 份额）：https://www.docin.com/touch_new/preview_new.do?id=5015800589
- CCleaner 隐私说明：https://www.ccleaner.com/ccleaner/is-ccleaner-safe ；Avast FTC 罚款：https://www.lebigdata.fr/ccleaner-analyse-est-il-fiable
- 上传文件到 AI 的数据去向：https://blog.askturing.ai/what-really-happens-to-your-files-when-you-upload-to-ai/
- LayerX 企业 AI 数据安全报告：https://go.layerxsecurity.com/hubfs/LayerX_Enterprise_AI_and_SaaS_Data_Security_Report.pdf
