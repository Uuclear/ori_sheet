# 环刀法压实度检测工具（C# WPF）

基于 C# WPF 的 Windows 桌面应用：环刀法原始记录填写、自动计算、LIMIS 工程信息拉取、Excel / Word 报告导出。

## 环境要求

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## 编译与运行

```bash
cd RingKnifeDetector
dotnet build
dotnet run --project RingKnifeDetector/RingKnifeDetector.csproj
```

发布单文件（可选）：

```bash
dotnet publish RingKnifeDetector/RingKnifeDetector.csproj -c Release -r win-x64 --self-contained false
```

## 功能

| 模块 | 说明 |
|------|------|
| 任务列表 | 按委托编号搜索 LIMIS 任务，分页浏览，双击进入记录填写 |
| 记录填写 | 原始记录表格录入、自动计算、右键删除测点 |
| LIMIS | 拉取工程信息、主检姓名（`GetUserName` API） |
| 导出 | Excel 原始记录；Word 检测报告（`环刀300.docx` 模板） |
| 草稿 | 本地 JSON 自动保存（`%LocalAppData%/RingKnifeDetector/`） |
| 设置 | LIMIS 服务器、账号、主检姓名、报告备注默认值 |

## 使用流程

1. **设置** — 填写 LIMIS 地址与账号，点击「测试登录」确认主检姓名
2. **任务列表** — 搜索委托编号，双击任务
3. **记录填写** — 录入数据 → 计算 → 保存草稿
4. **导出** — 生成 Excel 或 Word 报告

## 配置说明

- 应用设置保存在：`%LocalAppData%/RingKnifeDetector/settings.json`（含密码，勿提交版本库）
- Word 模板：`RingKnifeDetector/Resources/report_template.docx`
- 报告备注可在界面手动编辑，随草稿持久化

## 测试

```bash
dotnet test
```

## 技术栈

- WPF (.NET 8)
- ClosedXML（Excel）
- DocumentFormat.OpenXml（Word）
- xUnit

## 项目结构

```
RingKnifeDetector/
├── RingKnifeDetector.sln
├── RingKnifeDetector/          # 主程序
│   ├── Services/               # LIMIS、导出、计算、草稿
│   ├── Views/                  # 原始记录表格控件
│   ├── Models/
│   └── Resources/              # 模板、图标
└── RingKnifeDetector.Tests/
```
