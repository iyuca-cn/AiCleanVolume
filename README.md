# AI Clean Volume - 智能磁盘清理工具

一款简单好用的 Windows 电脑磁盘清理软件，帮你快速找出占空间的大文件，还能用 AI 帮你判断哪些可以安全删除。

## 这个软件能做什么？

### 🧹 磁盘扫描
- 扫描你的电脑硬盘，找出哪些文件和文件夹最占空间
- 用树状列表展示，一目了然看到空间都去哪了

### 🤖 AI 智能建议
- 可以连接 AI（比如 ChatGPT、DeepSeek）帮你分析哪些文件可以清理
- 如果不想用 AI，软件也会根据本地规则给出清理建议

### 🗑️ 安全删除
- 删除前会先评估文件是否安全删除
- 可以选择"移到回收站"或"永久删除"
- 支持"完全权限模式"，能处理更多系统文件

### ⚙️ 灵活配置
- 支持多种 AI 接口：标准 API 和 2API 两种模式
- 可以保存你的配置，下次打开自动加载

## 如何使用？

### 第一步：编译程序

如果你是开发者，可以这样编译：

```powershell
msbuild E:\work\mft\mftscan-core.vcxproj /t:Build /p:Configuration=Debug /p:Platform=x64
msbuild E:\work\ai-clean-volume\src\AiCleanVolume.NativeBridge\AiCleanVolume.NativeBridge.vcxproj /t:Build /p:Configuration=Debug /p:Platform=x64
dotnet build E:\work\ai-clean-volume\src\AiCleanVolume.Desktop\AiCleanVolume.Desktop.csproj -c Debug -f net48 -p:Platform=x64
```

### 第二步：运行程序

编译完成后，运行 .NET Framework 4.8 x64 版本：

```powershell
.\src\AiCleanVolume.Desktop\bin\x64\Debug\net48\AiCleanVolume.exe
```

### 第三步：配置 AI（可选）

如果你想用 AI 帮你分析清理建议：

1. 点击软件右侧的配置区
2. 打开 AI 开关
3. 选择接入类型：
   - **标准 API**：填入接口地址、API Key 和模型名称
   - **2API**：填入接口地址和模型名称，然后在"模型 Cookie"里配置每行一条映射（格式：`模型=完整Cookie`）
4. 接口地址支持多种写法：
   - 根地址：`http://127.0.0.1:3000`
   - 带版本：`http://127.0.0.1:3000/v1`
   - 完整地址：`http://127.0.0.1:3000/v1/chat/completions`
5. 点击"保存配置"即可

## 项目结构（开发者看这里）

```
src/AiCleanVolume.Core/          # 核心逻辑
  ├── Domain/                    # 存储树、清理建议、沙盒评估等核心数据
  └── Kernel/Ports/              # 各种接口定义（扫描、AI、删除等）

src/AiCleanVolume.Desktop/       # 桌面程序
  ├── Composition/               # 程序组装，把各个部件连起来
  ├── Infrastructure/            # 具体实现（native 扫描、AI接口、删除操作等）
  └── Presentation/              # 界面显示和用户交互

src/AiCleanVolume.NativeBridge/  # C++/CLI 扫描桥接，静态链接 mftscan-core

third_party/                     # 第三方工具
  ├── folder-size-ranker-cli/    # CLI 上游快照，不再复制到桌面端输出目录
  └── AntdUI-v2.3.0/            # 界面组件库
```

## 注意事项

- 扫描 NTFS 格式的硬盘时，程序可能需要管理员权限
- AI 功能使用的是 OpenAI 兼容接口（`/v1/chat/completions`）
- 2API 模式不会发送 API Key，而是根据模型名称匹配对应的 Cookie
- 当前 native 懒加载扫描主线验证 .NET Framework 4.8 x64
- 项目使用 MIT 开源许可证，可以自由使用和修改

## 遇到问题？

1. **扫描没反应？** 试试以管理员身份运行程序
2. **AI 不工作？** 检查接口地址和 API Key 是否正确
3. **删除文件失败？** 可能是权限不够，试试开启"完全权限模式"
