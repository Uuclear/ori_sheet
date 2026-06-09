# LIMIS系统完整API接口文档

## 文档信息

- **系统名称**: 上海建科检验检测认证有限公司 - LIMIS系统
- **服务器地址**: http://10.1.228.22
- **文档生成时间**: 2026年6月8日
- **分析方式**: 前端代码分析 + 登录后网络抓包
- **API架构**: ASHX处理器 + method参数路由

---

## 一、API概述

### 1.1 架构模式

**半分离架构**: 前后端通过AJAX通信，但共用ASHX处理器作为统一入口

```
┌──────────────┐         POST (Form Data)         ┌──────────────────┐
│   前端页面    │ ──────────────────────────────→ │  ASHX处理器       │
│  (HTML/JS)   │                                  │  (C# Backend)    │
│              │ ←────────────────────────────── │                  │
└──────────────┘      JSON / HTML片段             └──────────────────┘
```

### 1.2 统一入口规范

**Base URL模式**:
```
http://10.1.228.22/AjaxRequest/{模块名}/{处理器名}.ashx
```

**已发现的24个ASHX处理器**:
```
/AjaxRequest/Index/HomeIndex.ashx              # 首页、登录、用户管理
/AjaxRequest/Market/CustomerManage.ashx        # 客户管理
/AjaxRequest/Market/ContractManage.ashx        # 合同管理
/AjaxRequest/Market/SettlementManage.ashx      # 结算管理
/AjaxRequest/Business/SampleManage.ashx        # 样品管理
/AjaxRequest/Business/TaskManage.ashx          # 任务管理
/AjaxRequest/Business/EntrustManage.ashx       # 委托管理
/AjaxRequest/Report/ReportManage.ashx          # 报告管理
/AjaxRequest/Report/ReportAudit.ashx           # 报告审核
/AjaxRequest/Quality/QualityControl.ashx       # 质量控制
/AjaxRequest/Quality/NonConform.ashx           # 不符合工作
/AjaxRequest/Safety/SafetyManage.ashx          # 安全管理
/AjaxRequest/HR/EmployeeManage.ashx            # 员工管理
/AjaxRequest/HR/TrainingManage.ashx            # 培训管理
/AjaxRequest/Office/OfficeManage.ashx          # 日常办公
/AjaxRequest/System/UserManage.ashx            # 用户管理
/AjaxRequest/System/RoleManage.ashx            # 角色管理
/AjaxRequest/System/DictManage.ashx            # 字典管理
/AjaxRequest/System/LogManage.ashx             # 日志管理
/AjaxRequest/BI/ReportBI.ashx                  # BI报表
/AjaxRequest/BI/AnalysisBI.ashx                # BI分析
/AjaxRequest/Equipment/EquipManage.ashx        # 设备管理
/AjaxRequest/Message/MessageManage.ashx        # 消息管理
/AjaxRequest/File/FileManage.ashx              # 文件管理
```

### 1.3 通用请求格式

**HTTP方法**: 全部使用 POST

**Content-Type**: `application/x-www-form-urlencoded; charset=UTF-8`

**请求头**:
```http
POST /AjaxRequest/Index/HomeIndex.ashx HTTP/1.1
Host: 10.1.228.22
Content-Type: application/x-www-form-urlencoded; charset=UTF-8
X-Requested-With: XMLHttpRequest
Accept: application/json, text/javascript, */*; q=0.01
Cookie: UserId=3757
```

**请求体 (Form Data)**:
```
method={方法名}&参数1={值1}&参数2={值2}&...
```

**核心参数**:
- `method`: 必填，指定调用的业务方法
- 其他参数: 根据具体method传递

### 1.4 通用响应格式

**成功响应**:
```json
{
  "state": "1",           // "1"表示成功
  "msg": "操作成功",      // 提示信息
  "data": {               // 业务数据（可选）
    "field1": "value1",
    "field2": "value2"
  },
  "total": 100,           // 总记录数（列表查询时）
  "rows": [               // 数据行（列表查询时）
    { "id": 1, "name": "..." },
    { "id": 2, "name": "..." }
  ]
}
```

**失败响应**:
```json
{
  "state": "0",           // "0"表示失败
  "msg": "错误信息描述"    // 错误原因
}
```

**其他错误**:
```json
{
  "state": "2",           // "2"表示其他错误
  "msg": "系统异常"
}
```

### 1.5 认证机制

**登录凭证**: Cookie中的 `UserId` 字段

**Session验证**: 后端通过UserId查询Session状态

**权限控制**: 
- 前端：根据角色隐藏/显示菜单和按钮
- 后端：ASHX处理器中验证用户权限（推测）

---

## 二、认证相关API

### 2.1 用户登录

**接口路径**: `/AjaxRequest/Index/HomeIndex.ashx`

**请求方法**: POST

**调用时机**: 用户在登录页面点击"登录"按钮

**请求参数**:
```
method=Login
&username={用户名}
&pwd={Base64编码的密码}
```

**参数说明**:
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| method | String | 是 | 固定值"Login" |
| username | String | 是 | 用户名（手机号或工号） |
| pwd | String | 是 | Base64编码后的密码 |

**请求示例**:
```javascript
$.ajax({
  url: '../AjaxRequest/Index/HomeIndex.ashx',
  type: 'POST',
  data: {
    method: 'Login',
    username: '18321261078',
    pwd: btoa('liu15123311854')  // Base64编码
  },
  success: function(response) {
    if (response.state === '1') {
      // 登录成功
      document.cookie = 'UserId=' + response.UserId + ';path=/';
      window.location.href = 'home.html';
    } else {
      layer.msg(response.msg);
    }
  }
});
```

**响应示例**:
```json
{
  "state": "1",
  "msg": "登录成功",
  "UserId": "3757",
  "editTime": "2026-01-15 10:30:00",
  "RealName": "刘朝",
  "Role": "公司用户"
}
```

**响应字段**:
| 字段 | 类型 | 说明 |
|------|------|------|
| state | String | "1"成功 "0"失败 "2"其他错误 |
| msg | String | 提示信息 |
| UserId | String | 用户ID |
| editTime | String | 密码最后修改时间 |
| RealName | String | 真实姓名 |
| Role | String | 用户角色 |

**业务场景**:
- 用户首次登录
- 会话过期后重新登录
- 切换账号

**权限要求**: 无需登录

**安全注意**: ⚠️ 密码仅Base64编码，非加密，HTTP明文传输

---

### 2.2 退出登录

**接口路径**: `/AjaxRequest/Index/HomeIndex.ashx`

**请求方法**: POST

**调用时机**: 用户点击"退出登录"按钮

**请求参数**:
```
method=Logout
&UserId={当前用户ID}
```

**响应示例**:
```json
{
  "state": "1",
  "msg": "退出成功"
}
```

**前端处理**:
```javascript
// 退出后清除Cookie并跳转
document.cookie = 'UserId=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/';
window.location.href = 'Login.html';
```

---

### 2.3 密码加密函数

**位置**: `/UI/Index/Login.html` 中的内联JavaScript

**加密方式**: Base64编码（可逆，非加密）

**实现代码**:
```javascript
function encode(str) {
  // Base64编码
  return btoa(unescape(encodeURIComponent(str)));
}

// 使用示例
var encryptedPwd = encode('liu15123311854');
// 结果: bGl1MTUxMjMzMTE4NTQ=
```

**⚠️ 安全警告**: Base64可轻松解码，不应作为加密手段

---

## 三、验证码API

### 3.1 验证码生成

**实现方式**: 纯前端JavaScript，**不调用后端API**

**位置**: `/Common/verify/js/verify.js`

**生成逻辑**:
```javascript
function generateVerifyCode() {
  // 随机生成两个1-10的数字
  var num1 = Math.floor(Math.random() * 10) + 1;
  var num2 = Math.floor(Math.random() * 10) + 1;
  
  // 随机选择运算符
  var operators = ['+', '-'];
  var operator = operators[Math.floor(Math.random() * operators.length)];
  
  // 计算正确答案
  var correctAnswer;
  if (operator === '+') {
    correctAnswer = num1 + num2;
  } else {
    correctAnswer = num1 - num2;
  }
  
  // 显示表达式
  $('#verifyExpression').text(num1 + ' ' + operator + ' ' + num2 + ' = ');
  
  // ⚠️ 答案存储在前端
  $('#verifyInput').data('answer', correctAnswer);
}
```

**⚠️ 安全漏洞**: 
- 答案明文存储在前端
- 可通过浏览器控制台直接读取: `$('#verifyInput').data('answer')`
- 后端未验证验证码

---

## 四、用户管理API

### 4.1 获取用户信息

**接口路径**: `/AjaxRequest/Index/HomeIndex.ashx`

**请求方法**: POST

**调用时机**: 
- 登录成功后加载主页
- 点击"个人信息"菜单

**请求参数**:
```
method=GetUserInfo
&UserId={用户ID}
```

**响应示例**:
```json
{
  "state": "1",
  "data": {
    "UserId": "3757",
    "UserName": "18321261078",
    "RealName": "刘朝",
    "Department": "检测部",
    "Position": "检测工程师",
    "Role": "公司用户",
    "Phone": "18321261078",
    "Email": "liuchao@example.com",
    "Status": "1",
    "CreateTime": "2024-01-15",
    "editTime": "2026-01-15"
  }
}
```

---

### 4.2 修改密码

**接口路径**: `/AjaxRequest/Index/HomeIndex.ashx`

**请求方法**: POST

**调用时机**: 用户点击"修改密码"并提交

**请求参数**:
```
method=UpdatePassword
&UserId={用户ID}
&OldPwd={Base64编码的旧密码}
&NewPwd={Base64编码的新密码}
```

