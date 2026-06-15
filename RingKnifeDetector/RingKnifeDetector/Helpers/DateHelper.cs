namespace RingKnifeDetector.Helpers
{
    public static class DateHelper
    {
        public const string FormatPattern = "yyyy-MM-dd";

        public static DateTime? TryParse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (DateTime.TryParse(text.Trim(), out var dt))
                return dt.Date;
            return null;
        }

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
    }
}
