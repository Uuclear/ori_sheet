# 环刀法压实度检测 - 原始记录填写与报告生成

环刀法压实度检测工具，支持从 LIMIS 拉取工程信息、录入原始记录、自动计算并生成检测报告。

本项目包含两个实现：

| 目录 | 说明 |
|------|------|
| `ring_knife/` | Python + FastAPI 本地 Web 版 |
| [`RingKnifeDetector/`](RingKnifeDetector/) | **C# WPF 桌面版**（推荐日常使用） |

---

## C# 桌面版（RingKnifeDetector）

详见 [RingKnifeDetector/README.md](RingKnifeDetector/README.md)。

```bash
cd RingKnifeDetector
dotnet build
dotnet run --project RingKnifeDetector/RingKnifeDetector.csproj
```

---

## Python Web 版

基于 FastAPI 的本地 Web 工具：表格录入环刀法原始记录、自动计算干密度/压实系数，从 LIMIS 拉取工程信息，生成 `环刀300.docx` 格式检测报告。

## 环境要求

- Python 3.10+
- 模板文件位于项目根目录：`环刀（2个1组）.docx`、`环刀300.docx`

## 安装

```bash
cd d:\github\jsscript
pip install -r requirements.txt
copy .env.example .env
```

编辑 `.env` 填写 LIMIS 内网账号（可选，不填则工程信息手工录入）：

```
LIMIS_BASE_URL=http://10.1.228.22
LIMIS_USERNAME=你的用户名
LIMIS_PASSWORD=你的密码
```

## 启动

```bash
uvicorn ring_knife.main:app --reload --port 8765
```

浏览器打开：http://127.0.0.1:8765

## 页面导航

| 路径 | 说明 |
|------|------|
| `/` | 任务导航 — 按委托单编号模糊查询任务，点击进入原始记录 |
| `/record` | 原始记录填写与报告生成 |
| `/settings` | LIMIS 账号密码设置 |

## 功能

1. **设置页** — 在页面手动配置 LIMIS 服务器地址、用户名、密码，支持测试登录
2. **任务导航** — 输入委托单编号关键词（模糊匹配），查询任务列表，点击「填写记录」跳转并自动拉取工程信息
3. **工程信息** — 输入委托编号，点击「从 LIMIS 拉取」自动填充；也可全部手工填写
2. **检测参数** — 最大干密度、设计要求、检验标准等
3. **原始记录表格** — 每行 1 测点 + 2 铝盒，点击「计算」自动算湿密度、含水率、干密度、压实系数
4. **生成报告** — 填充 `环刀300.docx` 模板并下载 Word 文件

## API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/` | Web 界面 |
| POST | `/api/calc` | 计算 |
| POST | `/api/limis/login` | LIMIS 登录 |
| GET | `/api/limis/entrust/{no}` | 按委托编号查询工程信息 |
| POST | `/api/report/generate` | 生成并下载 docx 报告 |