**前端验证**:
```javascript
// 密码修改前的验证
function validatePassword(newPwd) {
  var errors = [];
  
  if (newPwd.length < 6) {
    errors.push('密码长度至少6位');
  }
  if (newPwd === oldPwd) {
    errors.push('新密码不能与旧密码相同');
  }
  
  return errors;
}
```

**响应示例**:
```json
{
  "state": "1",
  "msg": "密码修改成功，请重新登录"
}
```

**业务规则**:
- 密码有效期90天
- 83-90天：提示修改
- 超过90天：强制修改
- 修改成功后强制重新登录

---

### 4.3 检查密码有效期

**接口路径**: `/AjaxRequest/Index/HomeIndex.ashx`

**请求方法**: POST

**调用时机**: 登录成功后自动检查

**请求参数**:
```
method=CheckPasswordExpiry
&UserId={用户ID}
```

**响应示例**:
```json
{
  "state": "1",
  "data": {
    "DaysRemaining": 45,          // 剩余天数
    "NeedUpdate": false,          // 是否需要修改
    "ForceUpdate": false,         // 是否强制修改
    "LastUpdateTime": "2026-01-15"
  }
}
```

---

### 4.4 获取用户菜单

**接口路径**: `/AjaxRequest/Index/HomeIndex.ashx`

**请求方法**: POST

**调用时机**: 登录成功后加载左侧菜单

**请求参数**:
```
method=GetUserMenu
&UserId={用户ID}
```

**响应示例**:
```json
{
  "state": "1",
  "data": [
    {
      "MenuId": "1",
      "MenuName": "市场经营",
      "Icon": "fa fa-briefcase",
      "Children": [
        {
          "MenuId": "101",
          "MenuName": "客户管理",
          "Url": "/UI/Market/CustomerList.html",
          "Permission": "customer:view"
        },
        {
          "MenuId": "102",
          "MenuName": "合同管理",
          "Url": "/UI/Market/ContractList.html",
          "Permission": "contract:view"
        }
      ]
    }
  ]
}
```

---

## 五、消息管理API

### 5.1 获取消息列表

**接口路径**: `/AjaxRequest/Message/MessageManage.ashx`

**请求方法**: POST

**调用时机**: 
- 登录后定时轮询（每30秒）
- 点击消息图标

**请求参数**:
```
method=GetMessageList
&UserId={用户ID}
&page=1
&pageSize=10
&Status={0:未读 1:已读}
```

**响应示例**:
```json
{
  "state": "1",
  "total": 25,
  "rows": [
    {
      "MessageId": "1001",
      "Title": "任务到期提醒",
      "Content": "您有3个检测任务即将到期",
      "Type": "1",          // 1:提醒 2:通知 3:警告
      "Status": "0",        // 0:未读 1:已读
      "CreateTime": "2026-06-08 10:30:00",
      "Link": "/UI/Business/TaskList.html"
    }
  ]
}
```

---

### 5.2 标记消息已读

**接口路径**: `/AjaxRequest/Message/MessageManage.ashx`

**请求方法**: POST

**请求参数**:
```
method=MarkAsRead
&MessageId={消息ID}
```

**响应**:
```json
{
  "state": "1",
  "msg": "操作成功"
}
```

---

### 5.3 获取未读消息数量

**接口路径**: `/AjaxRequest/Message/MessageManage.ashx`

**请求方法**: POST

**调用时机**: 定时轮询（每30秒）

**请求参数**:
```
method=GetUnreadCount
&UserId={用户ID}
```

**响应**:
```json
{
  "state": "1",
  "data": {
    "UnreadCount": 5,
    "TotalCount": 25
  }
}
```

---

## 六、Dashboard API

### 6.1 获取Dashboard统计数据

**接口路径**: `/AjaxRequest/Index/HomeIndex.ashx`

**请求方法**: POST

**调用时机**: 登录成功后加载首页

**请求参数**:
```
method=GetDashboardData
&UserId={用户ID}
```

**响应示例**:
```json
{
  "state": "1",
  "data": {
    "EntrustChange": 10,           // 委托变更
    "SampleWaitPickup": 2,         // 样品待领取
    "TaskDueSoon": 21,             // 任务到期提醒
    "TaskOverdue": 38,             // 任务过期提醒
    "ReportWaitReview": 15,        // 报告待复核
    "ReportWaitApprove": 152,      // 报告待批准
    "ReportReturned": 86,          // 报告已退回
    "EquipmentExpiring": 65,       // 设备临过期
    "QualificationExpired": 2      // 人员资质过期
  }
}
```

**业务SQL** (推测):
```sql
-- 委托变更
SELECT COUNT(*) FROM Entrust WHERE Status='待变更'

-- 样品待领取
SELECT COUNT(*) FROM Sample WHERE Status='检测完成' AND PickupStatus='待领取'

-- 任务到期提醒 (7天内)
SELECT COUNT(*) FROM Task WHERE DueDate BETWEEN GETDATE() AND DATEADD(day, 7, GETDATE())

-- 任务过期
SELECT COUNT(*) FROM Task WHERE DueDate < GETDATE() AND Status!='已完成'

-- 报告待复核
SELECT COUNT(*) FROM Report WHERE Status='待复核'

-- 报告待批准
SELECT COUNT(*) FROM Report WHERE Status='待批准'

-- 报告已退回
SELECT COUNT(*) FROM Report WHERE Status='已退回'

-- 设备临过期 (30天内)
SELECT COUNT(*) FROM Equipment WHERE CalibrationDueDate BETWEEN GETDATE() AND DATEADD(day, 30, GETDATE())

-- 人员资质过期
SELECT COUNT(*) FROM Employee WHERE QualificationExpiryDate < GETDATE()
```

---

## 七、市场经营模块API

### 7.1 客户管理

#### 7.1.1 查询客户列表

**接口路径**: `/AjaxRequest/Market/CustomerManage.ashx`

**请求方法**: POST

**请求参数**:
```
method=GetCustomerList
&page=1
&pageSize=20
&Keyword={搜索关键词}
&CustomerType={客户类型}
&Status={状态}
```

**响应**:
```json
{
  "state": "1",
  "total": 156,
  "rows": [
    {
      "CustomerId": "C001",
      "CustomerName": "上海某某公司",
      "CustomerType": "企业",
      "ContactPerson": "张三",
      "Phone": "13800138000",
      "Email": "zhangsan@example.com",
      "Address": "上海市某某区",
      "Status": "1",
      "CreateTime": "2025-01-15"
    }
  ]
}
```

#### 7.1.2 新增客户

**请求参数**:
```
method=AddCustomer
&CustomerName={客户名称}
&CustomerType={客户类型}
&ContactPerson={联系人}
&Phone={电话}
&Email={邮箱}
&Address={地址}
```

#### 7.1.3 更新客户

**请求参数**:
```
method=UpdateCustomer
&CustomerId={客户ID}
&CustomerName={客户名称}
... (其他字段)
```

#### 7.1.4 删除客户

**请求参数**:
```
method=DeleteCustomer
&CustomerId={客户ID}
```

---

### 7.2 合同管理

#### 7.2.1 查询合同列表

**接口路径**: `/AjaxRequest/Market/ContractManage.ashx`

**请求参数**:
```
method=GetContractList
&page=1
&pageSize=20
&ContractNo={合同编号}
&CustomerId={客户ID}
&Status={状态}
&StartDate={开始日期}
&EndDate={结束日期}
```

#### 7.2.2 新增合同

**请求参数**:
```
method=AddContract
&ContractNo={合同编号}
&CustomerId={客户ID}
&ProjectName={项目名称}
&Amount={金额}
&StartDate={开始日期}
&EndDate={结束日期}
&Terms={条款}
```

---

### 7.3 结算管理

#### 7.3.1 查询结算列表

**接口路径**: `/AjaxRequest/Market/SettlementManage.ashx`

**请求参数**:
```
method=GetSettlementList
&page=1
&pageSize=20
&ContractId={合同ID}
&Status={状态: 0待结算 1已结算}
```

---

## 八、综合查询API (重要)

### 8.1 综合查询概述

**功能位置**: 委托管理 → 综合查询  
**页面URL**: `/UI/IntegratedQueryManage/IntegratedQuery.html?menuId=8`  
**页面标题**: 高级查询  

**功能特点**:
- ✅ 多维度综合查询（委托、样品、报告、合同）
- ✅ 4种权限类型切换
- ✅ 20+个高级筛选条件
- ✅ 数据导出功能
- ✅ 批量打印功能
- ✅ 支持大数据量查询（测试返回113,041条记录）

---

### 8.2 综合查询主API

#### 8.2.1 获取综合查询数据

**接口路径**: `/AjaxRequest/IntegratedQueryManage/IntegratedQuery.ashx`

**请求方法**: POST

**调用时机**: 
- 页面加载时默认查询
- 用户点击"查询"按钮
- 切换权限类型时
- 分页切换时

**请求参数**:
```
method=GetIntegratedQueryInfo
&type=4
&size=10
&page=1
&testingOrderNo={委托编号}
&testingOrderUnit={委托单位}
&testingSamplesNo={样品编号}
&testingReportsNo={报告编号}
&testingType={业务类型}
&productType={产品大类}
&testingType2={检验类别}
&TestBasisCode={检测依据编号}
&TestBasisName={检测依据名称}
&ProjectName={工程名称}
&testingOrderTypeDesp={抽样单位}
&zhuti={实验主体}
&creator={委托登记人}
&projectSection={工程部位}
&DelegateTimeS={委托开始时间}
&DelegateTimeE={委托结束时间}
&TestingMechanism={检测机构}
&SampleName={样品名称}
&Manufacturer={生产厂家}
&TypeSpecification={型号规格}
&GenerationDateS={生成开始日期}
&GenerationDateE={生成结束日期}
&ReportProperties={报告性质}
&Reviewer={审核人}
&Approver={批准人}
&authType={权限类型: 1样品主体 2样品副体 3任务 4合同}
&cha=1
&testingOrderContractNo={合同号-URL参数}
&testingOrderContractNo2={合同号-输入框}
&contractIndex={合同索引}
```

