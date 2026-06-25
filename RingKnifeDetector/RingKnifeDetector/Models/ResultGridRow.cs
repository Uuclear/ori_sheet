namespace RingKnifeDetector.Models
{
    /// <summary>
    /// 检测结果 DataGrid 行（group3 时每个环刀一行，左侧样品信息仅在首行显示）
    /// </summary>
    public class ResultGridRow
    {
        public string SampleNo { get; set; } = string.Empty;
        public string Elevation { get; set; } = string.Empty;
        public string Thickness { get; set; } = string.Empty;
        public string SamplingDateDisplay { get; set; } = string.Empty;
        public string TestDateDisplay { get; set; } = string.Empty;
        public string WetDensityDisplay { get; set; } = string.Empty;
        public string MoistureDisplay { get; set; } = string.Empty;
        public string DryDensityDisplay { get; set; } = string.Empty;
        public string CompactionDisplay { get; set; } = string.Empty;
        public string Conclusion { get; set; } = string.Empty;
    }
}
