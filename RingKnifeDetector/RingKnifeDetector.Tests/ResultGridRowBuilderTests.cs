using RingKnifeDetector.Helpers;
using RingKnifeDetector.Models;
using Xunit;

namespace RingKnifeDetector.Tests;

public class ResultGridRowBuilderTests
{
    [Fact]
    public void Build_Group3_ExpandsToThreeRowsPerSample()
    {
        var results = new List<SamplePointResult>
        {
            new()
            {
                SampleNo = "TG11-01",
                Elevation = "250",
                CompactionPercent = 95.3m,
                Rings =
                {
                    new RingPointResult { WetDensity = 1.91m, AvgMoisture = 17.2m, DryDensity = 1.63m, CompactionPercent = 95.3m },
                    new RingPointResult { WetDensity = 1.89m, AvgMoisture = 16.9m, DryDensity = 1.62m, CompactionPercent = 94.7m },
                    new RingPointResult { WetDensity = 1.90m, AvgMoisture = 17.2m, DryDensity = 1.62m, CompactionPercent = 94.7m },
                }
            }
        };

        var rows = ResultGridRowBuilder.Build(results, new RecordParams
        {
            RecordTemplate = "group3",
            ResultType = "compaction_percent"
        });

        Assert.Equal(3, rows.Count);
        Assert.Equal("TG11-01", rows[0].SampleNo);
        Assert.Equal(string.Empty, rows[1].SampleNo);
        Assert.Equal("1.91", rows[0].WetDensityDisplay);
        Assert.Equal("1.89", rows[1].WetDensityDisplay);
        Assert.Equal("94.7", rows[2].CompactionDisplay);
    }

    [Fact]
    public void Build_Group2_KeepsOneSummaryRowPerSample()
    {
        var results = new List<SamplePointResult>
        {
            new()
            {
                SampleNo = "TG11-01",
                AvgWetDensity = 1.90m,
                AvgMoisture = 17.1m,
                AvgDryDensity = 1.62m,
                CompactionCoeff = 0.95m,
            }
        };

        var rows = ResultGridRowBuilder.Build(results, new RecordParams
        {
            RecordTemplate = "group2",
            ResultType = "compaction_coeff"
        });

        Assert.Single(rows);
        Assert.Equal("1.90", rows[0].WetDensityDisplay);
        Assert.Equal("0.95", rows[0].CompactionDisplay);
    }
}