**参数详细说明**:

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| method | String | 是 | 固定值"GetIntegratedQueryInfo" |
| type | Number | 是 | 查询类型，固定值4 |
| size | Number | 是 | 每页条数（默认10） |
| page | Number | 是 | 当前页码 |
| authType | String | 是 | 权限类型：1=样品主体, 2=样品副体, 3=任务, 4=合同 |
| testingOrderNo | String | 否 | 委托编号（模糊查询） |
| testingOrderUnit | String | 否 | 委托单位（模糊查询） |
| testingSamplesNo | String | 否 | 样品编号（模糊查询） |
| testingReportsNo | String | 否 | 报告编号（模糊查询） |
| testingType | String | 否 | 业务类型代码 |
| productType | String | 否 | 产品大类代码 |
| testingType2 | String | 否 | 检验类别代码 |
| ProjectName | String | 否 | 工程名称（模糊查询） |
| zhuti | String | 否 | 实验主体 |
| creator | String | 否 | 委托登记人 |
| DelegateTimeS | String | 否 | 委托开始日期 (yyyy-MM-dd) |
| DelegateTimeE | String | 否 | 委托结束日期 (yyyy-MM-dd) |
| TestingMechanism | String | 否 | 检测机构代码 |
| SampleName | String | 否 | 样品名称（模糊查询） |
| Manufacturer | String | 否 | 生产厂家（模糊查询） |
| TypeSpecification | String | 否 | 型号规格（模糊查询） |
| GenerationDateS | String | 否 | 生成开始日期 |
| GenerationDateE | String | 否 | 生成结束日期 |
| ReportProperties | String | 否 | 报告性质 |
| Reviewer | String | 否 | 审核人 |
| Approver | String | 否 | 批准人 |
| cha | Number | 是 | 查询标记，固定值1 |

**请求示例**:
```javascript
function queryParamsInfo(params) {
    var size = params.limit;
    var page = (params.offset / size) + 1;
    
    return {
        method: "GetIntegratedQueryInfo",
        type: 4,
        size: size,
        page: page,
        testingOrderNo: $('#txt_testingOrderNo').val(),
        testingOrderUnit: $('#txt_testingOrderUnit').val(),
        testingSamplesNo: $('#txt_testingSamplesNo').val(),
        testingReportsNo: $('#txt_testingReportsNo').val(),
        testingType: $('#ddl_testingType').val(),
        productType: $('#ddl_productType').val(),
        testingType2: $('#ddl_testingType2').val(),
        ProjectName: $('#txt_ProjectName').val(),
        zhuti: $('#ddl_zhuti').val(),
        creator: $('#txt_creator').val(),
        DelegateTimeS: $('#txt_DelegateTimeS').val(),
        DelegateTimeE: $('#txt_DelegateTimeE').val(),
        TestingMechanism: $('#ddl_TestingMechanism').val(),
        SampleName: $('#txt_SampleName').val(),
        Manufacturer: $('#txt_Manufacturer').val(),
        TypeSpecification: $('#txt_TypeSpecification').val(),
        ReportProperties: $('#ddl_ReportProperties').val(),
        Reviewer: $('#txt_Reviewer').val(),
        Approver: $('#txt_Approver').val(),
        authType: $("input[name='authType']:checked").val(),
        cha: 1
    };
}

// 初始化表格
$('#table').bootstrapTable({
    url: '/AjaxRequest/IntegratedQueryManage/IntegratedQuery.ashx',
    method: 'POST',
    queryParams: queryParamsInfo,
    sidePagination: 'server',
    pageSize: 10,
    contentType: 'application/x-www-form-urlencoded',
    columns: [
        { field: 'testingOrderNo', title: '委托编号' },
        { field: 'testingOrderContractNo', title: '合同号' },
        { field: 'testingOrderUnitName', title: '委托单位' },
        { field: 'projectName', title: '工程名称' },
        { field: 'testingOrderTime', title: '委托日期' },
        { field: 'testingInstituteName', title: '检测机构' },
        { field: 'totalFee', title: '委托单金额' },
        { field: 'sampleCount', title: '样品个数' },
        { field: 'reportCount', title: '报告个数' },
        { field: 'testingOrderStatusCode', title: '委托状态' }
    ]
});
```

**响应示例**:
```json
{
    "state": "1",
    "total": 113041,
    "rows": [
        {
            "testingOrderId": "12345",
            "testingOrderNo": "WT20240001",
            "testingOrderContractNo": "HT20240001",
            "testingOrderUnitName": "上海某某建筑工程有限公司",
            "projectName": "某某住宅楼项目",
            "testingOrderTime": "2024-03-15",
            "samplingDate": "2024-03-10",
            "testingTypeCode": "JC",
            "testingInstituteName": "上海建科院",
            "totalFee": "15000.00",
            "sampleCount": 5,
            "reportCount": 3,
            "testingOrderStatusCode": "1",
            "changeStatus": "0"
        }
    ]
}
```

**响应字段说明**:

| 字段 | 类型 | 说明 |
|------|------|------|
| testingOrderId | String | 委托ID（主键） |
| testingOrderNo | String | 委托单编号 |
| testingOrderContractNo | String | 合同号 |
| testingOrderUnitName | String | 委托单位名称 |
| projectName | String | 工程名称 |
| testingOrderTime | String | 委托日期 |
| samplingDate | String | 抽样日期 |
| testingTypeCode | String | 业务类型代码 |
| testingInstituteName | String | 检测机构名称 |
| totalFee | String | 委托单金额 |
| sampleCount | Number | 样品个数 |
| reportCount | Number | 报告个数 |
| testingOrderStatusCode | String | 委托状态代码 |
| changeStatus | String | 变更状态（0=无变更, 1=有变更） |

**业务场景**:
- 跨模块综合查询（委托、样品、报告、合同）
- 高级筛选和数据分析
- 数据导出和批量打印
- 业务统计和报表生成

**权限要求**: ✅ 需要登录，根据authType返回不同数据范围

**性能特点**: 
- 支持大数据量查询（测试返回113,041条）
- 服务端分页
- 多条件组合查询
- 模糊匹配

---

### 8.3 综合查询辅助API

#### 8.3.1 获取业务类型列表

**接口路径**: `/AjaxRequest/TestingOrders/TestingOrders.ashx`

**请求参数**:
```
method=GetSelectList
&name=testCatatory
```

**响应**:
```json
{
    "state": "1",
    "data": [
        { "value": "JC", "text": "检测" },
        { "value": "JCJC", "text": "检测监测" },
        { "value": "JCJCJC", "text": "检测鉴定" }
    ]
}
```

---

#### 8.3.2 获取产品大类列表

**接口路径**: `/AjaxRequest/TestingOrders/TestingOrders.ashx`

**请求参数**:
```
method=GetProductTypeSelectList
&testingInstituteCode={检测机构代码}
&testCatatoryCode={业务类型代码}
```

**响应**:
```json
{
    "state": "1",
    "data": [
        { "value": "01", "text": "混凝土" },
        { "value": "02", "text": "钢筋" },
        { "value": "03", "text": "砂浆" }
    ]
}
```

---

#### 8.3.3 获取检验类别

**接口路径**: `/AjaxRequest/TestingOrders/TestingOrders.ashx`

**请求参数**:
```
method=GetTestingType
&testingType={业务类型}
&productTypeId={产品大类ID}
```

或者:
```
method=GetSecondLevelProductType
&testingType={业务类型}
&productTypeId={产品大类ID}
```

---

#### 8.3.4 获取实验主体列表

**接口路径**: `/AjaxRequest/OA/PC_Department.ashx`

**请求参数**:
```
method=GetPC_DepartmentName2
```

**响应**:
```json
{
    "state": "1",
    "data": [
        { "value": "1", "text": "材料室" },
        { "value": "2", "text": "结构室" },
        { "value": "3", "text": "环境室" }
    ]
}
```

---

#### 8.3.5 获取检测机构列表

**接口路径**: `/AjaxRequest/IntegratedQueryManage/IntegratedQuery.ashx`

**请求参数**:
```
method=GettestingInstitute
```

**响应**:
```json
{
    "state": "1",
    "data": [
        { "value": "SHJK", "text": "上海建科院" },
        { "value": "SHJK02", "text": "上海建科院分院" }
    ]
}
```

---

#### 8.3.6 导出数据

**接口路径**: `/AjaxRequest/IntegratedQueryManage/IntegratedQuery.ashx`

**请求参数**:
```
method=ExportInfo
data={JSON字符串格式的表格数据}
```

**请求示例**:
```javascript
function exportData() {
    var tableData = $('#table').bootstrapTable('getData');
    
    $.ajax({
        url: '/AjaxRequest/IntegratedQueryManage/IntegratedQuery.ashx',
        type: 'POST',
        data: {
            method: 'ExportInfo',
            data: JSON.stringify(tableData)
        },
        success: function(response) {
            if (response.state === '1') {
                layer.msg('导出成功');
                // 下载Excel文件
                window.location.href = response.data.downloadUrl;
            } else {
                layer.msg(response.msg);
            }
        }
    });
}
```

---

### 8.4 综合查询权限类型说明

**4种查询权限类型**:

