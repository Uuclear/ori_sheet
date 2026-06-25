using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RingKnifeDetector.Helpers;
using RingKnifeDetector.Models;

namespace RingKnifeDetector.Services
{
    /// <summary>
    /// LIMIS系统集成服务
    /// </summary>
    public class LimisService : IDisposable
    {
        private HttpClient? _httpClient;
        private CookieContainer _cookieContainer = new();
        private string? _baseUrl;
        private string? _username;
        private string? _password;
        private string? _userId;
        private string? _realName;
        private bool _taskSessionReady;

        public string? RealName => _realName;

        private const string TaskPagePath = "/UI/Task/TaskManagement.html";
        private const string TestingOrdersPath = "/AjaxRequest/TestingOrders/TestingOrders.ashx";
        private const string NonStandardPath = "/AjaxRequest/Task/NonStandard.ashx";
        private const string NonStandardPagePath = "/UI/Task/NonStandardReport.aspx";
        private const int MaxTaskResults = 500;

        /// <summary>
        /// 配置LIMIS连接
        /// </summary>
        public void Configure(string? baseUrl = null, string? username = null, string? password = null)
        {
            if (baseUrl != null)
                _baseUrl = baseUrl.TrimEnd('/');

            if (username != null)
                _username = username;

            if (password != null && !string.IsNullOrEmpty(password))
                _password = password;
        }

        /// <summary>
        /// 解析凭据
        /// </summary>
        private (string baseUrl, string username, string password) ResolveCredentials(
            string? baseUrl = null, string? username = null, string? password = null)
        {
            var url = (baseUrl ?? _baseUrl ?? "http://10.1.228.22").TrimEnd('/');
            var user = username ?? _username ?? string.Empty;
            var pwd = password ?? _password ?? string.Empty;
            return (url, user, pwd);
        }

        /// <summary>
        /// 获取或创建HttpClient
        /// </summary>
        private Task<HttpClient> GetClientAsync(string baseUrl)
        {
            if (_httpClient == null || _baseUrl != baseUrl)
            {
                _httpClient?.Dispose();
                _baseUrl = baseUrl;
                _cookieContainer = new CookieContainer();
                var handler = new HttpClientHandler
                {
                    CookieContainer = _cookieContainer,
                    UseCookies = true
                };
                _httpClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri(baseUrl),
                    Timeout = TimeSpan.FromSeconds(120)
                };

                _httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                _httpClient.DefaultRequestHeaders.Referrer = new Uri($"{baseUrl}{TaskPagePath}");
                _httpClient.DefaultRequestHeaders.Add("Origin", baseUrl);

                _taskSessionReady = false;

                if (!string.IsNullOrEmpty(_userId))
                    SetUserIdCookie(baseUrl, _userId);
            }

            return Task.FromResult(_httpClient);
        }

        private void SetUserIdCookie(string baseUrl, string userId)
        {
            var uri = new Uri(baseUrl);
            _cookieContainer.Add(uri, new Cookie("UserId", userId, "/", uri.Host));
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            ResetSession();
        }

