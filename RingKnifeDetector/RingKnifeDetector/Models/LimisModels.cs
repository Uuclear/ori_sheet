namespace RingKnifeDetector.Models
{
    /// <summary>
    /// LIMIS登录请求
    /// </summary>
    public class LimisLoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    /// <summary>
    /// LIMIS登录响应
    /// </summary>
    public class LimisLoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? RealName { get; set; }
    }

    /// <summary>
    /// LIMIS委托查询响应
    /// </summary>
    public class LimisEntrustResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ProjectInfo? Project { get; set; }
        public string SampleNo { get; set; } = string.Empty;
        public string SampleName { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        /// <summary>规格型号（见证送样）</summary>
        public string TypeSpecification { get; set; } = string.Empty;
        /// <summary>检测依据/检测标准（可从 LIMIS 委托单 HTML 获取）</summary>
        public string TestBasis { get; set; } = string.Empty;
        public bool IsWitnessSampling { get; set; }
        /// <summary>样品名称是否来自委托单 HTML（见证送样）</summary>
        public bool SampleNameFromHtml { get; set; }
        /// <summary>检测依据是否来自委托单 HTML</summary>
        public bool TestBasisFromHtml { get; set; }
    }

    /// <summary>
    /// 应用程序设置
    /// </summary>
    public class AppSettings
    {
        public string BaseUrl { get; set; } = "http://10.1.228.22";
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string RealName { get; set; } = string.Empty;
        public string DefaultReportRemarks { get; set; } = ReportDefaults.DefaultReportRemarks;
        public int ReportRemarksTemplateVersion { get; set; }
        public string DefaultApprover { get; set; } = string.Empty;
        public string DefaultReviewer { get; set; } = string.Empty;
        public string DefaultJudgeBasis { get; set; } = ReportDefaults.DefaultJudgeBasis;
    }

    /// <summary>
    /// 应用程序设置响应
    /// </summary>
    public class AppSettingsResponse
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool PasswordSet { get; set; }
    }

    /// <summary>
    /// 应用程序设置保存请求
    /// </summary>
    public class AppSettingsSaveRequest
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// 任务项
    /// </summary>
    public class TaskItem
    {
        public string TaskId { get; set; } = string.Empty;
        public string TestingOrderId { get; set; } = string.Empty;
        public string TaskNo { get; set; } = string.Empty;
        public string TestingOrderNo { get; set; } = string.Empty;
        public string SampleName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string PrincipalPart { get; set; } = string.Empty;
        public string TestingType { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string Executor { get; set; } = string.Empty;
        public string TestItems { get; set; } = string.Empty;
        public int? RemainDays { get; set; }
        /// <summary>本地草稿主检人（无草稿为空）</summary>
        public string DraftInspector { get; set; } = string.Empty;
    }

    /// <summary>
    /// 任务列表请求
    /// </summary>
    public class TaskListRequest
    {
        public string EntrustNo { get; set; } = string.Empty;
        public string? BaseUrl { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    /// <summary>
    /// 任务列表响应
    /// </summary>
    public class TaskListResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<TaskItem> Tasks { get; set; } = new();
        public string QueryKeyword { get; set; } = string.Empty;
    }

    /// <summary>
    /// 计算请求
    /// </summary>
    public class CalcRequest
    {
        public RecordParams Params { get; set; } = new();
        public List<RingKnifeSample> Samples { get; set; } = new();
    }

    /// <summary>
    /// 计算响应
    /// </summary>
    public class CalcResponse
    {
        public List<SamplePointResult> Results { get; set; } = new();
        public string OverallConclusion { get; set; } = string.Empty;
    }

    /// <summary>
    /// 报告生成请求
    /// </summary>
    public class ReportRequest
    {
        public ProjectInfo Project { get; set; } = new();
        public RecordParams Params { get; set; } = new();
        public List<RingKnifeSample> Samples { get; set; } = new();
    }

    /// <summary>
    /// 草稿保存请求
    /// </summary>
    public class DraftSaveRequest
    {
        public ProjectInfo Project { get; set; } = new();
        public RecordParams Params { get; set; } = new();
        public List<RingKnifeSample> Samples { get; set; } = new();
        public List<SamplePointResult> CalcResults { get; set; } = new();
        public string OverallConclusion { get; set; } = string.Empty;
        public string SampleNoPrefix { get; set; } = string.Empty;
        /// <summary>报告页脚备注（可编辑，与LIMIS原始记录备注无关）</summary>
        public string ReportRemarks { get; set; } = string.Empty;
        /// <summary>保存草稿时的主检姓名</summary>
        public string SavedByInspector { get; set; } = string.Empty;
        public string SavedApprover { get; set; } = string.Empty;
        public string SavedReviewer { get; set; } = string.Empty;
    }

    /// <summary>
    /// 草稿保存响应
    /// </summary>
    public class DraftSaveResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }

    /// <summary>
    /// 草稿加载响应
    /// </summary>
    public class DraftLoadResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DraftSaveRequest? Draft { get; set; }
        public string UpdatedAt { get; set; } = string.Empty;
    }
}