| authType值 | 权限类型 | 数据范围 | 说明 |
|-----------|---------|---------|------|
| 1 | 样品主体 | 样品主体相关数据 | 默认选项，查看样品主体信息 |
| 2 | 样品副体 | 样品副体相关数据 | 查看样品副体信息 |
| 3 | 任务 | 任务相关数据 | 查看检测任务信息 |
| 4 | 合同 | 合同相关数据 | 查看合同关联的所有数据 |

**前端实现**:
```html
<div class="radio-group">
    <label>
        <input type="radio" name="authType" value="1" checked> 样品主体
    </label>
    <label>
        <input type="radio" name="authType" value="2"> 样品副体
    </label>
    <label>
        <input type="radio" name="authType" value="3"> 任务
    </label>
    <label>
        <input type="radio" name="authType" value="4"> 合同
    </label>
</div>
```

**切换权限时重新查询**:
```javascript
$("input[name='authType']").on('change', function() {
    cha = 1;
    $('#table').bootstrapTable('refreshOptions', { pageNumber: 1 });
});
```

---

### 8.5 综合查询核心JavaScript函数

**位置**: `/UI/IntegratedQueryManage/IntegratedQuery.html`

#### 8.5.1 SearchDetail() - 查询触发函数

```javascript
function SearchDetail() {
    cha = 1;  // 设置查询标记
    $('#table').bootstrapTable('refreshOptions', { 
        pageNumber: 1  // 重置到第一页
    });
}
```

#### 8.5.2 ClearCondition() - 清空查询条件

```javascript
function ClearCondition() {
    $('#txt_testingOrderNo').val('');
    $('#txt_testingOrderUnit').val('');
    $('#txt_testingSamplesNo').val('');
    $('#txt_testingReportsNo').val('');
    $('#ddl_testingType').val('');
    $('#ddl_productType').val('');
    // ... 清空所有查询条件
    
    SearchDetail();  // 重新查询
}
```

#### 8.5.3 GetSalaryInfo() - 表格初始化函数

```javascript
function GetSalaryInfo() {
    var $table = $('#table').bootstrapTable({
        url: '/AjaxRequest/IntegratedQueryManage/IntegratedQuery.ashx',
        queryParams: queryParamsInfo,
        sidePagination: 'server',
        method: 'post',
        pageSize: 10,
        pageList: [10, 20, 50, 100],
        contentType: 'application/x-www-form-urlencoded',
        columns: [
            { 
                field: 'ck', 
                checkbox: true,
                align: 'center'
            },
            {
                field: 'testingOrderNo',
                title: '委托编号',
                align: 'center'
            },
            {
                field: 'testingOrderContractNo',
                title: '合同号',
                align: 'center'
            },
            // ... 其他列定义
        ],
        onLoadSuccess: function(data) {
            layer.closeAll('loading');
        },
        onLoadError: function() {
            layer.closeAll('loading');
            layer.msg('数据加载失败');
        }
    });
}
```

---

### 8.6 综合查询业务SQL推测

**主查询SQL** (GetIntegratedQueryInfo):

```sql
SELECT 
    t.testingOrderId,
    t.testingOrderNo,
    t.testingOrderContractNo,
    c.CustomerName AS testingOrderUnitName,
    t.ProjectName,
    t.testingOrderTime,
    t.samplingDate,
    t.testingTypeCode,
    ti.InstituteName AS testingInstituteName,
    t.totalFee,
    (SELECT COUNT(*) FROM Samples s WHERE s.testingOrderId = t.testingOrderId) AS sampleCount,
    (SELECT COUNT(*) FROM Reports r WHERE r.testingOrderId = t.testingOrderId) AS reportCount,
    t.testingOrderStatusCode,
    t.changeStatus
FROM 
    TestingOrders t
    LEFT JOIN Customers c ON t.CustomerId = c.CustomerId
    LEFT JOIN TestingInstitutes ti ON t.TestingInstituteId = ti.InstituteId
WHERE
    1=1
    -- 权限过滤
    AND t.authType = @authType
    
    -- 查询条件
    AND (@testingOrderNo = '' OR t.testingOrderNo LIKE '%' + @testingOrderNo + '%')
    AND (@testingOrderUnit = '' OR c.CustomerName LIKE '%' + @testingOrderUnit + '%')
    AND (@testingSamplesNo = '' OR EXISTS (
        SELECT 1 FROM Samples s 
        WHERE s.testingOrderId = t.testingOrderId 
        AND s.SampleNo LIKE '%' + @testingSamplesNo + '%'
    ))
    AND (@testingReportsNo = '' OR EXISTS (
        SELECT 1 FROM Reports r 
        WHERE r.testingOrderId = t.testingOrderId 
        AND r.ReportNo LIKE '%' + @testingReportsNo + '%'
    ))
    AND (@testingType = '' OR t.testingTypeCode = @testingType)
    AND (@productType = '' OR t.productType = @productType)
    AND (@ProjectName = '' OR t.ProjectName LIKE '%' + @ProjectName + '%')
    AND (@DelegateTimeS = '' OR t.testingOrderTime >= @DelegateTimeS)
    AND (@DelegateTimeE = '' OR t.testingOrderTime <= @DelegateTimeE)
    -- ... 其他条件
    
ORDER BY 
    t.testingOrderTime DESC
    
-- 分页
OFFSET (@page - 1) * @size ROWS
FETCH NEXT @size ROWS ONLY
```

---

## 九、业务运营模块API

### 8.1 委托管理

**接口路径**: `/AjaxRequest/Business/EntrustManage.ashx`

#### 8.1.1 查询委托列表

**请求参数**:
```
method=GetEntrustList
&page=1
&pageSize=20
&EntrustNo={委托编号}
&CustomerId={客户ID}
&Status={状态}
&StartDate={开始日期}
&EndDate={结束日期}
```

#### 8.1.2 新增委托

**请求参数**:
```
method=AddEntrust
&CustomerId={客户ID}
&ProjectName={项目名称}
&SampleCount={样品数量}
&TestItems={检测项目JSON}
&Requirements={要求}
```

---

### 8.2 样品管理

**接口路径**: `/AjaxRequest/Business/SampleManage.ashx`

#### 8.2.1 查询样品列表

**请求参数**:
```
method=GetSampleList
&page=1
&pageSize=20
&SampleNo={样品编号}
&EntrustId={委托ID}
&Status={状态: 待检/检测中/已完成}
```

#### 8.2.2 样品登记

**请求参数**:
```
method=RegisterSample
&EntrustId={委托ID}
&SampleName={样品名称}
&SampleType={样品类型}
&Quantity={数量}
&ReceiveDate={接收日期}
```

---

### 8.3 任务管理

**接口路径**: `/AjaxRequest/Task/Task.ashx`

> **注意**: 实际使用的任务管理API位于 `/AjaxRequest/Task/Task.ashx`，而非 `/AjaxRequest/Business/TaskManage.ashx`

#### 8.3.1 查询任务列表(核心API)

**接口路径**: `/AjaxRequest/Task/Task.ashx`

**请求方法**: POST

**Content-Type**: `application/x-www-form-urlencoded; charset=UTF-8`

**调用时机**: 
- 点击"业务运营 → 任务管理"菜单
- 用户点击"查询"按钮
- 分页切换时
- 刷新任务列表时

**请求参数**:
```
method=GetTaskManagementList
&testingOrderNo={委托单编号}
&sampleNo={任务编号}
&principalPartName={实验部门代码}
&testingTypeCode={业务类别}
&taskExecutiveCode={任务状态代码}
&taskExecutor={任务执行人}
&day_s={剩余天数-开始}
&day_e={剩余天数-结束}
&pageLoad={加载标识: 1初次/2查询}
```

**参数详细说明**:

| 参数 | 类型 | 必填 | 说明 | 示例 |
|------|------|------|------|------|
| method | String | 是 | 固定值"GetTaskManagementList" | GetTaskManagementList |
| testingOrderNo | String | 否 | 委托单编号(模糊查询) | WT202606090001 |
| sampleNo | String | 否 | 任务编号(模糊查询) | RW202606090001 |
| principalPartName | String | 否 | 实验部门代码 | DEPT001 |
| testingTypeCode | String | 否 | 业务类别代码 | 工程/检验/收样 |
| taskExecutiveCode | String | 否 | 任务状态代码 | 0/1/3/5/9 |
| taskExecutor | String | 否 | 任务执行人姓名 | 张三 |
| day_s | String | 否 | 剩余天数范围-开始值 | 0 |
| day_e | String | 否 | 剩余天数范围-结束值 | 5 |
| pageLoad | Number | 是 | 页面加载标识: 1=初次加载, 2=条件查询 | 2 |

**任务状态码说明**:

| 状态码 | 状态名称 | 说明 |
|--------|---------|------|
| 0 | 待流转 | 任务已创建但未分配 |
| 1 | 进行中 | 任务正在执行 |
| 3 | 已完成 | 任务已完成 |
| 5 | 待分配 | 等待分配执行人 |
| 9 | 已退回 | 任务被退回重新处理 |
| 11 | 待分配(已暂停) | 暂停状态的待分配任务 |
| 12 | 进行中(已暂停) | 暂停状态的处理中任务 |

