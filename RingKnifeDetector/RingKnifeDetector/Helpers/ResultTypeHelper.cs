using RingKnifeDetector.Models;

namespace RingKnifeDetector.Helpers
{
    internal static class ResultTypeHelper
    {
        public static void SyncFromDesignText(RecordParams parameters)
        {
            var text = parameters.DesignRequirementText ?? string.Empty;
            if (text.Contains("压实度", StringComparison.Ordinal))
                parameters.ResultType = "compaction_percent";
            else if (text.Contains("压实系数", StringComparison.Ordinal)
                     || text.Contains("固体体积率", StringComparison.Ordinal))
                parameters.ResultType = "compaction_coeff";
        }
    }
}
