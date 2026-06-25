# 环刀法压实度检测工具

Windows 桌面应用（C# WPF / .NET 8）：环刀法原始记录填写、自动计算、LIMIS 工程信息拉取、备注智能解析、Word 检测报告导出。

## 快速开始（已编译版本）

1. 从 [Releases](https://github.com/Uuclear/ori_sheet/releases) 下载最新版 `RingKnifeDetector-v*-win-x64.zip`
2. 解压到任意目录
3. 双击 `RingKnifeDetector.exe` 运行（已自带 .NET 运行时，无需单独安装）
4. 详细步骤见压缩包内 **使用说明.txt**

## 从源码编译

**环境**：Windows 10/11（64 位）+ [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
cd RingKnifeDetector
dotnet build -c Release
dotnet run --project RingKnifeDetector/RingKnifeDetector.csproj
```

**一键发布**（生成 `dist/` 目录与 zip 包）：

```powershell
.\build.ps1
```

产物路径：`dist/RingKnifeDetector-v1.3.0-win-x64/`

## 使用流程

| 步骤 | 操作 |
|------|------|
| 1. 设置 | 填写 LIMIS 服务器地址（默认 `http://10.1.228.22`）、账号密码 → **测试登录** → 确认主检姓名 |
| 2. 任务 | 在任务列表搜索委托编号，可按状态筛选，**双击**任务进入记录页 |
| 3. 录入 | LIMIS 拉取后备注字段自动解析补全 → 填写原始记录 → **计算** → **保存草稿** |
| 4. 导出 | **导出 Word**（检测报告）；导出成功时如有数据异常会弹出**提醒**（不阻断导出） |

## 功能说明

### LIMIS 集成

- 任务列表搜索、状态筛选、草稿主检列
- 拉取工程信息、样品编号、主检姓名
- **现场检测**与**见证送样**两种检测性质自适应表头与字段来源
- 见证送样：从委托单 HTML（`standBy3`）解析工程见证、样品取样、规格型号等专用字段
- 检测依据从 LIMIS 委托单 HTML「检验依据及项目」解析；**全称**按钮可切换是否显示《书名号》段
- 判定依据独立维护（默认 `JTG 3450-2019`，可手动修改，草稿单独保存）
- LIMIS 获取的字段以**绿色**色块标识来源

### 备注智能解析

从原始记录备注正则提取并补全：材料种类、最大干密度、最优含水率、设计要求、工程部位、监理/施工单位、标高、厚度等；提取值浅蓝高亮，悬停显示字段名。

### 原始记录与计算

- 多测点录入，右键删除测点，Tab 键按列优先跳转可编辑格
- 自动计算湿密度、含水率、干密度、压实系数/压实度及单项结论

### Word 报告导出

- 基于 `Resources/report_template.docx` 模板生成
- 见证送样表头：工程见证 / 样品取样 / 规格型号
- 委托编号页眉不换行；备注序号对齐；备注栏无多余上下空行
- **导出提醒**（仅提示，不影响导出）：
  - 报告字段是否存在空白
  - 委托日期 → 取样日期 → 检测日期 → 报告日期顺序
  - 检测结论是否不合格（不符合设计要求）
  - 压实度 ≥ 100% 或压实系数 ≥ 1

### 本地数据

| 内容 | 路径 |
|------|------|
| 应用设置 | `%LocalAppData%\RingKnifeDetector\settings.json` |
| 草稿文件 | `%LocalAppData%\RingKnifeDetector\Drafts\` |

> 设置文件含 LIMIS 凭据，请勿分享。

## 项目结构

```
ori_sheet/
├── RingKnifeDetector/          # 解决方案与源码
│   ├── RingKnifeDetector/      # WPF 主程序
│   └── RingKnifeDetector.Tests/
├── docs/                       # LIMIS API 参考、见证送样字段说明等
├── build.ps1                   # 一键发布脚本
├── CHANGELOG.md                # 更新日志
├── USER_GUIDE.md               # 用户手册（Release 包内为 使用说明.txt）
└── dist/                       # 本地编译输出（git 忽略）
```

## 测试

```powershell
cd RingKnifeDetector
dotnet test
```

当前约 39 项单元测试，覆盖计算、备注解析、LIMIS HTML 解析、Word 备注格式、导出提醒等。

## 文档

| 文档 | 说明 |
|------|------|
| [USER_GUIDE.md](USER_GUIDE.md) | 面向最终用户的操作手册 |
| [CHANGELOG.md](CHANGELOG.md) | 版本更新记录 |
| [docs/LIMIS见证送样字段获取说明.md](docs/LIMIS见证送样字段获取说明.md) | 见证送样字段与 HTML 解析 |
| [docs/](docs/) | LIMIS 系统 API 参考 |

## 版本

当前版本：**v1.3.0** — 详见 [CHANGELOG.md](CHANGELOG.md)