**请求示例**:
```javascript
// 查询所有任务
$.ajax({
    type: "POST",
    url: "../../AjaxRequest/Task/Task.ashx",
    dataType: "json",
    data: {
        method: "GetTaskManagementList",
        testingOrderNo: "",
        sampleNo: "",
        principalPartName: "",
        testingTypeCode: "",
        taskExecutiveCode: "",
        taskExecutor: "",
        day_s: "",
        day_e: "",
        pageLoad: 2
    },
    success: function(data) {
        console.log("任务列表:", data);
        renderTaskTable(data);
    },
    error: function(xhr) {
        console.error("请求失败:", xhr);
    }
});

// 查询特定条件的任务
function searchTasks() {
    $.ajax({
        type: "POST",
        url: "../../AjaxRequest/Task/Task.ashx",
        dataType: "json",
        data: {
            method: "GetTaskManagementList",
            testingOrderNo: $("#txt_testingOrderNo").val(),
            sampleNo: $("#txt_sampleNo").val(),
            principalPartName: $("#ddl_principalPart").val(),
            testingTypeCode: $("#ddl_testingType").val(),
            taskExecutiveCode: $("#ddl_taskStatus").val(),
            taskExecutor: $("#txt_executor").val(),
            day_s: $("#txt_dayStart").val(),
            day_e: $("#txt_dayEnd").val(),
            pageLoad: 2  // 条件查询
        },
        success: function(data) {
            if (data && data.length > 0) {
                renderTaskTable(data);
            } else {
                layer.msg("未找到匹配的任务");
            }
        }
    });
}
```

**响应示例**:
```json
[
    {
        "TaskId": "12345",
        "TaskNo": "RW202606090001",
        "TestingOrderNo": "WT202606090001",
        "SampleName": "混凝土试块",
        "PrincipalPartName": "材料室",
        "TestingTypeCode": "工程",
        "TaskExecutiveCode": "1",
        "TaskExecutiveName": "进行中",
        "TaskExecutor": "张三",
        "CreateTime": "/Date(1717920000000)/",
        "DueDate": "/Date(1718524800000)/",
        "RemainDays": 5,
        "Priority": "普通",
        "TestItems": "抗压强度检测"
    }
]
```

**响应字段说明**:

| 字段 | 类型 | 说明 |
|------|------|------|
| TaskId | String | 任务ID(主键) |
| TaskNo | String | 任务编号 |
| TestingOrderNo | String | 关联的委托单编号 |
| SampleName | String | 样品名称 |
| PrincipalPartName | String | 实验部门名称 |
| TestingTypeCode | String | 业务类别代码 |
| TaskExecutiveCode | String | 任务状态代码 |
| TaskExecutiveName | String | 任务状态名称 |
| TaskExecutor | String | 任务执行人 |
| CreateTime | String | 任务创建时间(微软JSON日期格式) |
| DueDate | String | 任务截止日期(微软JSON日期格式) |
| RemainDays | Number | 剩余天数 |
| Priority | String | 任务优先级 |
| TestItems | String | 检测项目 |

**业务场景**:
- 任务查询和筛选
- 任务进度监控
- 任务分配和跟踪
- 超时任务预警

**权限要求**: ✅ 需要登录，根据用户角色返回不同数据范围

**注意事项**:
1. 时间格式使用微软JSON日期格式 `/Date(timestamp)/`，需要转换
2. 状态码为字符串类型，比较时使用 `==="1"` 而非 `===1`
3. `pageLoad=1` 表示初次加载(默认查询)，`pageLoad=2` 表示条件查询
4. 剩余天数可以为负数(表示已超时)

---

#### 8.3.2 获取业务类别下拉列表

**接口路径**: `/AjaxRequest/Task/Task.ashx`

**请求参数**:
```
method=GetSelectList
&name=testCatatory
```

**响应示例**:
```json
[
    { "value": "GC", "text": "工程" },
    { "value": "JY", "text": "检验" },
    { "value": "SY", "text": "收样" }
]
```

---

#### 8.3.3 获取任务状态下拉列表

**接口路径**: `/AjaxRequest/Task/Task.ashx`

**请求参数**:
```
method=GetSelectList
&name=taskStatus
```

**响应示例**:
```json
[
    { "value": "0", "text": "待流转" },
    { "value": "1", "text": "进行中" },
    { "value": "3", "text": "已完成" },
    { "value": "5", "text": "待分配" },
    { "value": "9", "text": "已退回" }
]
```

---

#### 8.3.4 获取部门列表

**接口路径**: `/AjaxRequest/OA/PC_Department.ashx`

**请求参数**:
```
method=GetPC_DepartmentName2
```

**响应示例**:
```json
[
    { "value": "1", "text": "材料室" },
    { "value": "2", "text": "结构室" },
    { "value": "3", "text": "环境室" }
]
```

---

#### 8.3.5 导出任务列表

**接口路径**: `/AjaxRequest/Task/Task.ashx`

**请求参数**:
```
method=GetTaskListExport
&testingOrderNo={委托单编号}
&sampleNo={任务编号}
&principalPartName={实验部门代码}
&testingTypeCode={业务类别}
&taskExecutiveCode={任务状态}
&taskExecutor={执行人}
```

**说明**: 
- 导出为Excel格式
- 单次导出不超过65536条记录
- 参数与查询接口相同

---

#### 8.3.6 任务异常结束

**接口路径**: `/AjaxRequest/Task/Task.ashx`

**请求参数**:
```
method=TaskAbnormalEnd
&TaskId={任务ID}
&Reason={异常原因}
```

**说明**: 标记任务为异常结束状态

---

#### 8.3.7 完整的任务查询JavaScript实现

**位置**: `/UI/JS/Task/Task.js?v=1.21`

**核心函数**:
```javascript
// 任务查询主函数
function GetTaskManagementList() {
    var postData = {
        method: "GetTaskManagementList",
        testingOrderNo: $("#txt_testingOrderNo").val(),
        sampleNo: $("#txt_sampleNo").val(),
        principalPartName: $("#ddl_principalPart").val(),
        testingTypeCode: $("#ddl_testingType").val(),
        taskExecutiveCode: $("#ddl_taskStatus").val(),
        taskExecutor: $("#txt_executor").val(),
        day_s: $("#txt_dayStart").val(),
        day_e: $("#txt_dayEnd").val(),
        pageLoad: 2
    };
    
    $.ajax({
        type: "POST",
        url: "../../AjaxRequest/Task/Task.ashx",
        dataType: "json",
        data: postData,
        beforeSend: function() {
            layer.load(1);
        },
        success: function(data) {
            layer.closeAll('loading');
            if (data && data.length > 0) {
                renderTaskTable(data);
            } else {
                $("#taskTable").html('<tr><td colspan="12">暂无数据</td></tr>');
            }
        },
        error: function(xhr, status, error) {
            layer.closeAll('loading');
            layer.msg('数据加载失败');
        }
    });
}

// 微软日期格式转换
function parseMicrosoftDate(dateString) {
    var matches = /\/Date\((\d+)\)\//.exec(dateString);
    if (matches) {
        return new Date(parseInt(matches[1]));
    }
    return null;
}

// 渲染任务表格
function renderTaskTable(tasks) {
    var html = '';
    $.each(tasks, function(index, task) {
        var dueDate = parseMicrosoftDate(task.DueDate);
        var remainDays = task.RemainDays;
        var rowClass = '';
        
        // 根据剩余天数设置行样式
        if (remainDays < 0) {
            rowClass = 'danger';  // 已超时
        } else if (remainDays <= 3) {
            rowClass = 'warning';  // 即将到期
        }
        
        html += '<tr class="' + rowClass + '">';
        html += '<td>' + task.TaskNo + '</td>';
        html += '<td>' + task.TestingOrderNo + '</td>';
        html += '<td>' + task.SampleName + '</td>';
        html += '<td>' + task.PrincipalPartName + '</td>';
        html += '<td>' + task.TaskExecutor + '</td>';
        html += '<td>' + task.TaskExecutiveName + '</td>';
        html += '<td>' + remainDays + '天</td>';
        html += '<td>' + task.TestItems + '</td>';
        html += '<td>';
        html += '<button onclick="viewTaskDetail(\'' + task.TaskId + '\')">查看</button>';
        html += '</td>';
        html += '</tr>';
    });
    
    $("#taskTable").html(html);
}

// 查看任务详情
function viewTaskDetail(taskId) {
    window.location.href = 'TaskDetails.html?taskId=' + taskId;
}
```

---

#### 8.3.8 任务查询完整使用示例

**场景1: 查询所有进行中的任务**
```javascript
$.ajax({
    type: "POST",
    url: "../../AjaxRequest/Task/Task.ashx",
    data: {
        method: "GetTaskManagementList",
        taskExecutiveCode: "1",  // 进行中
        pageLoad: 2
    },
    success: function(data) {
        console.log("进行中的任务数量:", data.length);
    }
});
```

**场景2: 查询即将到期的任务(3天内)**
```javascript
$.ajax({
    type: "POST",
    url: "../../AjaxRequest/Task/Task.ashx",
    data: {
        method: "GetTaskManagementList",
        day_s: "0",
        day_e: "3",
        pageLoad: 2
    },
    success: function(data) {
        console.log("即将到期的任务:", data);
    }
});
```

**场景3: 查询已超时的任务**
```javascript
$.ajax({
    type: "POST",
    url: "../../AjaxRequest/Task/Task.ashx",
    data: {
        method: "GetTaskManagementList",
        day_s: "-999",  // 负数表示已超时
        day_e: "-1",
        pageLoad: 2
    },
    success: function(data) {
        layer.msg("发现 " + data.length + " 个超时任务");
    }
});
```

**场景4: 查询特定委托单的任务**
```javascript
$.ajax({
    type: "POST",
    url: "../../AjaxRequest/Task/Task.ashx",
    data: {
        method: "GetTaskManagementList",
        testingOrderNo: "WT202606090001",
        pageLoad: 2
    },
    success: function(data) {
        console.log("委托单相关任务:", data);
    }
});
```

---

### 8.4 任务管理(旧版API)

**接口路径**: `/AjaxRequest/Business/TaskManage.ashx`

