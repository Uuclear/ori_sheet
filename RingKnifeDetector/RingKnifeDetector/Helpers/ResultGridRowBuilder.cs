using RingKnifeDetector.Models;

namespace RingKnifeDetector.Helpers
{
    public static class ResultGridRowBuilder
    {
        private const int Group3RingsPerBlock = 3;

        public static List<ResultGridRow> Build(IEnumerable<SamplePointResult> results, RecordParams parameters)
        {
            var rows = new List<ResultGridRow>();
            var isMultiRing = parameters.RecordTemplate == "group3";
            var isPercent = parameters.ResultType == "compaction_percent";

            foreach (var sample in results)
            {
                if (!isMultiRing)
                {
                    rows.Add(BuildSummaryRow(sample, isPercent));
                    continue;
                }

                for (var ringIndex = 0; ringIndex < Group3RingsPerBlock; ringIndex++)
                {
                    var ring = sample.Rings.ElementAtOrDefault(ringIndex);
                    var isFirst = ringIndex == 0;
                    rows.Add(new ResultGridRow
                    {
                        SampleNo = isFirst ? sample.SampleNo : string.Empty,
                        Elevation = isFirst ? sample.Elevation : string.Empty,
                        Thickness = isFirst ? sample.Thickness : string.Empty,
                        SamplingDateDisplay = isFirst ? sample.SamplingDateDisplay : string.Empty,
                        TestDateDisplay = isFirst ? sample.TestDateDisplay : string.Empty,
                        WetDensityDisplay = CompactionFormat.FormatDensity(ring?.WetDensity),
                        MoistureDisplay = CompactionFormat.FormatMoisture(ring?.AvgMoisture),
                        DryDensityDisplay = CompactionFormat.FormatDensity(ring?.DryDensity),
                        CompactionDisplay = isPercent
                            ? CompactionFormat.FormatPercent(ring?.CompactionPercent)
                            : CompactionFormat.FormatCoeff(ring?.CompactionCoeff),
                        Conclusion = ring?.Conclusion ?? string.Empty
                    });
                }
            }

            return rows;
        }

        private static ResultGridRow BuildSummaryRow(SamplePointResult sample, bool isPercent) =>
            new()
            {
                SampleNo = sample.SampleNo,
                Elevation = sample.Elevation,
                Thickness = sample.Thickness,
                SamplingDateDisplay = sample.SamplingDateDisplay,
                TestDateDisplay = sample.TestDateDisplay,
                WetDensityDisplay = CompactionFormat.FormatDensity(sample.AvgWetDensity ?? sample.WetDensity),
                MoistureDisplay = CompactionFormat.FormatMoisture(sample.AvgMoisture),
                DryDensityDisplay = CompactionFormat.FormatDensity(sample.AvgDryDensity ?? sample.DryDensity),
                CompactionDisplay = isPercent
                    ? CompactionFormat.FormatPercent(sample.CompactionPercent)
                    : CompactionFormat.FormatCoeff(sample.CompactionCoeff),
                Conclusion = sample.Conclusion
            };
    }
}
