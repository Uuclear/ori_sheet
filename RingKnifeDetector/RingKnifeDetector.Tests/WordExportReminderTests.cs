using RingKnifeDetector.Models;
using RingKnifeDetector.Services;

namespace RingKnifeDetector.Tests;

public class WordExportReminderTests
{
    private static ProjectInfo CreateProject() => new()
    {
        EntrustNo = "TG11-001",
        ReportNo = "BG-001",
        EntrustUnit = "委托单位",
        Contact = "张三",
        SupervisionUnit = "监理",
        ConstructionUnit = "施工",
        ProjectName = "工程",
        UnitAddress = "地址",
        ProjectAddress = "工程地址",
        EntrustDate = "2026-01-01",
        ProjectSection = "路基",
        ReportDate = "2026-01-10",
        TestNature = "现场检测",
    };

    private static RecordParams CreateParams() => new()
    {
        SampleName = "回填土",
        MaterialType = "素土",
        RingSpec = "200cm³",
        CompactionMethod = "轻型",
        DesignRequirementText = "≥90%",
        DesignRequirement = 90,
        MaxDryDensity = 1.8m,
        TestLocation = "现场",
        OptimalMoisture = 12m,
        TestBasis = "JTG 3450-2019",
        JudgeBasis = "JTG 3450-2019",
        ResultType = "compaction_percent",
    };

    private static SamplePointResult CreateResult(
        string sampling = "2026-01-02",
        string test = "2026-01-03",
        decimal compactionPercent = 95m,
        decimal compactionCoeff = 0.95m,
        string conclusion = "符合设计要求") => new()
    {
        SampleNo = "YP-01",
        Elevation = "100",
        Thickness = "30",
        SamplingDate = sampling,
        TestDate = test,
        AvgWetDensity = 2.0m,
        AvgMoisture = 10m,
        AvgDryDensity = 1.8m,
        CompactionPercent = compactionPercent,
        CompactionCoeff = compactionCoeff,
        Conclusion = conclusion,
    };

    [Fact]
    public void CollectReminders_WithCompleteData_ReturnsEmpty()
    {
        var reminders = WordExportReminderService.CollectReminders(
            CreateProject(),
            CreateParams(),
            new[] { CreateResult() },
            "所检样品压实度符合设计要求。");

        Assert.Empty(reminders);
    }

    [Fact]
    public void CollectReminders_DetectsBlankFields()
    {
        var project = CreateProject();
        project.EntrustNo = string.Empty;

        var reminders = WordExportReminderService.CollectReminders(
            project,
            CreateParams(),
            new[] { CreateResult() },
            "所检样品压实度符合设计要求。");

        Assert.Contains(reminders, r => r.Contains("空白字段") && r.Contains("委托编号"));
    }

    [Fact]
    public void CollectReminders_DetectsDateOrderIssues()
    {
        var project = CreateProject();
        project.EntrustDate = "2026-01-05";

        var reminders = WordExportReminderService.CollectReminders(
            project,
            CreateParams(),
            new[] { CreateResult(sampling: "2026-01-02") },
            "所检样品压实度符合设计要求。");

        Assert.Contains(reminders, r => r.Contains("委托日期晚于取样日期"));
    }

    [Fact]
    public void CollectReminders_DetectsUnqualifiedConclusion()
    {
        var reminders = WordExportReminderService.CollectReminders(
            CreateProject(),
            CreateParams(),
            new[] { CreateResult(conclusion: "不符合设计要求") },
            "所检样品压实度不符合设计要求。");

        Assert.Contains(reminders, r => r.Contains("检测结论不合格"));
        Assert.Contains(reminders, r => r.Contains("单项结论不合格"));
    }

    [Fact]
    public void CollectReminders_DetectsHighCompactionValues()
    {
        var reminders = WordExportReminderService.CollectReminders(
            CreateProject(),
            CreateParams(),
            new[] { CreateResult(compactionPercent: 100m, compactionCoeff: 1.0m) },
            "所检样品压实度符合设计要求。");

        Assert.Contains(reminders, r => r.Contains("压实度") && r.Contains("100"));
        Assert.Contains(reminders, r => r.Contains("压实系数") && r.Contains("1"));
    }
}