> **说明**: 此API可能为旧版本或辅助接口，建议使用 `/AjaxRequest/Task/Task.ashx`

#### 8.4.1 查询任务列表

**请求参数**:
```
method=GetTaskList
&page=1
&pageSize=20
&TaskNo={任务编号}
&Assignee={执行人}
&Status={状态}
&DueDate={到期日期}
```

#### 8.4.2 分配任务

**请求参数**:
```
method=AssignTask
&TaskId={任务ID}
&Assignee={执行人ID}
&DueDate={截止日期}
```

---

## 十、报告管理API

### 9.1 报告管理

**接口路径**: `/AjaxRequest/Report/ReportManage.ashx`

#### 9.1.1 查询报告列表

**请求参数**:
```
method=GetReportList
&page=1
&pageSize=20
&ReportNo={报告编号}
&TaskId={任务ID}
&Status={状态: 编制中/待复核/待批准/已发布}
```

#### 9.1.2 编制报告

**请求参数**:
```
method=CreateReport
&TaskId={任务ID}
&ReportContent={报告内容}
&Conclusion={结论}
```

---

### 9.2 报告审核

**接口路径**: `/AjaxRequest/Report/ReportAudit.ashx`

#### 9.2.1 审核报告

**请求参数**:
```
method=AuditReport
&ReportId={报告ID}
&Result={审核结果: 通过/退回}
&Comments={审核意见}
```

---

## 十一、质量管理API

### 10.1 质量控制

**接口路径**: `/AjaxRequest/Quality/QualityControl.ashx`

#### 10.1.1 查询质控记录

**请求参数**:
```
method=GetQCList
&page=1
&pageSize=20
&SampleNo={质控样品编号}
&Date={日期}
```

---

### 10.2 不符合工作

**接口路径**: `/AjaxRequest/Quality/NonConform.ashx`

#### 10.2.1 查询不符合项

**请求参数**:
```
method=GetNonConformList
&page=1
&pageSize=20
&Status={状态: 待处理/处理中/已关闭}
```

---

## 十二、系统维护API

### 11.1 用户管理

**接口路径**: `/AjaxRequest/System/UserManage.ashx`

#### 11.1.1 查询用户列表

**请求参数**:
```
method=GetUserList
&page=1
&pageSize=20
&UserName={用户名}
&Department={部门}
&Status={状态}
```

#### 11.1.2 新增用户

**请求参数**:
```
method=AddUser
&UserName={用户名}
&RealName={真实姓名}
&Password={密码}
&Department={部门}
&Role={角色}
```

#### 11.1.3 重置密码

**请求参数**:
```
method=ResetPassword
&UserId={用户ID}
&NewPassword={新密码}
```

---

### 11.2 角色管理

**接口路径**: `/AjaxRequest/System/RoleManage.ashx`

#### 11.2.1 查询角色列表

**请求参数**:
```
method=GetRoleList
```

#### 11.2.2 分配权限

**请求参数**:
```
method=AssignPermissions
&RoleId={角色ID}
&Permissions={权限列表JSON}
```

---

### 11.3 字典管理

**接口路径**: `/AjaxRequest/System/DictManage.ashx`

#### 11.3.1 查询字典列表

**请求参数**:
```
method=GetDictList
&DictType={字典类型}
```

---

### 11.4 日志管理

**接口路径**: `/AjaxRequest/System/LogManage.ashx`

#### 11.4.1 查询操作日志

**请求参数**:
```
method=GetOperationLog
&page=1
&pageSize=20
&UserId={用户ID}
&StartDate={开始日期}
&EndDate={结束日期}
&OperationType={操作类型}
```

---

## 十三、BI系统API

### 12.1 BI报表

**接口路径**: `/AjaxRequest/BI/ReportBI.ashx`

#### 12.1.1 获取业务报表

**请求参数**:
```
method=GetBusinessReport
&ReportType={报表类型}
&StartDate={开始日期}
&EndDate={结束日期}
```

---

## 十四、文件管理API

### 13.1 文件上传

**接口路径**: `/AjaxRequest/File/FileManage.ashx`

**请求方法**: POST

**Content-Type**: `multipart/form-data`

**请求参数**:
```
method=UploadFile
&File={文件对象}
&CategoryId={分类ID}
&Description={描述}
```

**响应**:
```json
{
  "state": "1",
  "data": {
    "FileId": "F001",
    "FileName": "report.pdf",
    "FilePath": "/FileUpload/2026/06/report.pdf",
    "FileSize": 1024000,
    "UploadTime": "2026-06-08 10:30:00"
  }
}
```

### 13.2 文件下载

**请求参数**:
```
method=DownloadFile
&FileId={文件ID}
```

**响应**: 文件流

---

## 十五、设备管理API

### 14.1 设备管理

**接口路径**: `/AjaxRequest/Equipment/EquipManage.ashx`

#### 14.1.1 查询设备列表

**请求参数**:
```
method=GetEquipmentList
&page=1
&pageSize=20
&EquipmentName={设备名称}
&Status={状态}
&CalibrationDueDate={校准到期日期}
```

---

## 十六、人力资源API

### 15.1 员工管理

**接口路径**: `/AjaxRequest/HR/EmployeeManage.ashx`

#### 15.1.1 查询员工列表

**请求参数**:
```
method=GetEmployeeList
&page=1
&pageSize=20
&EmployeeName={员工姓名}
&Department={部门}
&Status={状态}
```

---

### 15.2 培训管理

**接口路径**: `/AjaxRequest/HR/TrainingManage.ashx`

#### 15.2.1 查询培训记录

**请求参数**:
```
method=GetTrainingList
&page=1
&pageSize=20
&EmployeeId={员工ID}
```

---

## 十七、API错误码说明

### 16.1 状态码

| state值 | 含义 | 说明 |
|---------|------|------|
| "1" | 成功 | 操作成功执行 |
| "0" | 失败 | 操作失败，查看msg字段获取原因 |
| "2" | 其他错误 | 系统异常或其他错误 |

### 16.2 常见错误信息

| 错误信息 | 原因 | 解决方案 |
|---------|------|---------|
| "用户名或密码错误" | 登录凭据不正确 | 检查用户名密码 |
| "会话已过期，请重新登录" | Cookie过期或无效 | 重新登录 |
| "无权限访问" | 用户角色无此功能权限 | 联系管理员分配权限 |
| "参数错误" | 请求参数不完整或格式错误 | 检查请求参数 |
| "系统错误" | 后端异常 | 联系技术支持 |
| "密码已过期，请修改" | 密码超过90天 | 修改密码 |

---

## 十八、前端核心函数清单

### 17.1 handle.js 核心函数

**位置**: `/Common/JS/handle.js` (967行)

#### AJAX封装函数

```javascript
// 通用AJAX请求
function ajaxRequest(url, data, successCallback, errorCallback) {
  $.ajax({
    url: url,
    type: 'POST',
    data: data,
    dataType: 'json',
    success: function(response) {
      if (response.state === '1') {
        successCallback(response);
      } else {
        layer.msg(response.msg);
        if (errorCallback) errorCallback(response);
      }
    },
    error: function(xhr) {
      layer.msg('网络错误');
    }
  });
}

// 获取Cookie
function getCookie(name) {
  var arr = document.cookie.match(new RegExp("(^| )" + name + "=([^;]*)(;|$)"));
  if (arr != null) {
    return unescape(arr[2]);
  }
  return null;
}

// 检查登录状态
function checkLogin() {
  var userId = getCookie('UserId');
  if (!userId) {
    layer.msg('请先登录');
    window.location.href = '/UI/Index/Login.html';
    return false;
  }
  return true;
}
```

#### 工具函数

```javascript
// 日期格式化
function formatDate(date, format) {
  // 实现...
}

// 金额格式化
function formatMoney(amount) {
  // 实现...
}

// 导出Excel
function exportExcel(url, params) {
  // 实现...
}

// 打印报告
function printReport(reportId) {
  // 实现...
}
```

---

## 十九、安全注意事项

### 18.1 🔴 高危安全问题

**1. 密码传输未加密**
- 仅Base64编码，可轻松解码
- HTTP明文传输，可被截获
- **建议**: 启用HTTPS + RSA加密

**2. 验证码可绕过**
- 纯前端验证，答案明文存储
- 后端未校验
- **建议**: 改为后端验证

**3. Cookie安全缺失**
- 无HttpOnly标志
- 无Secure标志
- 无SameSite标志
- **建议**: 添加安全标志

### 18.2 🟡 中危安全问题

**4. 缺少CSRF防护**
- 无Anti-Forgery Token
- **建议**: 实施CSRF Token机制

**5. 缺少安全HTTP头**
- 无X-Frame-Options
- 无Content-Security-Policy
- 无X-Content-Type-Options
- **建议**: 添加安全响应头

### 18.3 API调用最佳实践

```javascript
// ✅ 正确的API调用方式
function callApi(method, params, successCallback) {
  // 1. 检查登录状态
  if (!checkLogin()) return;
  
  // 2. 构建请求参数
  var data = { method: method };
  $.extend(data, params);
  
  // 3. 发起请求
  $.ajax({
    url: '/AjaxRequest/Index/HomeIndex.ashx',
    type: 'POST',
    data: data,
    dataType: 'json',
    success: function(response) {
      if (response.state === '1') {
        successCallback(response.data);
      } else if (response.state === '0') {
        layer.msg(response.msg);
      } else {
        layer.msg('系统错误');
        // 检查是否会话过期
        if (response.msg.indexOf('登录') > -1) {
          window.location.href = '/UI/Index/Login.html';
        }
      }
    },
    error: function() {
      layer.msg('网络错误');
    }
  });
}

// 使用示例
callApi('GetCustomerList', { page: 1, pageSize: 20 }, function(data) {
  renderCustomerTable(data);
});
```

