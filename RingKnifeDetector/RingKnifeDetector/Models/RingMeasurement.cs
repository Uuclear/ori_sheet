namespace RingKnifeDetector.Models
{
    /// <summary>
    /// 环刀测量数据
    /// </summary>
    public class RingMeasurement
    {
        /// <summary>
        /// 环刀加湿土质量
        /// </summary>
        public decimal? RingSampleMass { get; set; }

        /// <summary>
        /// 环刀质量
        /// </summary>
        public decimal? RingMass { get; set; }

        /// <summary>
        /// 环刀体积
        /// </summary>
        public decimal? RingVolume { get; set; }

        /// <summary>
        /// 铝盒列表
        /// </summary>
        public List<AluminumBox> Boxes { get; set; } = new() { new AluminumBox(), new AluminumBox() };
    }
}