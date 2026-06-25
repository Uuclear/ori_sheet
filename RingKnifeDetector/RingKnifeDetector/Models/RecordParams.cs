using System.ComponentModel.DataAnnotations;

namespace RingKnifeDetector.Models
{
    /// <summary>
    /// 检测参数
    /// </summary>
    public class RecordParams
    {
        /// <summary>
        /// 土类型
        /// </summary>
        public string SoilType { get; set; } = string.Empty;

        /// <summary>
        /// 最大干密度
        /// </summary>
        public decimal? MaxDryDensity { get; set; }

        /// <summary>
        /// 压实方法
        /// </summary>
        public string CompactionMethod { get; set; } = string.Empty;

        /// <summary>
        /// 最优含水率
        /// </summary>
        public decimal? OptimalMoisture { get; set; }

        /// <summary>
        /// 检验标准
        /// </summary>
        public List<string> Standards { get; set; } = new();

        /// <summary>
        /// 环刀规格
        /// </summary>
        public string RingSpec { get; set; } = "200cm³";

        /// <summary>
        /// 设计要求
        /// </summary>
        public decimal? DesignRequirement { get; set; }

        /// <summary>
        /// 设计要求原文（导出 Word 时按此显示，如 ≥90%）
        /// </summary>
        public string DesignRequirementText { get; set; } = string.Empty;

        /// <summary>
        /// 样品名称
        /// </summary>
        public string SampleName { get; set; } = "回填土";

        /// <summary>
        /// 材料类型
        /// </summary>
        public string MaterialType { get; set; } = string.Empty;

        /// <summary>
        /// 检测依据
        /// </summary>
        public string TestBasis { get; set; } = "JTG 3450-2019";

        /// <summary>
        /// 判定依据
        /// </summary>
        public string JudgeBasis { get; set; } = ReportDefaults.MissingFieldPlaceholder;

        /// <summary>LIMIS 获取的检测标准全文（含《书名号》）。</summary>
        public string TestBasisFull { get; set; } = string.Empty;

        /// <summary>检测依据是否显示《书名号》段。</summary>
        public bool UseFullBasisName { get; set; }

        /// <summary>
        /// 结果类型：压实系数或压实度
        /// </summary>
        public string ResultType { get; set; } = "compaction_coeff";

        /// <summary>
        /// 记录模板类型：2个一组或3个一组
        /// </summary>
        public string RecordTemplate { get; set; } = "group2";

        /// <summary>
        /// 设备天平
        /// </summary>
        public List<string> EquipmentBalance { get; set; } = new();

        /// <summary>
        /// 设备烘箱
        /// </summary>
        public List<string> EquipmentOven { get; set; } = new();

        /// <summary>
        /// 检测地点
        /// </summary>
        public string TestLocation { get; set; } = string.Empty;

        /// <summary>
        /// LIMIS原始记录备注（与报告备注无关）
        /// </summary>
        public string LimisRemark { get; set; } = string.Empty;

        /// <summary>
        /// 兼容旧草稿字段
        /// </summary>
        [Obsolete("Use LimisRemark")]
        public string Remark
        {
            get => LimisRemark;
            set => LimisRemark = value;
        }

        /// <summary>
        /// 见证单位
        /// </summary>
        public string WitnessUnit { get; set; } = string.Empty;

        /// <summary>
        /// 见证人
        /// </summary>
        public string WitnessPerson { get; set; } = string.Empty;

        /// <summary>
        /// 取样单位
        /// </summary>
        public string SamplingUnit { get; set; } = string.Empty;

        /// <summary>
        /// 取样人
        /// </summary>
        public string SamplingPerson { get; set; } = string.Empty;
    }
}