---

## 二十、技术栈总结

### 19.1 后端技术
- **框架**: ASP.NET (.NET Framework 4.8)
- **处理器**: ASHX Generic Handlers
- **数据访问**: ADO.NET / Entity Framework (推测)
- **会话管理**: Session + Cookie

### 19.2 前端技术
- **核心库**: jQuery 1.7.1 / 3.7.0
- **UI框架**: Bootstrap 3.3.5
- **弹窗组件**: layer.js
- **表格组件**: Bootstrap Table
- **图标库**: Font Awesome 4.4.0

### 19.3 API架构特点
- **统一入口**: ASHX处理器
- **路由方式**: method参数
- **数据格式**: Form Data (请求) + JSON (响应)
- **认证方式**: Cookie (UserId)
- **权限控制**: 前端隐藏 + 后端验证

---

## 二十一、附录

### 20.1 完整的ASHX处理器清单

**发现时间**: 2026年6月8日  
**总数**: **51个ASHX处理器** (初始24个 + 深度挖掘27个)

#### 原有24个处理器

| 序号 | 模块 | 处理器路径 | 主要功能 |
|------|------|-----------|---------|
| 1 | 首页 | /AjaxRequest/Index/HomeIndex.ashx | 登录、用户信息、Dashboard |
| 2 | 市场-客户 | /AjaxRequest/Market/CustomerManage.ashx | 客户CRUD |
| 3 | 市场-合同 | /AjaxRequest/Market/ContractManage.ashx | 合同CRUD |
| 4 | 市场-结算 | /AjaxRequest/Market/SettlementManage.ashx | 结算管理 |
| 6 | 综合查询 | /AjaxRequest/IntegratedQueryManage/IntegratedQuery.ashx | 综合查询 |
| 7 | 业务-样品 | /AjaxRequest/Business/SampleManage.ashx | 样品管理 |
| 6 | 业务-任务 | /AjaxRequest/Business/TaskManage.ashx | 任务管理 |
| 8 | 业务-任务 | /AjaxRequest/Business/TaskManage.ashx | 任务管理 |
| 9 | 业务-委托 | /AjaxRequest/Business/EntrustManage.ashx | 委托管理 |
| 10 | 报告 | /AjaxRequest/Report/ReportManage.ashx | 报告管理 |
| 11 | 报告审核 | /AjaxRequest/Report/ReportAudit.ashx | 报告审核 |
| 12 | 质量-质控 | /AjaxRequest/Quality/QualityControl.ashx | 质量控制 |
| 13 | 质量-不符合 | /AjaxRequest/Quality/NonConform.ashx | 不符合工作 |
| 14 | 安全 | /AjaxRequest/Safety/SafetyManage.ashx | 安全管理 |
| 15 | 人事-员工 | /AjaxRequest/HR/EmployeeManage.ashx | 员工管理 |
| 16 | 人事-培训 | /AjaxRequest/HR/TrainingManage.ashx | 培训管理 |
| 17 | 办公 | /AjaxRequest/Office/OfficeManage.ashx | 日常办公 |
| 18 | 系统-用户 | /AjaxRequest/System/UserManage.ashx | 用户管理 |
| 19 | 系统-角色 | /AjaxRequest/System/RoleManage.ashx | 角色管理 |
| 20 | 系统-字典 | /AjaxRequest/System/DictManage.ashx | 字典管理 |
| 21 | 系统-日志 | /AjaxRequest/System/LogManage.ashx | 日志管理 |
| 22 | BI-报表 | /AjaxRequest/BI/ReportBI.ashx | BI报表 |
| 23 | BI-分析 | /AjaxRequest/BI/AnalysisBI.ashx | BI分析 |
| 24 | 设备 | /AjaxRequest/Equipment/EquipManage.ashx | 设备管理 |
| 25 | 消息 | /AjaxRequest/Message/MessageManage.ashx | 消息管理 |
| 26 | 文件 | /AjaxRequest/File/FileManage.ashx | 文件管理 |

#### 🆕 新增27个处理器（深度挖掘发现）

**Index模块扩展**:

| 27 | 首页扩展 | /AjaxRequest/Index/Main.ashx | 主页数据加载 |

**basicInfo基础信息模块**:

| 28 | 基础信息 | /AjaxRequest/basicInfo/Common.ashx | 基础信息通用 |
| 29 | 基础信息 | /AjaxRequest/basicInfo/TaskService.ashx | 任务服务 |
| 30 | 基础信息 | /AjaxRequest/basicInfo/TaskService_new.ashx | 任务服务新版 |

**TPcertificate第三方证书模块**:

| 31 | 第三方证书 | /AjaxRequest/TPcertificate/TPcertificate.ashx | 第三方证书管理 |

**UserManage用户资质扩展**:

| 32 | 用户资质 | /AjaxRequest/UserManage/SafetyQualifications.ashx | 安全资质管理 |
| 33 | 用户资质 | /AjaxRequest/UserManage/UserQualificationsashx.ashx | 用户资质管理 |
| 34 | 用户资质 | /AjaxRequest/UserManage/UserQualificationsApply.ashx | 用户资质申请 |
| 35 | 用户资质 | /AjaxRequest/UserManage/PersonnelCertified.ashx | 人员认证管理 |

**SecurityDevice安全设备模块**:

| 36 | 安全设备 | /AjaxRequest/SecurityDevice/SecurityDevice.ashx | 安全设备管理 |

**SignetManage印章管理模块**:

| 37 | 印章管理 | /AjaxRequest/SignetManage/SignetManage.ashx | 印章管理 |

**PlansManage计划管理模块**:

| 38 | 计划管理 | /AjaxRequest/PlansManage/TrainingPlan.ashx | 培训计划管理 |
| 39 | 计划管理 | /AjaxRequest/PlansManage/DeviceUsageAuth.ashx | 设备使用授权 |
| 40 | 计划管理 | /AjaxRequest/PlansManage/SafetyTrainingPlan.ashx | 安全培训计划 |

**EnvironmentFactor环境因素模块**:

| 41 | 环境因素 | /AjaxRequest/EnvironmentFactor/EnvironmentFactor.ashx | 环境因素管理 |

**Hazard危险源模块**:

| 42 | 危险源 | /AjaxRequest/Hazard/Hazard.ashx | 危险源管理 |

**OA办公扩展**:

| 43 | OA办公 | /AjaxRequest/OA/infoCommunication.ashx | 信息沟通管理 |

**reviewManagement评审管理模块**:

| 44 | 评审管理 | /AjaxRequest/reviewManagement/reviewCheck.ashx | 评审检查管理 |

**PersonnelAssessment人员考核模块**:

| 45 | 人员考核 | /AjaxRequest/PersonnelAssessment/Rating.ashx | 人员考核评级 |

**Document文档管理模块**:

| 46 | 文档管理 | /AjaxRequest/Document/FileControlled.ashx | 受控文件管理 |

**abilityProcess能力验证模块**:

| 47 | 能力验证 | /AjaxRequest/abilityProcess/abilityProcess.ashx | 能力验证流程 |

**badRecords不良记录模块**:

| 48 | 不良记录 | /AjaxRequest/badRecords/badRecords.ashx | 不良记录管理 |

**SupportCenter支持中心**:

| 49 | 支持中心 | /AjaxRequest/SupportCenter/systemSupport.ashx | 系统支持管理 |

**report报告扩展**:

| 50 | 报告扩展 | /AjaxRequest/report/reportBorrowing.ashx | 报告借阅管理 |

**safetyCheck安全检查模块**:

| 51 | 安全检查 | /AjaxRequest/safetyCheck/safetyCheck.ashx | 安全检查管理 |

---

## 二十二、新增处理器API推测（深度挖掘发现）

> **说明**: 以下API接口基于处理器名称和LIMS系统业务逻辑推测，具体参数需进一步抓包验证。

### 22.1 基础信息模块 (basicInfo)

#### 22.1.1 通用基础信息查询

**接口路径**: `/AjaxRequest/basicInfo/Common.ashx`

**推测方法**:
- `GetCommonData` - 获取通用基础数据
- `GetDictByType` - 按类型获取字典
- `GetBaseInfo` - 获取基础配置信息

**业务场景**: 提供系统通用的基础数据查询服务

---

#### 22.1.2 任务服务

**接口路径**: `/AjaxRequest/basicInfo/TaskService.ashx`

**推测方法**:
- `GetTaskList` - 获取任务列表
- `GetTaskDetail` - 获取任务详情
- `UpdateTaskStatus` - 更新任务状态

**业务场景**: 任务相关的通用服务接口

---

### 22.2 第三方证书管理 (TPcertificate)

#### 22.2.1 证书管理

**接口路径**: `/AjaxRequest/TPcertificate/TPcertificate.ashx`

**推测方法**:
- `GetCertificateList` - 查询证书列表
- `AddCertificate` - 新增证书
- `UpdateCertificate` - 更新证书
- `VerifyCertificate` - 验证证书有效性
- `ExportCertificate` - 导出证书

**请求参数推测**:
```
method=GetCertificateList
&page=1
&pageSize=20
&CertificateNo={证书编号}
&CompanyName={企业名称}
&ValidStatus={有效状态}
```

**业务场景**: 管理第三方检测证书、资质认证等

---

### 22.3 用户资质管理扩展 (UserManage)

#### 22.3.1 安全资质管理

**接口路径**: `/AjaxRequest/UserManage/SafetyQualifications.ashx`

**推测方法**:
- `GetSafetyQualList` - 查询安全资质列表
- `AddSafetyQual` - 新增安全资质
- `UpdateSafetyQual` - 更新安全资质
- `CheckExpiry` - 检查资质过期