        /// <summary>
        /// 登录LIMIS系统
        /// </summary>
        public async Task<LimisLoginResponse> LoginAsync(
            string? username = null, string? password = null, string? baseUrl = null)
        {
            var (url, user, pwd) = ResolveCredentials(baseUrl, username, password);

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pwd))
            {
                return new LimisLoginResponse
                {
                    Success = false,
                    Message = "未配置 LIMIS 用户名或密码，请在设置页填写"
                };
            }

            Configure(url, user, pwd);
            ResetSession();

            var client = await GetClientAsync(url);

            var encodedPwd = Convert.ToBase64String(Encoding.UTF8.GetBytes(pwd));
            var formData = new Dictionary<string, string>
            {
                {"method", "Login"},
                {"username", user},
                {"pwd", encodedPwd}
            };

            try
            {
                var content = new FormUrlEncodedContent(formData);
                var response = await client.PostAsync("/AjaxRequest/Index/HomeIndex.ashx", content);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (GetJsonState(root) != "1")
                {
                    return new LimisLoginResponse
                    {
                        Success = false,
                        Message = PickJsonString(root, "msg") ?? "登录失败"
                    };
                }

                _userId = PickJsonString(root, "UserId", "userId");
                if (!string.IsNullOrEmpty(_userId))
                    SetUserIdCookie(url, _userId);

                _realName = await ResolveRealNameAsync(client, root, user);
                _taskSessionReady = false;

                return new LimisLoginResponse
                {
                    Success = true,
                    Message = PickJsonString(root, "msg") ?? "登录成功",
                    UserId = _userId,
                    RealName = _realName
                };
            }
            catch (Exception ex)
            {
                return new LimisLoginResponse
                {
                    Success = false,
                    Message = $"登录请求失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 确保已登录
        /// </summary>
        private async Task<LimisLoginResponse> EnsureLoginAsync(
            string? baseUrl = null, string? username = null, string? password = null)
        {
            if (string.IsNullOrEmpty(_userId))
                return await LoginAsync(username, password, baseUrl);

            var (url, user, _) = ResolveCredentials(baseUrl, username, password);
            if (!IsAcceptableRealName(_realName, user))
            {
                var client = await GetClientAsync(url);
                if (!string.IsNullOrEmpty(_userId))
                    SetUserIdCookie(url, _userId);
                _realName = await FetchRealNameAsync(client);
            }

            return new LimisLoginResponse { Success = true, RealName = _realName, UserId = _userId };
        }

        private void ResetSession()
        {
            _httpClient?.Dispose();
            _httpClient = null;
            _userId = null;
            _realName = null;
            _taskSessionReady = false;
            _cookieContainer = new CookieContainer();
        }

        private async Task<string?> ResolveRealNameAsync(HttpClient client, JsonElement loginRoot, string loginUsername)
        {
            // 实测：登录响应不含 RealName；需调用 GetUserName（非文档中的 GetUserInfo）
            var fetched = await FetchRealNameAsync(client);
            if (IsAcceptableRealName(fetched, loginUsername))
                return fetched?.Trim();

            string? fromLogin = null;
            if (loginRoot.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                fromLogin = PickJsonString(data, "RealName", "realName", "real_name");
            if (string.IsNullOrWhiteSpace(fromLogin))
                fromLogin = PickJsonString(loginRoot, "RealName", "realName", "real_name");

            return IsAcceptableRealName(fromLogin, loginUsername) ? fromLogin?.Trim() : fetched?.Trim();
        }

        private static bool IsAcceptableRealName(string? name, string? loginUsername)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var trimmed = name.Trim();
            if (!string.IsNullOrWhiteSpace(loginUsername) &&
                string.Equals(trimmed, loginUsername.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;
            if (trimmed.Length >= 11 && trimmed.All(char.IsDigit))
                return false;
            return true;
        }

        private static string? GetJsonState(JsonElement root)
        {
            if (!root.TryGetProperty("state", out var state)) return null;
            return state.ValueKind switch
            {
                JsonValueKind.String => state.GetString(),
                JsonValueKind.Number => state.GetRawText(),
                _ => state.ToString()
            };
        }

        /// <summary>
        /// 获取当前登录用户真实姓名（HomeIndex.ashx method=GetUserName，姓名字段为 username）
        /// </summary>
        private async Task<string?> FetchRealNameAsync(HttpClient client)
        {
            try
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "method", "GetUserName" }
                });
                using var request = new HttpRequestMessage(HttpMethod.Post, "/AjaxRequest/Index/HomeIndex.ashx")
                {
                    Content = content
                };
                if (!string.IsNullOrEmpty(_baseUrl))
                    request.Headers.Referrer = new Uri($"{_baseUrl}/UI/Index/home.html");

                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json)) return null;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (GetJsonState(root) != "1") return null;

                return PickJsonString(root, "username", "RealName", "realName", "EmployeeName");
            }
            catch
            {
                return null;
            }
        }

        private static string? PickJsonString(JsonElement el, params string[] keys)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;
            foreach (var key in keys)
            {
                foreach (var prop in el.EnumerateObject())
                {
                    if (!string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase)) continue;
                    var text = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString()?.Trim()
                        : prop.Value.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            return null;
        }

        /// <summary>
        /// 预热任务会话
        /// </summary>
        private async Task WarmTaskSessionAsync(string baseUrl)
        {
            if (_taskSessionReady)
                return;

            var client = await GetClientAsync(baseUrl);
            await client.GetAsync(TaskPagePath);
            _taskSessionReady = true;
        }

        /// <summary>
        /// 预热详情会话
        /// </summary>
        private async Task WarmDetailSessionAsync(string baseUrl, string testingOrderId)
        {
            var client = await GetClientAsync(baseUrl);
            var detailUrl = $"/UI/Task/TaskDetailsEngineering.html?testingOrderId={testingOrderId}";
            client.DefaultRequestHeaders.Referrer = new Uri($"{baseUrl}{detailUrl}");
            await client.GetAsync(detailUrl);
        }

        /// <summary>
        /// 预热报告会话
        /// </summary>
        private async Task WarmReportSessionAsync(string baseUrl, string testingOrderId, string sampleId, string? taskId = null)
        {
            var client = await GetClientAsync(baseUrl);
            var page = $"{NonStandardPagePath}?testingOrderId={testingOrderId}&sampleId={sampleId}";
            if (!string.IsNullOrEmpty(taskId))
            {
                page += $"&taskId={taskId}";
            }

            client.DefaultRequestHeaders.Referrer = new Uri($"{baseUrl}{page}");
            await client.GetAsync(page);
        }

        /// <summary>
        /// 构建任务查询参数
        /// </summary>
        private Dictionary<string, string> BuildTaskQueryPayload(
            string testingOrderNo = "", string sampleNo = "", int pageLoad = 2)
        {
            return new Dictionary<string, string>
            {
                {"method", "GetTaskManagementList"},
                {"testingOrderNo", testingOrderNo},
                {"sampleNo", sampleNo},
                {"standardcode", ""},
                {"back", ""},
                {"principalPartName", ""},
                {"testingTypeCode", ""},
                {"setlementStatus", ""},
                {"setlementType", ""},
                {"taskExecutiveCode", ""},
                {"taskExecutor", ""},
                {"day_s", ""},
                {"day_e", ""},
                {"type", ""},
                {"taskStatusCode", ""},
                {"pageLoad", pageLoad.ToString()}
            };
        }

        /// <summary>
        /// 获取委托基本信息
        /// </summary>
        private async Task<Dictionary<string, object>?> FetchTestingOrderBaseAsync(
            string testingOrderId, string baseUrl)
        {
            await WarmDetailSessionAsync(baseUrl, testingOrderId);
            var client = await GetClientAsync(baseUrl);

            var formData = new Dictionary<string, string>
            {
                {"method", "GetTestingOrdersBaseType"},
                {"testingOrderId", testingOrderId}
            };

            var content = new FormUrlEncodedContent(formData);
            var response = await client.PostAsync(TestingOrdersPath, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"委托详情接口返回 {(int)response.StatusCode}: {ExtractErrorText(errorText)}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<object>(json);

            if (data is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() > 0)
                {
                    return element[0].Deserialize<Dictionary<string, object>>();
                }
                else if (element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty("rows", out var rows) && rows.GetArrayLength() > 0)
                        return rows[0].Deserialize<Dictionary<string, object>>();
                    if (element.TryGetProperty("data", out var dataProp) && dataProp.GetArrayLength() > 0)
                        return dataProp[0].Deserialize<Dictionary<string, object>>();
                }
            }

            return null;
        }

        /// <summary>
        /// 获取见证送样委托单 HTML（orderRow.standBy3）。
        /// </summary>
        private async Task<string?> FetchTestingOrderHtmlAsync(
            Dictionary<string, object> orderRow, string baseUrl)
        {
            if (!orderRow.TryGetValue("standBy3", out var pathObj))
                return null;

            var path = pathObj?.ToString()?.Trim();
            if (string.IsNullOrEmpty(path))
                return null;

            if (path.StartsWith("~/", StringComparison.Ordinal))
                path = path[1..];
            if (!path.StartsWith('/'))
                path = "/" + path;

            var client = await GetClientAsync(baseUrl);
            var response = await client.GetAsync(path);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// 格式化联系人信息
        /// </summary>
        private string FormatContact(object? person, object? phone)
        {
            var name = person?.ToString()?.Trim() ?? string.Empty;
            var tel = phone?.ToString()?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(tel))
                return $"{name} {tel}";

            return name ?? tel ?? string.Empty;
        }

        /// <summary>
        /// 选择单位地址
        /// </summary>
        private string PickUnitAddress(Dictionary<string, object> row)
        {
            foreach (var key in new[] { "clientAddress", "clientArea" })
            {
                if (row.TryGetValue(key, out var value))
                {
                    var val = value?.ToString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(val))
                        return val;
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// 选择任务行
        /// </summary>
        private Dictionary<string, object>? PickTaskRow(
            List<Dictionary<string, object>> tasks, string? taskId = null, string? taskNo = null)
        {
            if (!string.IsNullOrEmpty(taskId))
            {
                foreach (var row in tasks)
                {
                    if (row.TryGetValue("taskId", out var id) && id?.ToString() == taskId)
                        return row;
                }
            }

            if (!string.IsNullOrEmpty(taskNo))
            {
                var needle = taskNo.Trim();
                foreach (var row in tasks)
                {
                    if (row.TryGetValue("sampleNo", out var sampleNo) && sampleNo?.ToString() == needle)
                        return row;
                    if (row.TryGetValue("taskName", out var taskName) && taskName?.ToString() == needle)
                        return row;
                }
            }

            if (tasks.Count == 1)
                return tasks[0];

            return null;
        }

        /// <summary>
        /// 获取报告信息
        /// </summary>
        private async Task<Dictionary<string, object>?> FetchReportInfoAsync(
            string testingOrderId, string sampleId, string? taskId, string baseUrl)
        {
            await WarmReportSessionAsync(baseUrl, testingOrderId, sampleId, taskId);
            var client = await GetClientAsync(baseUrl);

            var payload = new Dictionary<string, string>
            {
                {"method", "GetReport"},
                {"testingOrderId", testingOrderId},
                {"sampleId", sampleId}
            };

            if (!string.IsNullOrEmpty(taskId))
            {
                payload["taskId"] = taskId;
            }

            var content = new FormUrlEncodedContent(payload);
            var response = await client.PostAsync(NonStandardPath, content);

            if (!response.IsSuccessStatusCode)
                return null;

            try
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<object>(json);

                if (data is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() > 0)
                    {
                        var row = element[0];
                        if (row.ValueKind == JsonValueKind.Object)
                            return row.Deserialize<Dictionary<string, object>>();
                    }
                    else if (element.ValueKind == JsonValueKind.Object)
                    {
                        if (element.TryGetProperty("rows", out var rows) && rows.GetArrayLength() > 0)
                        {
                            var row = rows[0];
                            if (row.ValueKind == JsonValueKind.Object)
                                return row.Deserialize<Dictionary<string, object>>();
                        }
                        if (element.TryGetProperty("data", out var dataProp) && dataProp.GetArrayLength() > 0)
                        {
                            var row = dataProp[0];
                            if (row.ValueKind == JsonValueKind.Object)
                                return row.Deserialize<Dictionary<string, object>>();
                        }
                    }
                }
            }
            catch
            {
                // 解析失败返回null
            }

            return null;
        }

        /// <summary>
        /// 从行数据中解析样品名称
        /// </summary>
        private string ResolveSampleNameFromRow(Dictionary<string, object> row)
        {
            var sampleNo = string.Empty;
            if (row.TryGetValue("sampleNo", out var sampleNoObj))
                sampleNo = sampleNoObj?.ToString()?.Trim() ?? string.Empty;

            foreach (var key in new[] { "sampleName", "SampleName", "sampleDesc", "productName", "specimenName", "manufacturer" })
            {
                if (row.TryGetValue(key, out var value))
                {
                    var val = value?.ToString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(val) && val != sampleNo)
                        return val;
                }
            }

            return string.Empty;
        }

        private static string PickRemark(Dictionary<string, object> row)
        {
            foreach (var key in new[] { "remark", "testingOrderRemark", "orderRemark", "memo", "note", "testingRemark", "sampleRemark", "bz", "remarks" })
            {
                if (row.TryGetValue(key, out var val))
                {
                    var text = val?.ToString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// 将行数据映射到ProjectInfo
        /// </summary>
        private ProjectInfo MapOrderRowToProject(
            Dictionary<string, object> row, string entrustNo = "", string reportNo = "")
        {
            var section = string.Empty;
            if (row.TryGetValue("projectSection", out var sectionObj))
            {
                section = sectionObj?.ToString()?.Trim() ?? string.Empty;
                if (section == "/")
                    section = string.Empty;
            }

            var entrustUnit = string.Empty;
            if (row.TryGetValue("testingOrderUnitName", out var unitObj))
                entrustUnit = unitObj?.ToString() ?? string.Empty;

            var contact = FormatContact(
                row.TryGetValue("clientPostNo", out var contactObj) ? contactObj : null,
                row.TryGetValue("clientTel", out var telObj) ? telObj : null);

            var supervisionUnit = string.Empty;
            if (row.TryGetValue("supervisorUnitName", out var supObj))
                supervisionUnit = supObj?.ToString() ?? string.Empty;
            else if (row.TryGetValue("supervisionUnitName", out var supObj2))
                supervisionUnit = supObj2?.ToString() ?? string.Empty;
            else if (row.TryGetValue("jlUnitName", out var supObj3))
                supervisionUnit = supObj3?.ToString() ?? string.Empty;

            var constructionUnit = string.Empty;
            if (row.TryGetValue("constructionUnitName", out var conObj))
                constructionUnit = conObj?.ToString() ?? string.Empty;
            else if (row.TryGetValue("buildUnitName", out var conObj2))
                constructionUnit = conObj2?.ToString() ?? string.Empty;
            else if (row.TryGetValue("sgUnitName", out var conObj3))
                constructionUnit = conObj3?.ToString() ?? string.Empty;

            var projectName = string.Empty;
            if (row.TryGetValue("projectName", out var projObj))
                projectName = projObj?.ToString() ?? string.Empty;

            var unitAddress = PickUnitAddress(row);

            var projectAddress = string.Empty;
            if (row.TryGetValue("projectAddress", out var addrObj))
                projectAddress = addrObj?.ToString() ?? string.Empty;

            var entrustDate = FormatDate(row.TryGetValue("testingOrderTime", out var dateObj) ? dateObj : null);

            var reportDate = FormatDate(row.TryGetValue("reportDate", out var reportDateObj) ? reportDateObj : null);

            var testNature = string.Empty;
            if (row.TryGetValue("testingTypeDesc", out var natureObj))
                testNature = NormalizeTestNature(natureObj?.ToString() ?? string.Empty);
            else if (row.TryGetValue("testingTypeName", out var natureObj2))
                testNature = NormalizeTestNature(natureObj2?.ToString() ?? string.Empty);

            var entrustNoValue = string.Empty;
            if (row.TryGetValue("testingOrderNo", out var entrustNoObj))
                entrustNoValue = entrustNoObj?.ToString() ?? string.Empty;

            var project = new ProjectInfo
            {
                EntrustNo = string.IsNullOrEmpty(entrustNoValue) ? entrustNo : entrustNoValue,
                ReportNo = reportNo,
                EntrustUnit = entrustUnit,
                Contact = contact,
                SupervisionUnit = supervisionUnit,
                ConstructionUnit = constructionUnit,
                ProjectName = projectName,
                UnitAddress = unitAddress,
                ProjectAddress = projectAddress,
                EntrustDate = entrustDate,
                ProjectSection = section,
                ReportDate = reportDate,
                TestNature = testNature
            };
            TextSanitizer.SanitizeProject(project);
            return project;
        }

        /// <summary>
        /// 解析测试订单ID
        /// </summary>
        private async Task<string?> ResolveTestingOrderIdAsync(
            string entrustNo, string? testingOrderId, string baseUrl)
        {
            if (!string.IsNullOrEmpty(testingOrderId))
                return testingOrderId;

            var rows = await FetchTaskListAsync(entrustNo, baseUrl);
            if (rows == null || rows.Count == 0)
                return null;

            foreach (var row in rows)
            {
                if (row.TryGetValue("testingOrderNo", out var noObj))
                {
                    var no = noObj?.ToString() ?? string.Empty;
                    if (no == entrustNo || no.Contains(entrustNo))
                    {
                        if (row.TryGetValue("testingOrderId", out var idObj))
                            return idObj?.ToString();
                    }
                }
            }

            if (rows[0].TryGetValue("testingOrderId", out var firstIdObj))
                return firstIdObj?.ToString();

            return null;
        }

        /// <summary>
        /// 获取委托信息
        /// </summary>
        public async Task<LimisEntrustResponse> GetEntrustByNoAsync(
            string entrustNo, string? testingOrderId = null, string? taskId = null,
            string? taskNo = null, string? sampleId = null,
            string? baseUrl = null, string? username = null, string? password = null)
        {
            var loginResult = await EnsureLoginAsync(baseUrl, username, password);
            if (!loginResult.Success)
            {
                return new LimisEntrustResponse
                {
                    Success = false,
                    Message = loginResult.Message,
                    Project = null,
                    SampleNo = string.Empty,
                    SampleName = string.Empty
                };
            }

            try
            {
                var (url, _, _) = ResolveCredentials(baseUrl, username, password);

                var orderId = await ResolveTestingOrderIdAsync(entrustNo, testingOrderId, url);
                if (string.IsNullOrEmpty(orderId))
                {
                    return new LimisEntrustResponse
                    {
                        Success = false,
                        Message = $"未找到委托编号: {entrustNo}",
                        Project = null,
                        SampleNo = string.Empty,
                        SampleName = string.Empty
                    };
                }

                var row = await FetchTestingOrderBaseAsync(orderId, url);
                if (row == null)
                {
                    return new LimisEntrustResponse
                    {
                        Success = false,
                        Message = $"未获取到委托详情: {entrustNo}",
                        Project = null,
                        SampleNo = string.Empty,
                        SampleName = string.Empty
                    };
                }

                var taskRows = await FetchTaskListAsync(entrustNo, url);
                var taskRow = PickTaskRow(taskRows ?? new List<Dictionary<string, object>>(), taskId, taskNo);

                var resolvedSampleId = sampleId;
                var resolvedTaskId = taskId;

                if (string.IsNullOrEmpty(resolvedSampleId) && taskRow != null)
                {
                    if (taskRow.TryGetValue("sampleId", out var sampleIdObj))
                        resolvedSampleId = sampleIdObj?.ToString();
                }

                if (string.IsNullOrEmpty(resolvedTaskId) && taskRow != null)
                {
                    if (taskRow.TryGetValue("taskId", out var taskIdObj))
                        resolvedTaskId = taskIdObj?.ToString();
                }

                var reportNo = string.Empty;
                var sampleNo = string.Empty;
                var sampleName = string.Empty;
                var reportDateOverride = string.Empty;
                Dictionary<string, object>? reportRow = null;

                if (!string.IsNullOrEmpty(resolvedSampleId))
                {
                    reportRow = await FetchReportInfoAsync(orderId, resolvedSampleId, resolvedTaskId, url);
                    if (reportRow != null)
                    {
                        if (reportRow.TryGetValue("testingReportCode", out var reportCodeObj))
                            reportNo = reportCodeObj?.ToString()?.Trim() ?? string.Empty;

                        if (reportRow.TryGetValue("sampleNo", out var reportSampleNoObj))
                            sampleNo = reportSampleNoObj?.ToString()?.Trim() ?? string.Empty;

                        if (reportRow.TryGetValue("reportDate", out var reportDateObj))
                            reportDateOverride = FormatDate(reportDateObj);
                    }
                }

                if (string.IsNullOrEmpty(sampleNo) && taskRow != null)
                {
                    if (taskRow.TryGetValue("sampleNo", out var taskSampleNoObj))
                        sampleNo = taskSampleNoObj?.ToString()?.Trim() ?? string.Empty;
                }

                if (taskRow != null)
                {
                    sampleName = ResolveSampleNameFromRow(taskRow);
                }

                var project = MapOrderRowToProject(row, entrustNo, reportNo);
                if (!string.IsNullOrEmpty(reportDateOverride))
                {
                    project.ReportDate = reportDateOverride;
                }

                var witnessFields = LimisWitnessMapper.Map(project, row, taskRow, reportRow);
                WitnessSamplingFields htmlFields = new();
                var orderHtml = await FetchTestingOrderHtmlAsync(row, url);
                if (!string.IsNullOrEmpty(orderHtml))
                    htmlFields = LimisOrderHtmlParser.Parse(orderHtml);

                if (TestNatureHelper.IsWitnessSampling(project.TestNature))
                    LimisWitnessMapper.MergeHtml(witnessFields, htmlFields);

                LimisWitnessMapper.ApplyToProject(project, witnessFields);
                TextSanitizer.SanitizeProject(project);

                var remark = PickRemark(row);
                if (string.IsNullOrEmpty(remark) && taskRow != null)
                    remark = PickRemark(taskRow);

                var isWitness = TestNatureHelper.IsWitnessSampling(project.TestNature);
                var testBasisFromHtml = !string.IsNullOrWhiteSpace(htmlFields.TestBasis);
                var sampleNameFromHtml = isWitness && !string.IsNullOrWhiteSpace(htmlFields.SampleName);
                var resolvedSampleName = sampleNameFromHtml
                    ? htmlFields.SampleName
                    : (!string.IsNullOrWhiteSpace(witnessFields.SampleName) ? witnessFields.SampleName : sampleName);

                return new LimisEntrustResponse
                {
                    Success = true,
                    Message = "查询成功",
                    Project = project,
                    SampleNo = sampleNo,
                    SampleName = resolvedSampleName,
                    Remark = remark,
                    TypeSpecification = witnessFields.TypeSpecification,
                    TestBasis = htmlFields.TestBasis,
                    IsWitnessSampling = isWitness,
                    SampleNameFromHtml = sampleNameFromHtml,
                    TestBasisFromHtml = testBasisFromHtml
                };
            }
            catch (Exception ex)
            {
                return new LimisEntrustResponse
                {
                    Success = false,
                    Message = $"查询请求失败: {ex.Message}",
                    Project = null,
                    SampleNo = string.Empty,
                    SampleName = string.Empty
                };
            }
        }

        /// <summary>
        /// 获取任务列表
        /// </summary>
        private async Task<List<Dictionary<string, object>>?> FetchTaskListAsync(
            string testingOrderNo = "", string baseUrl = "")
        {
            var (url, _, _) = ResolveCredentials(baseUrl, null, null);
            await WarmTaskSessionAsync(url);
            var client = await GetClientAsync(url);

            var payload = BuildTaskQueryPayload(
                testingOrderNo: testingOrderNo,
                pageLoad: string.IsNullOrEmpty(testingOrderNo) ? 1 : 2);

            var content = new FormUrlEncodedContent(payload);
            var response = await client.PostAsync("/AjaxRequest/Task/Task.ashx", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"任务接口返回 {(int)response.StatusCode}: {ExtractErrorText(errorText)}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<object>(json);

            if (data is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Array)
                {
                    return element.Deserialize<List<Dictionary<string, object>>>();
                }
                else if (element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty("msg", out var msg) && msg.ValueKind != JsonValueKind.Null)
                        return new List<Dictionary<string, object>>();

                    if (element.TryGetProperty("rows", out var rows))
                        return rows.Deserialize<List<Dictionary<string, object>>>();
                    if (element.TryGetProperty("data", out var dataProp))
                        return dataProp.Deserialize<List<Dictionary<string, object>>>();
                }
            }

            return new List<Dictionary<string, object>>();
        }

        /// <summary>
        /// 将行数据转换为TaskItem
        /// </summary>
        private TaskItem ToTaskItem(Dictionary<string, object> row)
        {
            return new TaskItem
            {
                TaskId = row.TryGetValue("taskId", out var taskIdObj) ? taskIdObj?.ToString() ?? string.Empty : string.Empty,
                TestingOrderId = row.TryGetValue("testingOrderId", out var testingOrderIdObj) ? testingOrderIdObj?.ToString() ?? string.Empty : string.Empty,
                TaskNo = row.TryGetValue("sampleNo", out var sampleNoObj) ? sampleNoObj?.ToString() ?? string.Empty :
                        row.TryGetValue("taskName", out var taskNameObj) ? taskNameObj?.ToString() ?? string.Empty : string.Empty,
                TestingOrderNo = row.TryGetValue("testingOrderNo", out var testingOrderNoObj) ? testingOrderNoObj?.ToString() ?? string.Empty : string.Empty,
                SampleName = ResolveSampleNameFromRow(row),
                ProjectName = row.TryGetValue("projectName", out var projectNameObj) ? projectNameObj?.ToString() ?? string.Empty : string.Empty,
                PrincipalPart = row.TryGetValue("deptName", out var deptNameObj) ? deptNameObj?.ToString() ?? string.Empty : string.Empty,
                TestingType = row.TryGetValue("testingTypeCode", out var testingTypeCodeObj) ? testingTypeCodeObj?.ToString() ?? string.Empty : string.Empty,
                StatusCode = row.TryGetValue("taskStatusCode", out var statusCodeObj) ? statusCodeObj?.ToString() ?? string.Empty : string.Empty,
                StatusName = row.TryGetValue("taskStatusName", out var statusNameObj) ? statusNameObj?.ToString() ?? string.Empty : string.Empty,
                Executor = row.TryGetValue("editor", out var editorObj) ? editorObj?.ToString() ?? string.Empty : string.Empty,
                TestItems = row.TryGetValue("taskName", out var testItemsObj) ? testItemsObj?.ToString() ?? string.Empty : string.Empty,
                RemainDays = row.TryGetValue("remainingDay", out var remainDaysObj) && remainDaysObj != null ?
                            int.TryParse(remainDaysObj?.ToString(), out var days) ? days : (int?)null : (int?)null
            };
        }

        /// <summary>
        /// 去重任务列表
        /// </summary>
        private List<TaskItem> DedupeTasks(List<TaskItem> tasks)
        {
            var seen = new HashSet<string>();
            var unique = new List<TaskItem>();

            foreach (var task in tasks)
            {
                var key = string.IsNullOrEmpty(task.TaskId) ?
                    $"{task.TestingOrderNo}|{task.TaskNo}|{task.SampleName}" : task.TaskId;

                if (seen.Add(key))
                {
                    unique.Add(task);
                }
            }

            return unique;
        }

        /// <summary>
        /// 探测用：导出委托原始字段（order / task / report）。
        /// </summary>
        public async Task<string> DumpRawEntrustJsonAsync(
            string entrustNo, string? baseUrl = null, string? username = null, string? password = null,
            string? testingOrderIdOverride = null)
        {
            var loginResult = await EnsureLoginAsync(baseUrl, username, password);
            if (!loginResult.Success)
                return JsonSerializer.Serialize(new { error = loginResult.Message });

            var (url, _, _) = ResolveCredentials(baseUrl, username, password);
            var orderId = testingOrderIdOverride
                ?? await ResolveTestingOrderIdAsync(entrustNo, null, url);
            if (string.IsNullOrEmpty(orderId))
                return JsonSerializer.Serialize(new { error = $"未找到委托: {entrustNo}" });

            var row = await FetchTestingOrderBaseAsync(orderId, url);
            var taskRows = await FetchTaskListAsync(entrustNo, url);
            var taskRow = PickTaskRow(taskRows ?? new List<Dictionary<string, object>>(), null, null);

            Dictionary<string, object>? reportRow = null;
            if (taskRow != null
                && taskRow.TryGetValue("sampleId", out var sampleIdObj)
                && taskRow.TryGetValue("taskId", out var taskIdObj))
            {
                reportRow = await FetchReportInfoAsync(orderId, sampleIdObj?.ToString() ?? "", taskIdObj?.ToString(), url);
            }

            var project = row != null ? MapOrderRowToProject(row, entrustNo, "") : null;
            var witness = project != null ? LimisWitnessMapper.Map(project, row, taskRow, reportRow) : null;
            var jsonOnlyWitness = witness == null ? null : new WitnessSamplingFields
            {
                SupervisionWitness = witness.SupervisionWitness,
                SampleSampling = witness.SampleSampling,
                Contact = witness.Contact,
                SampleName = witness.SampleName,
                TypeSpecification = witness.TypeSpecification,
                TestBasis = witness.TestBasis
            };
            WitnessSamplingFields? htmlWitness = null;
            if (project != null && row != null && TestNatureHelper.IsWitnessSampling(project.TestNature))
            {
                var orderHtml = await FetchTestingOrderHtmlAsync(row, url);
                if (!string.IsNullOrEmpty(orderHtml))
                {
                    htmlWitness = LimisOrderHtmlParser.Parse(orderHtml);
                    if (witness != null)
                        LimisWitnessMapper.MergeHtml(witness, htmlWitness);
                }
            }

            return JsonSerializer.Serialize(new
            {
                entrustNo,
                orderId,
                testNature = project?.TestNature,
                orderRow = row,
                orderRowKeys = row?.Keys.OrderBy(k => k).ToList(),
                witnessRelatedKeys = row?.Keys
                    .Where(k => ContainsWitnessHint(k))
                    .ToDictionary(k => k, k => row[k]),
                taskRow,
                taskRowKeys = taskRow?.Keys.OrderBy(k => k).ToList(),
                reportRow,
                reportRowKeys = reportRow?.Keys.OrderBy(k => k).ToList(),
                standBy3 = row?.GetValueOrDefault("standBy3")?.ToString(),
                jsonOnlyWitness,
                htmlWitness,
                mappedWitness = witness
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        private static bool ContainsWitnessHint(string key)
        {
            var k = key.ToLowerInvariant();
            return k.Contains("witness")
                || k.Contains("sampling")
                || k.Contains("sampler")
                || k.Contains("sample")
                || k.Contains("spec")
                || k.Contains("basis")
                || k.Contains("standard")
                || k.Contains("standby")
                || k.Contains("supervisor")
                || k.Contains("construction")
                || k.Contains("jl")
                || k.Contains("sg")
                || k.Contains("qy");
        }

        /// <summary>探测其他可能返回见证/样品字段的 API（内网调试用）。</summary>
        public async Task<string> ProbeAlternativeApisAsync(
            string entrustNo,
            string? testingOrderIdOverride,
            string? baseUrl = null,
            string? username = null,
            string? password = null)
        {
            var loginResult = await EnsureLoginAsync(baseUrl, username, password);
            if (!loginResult.Success)
                return JsonSerializer.Serialize(new { error = loginResult.Message });

            var (url, _, _) = ResolveCredentials(baseUrl, username, password);
            var orderId = testingOrderIdOverride
                ?? await ResolveTestingOrderIdAsync(entrustNo, null, url);
            if (string.IsNullOrEmpty(orderId))
                return JsonSerializer.Serialize(new { error = $"未找到委托: {entrustNo}" });

            var taskRows = await FetchTaskListAsync(entrustNo, url);
            var taskRow = PickTaskRow(taskRows ?? new List<Dictionary<string, object>>(), null, null);
            var sampleId = taskRow?.GetValueOrDefault("sampleId")?.ToString() ?? "";
            var taskId = taskRow?.GetValueOrDefault("taskId")?.ToString() ?? "";

            var methods = new (string path, Dictionary<string, string> payload)[]
            {
                (TestingOrdersPath, new() { ["method"] = "GetTestingOrderSampleList", ["testingOrderId"] = orderId }),
                (TestingOrdersPath, new() { ["method"] = "GetTestingOrderSample", ["testingOrderId"] = orderId }),
                (TestingOrdersPath, new() { ["method"] = "GetSampleById", ["testingOrderId"] = orderId, ["sampleId"] = sampleId }),
                (TestingOrdersPath, new() { ["method"] = "GetTestingOrdersDetail", ["testingOrderId"] = orderId }),
                (TestingOrdersPath, new() { ["method"] = "GetTestingOrderDetail", ["testingOrderId"] = orderId }),
                (TestingOrdersPath, new() { ["method"] = "GetTestingOrdersById", ["testingOrderId"] = orderId }),
                ("/AjaxRequest/Business/SampleManage.ashx", new() { ["method"] = "GetSampleList", ["page"] = "1", ["pageSize"] = "20", ["EntrustId"] = orderId }),
                ("/AjaxRequest/Business/EntrustManage.ashx", new() { ["method"] = "GetEntrustList", ["page"] = "1", ["pageSize"] = "20", ["EntrustNo"] = entrustNo }),
                ("/AjaxRequest/IntegratedQueryManage/IntegratedQuery.ashx", new()
                {
                    ["method"] = "GetIntegratedQueryInfo",
                    ["type"] = "4",
                    ["size"] = "10",
                    ["page"] = "1",
                    ["testingOrderNo"] = entrustNo,
                    ["authType"] = "1",
                    ["cha"] = "1"
                }),
            };

            var results = new List<object>();
            var client = await GetClientAsync(url);
            foreach (var (path, payload) in methods)
            {
                if (payload.ContainsKey("sampleId") && string.IsNullOrEmpty(payload["sampleId"]))
                {
                    results.Add(new { path, method = payload["method"], skipped = "no sampleId" });
                    continue;
                }

                try
                {
                    var response = await client.PostAsync(path, new FormUrlEncodedContent(payload));
                    var text = await response.Content.ReadAsStringAsync();
                    var preview = text.Length > 2000 ? text[..2000] + "…" : text;
                    object? parsed = null;
                    try { parsed = JsonSerializer.Deserialize<object>(text); } catch { /* raw text */ }

                    results.Add(new
                    {
                        path,
                        method = payload["method"],
                        status = (int)response.StatusCode,
                        length = text.Length,
                        preview,
                        parsed
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new { path, method = payload["method"], error = ex.Message });
                }
            }

            return JsonSerializer.Serialize(new
            {
                entrustNo,
                orderId,
                sampleId,
                taskId,
                apiProbes = results
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// 搜索任务
        /// </summary>
        public async Task<TaskListResponse> SearchTasksByEntrustAsync(
            string entrustKeyword, string? baseUrl = null, string? username = null, string? password = null)
        {
            var keyword = entrustKeyword.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                return new TaskListResponse
                {
                    Success = false,
                    Message = "请输入委托单编号关键词",
                    Tasks = new List<TaskItem>(),
                    QueryKeyword = string.Empty
                };
            }

            var loginResult = await EnsureLoginAsync(baseUrl, username, password);
            if (!loginResult.Success)
            {
                return new TaskListResponse
                {
                    Success = false,
                    Message = loginResult.Message,
                    Tasks = new List<TaskItem>(),
                    QueryKeyword = keyword
                };
            }

            try
            {
                var (url, _, _) = ResolveCredentials(baseUrl, username, password);
                var taskRows = await FetchTaskListAsync(keyword, url);
                var tasks = DedupeTasks(taskRows?.Select(ToTaskItem).ToList() ?? new List<TaskItem>());
                tasks = tasks.OrderBy(t => t.TestingOrderNo).ThenBy(t => t.TaskNo).ToList();

                if (tasks.Count == 0)
                {
                    return new TaskListResponse
                    {
                        Success = true,
                        Message = $"未找到委托编号包含「{keyword}」的任务",
                        Tasks = new List<TaskItem>(),
                        QueryKeyword = keyword
                    };
                }

                var total = tasks.Count;
                var truncated = total > MaxTaskResults;
                if (truncated)
                {
                    tasks = tasks.Take(MaxTaskResults).ToList();
                }

                var message = $"共找到 {total} 条任务";
                if (truncated)
                {
                    message += $"，显示前 {MaxTaskResults} 条，请缩小关键词";
                }

                return new TaskListResponse
                {
                    Success = true,
                    Message = message,
                    Tasks = tasks,
                    QueryKeyword = keyword
                };
            }
            catch (Exception ex)
            {
                return new TaskListResponse
                {
                    Success = false,
                    Message = $"任务查询失败: {ex.Message}",
                    Tasks = new List<TaskItem>(),
                    QueryKeyword = keyword
                };
            }
        }

        /// <summary>
        /// 规范化检测性质
        /// </summary>
        private string NormalizeTestNature(string value)
        {
            var text = value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // 移除开头的数字前缀
            var dashIndex = text.IndexOf('-');
            if (dashIndex > 0)
            {
                var prefix = text.Substring(0, dashIndex);
                if (int.TryParse(prefix, out _))
                {
                    text = text.Substring(dashIndex + 1).Trim();
                }
            }

            return string.IsNullOrEmpty(text) ? value?.Trim() ?? string.Empty : text;
        }

        /// <summary>
        /// 格式化日期
        /// </summary>
        private string FormatDate(object? value)
        {
            if (value == null)
                return string.Empty;

            var text = value.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var datePart = text.Split('T')[0].Split(' ')[0];

            var formats = new[] { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd" };
            foreach (var fmt in formats)
            {
                if (DateTime.TryParseExact(datePart, fmt, null, System.Globalization.DateTimeStyles.None, out var date))
                {
                    return date.ToString("yyyy-MM-dd");
                }
            }

            var normalized = datePart.Replace("/", "-").Replace(".", "-");
            var parts = normalized.Split('-');
            if (parts.Length >= 3)
            {
                if (int.TryParse(parts[0], out var y) &&
                    int.TryParse(parts[1], out var m) &&
                    int.TryParse(parts[2], out var d))
                {
                    try
                    {
                        return new DateTime(y, m, d).ToString("yyyy-MM-dd");
                    }
                    catch
                    {
                        // 无效日期
                    }
                }
            }

            return datePart;
        }

        /// <summary>
        /// 提取错误文本
        /// </summary>
        private string ExtractErrorText(string text)
        {
            if (text.Contains("Object reference not set"))
                return "服务端参数不完整（NullReferenceException）";

            if (text.Contains("<title>"))
            {
                var start = text.IndexOf("<title>") + 7;
                var end = text.IndexOf("</title>", start);
                if (end > start)
                {
                    return text.Substring(start, end - start).Trim();
                }
            }

            return text.Length > 200 ? text.Substring(0, 200) : text;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }
    }
}