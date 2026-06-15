using System.ComponentModel.DataAnnotations;

namespace RingKnifeDetector.Models
{
    /// <summary>
    /// 工程信息
    /// </summary>
    public class ProjectInfo
    {
        /// <summary>
        /// 委托编号
        /// </summary>
        public string EntrustNo { get; set; } = string.Empty;

        /// <summary>
        /// 报告编号
        /// </summary>
        public string ReportNo { get; set; } = string.Empty;

        /// <summary>
        /// 委托单位
        /// </summary>
        public string EntrustUnit { get; set; } = string.Empty;

        /// <summary>
        /// 联系人
        /// </summary>
        public string Contact { get; set; } = string.Empty;

        /// <summary>
        /// 监理单位
        /// </summary>
        public string SupervisionUnit { get; set; } = string.Empty;

        /// <summary>
        /// 施工单位
        /// </summary>
        public string ConstructionUnit { get; set; } = string.Empty;

        /// <summary>
        /// 工程名称
        /// </summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// 单位地址
        /// </summary>
        public string UnitAddress { get; set; } = string.Empty;

        /// <summary>
        /// 工程地址
        /// </summary>
        public string ProjectAddress { get; set; } = string.Empty;

        /// <summary>
        /// 委托日期
        /// </summary>
        public string EntrustDate { get; set; } = string.Empty;

        /// <summary>
        /// 工程部位
        /// </summary>
        public string ProjectSection { get; set; } = string.Empty;

        /// <summary>
        /// 报告日期
        /// </summary>
        public string ReportDate { get; set; } = string.Empty;

        /// <summary>
        /// 检测性质
        /// </summary>
        public string TestNature { get; set; } = string.Empty;
    }
}