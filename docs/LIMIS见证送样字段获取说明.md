# LIMIS 见证送样委托字段获取说明

## 文档信息

- **记录时间**: 2026年6月15日
- **验证样例**: `TG11-260350`、`TG11-260327`（`testingOrderId=1265826`，检测性质：见证送样）
- **适用工具**: 环刀法压实度检测工具 `RingKnifeDetector`

---

## 一、核心发现

见证送样委托的 **工程见证、样品取样、规格型号、检测依据** 等字段，**不在** `GetTestingOrdersBaseType` 返回的 JSON 中，而是保存在委托单 **HTML 快照** 里。

| 数据来源 | 接口/路径 | 见证送样可用字段 |
|---------|-----------|-----------------|
| 委托基础 JSON | `TestingOrders.ashx` → `method=GetTestingOrdersBaseType` | 委托单位、工程名称、联系方式（电话）、备注、检测性质等 |
| 任务列表 JSON | `Task.ashx` | 样品名称、样品编号、sampleId |
| **委托单 HTML** | `orderRow.standBy3` → `/FileUpload/TestingOrderHtml/{orderId}-{timestamp}.html` | **工程见证、样品取样、规格型号、检测依据及项目** |

探测 `TG11-260350` / `TG11-260327` 时，`orderRow` / `taskRow` / `reportRow` 均无 `witnessUnitName`、`typeSpecification`、`sampleName`、`testBasis` 等结构化键；`jsonOnlyWitness`（仅 JSON 映射）除联系方式外全部为空。拉取 `standBy3` HTML 后字段完整。

### 1.1 TG11-260327 实测（2026-06-15）

`GetTestingOrdersBaseType(1265826)` 返回 **77 个字段**，与见证相关的仅有：

| JSON 键 | 值 | 是否等同 UI 字段 |
|--------|-----|-----------------|
| `standBy3` | `~/FileUpload/TestingOrderHtml/1265826-….html` | HTML 路径 |
| `judgmentBasis` | 空 | 判定依据（未填） |
| `testingSampleAddress` | 浦东机场 | 检测地点，非工程见证 |
| `samplingDate` | 空 | |
| `remark` | 含最大干密度、材料种类、设计要求、取样部位等 | 备注正则可解析 |

**JSON 中不存在** `supervisorUnitName`、`witnessUnitName`、`sampleName`、`typeSpecification`、`testBasis` 等键。

HTML 解析结果（`standBy3`）：

| 字段 | 值 |
|------|-----|
| 工程见证 | 上海同济工程项目管理咨询有限公司 史博 18917610664 |
| 样品取样 | 上海城建城市运营（集团）有限公司 罗红云 |
| 样品名称 | 回填土（环刀） |
| 规格型号 | 200cm³ |
| 检测依据 | JTG 3430-2020《公路土工试验规程》 |

已尝试的其它 API（`GetTestingOrderSampleList`、`GetTestingOrderDetail`、`GetIntegratedQueryInfo` 等）对本单返回 **空 body 或 404**，无法替代 HTML。

---

## 二、HTML 字段位置与格式

委托单 HTML 为检验委托单（通用）打印页，关键结构在 `#tbInfo` 表：

### 2.1 工程见证 / 样品取样

```html
<tr class="apendTr">
  <th>工程见证</th>
  <td colspan="3"> 单位,见证人,工号,手机号 </td>
  <th>样品取样</th>
  <td colspan="3"> 单位,取样人,工号,手机号 </td>
</tr>
```

**样例（TG11-260350）**：

| 字段 | 原始 HTML 单元格内容 | 工具格式化后 |
|------|---------------------|-------------|
| 工程见证 | `上海信达工程建设监理有限公司,叶绍清,18556,15900826288` | `上海信达工程建设监理有限公司 叶绍清 15900826288` |
| 样品取样 | `中国建筑第二工程局有限公司,孟献龙,31875,15257554555` | `中国建筑第二工程局有限公司 孟献龙 15257554555` |

格式化规则：逗号分段，跳过 3–6 位纯数字工号，保留单位、人名、手机/固话。

### 2.2 联系方式（与现场检测的差异）

| 检测性质 | LIMIS 来源 | 说明 |
|---------|-----------|------|
| **现场检测** | `clientPostNo` + `clientTel` | 联系人姓名 + 电话，拼接为「姓名 电话」 |
| **见证送样** | HTML 表头「联系方式」单元格，或 JSON `clientTel` | 通常**仅电话**（如 `021-63245336`），`clientPostNo` 常为空 |

### 2.3 样品名称 / 规格型号

样品表第一行数据（`tr.tbSamples`，序号列为 `1`）：

| 列 | 样例值 |
|----|--------|
| 样品名称 | 回填土（环刀） |
| 型号规格 | `200cm<sup>3</sup>` → 解析为 `200cm³` |

### 2.4 检测依据（检测标准）

解析后**仅保留标准号与书名号名称**，舍弃检测项目等后缀：

| 原始 HTML 内容 | 工具填入值 |
|---------------|-----------|
| `TG11-260350-01: GB/T 50123-2019《土工试验方法标准》( ): (压实系数)` | `GB/T 50123-2019《土工试验方法标准》` |

现场检测与见证送样均从同一 HTML「检验依据及项目」单元格获取（`standBy3` 委托单）。

---

## 三、获取流程（RingKnifeDetector 实现）

```
1. Login → TaskManagement.html 预热会话
2. GetTestingOrdersBaseType(testingOrderId)  → orderRow
3. Task.ashx 按委托编号查 taskRow
4. 若 testingTypeDesc 含「见证送样」：
     a. GET orderRow.standBy3 路径（~/FileUpload/TestingOrderHtml/...）
     b. LimisOrderHtmlParser 解析 HTML
     c. 合并到表单：SupervisionUnit→工程见证、ConstructionUnit→样品取样
5. 备注仍从 orderRow.remark 正则补全（最大干密度、压实系数等）
```

**代码位置**：

- `Services/LimisOrderHtmlParser.cs` — HTML 解析
- `Services/LimisWitnessMapper.cs` — JSON/HTML 字段合并
- `Services/LimisService.cs` — `FetchTestingOrderHtmlAsync`（读取 `standBy3`）

---

## 四、已尝试但不可用的 API

以下方法对 `testingOrderId=1267636` / `sampleId=1873259` 均返回 HTTP 500 或无数据，**不宜作为见证字段来源**：

- `TestingOrders.ashx`: `GetTestingOrderSampleList`、`GetSampleById`、`GetTestingOrderSample` 等
- `Sample.ashx`: `GetSampleById`、`GetSampleInfo`

---

## 五、UI / Word 导出标签映射

检测性质含「见证送样」时，界面与 Word 报告表头使用：

| 现场检测标签 | 见证送样标签 |
|-------------|-------------|
| 监理单位 | **工程见证** |
| 施工单位 | **样品取样** |
| 环刀规格 | **规格型号** |

---

## 六、本地探测命令

设置环境变量 `LIMIS_PROBE=1` 后运行探测测试，可将 `TG11-260350` 原始 JSON + HTML 解析结果写入 `%TEMP%\limis-probe-TG11-260350.json`：

```powershell
cd ori_sheet\RingKnifeDetector
$env:LIMIS_PROBE = "1"
dotnet test -c Release --filter Probe_TG11
```

---

*关联文档：[LIMIS系统新增API接口补充文档](./LIMIS系统新增API接口补充文档.md)*
