using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RingKnifeDetector.Models;
using RingKnifeDetector.Services;

namespace RingKnifeDetector.Tests;

public class RecordWordExportTests
{
    [Fact]
    public void ExportRecord_Group3_PadsEmptySlotOnPage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"record_pad_{Guid.NewGuid():N}.docx");
        try
        {
            var service = new RecordWordExportService();
            service.ExportRecord(
                new List<RingKnifeSample>
                {
                    new() { SampleNo = "TG11-01", Rings = { new(), new(), new() } }
                },
                new RecordParams { RecordTemplate = "group3", ResultType = "compaction_coeff", SoilType = "土" },
                new List<SamplePointResult> { new() },
                "", "",
                path);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ExportRecord_Group3_GeneratesDocx()
    {
        var samples = new List<RingKnifeSample>
        {
            new()
            {
                SampleNo = "TG11-260311-01",
                Elevation = "250",
                SamplingDate = "2026-03-11",
                TestDate = "2026年03月11日~2026年03月11日",
                Rings =
                {
                    new RingMeasurement
                    {
                        RingSampleMass = 537m, RingMass = 177m, RingVolume = 200m,
                        Boxes = { new AluminumBox { BoxNo = "1", BoxMass = 15m, WetSampleMass = 30m, DrySampleMass = 28m },
                                  new AluminumBox { BoxNo = "1", BoxMass = 15m, WetSampleMass = 30m, DrySampleMass = 28m } }
                    },
                    new RingMeasurement
                    {
                        RingSampleMass = 537m, RingMass = 177m, RingVolume = 200m,
                        Boxes = { new AluminumBox { BoxNo = "1", BoxMass = 15m, WetSampleMass = 30m, DrySampleMass = 28m },
                                  new AluminumBox { BoxNo = "1", BoxMass = 15m, WetSampleMass = 30m, DrySampleMass = 28m } }
                    },
                    new RingMeasurement
                    {
                        RingSampleMass = 537m, RingMass = 177m, RingVolume = 200m,
                        Boxes = { new AluminumBox { BoxNo = "1", BoxMass = 15m, WetSampleMass = 30m, DrySampleMass = 28m },
                                  new AluminumBox { BoxNo = "1", BoxMass = 15m, WetSampleMass = 30m, DrySampleMass = 28m } }
                    }
                }
            }
        };

        var results = new List<SamplePointResult>
        {
            new()
            {
                Rings =
                {
                    new RingPointResult { WetMass = 360m, WetDensity = 1.8m, AvgMoisture = 15.4m, DryDensity = 1.56m, CompactionCoeff = 0.99m,
                        MoistureRates = { 15.4m, 15.4m } },
                    new RingPointResult { WetMass = 360m, WetDensity = 1.8m, AvgMoisture = 15.4m, DryDensity = 1.56m, CompactionCoeff = 0.99m,
                        MoistureRates = { 15.4m, 15.4m } },
                    new RingPointResult { WetMass = 360m, WetDensity = 1.8m, AvgMoisture = 15.4m, DryDensity = 1.56m, CompactionCoeff = 0.99m,
                        MoistureRates = { 15.4m, 15.4m } },
                }
            }
        };

        var path = Path.Combine(Path.GetTempPath(), $"record_export_test_{Guid.NewGuid():N}.docx");
        try
        {
            var service = new RecordWordExportService();
            var saved = service.ExportRecord(
                samples,
                new RecordParams
                {
                    RecordTemplate = "group3",
                    ResultType = "compaction_coeff",
                    SoilType = "土",
                    MaxDryDensity = 1.72m,
                    OptimalMoisture = 15.4m,
                    TestLocation = "土基层",
                    TestBasis = "GB/T 50123-2019"
                },
                results,
                "检测员",
                "复核员",
                path);

            Assert.True(File.Exists(saved));
            Assert.True(new FileInfo(saved).Length > 1000);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void ExportRecord_Group2_MatchesTemplateTableShape()
    {
        var path = Path.Combine(Path.GetTempPath(), $"record_shape_{Guid.NewGuid():N}.docx");
        try
        {
            var service = new RecordWordExportService();
            service.ExportRecord(
                new List<RingKnifeSample>
                {
                    new() { SampleNo = "A", Rings = { new(), new() } },
                    new() { SampleNo = "B", Rings = { new(), new() } },
                },
                new RecordParams { RecordTemplate = "group2", SoilType = "土" },
                new List<SamplePointResult> { new(), new() },
                "", "",
                path);

            using var doc = WordprocessingDocument.Open(path, false);
            var table = doc.MainDocumentPart!.Document!.Body!.Elements<Table>().First();
            var rows = table.Elements<TableRow>().ToList();
            var cols = table.Elements<TableGrid>().First().Elements<GridColumn>().ToList();

            Assert.Equal(19, rows.Count);
            Assert.Equal(21, cols.Count);

            int Span(TableRow row) => row.Elements<TableCell>()
                .Sum(c => c.TableCellProperties?.GridSpan?.Val?.Value ?? 1);

            Assert.Equal(21, Span(rows[0]));
            Assert.Equal(21, Span(rows[17]));
            Assert.Equal(21, Span(rows[18]));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ExportRecord_UsesIncrementedNameWhenTargetLocked()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"record_dup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, "原始记录.docx");
        var lockStream = new FileStream(target, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        try
        {
            var service = new RecordWordExportService();
            var saved = service.ExportRecord(
                new List<RingKnifeSample> { new() { SampleNo = "S1", Rings = { new(), new() } } },
                new RecordParams { RecordTemplate = "group2", SoilType = "土" },
                new List<SamplePointResult> { new() },
                "", "",
                target);

            Assert.Equal(Path.Combine(dir, "原始记录(2).docx"), saved);
            Assert.True(File.Exists(saved));
        }
        finally
        {
            lockStream.Dispose();
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }
}
