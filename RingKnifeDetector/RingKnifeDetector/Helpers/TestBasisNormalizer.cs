using System.Text.RegularExpressions;

namespace RingKnifeDetector.Helpers
{
    /// <summary>
    /// 将 LIMIS 检验依据原文规范为「标准号+书名号名称」，舍弃检测项目等后缀。
    /// </summary>
    public static class TestBasisNormalizer
    {
        public static string Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var text = raw.Trim();
            text = Regex.Replace(text, @"^[A-Z]{2}\d{2}-\d{6}-\d{2}\s*:\s*", string.Empty, RegexOptions.IgnoreCase);

            var bookEnd = text.IndexOf('》');
            if (bookEnd >= 0)
                return text[..(bookEnd + 1)].Trim();

            var paren = text.IndexOf('(');
            if (paren > 0)
                return text[..paren].Trim();

            var colon = text.IndexOf(':');
            if (colon > 0 && colon < 30)
                return text[..colon].Trim();

            return text.Trim();
        }

        private static readonly Regex BookTitlePart = new(@"《[^》]*》", RegexOptions.Compiled);

        /// <summary>仅保留标准号（正则去掉《…》书名号段）。</summary>
        public static string ExtractCodeOnly(string? full)
        {
            var text = Normalize(full);
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return BookTitlePart.Replace(text, string.Empty).Trim();
        }

        /// <summary>按是否显示书名号段切换检测依据展示文本。</summary>
        public static string ToDisplay(string? full, bool showBookTitle)
        {
            var text = Normalize(full);
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return showBookTitle ? text : ExtractCodeOnly(text);
        }
    }
}
