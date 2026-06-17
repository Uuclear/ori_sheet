namespace RingKnifeDetector.Helpers
{
    public static class DateHelper
    {
        public const string FormatPattern = "yyyy-MM-dd";

        public static DateTime? TryParse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var trimmed = text.Trim();
            if (trimmed.Contains('~'))
                trimmed = trimmed.Split('~', StringSplitOptions.TrimEntries)[0];
            if (DateTime.TryParse(trimmed, out var dt))
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
            date.HasValue ? date.Value.ToString(FormatPattern) : string.Empty;

        public static string FormatOrToday(DateTime? date) =>
            Format(date) == string.Empty ? DateTime.Today.ToString(FormatPattern) : Format(date);

        public static bool IsValid(string? text) => TryParse(text).HasValue;

        public static string Normalize(string? text)
        {
            var parsed = TryParse(text);
            return parsed.HasValue ? Format(parsed) : string.Empty;
        }

        /// <summary>
        /// Word 报告日期格式：YYYY/M/D（月、日不补零）
        /// </summary>
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
                        return $"{start}~{end}";
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
            var date = parsed.Value;
            return $"{date.Year}/{date.Month}/{date.Day}";
        }
    }
}
