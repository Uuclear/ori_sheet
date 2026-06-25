using System.Net;
using System.Text.RegularExpressions;
using RingKnifeDetector.Helpers;

namespace RingKnifeDetector.Services
{
    /// <summary>
    /// 从 LIMIS 委托单 HTML（standBy3）解析见证送样专用字段。
    /// </summary>
    internal static class LimisOrderHtmlParser
    {
        public static WitnessSamplingFields Parse(string html)
        {
            var fields = new WitnessSamplingFields();
            if (string.IsNullOrWhiteSpace(html))
                return fields;

            html = WebUtility.HtmlDecode(html);

            fields.Contact = CleanCellHtml(ExtractCellAfterHeader(html, "联系方式"));
            fields.SupervisionWitness = FormatWitnessParty(ExtractCellAfterHeader(html, "工程见证"));
            fields.SampleSampling = FormatWitnessParty(ExtractCellAfterHeader(html, "样品取样"));

            var (sampleName, typeSpec) = ExtractFirstSampleRow(html);
            fields.SampleName = sampleName;
            fields.TypeSpecification = typeSpec;
            fields.TestBasis = ExtractTestBasis(html);

            return fields;
        }

        private static string ExtractCellAfterHeader(string html, string headerText)
        {
            var escaped = Regex.Escape(headerText);
            var patterns = new[]
            {
                $@"<th[^>]*>\s*{escaped}\s*</th>\s*<td[^>]*>(.*?)</td>",
                $@"<th[^>]*>\s*{escaped}[\s\S]*?</th>\s*<td[^>]*>(.*?)</td>",
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (match.Success)
                    return match.Groups[1].Value;
            }

            return string.Empty;
        }

        private static (string SampleName, string TypeSpecification) ExtractFirstSampleRow(string html)
        {
            var match = Regex.Match(
                html,
                @"<tr[^>]*class=""[^""]*tbSamples[^""]*""[^>]*>\s*<td[^>]*>\s*1\s*</td>\s*<td[^>]*>(.*?)</td>\s*<td[^>]*>(.*?)</td>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!match.Success)
                return (string.Empty, string.Empty);

            return (CleanCellHtml(match.Groups[1].Value), CleanCellHtml(match.Groups[2].Value));
        }

        private static string ExtractTestBasis(string html)
        {
            var raw = CleanCellHtml(ExtractCellAfterHeader(html, "检验依据"));
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            raw = Regex.Replace(raw, @"^[A-Z]{2}\d{2}-\d{6}-\d{2}\s*:\s*", string.Empty, RegexOptions.IgnoreCase);
            return TestBasisNormalizer.Normalize(raw);
        }

        internal static string CleanCellHtml(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            raw = Regex.Replace(raw, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
            raw = Regex.Replace(raw, @"<sup[^>]*>\s*3\s*</sup>", "³", RegexOptions.IgnoreCase);
            raw = Regex.Replace(raw, @"<sup[^>]*>(.*?)</sup>", "$1", RegexOptions.IgnoreCase);
            raw = Regex.Replace(raw, "<[^>]+>", string.Empty);
            raw = WebUtility.HtmlDecode(raw);
            raw = raw.Replace('\t', ' ');
            raw = Regex.Replace(raw, @"\s+", " ").Trim();
            return TextSanitizer.RemoveChinesePeriods(raw);
        }

        internal static string FormatWitnessParty(string raw)
        {
            raw = CleanCellHtml(raw);
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            // 保留 LIMIS 委托单原始逗号分隔格式（单位,人,工号,电话）
            var parts = raw.Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p));
            return string.Join(",", parts);
        }
    }
}
