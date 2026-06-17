namespace RingKnifeDetector.Models
{
    public enum FieldSource
    {
        None = 0,
        /// <summary>LIMIS 系统拉取</summary>
        System = 1,
        /// <summary>备注正则提取</summary>
        Remark = 2,
        /// <summary>用户手动修改</summary>
        Manual = 3
    }
}
