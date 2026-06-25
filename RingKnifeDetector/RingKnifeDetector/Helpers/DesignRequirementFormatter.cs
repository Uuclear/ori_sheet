using RingKnifeDetector.Models;

namespace RingKnifeDetector.Helpers
{
    internal static class DesignRequirementFormatter
    {
        public static string FormatForWord(RecordParams parameters)
        {
            var text = parameters.DesignRequirementText?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                if (!parameters.DesignRequirement.HasValue)
                    return string.Empty;

                var value = TrimDecimal(parameters.DesignRequirement.Value);
                text = parameters.ResultType == "compaction_percent"
                    ? $"≥{value}%"
                    : $"≥{value}";
            }

            if (text.Contains("压实度", StringComparison.Ordinal)
                || text.Contains("压实系数", StringComparison.Ordinal)
                || text.Contains("固体体积率", StringComparison.Ordinal))
                return text;

            var prefix = parameters.ResultType == "compaction_percent" ? "压实度" : "压实系数";
            return $"{prefix}{text}";
        }

        private static string TrimDecimal(decimal value) =>
            value.ToString(value % 1 == 0 ? "0" : "0.####################");
    }
}