**业务场景**: 管理员工的安全相关资质证书

---

#### 22.3.2 用户资质管理

**接口路径**: `/AjaxRequest/UserManage/UserQualificationsashx.ashx`

**推测方法**:
- `GetUserQualList` - 查询用户资质
- `AddUserQual` - 新增用户资质
- `AuditUserQual` - 审核用户资质

**业务场景**: 用户个人资质档案管理

---

#### 22.3.3 用户资质申请

**接口路径**: `/AjaxRequest/UserManage/UserQualificationsApply.ashx`

**推测方法**:
- `SubmitApply` - 提交资质申请
- `GetApplyList` - 查询申请列表
- `AuditApply` - 审核申请
- `CancelApply` - 取消申请

**业务场景**: 用户申请新资质或资质续期

---

#### 22.3.4 人员认证

**接口路径**: `/AjaxRequest/UserManage/PersonnelCertified.ashx`

**推测方法**:
- `GetCertifiedList` - 查询认证人员
- `CertifyPersonnel` - 认证人员
- `RevokeCertification` - 撤销认证

**业务场景**: 人员资质认证管理

---

### 22.4 安全设备管理 (SecurityDevice)

#### 22.4.1 安全设备管理

**接口路径**: `/AjaxRequest/SecurityDevice/SecurityDevice.ashx`

**推测方法**:
- `GetDeviceList` - 查询安全设备列表
- `AddDevice` - 新增设备
- `UpdateDevice` - 更新设备
- `CheckCalibration` - 检查校准状态

**业务场景**: 管理安全防护设备、监控设备等

---

### 22.5 印章管理 (SignetManage)

#### 22.5.1 印章管理

**接口路径**: `/AjaxRequest/SignetManage/SignetManage.ashx`

**推测方法**:
- `GetSignetList` - 查询印章列表
- `RegisterSignet` - 登记印章
- `BorrowSignet` - 借用印章
- `ReturnSignet` - 归还印章
- `CancelSignet` - 注销印章

**请求参数推测**:
```
method=GetSignetList
&page=1
&pageSize=20
&SignetName={印章名称}
&Status={状态: 在库/借出/注销}
```

**业务场景**: 公司公章、检测专用章等印章管理

---

### 22.6 计划管理 (PlansManage)

#### 22.6.1 培训计划管理

**接口路径**: `/AjaxRequest/PlansManage/TrainingPlan.ashx`

**推测方法**:
- `GetTrainingPlanList` - 查询培训计划
- `CreateTrainingPlan` - 创建培训计划
- `UpdateTrainingPlan` - 更新培训计划
- `ExecuteTraining` - 执行培训

**业务场景**: 年度培训计划、专项培训计划

---

#### 22.6.2 设备使用授权

**接口路径**: `/AjaxRequest/PlansManage/DeviceUsageAuth.ashx`

**推测方法**:
- `GetAuthList` - 查询授权列表
- `GrantAuth` - 授予设备使用权限
- `RevokeAuth` - 撤销授权
- `CheckAuth` - 检查授权状态

**业务场景**: 大型仪器设备使用授权管理

---

#### 22.6.3 安全培训计划

**接口路径**: `/AjaxRequest/PlansManage/SafetyTrainingPlan.ashx`

**推测方法**:
- `GetSafetyPlanList` - 查询安全培训计划
- `CreateSafetyPlan` - 创建安全培训计划
- `RecordTraining` - 记录培训

**业务场景**: 安全生产培训、应急演练计划

---

### 22.7 环境因素管理 (EnvironmentFactor)

#### 22.7.1 环境因素管理

**接口路径**: `/AjaxRequest/EnvironmentFactor/EnvironmentFactor.ashx`

**推测方法**:
- `GetEnvFactorList` - 查询环境因素
- `AddEnvFactor` - 新增环境因素
- `AssessEnvFactor` - 评估环境因素
- `ControlEnvFactor` - 控制措施

**业务场景**: 实验室环境因素识别和管理（温湿度、噪音等）

---

### 22.8 危险源管理 (Hazard)

#### 22.8.1 危险源管理

**接口路径**: `/AjaxRequest/Hazard/Hazard.ashx`

**推测方法**:
- `GetHazardList` - 查询危险源列表
- `IdentifyHazard` - 识别危险源
- `AssessRisk` - 风险评估
- `ControlMeasure` - 控制措施

**业务场景**: 实验室危险源识别、风险评估和控制

---

### 22.9 OA办公扩展

#### 22.9.1 信息沟通管理

**接口路径**: `/AjaxRequest/OA/infoCommunication.ashx`

**推测方法**:
- `GetCommList` - 查询沟通记录
- `SendComm` - 发送沟通信息
- `ReplyComm` - 回复沟通
- `ArchiveComm` - 归档

**业务场景**: 内部沟通记录、工作协调

---

### 22.10 评审管理 (reviewManagement)

#### 22.10.1 评审检查

**接口路径**: `/AjaxRequest/reviewManagement/reviewCheck.ashx`

**推测方法**:
- `GetReviewList` - 查询评审列表
- `CreateReview` - 创建评审
- `SubmitReview` - 提交评审
- `ApproveReview` - 批准评审

**业务场景**: 管理评审、内审外审等

---

### 22.11 人员考核 (PersonnelAssessment)

#### 22.11.1 人员考核评级

**接口路径**: `/AjaxRequest/PersonnelAssessment/Rating.ashx`

**推测方法**:
- `GetRatingList` - 查询考核列表
- `CreateRating` - 创建考核
- `SubmitRating` - 提交评分
- `CalculateResult` - 计算结果

**请求参数推测**:
```
method=GetRatingList
&EmployeeId={员工ID}
&Year={考核年度}
&Quarter={季度}
```

**业务场景**: 员工绩效考核、能力评价

---

### 22.12 文档管理 (Document)

#### 22.12.1 受控文件管理

**接口路径**: `/AjaxRequest/Document/FileControlled.ashx`

**推测方法**:
- `GetControlledFileList` - 查询受控文件
- `UploadControlledFile` - 上传受控文件
- `ApproveFile` - 审批文件
- `PublishFile` - 发布文件
- `WithdrawFile` - 撤回文件

**业务场景**: 体系文件、作业指导书等受控文档管理

---

### 22.13 能力验证 (abilityProcess)

#### 22.13.1 能力验证流程

**接口路径**: `/AjaxRequest/abilityProcess/abilityProcess.ashx`

**推测方法**:
- `GetAbilityProcessList` - 查询能力验证
- `CreateProcess` - 创建验证流程
- `ExecuteProcess` - 执行验证
- `EvaluateResult` - 评价结果

**业务场景**: 实验室间比对、能力验证计划

---

### 22.14 不良记录 (badRecords)

#### 22.14.1 不良记录管理

**接口路径**: `/AjaxRequest/badRecords/badRecords.ashx`

**推测方法**:
- `GetBadRecordList` - 查询不良记录
- `AddBadRecord` - 新增不良记录
- `HandleBadRecord` - 处理不良记录
- `AnalyzeBadRecord` - 分析统计

**业务场景**: 质量事故、投诉、不符合项等不良记录

---

### 22.15 支持中心 (SupportCenter)

#### 22.15.1 系统支持管理

**接口路径**: `/AjaxRequest/SupportCenter/systemSupport.ashx`

**推测方法**:
- `GetSupportList` - 查询支持请求
- `SubmitSupport` - 提交支持请求
- `HandleSupport` - 处理支持请求
- `CloseSupport` - 关闭支持请求

**业务场景**: IT支持、系统问题反馈

---

### 22.16 报告扩展

#### 22.16.1 报告借阅管理

**接口路径**: `/AjaxRequest/report/reportBorrowing.ashx`

**推测方法**:
- `GetBorrowList` - 查询借阅列表
- `BorrowReport` - 借阅报告
- `ReturnReport` - 归还报告
- `RenewBorrow` - 续借

**请求参数推测**:
```
method=GetBorrowList
&page=1
&pageSize=20
&ReportNo={报告编号}
&Borrower={借阅人}
&Status={状态: 借出/已归还/逾期}
```

**业务场景**: 纸质报告借阅管理

---

### 22.17 安全检查 (safetyCheck)

#### 22.17.1 安全检查管理

**接口路径**: `/AjaxRequest/safetyCheck/safetyCheck.ashx`

**推测方法**:
- `GetCheckList` - 查询安全检查记录
- `CreateCheck` - 创建安全检查
- `SubmitCheck` - 提交检查结果
- `RectifyIssue` - 整改问题

**业务场景**: 定期安全检查、专项安全检查

---

## 二十三、首页加载请求统计

**总请求数**: 49个XHR请求

**请求分布**:
- HomeIndex.ashx: 12个请求 (登录、用户信息、菜单、Dashboard)
- MessageManage.ashx: 3个请求 (消息列表、未读数)
- CustomerManage.ashx: 2个请求
- 其他处理器: 32个请求

### 20.3 API命名规范

**method参数命名**:
- 查询列表: `Get{Entity}List`
- 获取详情: `Get{Entity}Detail`
- 新增: `Add{Entity}`
- 更新: `Update{Entity}`
- 删除: `Delete{Entity}`
- 审核: `Audit{Entity}`
- 提交: `Submit{Entity}`
- 导出: `Export{Entity}`
- 统计: `Get{Entity}Statistics`

---

## 文档说明

1. 本文档基于前端代码分析和网络抓包整理
2. 部分API参数和响应格式为推测，实际以服务端为准
3. 建议在测试环境验证所有API接口
4. 文档中的安全建议仅供参考，实施前需充分评估
5. 本文档仅供内部使用，请妥善保管

---

*文档结束*

