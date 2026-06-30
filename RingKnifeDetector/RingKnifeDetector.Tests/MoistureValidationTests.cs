using RingKnifeDetector.Helpers;
using RingKnifeDetector.Models;
using RingKnifeDetector.Services;

namespace RingKnifeDetector.Tests;

public class MoistureValidationTests
{
    [Fact]
    public void HasExcessiveBoxMoistureSpread_DetectsDifferenceAboveOnePercent()
    {
        var ring = new RingPointResult
        {
            MoistureRates = { 14.0m, 15.5m },
            AvgMoisture = 14.8m
        };

        Assert.True(MoistureValidation.HasExcessiveBoxMoistureSpread(ring));
    }

    [Fact]
    public void HasExcessiveBoxMoistureSpread_IgnoresRingToRingDifference()
    {
        var result = new SamplePointResult
        {
            Rings =
            {
                new RingPointResult { MoistureRates = { 14.0m, 14.2m }, AvgMoisture = 14.1m },
                new RingPointResult { MoistureRates = { 16.0m, 16.1m }, AvgMoisture = 16.05m },
            }
        };

        Assert.False(MoistureValidation.HasExcessiveBoxMoistureSpread(result));
    }

    [Fact]
    public void CollectBoxMoistureWarnings_IncludesRingLabel()
    {
        var warnings = MoistureValidation.CollectBoxMoistureWarnings(new[]
        {
            new SamplePointResult
            {
                SampleNo = "YP-01",
                Rings =
                {
                    new RingPointResult { MoistureRates = { 14.0m, 15.5m }, AvgMoisture = 14.8m },
                }
            }
        }).ToList();

        Assert.Single(warnings);
        Assert.Contains("YP-01", warnings[0]);
        Assert.Contains("环刀1", warnings[0]);
        Assert.Contains("盒号", warnings[0]);
    }

    [Fact]
    public void CollectReminders_IncludesBoxMoistureWarning()
    {
        var reminders = WordExportReminderService.CollectReminders(
            new ProjectInfo { EntrustNo = "TG11-001", ReportNo = "BG-001", EntrustUnit = "A", Contact = "B",
                SupervisionUnit = "C", ConstructionUnit = "D", ProjectName = "E", UnitAddress = "F",
                ProjectAddress = "G", EntrustDate = "2026-01-01", ProjectSection = "H", ReportDate = "2026-01-10",
                TestNature = "现场检测" },
            new RecordParams
            {
                SampleName = "回填土", MaterialType = "素土", RingSpec = "200cm³", CompactionMethod = "轻型",
                DesignRequirementText = "≥90%", DesignRequirement = 90, MaxDryDensity = 1.8m, TestLocation = "现场",
                OptimalMoisture = 12m, TestBasis = "JTG 3450-2019", JudgeBasis = "设计要求",
                ResultType = "compaction_percent"
            },
            new[]
            {
                new SamplePointResult
                {
                    SampleNo = "YP-01", Elevation = "100", Thickness = "30", SamplingDate = "2026-01-02",
                    TestDate = "2026-01-03", AvgWetDensity = 2.0m, AvgMoisture = 14.7m, AvgDryDensity = 1.8m,
                    CompactionPercent = 95m, Conclusion = "符合设计要求",
                    Rings =
                    {
                        new RingPointResult { MoistureRates = { 14.0m, 15.5m }, AvgMoisture = 14.8m },
                    }
                }
            },
            "所检样品压实度符合设计要求。");

        Assert.Contains(reminders, r => r.Contains("盒号含水率相差"));
    }
}
