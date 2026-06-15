# 环刀法压实度检测工具

Windows 桌面应用（C# WPF）：环刀法原始记录填写、自动计算、LIMIS 工程信息拉取、Excel / Word 报告导出。

## 快速开始（已编译版本）

1. 从 [Releases](https://github.com/Uuclear/ori_sheet/releases) 下载 `RingKnifeDetector-v1.0.0-win-x64.zip`
2. 解压到任意目录
3. 双击 `RingKnifeDetector.exe` 运行（已自带 .NET 运行时，无需单独安装）
4. 详细步骤见压缩包内 **使用说明.txt**

## 从源码编译

**环境**：Windows 10/11 + [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
cd RingKnifeDetector
dotnet build -c Release
dotnet run --project RingKnifeDetector/RingKnifeDetector.csproj
```

**一键发布**（生成 `dist/` 目录与 zip 包）：

```powershell
.\build.ps1
```

产物路径：`dist/RingKnifeDetector-v1.0.0-win-x64/`

## 使用流程

| 步骤 | 操作 |
|------|------|
| 1. 设置 | 填写 LIMIS 服务器地址（默认 `http://10.1.228.22`）、账号密码 → **测试登录** → 确认主检姓名 |
| 2. 任务 | 在任务列表搜索委托编号，**双击**任务进入记录页 |
| 3. 录入 | 填写原始记录 → 点击 **计算** → **保存草稿** |
| 4. 导出 | **导出 Excel**（原始记录）或 **导出 Word**（检测报告） |

## 功能说明

- **LIMIS 集成**：拉取工程信息、样品编号、主检姓名（`GetUserName` API）
- **原始记录表**：支持多测点、右键删除、Tab 键按列优先跳转可编辑格
- **自动计算**：湿密度、含水率、干密度、压实系数/压实度
- **Word 报告**：基于 `Resources/report_template.docx` 模板生成
- **草稿**：自动保存至 `%LocalAppData%/RingKnifeDetector/Drafts/`
- **设置**：保存至 `%LocalAppData%/RingKnifeDetector/settings.json`（勿分享此文件）

## 项目结构

```
├── RingKnifeDetector/          # 解决方案与源码
│   ├── RingKnifeDetector/      # WPF 主程序
│   └── RingKnifeDetector.Tests/
├── docs/                       # LIMIS API 参考文档
├── build.ps1                   # 发布脚本
├── USER_GUIDE.md               # 用户手册（Release 包内为 使用说明.txt）
└── dist/                       # 本地编译输出（git 忽略）
```

## 测试

```powershell
cd RingKnifeDetector
dotnet test
```

## 版本

当前版本：**v1.0.0**
