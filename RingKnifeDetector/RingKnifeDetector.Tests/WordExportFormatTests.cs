using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RingKnifeDetector.Models;
using RingKnifeDetector.Services;
using Xunit;

namespace RingKnifeDetector.Tests;

public class WordExportFormatTests
{
    [Fact]
    public void ExportToWord_UsesChineseDatesAndFullDesignRequirement()
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Resources", "report_template.docx");
        if (!File.Exists(templatePath))
            return;

        var project = new ProjectInfo
        {
            EntrustNo = "TG11-260327",
            ReportNo = "TG118-260608",
            EntrustUnit = "测试单位",
            TestNature = "见证送样",
            EntrustDate = "2026-06-08",
            ReportDate = "2026-06-10",
            ProjectName = "测试工程",
        };

        var parameters = new RecordParams
        {
            ResultType = "compaction_percent",
            RecordTemplate = "group3",
            DesignRequirementText = "≥94%",
            DesignRequirement = 94,
            MaxDryDensity = 1.71m,
            SampleName = "回填土（环刀）",
            MaterialType = "素土",
            TestLocation = "土基层",
        };

        var results = new List<SamplePointResult>
        {
            new()
            {
                SampleNo = "TG11-260327-01",
                Elevation = "250",
                Thickness = "300",
                SamplingDate = "2026-06-08",
                TestDate = "2026-06-09~2026-06-10",
                AvgWetDensity = 1.913m,
                AvgMoisture = 17.73m,
                AvgDryDensity = 1.623m,
                CompactionPercent = 95.3m,
                Rings =
                {
                    new RingPointResult
                    {
                        WetDensity = 1.92m,
                        AvgMoisture = 17.6m,
                        DryDensity = 1.63m,
                        CompactionPercent = 95.3m,
                    },
                    new RingPointResult
                    {
                        WetDensity = 1.92m,
                        AvgMoisture = 17.8m,
                        DryDensity = 1.63m,
                        CompactionPercent = 95.3m,
                    },
                    new RingPointResult
                    {
                        WetDensity = 1.90m,
                        AvgMoisture = 17.8m,
                        DryDensity = 1.61m,
                        CompactionPercent = 94.2m,
                    },
                }
            }
        };

        var output = Path.Combine(Path.GetTempPath(), $"ring_export_test_{Guid.NewGuid():N}.docx");
        try
        {
            var service = new WordExportService();
            service.ExportToWord(project, parameters, results, "符合设计要求", "备注：1.测试",
                "姜华", "陈胜华", "秦臻", output);

            using var doc = WordprocessingDocument.Open(output, false);
            var text = doc.MainDocumentPart!.Document!.Body!.InnerText;

            Assert.Contains("2026年06月08日", text);
            Assert.Contains("2026年06月09日~2026年06月10日", text);
            Assert.Contains("压实度≥94%", text);
            Assert.Contains("压实度/%", text);
            Assert.Contains("95.3", text);
            Assert.Contains("94.2", text);
        }
        finally
        {
            if (File.Exists(output))
                File.Delete(output);
        }
    }
}
