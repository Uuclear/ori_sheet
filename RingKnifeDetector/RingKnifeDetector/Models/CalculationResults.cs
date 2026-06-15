namespace RingKnifeDetector.Models
{
    /// <summary>
    /// 单个环刀计算结果
    /// </summary>
    public class RingPointResult
    {
        /// <summary>
        /// 湿土质量
        /// </summary>
        public decimal? WetMass { get; set; }

        /// <summary>
        /// 湿密度
        /// </summary>
        public decimal? WetDensity { get; set; }

        /// <summary>
        /// 含水率列表
        /// </summary>
        public List<decimal?> MoistureRates { get; set; } = new();

        /// <summary>
        /// 平均含水率
        /// </summary>
        public decimal? AvgMoisture { get; set; }

        /// <summary>
        /// 干密度
        /// </summary>
        public decimal? DryDensity { get; set; }
    }

    /// <summary>
    /// 单个测点计算结果
    /// </summary>
    public class SamplePointResult
    {
        /// <summary>
        /// 样品编号
        /// </summary>
        public string SampleNo { get; set; } = string.Empty;

        /// <summary>
        /// 高程
        /// </summary>
        public string Elevation { get; set; } = string.Empty;

        /// <summary>
        /// 厚度
        /// </summary>
        public string Thickness { get; set; } = string.Empty;

        /// <summary>
        /// 取样日期
        /// </summary>
        public string SamplingDate { get; set; } = string.Empty;

        /// <summary>
        /// 检测日期
        /// </summary>
        public string TestDate { get; set; } = string.Empty;

        /// <summary>
        /// 湿土质量
        /// </summary>
        public decimal? WetMass { get; set; }

        /// <summary>
        /// 湿密度
        /// </summary>
        public decimal? WetDensity { get; set; }

        /// <summary>
        /// 平均湿密度
        /// </summary>
        public decimal? AvgWetDensity { get; set; }

        /// <summary>
        /// 含水率列表
        /// </summary>
        public List<decimal?> MoistureRates { get; set; } = new();

        /// <summary>
        /// 平均含水率
        /// </summary>
        public decimal? AvgMoisture { get; set; }

        /// <summary>
        /// 干密度
        /// </summary>
        public decimal? DryDensity { get; set; }

        /// <summary>
        /// 平均干密度
        /// </summary>
        public decimal? AvgDryDensity { get; set; }

        /// <summary>
        /// 压实系数
        /// </summary>
        public decimal? CompactionCoeff { get; set; }

        /// <summary>
        /// 压实度
        /// </summary>
        public decimal? CompactionPercent { get; set; }

        /// <summary>
        /// 结论
        /// </summary>
        public string Conclusion { get; set; } = string.Empty;

        /// <summary>
        /// 环刀结果列表
        /// </summary>
        public List<RingPointResult> Rings { get; set; } = new();
    }
}