namespace RingKnifeDetector.Helpers
{
    public static class CompactionFormat
    {
        public static decimal? RoundPercent(decimal? value) =>
            value.HasValue ? Math.Round(value.Value, 1, MidpointRounding.AwayFromZero) : null;

        public static decimal? RoundCoeff(decimal? value) =>
            value.HasValue ? Math.Round(value.Value, 2, MidpointRounding.AwayFromZero) : null;

        public static decimal? RoundMoisture(decimal? value) =>
            value.HasValue ? Math.Round(value.Value, 1, MidpointRounding.AwayFromZero) : null;

        public static decimal? RoundDensity(decimal? value) =>
            value.HasValue ? Math.Round(value.Value, 2, MidpointRounding.AwayFromZero) : null;

        public static string FormatPercent(decimal? value) =>
            value.HasValue ? value.Value.ToString("F1") : string.Empty;

        public static string FormatCoeff(decimal? value) =>
            value.HasValue ? value.Value.ToString("F2") : string.Empty;

        public static string FormatMoisture(decimal? value) =>
            value.HasValue ? value.Value.ToString("F1") : string.Empty;

        public static string FormatDensity(decimal? value) =>
            value.HasValue ? value.Value.ToString("F2") : string.Empty;
    }
}
