using System.Globalization;
using System.Text.RegularExpressions;

namespace RingKnifeDetector.Helpers
{
    public static class DateHelper
    {
        public const string FormatPattern = "yyyy-MM-dd";
        private static readonly Regex ChineseDateRegex = new(
            @"^(\d{4})\s*年\s*(\d{1,2})\s*月\s*(\d{1,2})\s*日$",
            RegexOptions.Compiled);

        public static DateTime? TryParse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var trimmed = text.Trim();
            if (trimmed.Contains('~'))
                trimmed = trimmed.Split('~', StringSplitOptions.TrimEntries)[0];
            return TryParseSingle(trimmed);
        }

        private static DateTime? TryParseSingle(string text)
        {
            var chinese = ChineseDateRegex.Match(text);
            if (chinese.Success
                && int.TryParse(chinese.Groups[1].Value, out var year)
                && int.TryParse(chinese.Groups[2].Value, out var month)
                && int.TryParse(chinese.Groups[3].Value, out var day))
            {
                try
                {
                    return new DateTime(year, month, day);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return null;
                }
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt.Date;
            if (DateTime.TryParse(text, out dt))
                return dt.Date;
            return null;
        }

        public static (string? start, string? end) ParseRange(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return (null, null);
            var parts = text.Split('~', StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
                return (Normalize(parts[0]), Normalize(parts[1]));
            var single = Normalize(text);
            return (single, single);
        }

        public static string FormatRange(string? start, string? end)
        {
            var s = Normalize(start);
            var e = Normalize(end);
            if (string.IsNullOrEmpty(s) && string.IsNullOrEmpty(e)) return string.Empty;
            if (string.IsNullOrEmpty(s)) return e;
            if (string.IsNullOrEmpty(e)) return s;
            return $"{s}~{e}";
        }

        public static string FormatRange(DateTime? start, DateTime? end) =>
            FormatRange(Format(start), Format(end));

        public static string Format(DateTime? date) =>
            date.HasValue ? FormatDate(date.Value) : string.Empty;

        public static string FormatOrToday(DateTime? date) =>
            Format(date) == string.Empty ? FormatDate(DateTime.Today) : Format(date);

        public static bool IsValid(string? text) => TryParse(text).HasValue;

        public static string Normalize(string? text)
        {
            var parsed = TryParse(text);
            return parsed.HasValue ? FormatDate(parsed.Value) : string.Empty;
        }

        /// <summary>将单日文本补全为起止区间（用于检测日期展示/导出）。</summary>
        public static string EnsureRangeFormat(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var trimmed = text.Trim();
            if (trimmed.Contains('~')) return trimmed;

            var normalized = Normalize(trimmed);
            return string.IsNullOrEmpty(normalized)
                ? trimmed
                : FormatRange(normalized, normalized);
        }

        /// <summary>界面与 Word 报告统一日期格式：yyyy年MM月dd日</summary>
        public static string FormatWordDate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var trimmed = text.Trim();
            if (trimmed.Contains('~'))
            {
                var parts = trimmed.Split('~', StringSplitOptions.TrimEntries);
                if (parts.Length >= 2)
                {
                    var start = FormatWordDateSingle(parts[0]);
                    var end = FormatWordDateSingle(parts[1]);
                    if (!string.IsNullOrEmpty(start) && !string.IsNullOrEmpty(end))
                        return start == end ? start : $"{start}~{end}";
                    if (!string.IsNullOrEmpty(start)) return start;
                    if (!string.IsNullOrEmpty(end)) return end;
                }
            }

            return FormatWordDateSingle(trimmed);
        }

        private static string FormatWordDateSingle(string? text)
        {
            var parsed = TryParse(text);
            if (!parsed.HasValue) return text?.Trim() ?? string.Empty;
            return FormatDate(parsed.Value);
        }

        public static string FormatRangeMultiline(string? text)
        {
            var (start, end) = ParseRange(text);
            var startText = FormatWordDate(start);
            var endText = FormatWordDate(end);
            if (string.IsNullOrEmpty(startText) && string.IsNullOrEmpty(endText))
                return string.Empty;
            if (string.IsNullOrEmpty(endText) || startText == endText)
                return startText;
            return $"{startText}\n{endText}";
        }

        private static string FormatDate(DateTime date) =>
            $"{date.Year}年{date.Month:D2}月{date.Day:D2}日";
    }
}
