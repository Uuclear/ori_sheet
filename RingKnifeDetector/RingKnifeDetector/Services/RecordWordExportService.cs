using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RingKnifeDetector.Helpers;
using RingKnifeDetector.Models;

namespace RingKnifeDetector.Services
{
    public class RecordWordExportService
    {
        private const string CnFont = "仿宋_GB2312";
        private const string EnFont = "Times New Roman";
        private const string BodyFontSize = "18"; // 9pt
        private const string TitleFontSize = "30"; // 15pt
        private const string Placeholder = "---";
        private const uint MinRowHeightTwips = 340; // 约 0.6cm
        private const int Group3SlotsPerPage = 2;
        private const int Group2SlotsPerPage = 3;

        private static readonly string[] StandardOptions =
        {
            "GB/T 50123-2019", "JTG 3450-2019", "JTG 3430-2020"
        };

        private static readonly string[] DefaultBalances = { "2161730", "2161731" };
        private static readonly string[] DefaultOvens = { "8161209", "8161905" };

        // 官方模版 21 列（group2 末两列与 group3 略有差异）
        private static readonly int[] ColWidthsGroup2 =
        {
            733, 705, 795, 885, 810, 735, 669, 651, 600, 804, 578, 658, 489, 336, 810, 756, 724, 725, 570, 753, 754
        };

        private static readonly int[] ColWidthsGroup3 =
        {
            733, 705, 795, 885, 810, 735, 669, 651, 600, 804, 578, 658, 489, 336, 810, 756, 724, 725, 570, 705, 802
        };

        public string ExportRecord(
            List<RingKnifeSample> samples,
            RecordParams parameters,
            List<SamplePointResult> results,
            string inspectorName,
            string reviewerName,
            string filePath)
        {
            ResultTypeHelper.SyncFromDesignText(parameters);
            var isGroup3 = parameters.RecordTemplate == "group3";
            var slotsPerPage = isGroup3 ? Group3SlotsPerPage : Group2SlotsPerPage;
            var formId = isGroup3 ? "JC/JL 38-422-2025" : "JC/JL 38-421-2025";
            var colWidths = isGroup3 ? ColWidthsGroup3 : ColWidthsGroup2;

            var slots = BuildSlots(samples, results, slotsPerPage);
            var totalPages = Math.Max(1, (slots.Count + slotsPerPage - 1) / slotsPerPage);

            var workPath = Path.Combine(Path.GetTempPath(), $"ring_record_{Guid.NewGuid():N}.docx");
            try
            {
                using (var doc = WordprocessingDocument.Create(workPath, WordprocessingDocumentType.Document))
                {
                    var mainPart = doc.AddMainDocumentPart();
                    mainPart.Document = new Document(new Body());
                    var body = mainPart.Document.Body!;

                    for (var page = 0; page < totalPages; page++)
                    {
                        if (page > 0)
                            body.AppendChild(CreatePageBreakParagraph());

                        body.AppendChild(CreateTitleParagraph("路基路面压实度检测原始记录（环刀法）"));
                        body.AppendChild(CreateEmptyParagraph());
                        body.AppendChild(CreatePageTable(
                            slots, page, slotsPerPage, parameters, isGroup3, colWidths));
                        body.AppendChild(CreateSignatureTable(inspectorName, reviewerName));

                        var headerId = AddHeaderPart(mainPart, page + 1, totalPages);
                        var footerId = AddFooterPart(mainPart, formId);
                        body.AppendChild(CreateSectionProperties(headerId, footerId));
                    }
                }

                return WordFileCommitHelper.CommitFile(workPath, filePath);
            }
            catch (IOException ex)
            {
                if (File.Exists(workPath))
                {
                    try { File.Delete(workPath); } catch { /* ignore */ }
                }
                throw new IOException(ex.Message, ex);
            }
            catch
            {
                if (File.Exists(workPath))
                {
                    try { File.Delete(workPath); } catch { /* ignore */ }
                }
                throw;
            }
        }

        private static List<(RingKnifeSample? Sample, SamplePointResult? Result)> BuildSlots(
            List<RingKnifeSample> samples,
            List<SamplePointResult> results,
            int slotsPerPage)
        {
            var slots = new List<(RingKnifeSample?, SamplePointResult?)>();
            for (var i = 0; i < samples.Count; i++)
            {
                var result = i < results.Count ? results[i] : null;
                slots.Add((samples[i], result));
            }

            var pageCount = Math.Max(1, (slots.Count + slotsPerPage - 1) / slotsPerPage);
            var target = pageCount * slotsPerPage;
            while (slots.Count < target)
                slots.Add((null, null));

            return slots;
        }

        private static string AddHeaderPart(MainDocumentPart mainPart, int pageNumber, int totalPages)
        {
            var headerPart = mainPart.AddNewPart<HeaderPart>();
            var header = new Header();
            var para = new Paragraph();
            para.Append(new ParagraphProperties(new Justification { Val = JustificationValues.Right }));
            AppendRuns(para, $"共 {totalPages} 页  第 {pageNumber} 页", BodyFontSize);
            header.Append(para);
            headerPart.Header = header;
            return mainPart.GetIdOfPart(headerPart);
        }

        private static string AddFooterPart(MainDocumentPart mainPart, string formId)
        {
            var footerPart = mainPart.AddNewPart<FooterPart>();
            var footer = new Footer();
            var para = new Paragraph();
            para.Append(new ParagraphProperties(new Justification { Val = JustificationValues.Right }));
            AppendRuns(para, formId, BodyFontSize);
            footer.Append(para);
            footerPart.Footer = footer;
            return mainPart.GetIdOfPart(footerPart);
        }

        private static SectionProperties CreateSectionProperties(string headerId, string footerId)
        {
            return new SectionProperties(
                new PageSize
                {
                    Width = (UInt32Value)16840U,
                    Height = (UInt32Value)11907U,
                    Orient = PageOrientationValues.Landscape
                },
                new PageMargin
                {
                    Top = 720,
                    Right = 1080,
                    Bottom = 900,
                    Left = 1080,
                    Header = 720,
                    Footer = 720
                },
                new HeaderReference { Type = HeaderFooterValues.Default, Id = headerId },
                new FooterReference { Type = HeaderFooterValues.Default, Id = footerId });
        }

        private static Paragraph CreatePageBreakParagraph()
        {
            var para = new Paragraph();
            para.Append(new Run(new Break { Type = BreakValues.Page }));
            return para;
        }

        private static Paragraph CreateEmptyParagraph()
        {
            var para = new Paragraph();
            para.Append(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
            return para;
        }

        private static Table CreateSignatureTable(string inspector, string reviewer)
        {
            var table = new Table();
            table.AppendChild(new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));
            var grid = new TableGrid();
            grid.Append(new GridColumn { Width = "4680" });
            grid.Append(new GridColumn { Width = "4680" });
            table.AppendChild(grid);

            var row = CreateRow(
                CreateCell($"检测：{DashIfEmpty(inspector)}", align: JustificationValues.Left),
                CreateCell($"复核：{DashIfEmpty(reviewer)}", align: JustificationValues.Center));
            table.Append(row);
            return table;
        }

        private static Table CreatePageTable(
            List<(RingKnifeSample? Sample, SamplePointResult? Result)> slots,
            int pageIndex,
            int slotsPerPage,
            RecordParams p,
            bool isGroup3,
            int[] colWidths)
        {
            var table = CreateTable(colWidths);
            var isPercent = p.ResultType == "compaction_percent";
            var resultHeader = BuildResultTypeHeader(isPercent);

            AppendInfoRows(table, p);
            table.Append(CreateHeaderRow1(resultHeader));
            table.Append(CreateHeaderRow2());

            var ringsPerBlock = isGroup3 ? 3 : 2;
            var rowsPerBlock = ringsPerBlock * 2;
            var pageDataRows = slotsPerPage * rowsPerBlock;
            var start = pageIndex * slotsPerPage;
            var pageDatesPlaced = false;
            var pageSampling = Placeholder;
            var pageTestDate = Placeholder;
            for (var i = 0; i < slotsPerPage; i++)
            {
                var (sample, _) = slots[start + i];
                if (sample == null) continue;
                pageSampling = DashIfEmpty(DateHelper.FormatWordDate(sample.SamplingDate));
                pageTestDate = DashIfEmpty(DateHelper.EnsureRangeFormat(sample.TestDate) ?? string.Empty);
                break;
            }

            for (var i = 0; i < slotsPerPage; i++)
            {
                var (sample, result) = slots[start + i];
                AppendSampleBlock(
                    table, sample, result, isGroup3, rowsPerBlock, ringsPerBlock, isPercent,
                    ref pageDatesPlaced, pageDataRows, pageSampling, pageTestDate);
            }

            AppendFooterRows(table);
            return table;
        }

        private static void AppendInfoRows(Table table, RecordParams p)
        {
            var soil = DashIfEmpty(string.IsNullOrWhiteSpace(p.SoilType) ? p.MaterialType : p.SoilType);
            if (soil == Placeholder)
                soil = "土";

            var row0 = CreateRow();
            row0.Append(
                LabelSpan("土样种类", 2),
                ValueSpan(soil, 2),
                LabelSpan("最大干密度(g/cm³)", 3),
                ValueSpan(FmtOrDash(p.MaxDryDensity), 3),
                CreateCell("测点示意图", 3, 3, align: JustificationValues.Center),
                CreateCell(string.Empty, 8, 3, align: JustificationValues.Center));
            table.Append(row0);

            var row1 = CreateRow();
            row1.Append(
                LabelSpan("夯实方法", 2),
                ValueSpan(DashIfEmpty(p.CompactionMethod), 2),
                LabelSpan("最佳含水率(%)", 3),
                ValueSpan(FmtOrDash(p.OptimalMoisture), 3),
                ContinueSpanCell(3),
                ContinueSpanCell(8));
            table.Append(row1);

            var row2 = CreateRow();
            row2.Append(
                LabelSpan("检验标准", 2),
                SpanCell(BuildStandardsText(p), 8, JustificationValues.Center),
                ContinueSpanCell(3),
                ContinueSpanCell(8));
            table.Append(row2);
        }

        private static TableCell LabelSpan(string text, int colspan) =>
            CreateCell(text, colspan, 1, align: JustificationValues.Center);

        private static TableCell ValueSpan(string text, int colspan) =>
            CreateCell(text, colspan, 1, align: JustificationValues.Center);

        private static string BuildResultTypeHeader(bool isPercent)
        {
            var percentMark = isPercent ? "☑" : "□";
            var coeffMark = isPercent ? "□" : "☑";
            return $"{percentMark}压实度% {coeffMark}压实系数";
        }

        private static void AppendSampleBlock(
            Table table,
            RingKnifeSample? sample,
            SamplePointResult? result,
            bool isGroup3,
            int rowsPerBlock,
            int ringsPerBlock,
            bool isPercent,
            ref bool pageDatesPlaced,
            int pageDataRows,
            string pageSampling,
            string pageTestDate)
        {
            var isEmpty = sample == null;
            var sampling = isEmpty ? Placeholder : pageSampling;
            var testDate = isEmpty ? Placeholder : pageTestDate;
            var sampleNo = isEmpty ? Placeholder : DashIfEmpty(sample!.SampleNo);
            var elevation = isEmpty ? Placeholder : DashIfEmpty(sample!.Elevation);

            for (var ri = 0; ri < ringsPerBlock; ri++)
            {
                var ring = sample?.Rings.ElementAtOrDefault(ri);
                var ringResult = result?.Rings.ElementAtOrDefault(ri);
                var box1 = ring?.Boxes.ElementAtOrDefault(0);
                var box2 = ring?.Boxes.ElementAtOrDefault(1);

                var ringRow = CreateRow();
                if (ri == 0)
                {
                    ringRow.Append(
                        VRestart(sampleNo, rowsPerBlock),
                        VRestart(elevation, rowsPerBlock));
                    if (!pageDatesPlaced)
                    {
                        ringRow.Append(
                            VRestart(sampling, pageDataRows),
                            VRestart(testDate, pageDataRows));
                        pageDatesPlaced = true;
                    }
                    else
                    {
                        ringRow.Append(VContinue(), VContinue());
                    }
                }
                else
                {
                    ringRow.Append(VContinue(), VContinue(), VContinue(), VContinue());
                }

                ringRow.Append(
                    VRestart(isEmpty ? Placeholder : FmtOrDash(ring?.RingSampleMass), 2),
                    VRestart(isEmpty ? Placeholder : FmtOrDash(ring?.RingMass), 2),
                    VRestart(isEmpty ? Placeholder : FmtOrDash(ring?.RingVolume ?? 200), 2),
                    VRestart(isEmpty ? Placeholder : FormatMassOrDash(ringResult?.WetMass), 2),
                    VRestart(isEmpty ? Placeholder : FormatDensityOrDash(ringResult?.WetDensity), 2));

                if (ri == 0)
                    ringRow.Append(VRestart(isEmpty ? Placeholder : FormatDensityOrDash(result?.AvgWetDensity ?? result?.WetDensity), rowsPerBlock));
                else
                    ringRow.Append(VContinue());

                ringRow.Append(
                    Cell(isEmpty ? Placeholder : DashIfEmpty(box1?.BoxNo)),
                    Cell(isEmpty ? Placeholder : FmtOrDash(box1?.BoxMass)),
                    Cell(isEmpty ? Placeholder : FmtOrDash(box1?.WetSampleMass), colspan: 2),
                    Cell(isEmpty ? Placeholder : FmtOrDash(box1?.DrySampleMass)),
                    Cell(isEmpty ? Placeholder : FormatMoistureOrDash(ringResult?.MoistureRates.ElementAtOrDefault(0))),
                    VRestart(isEmpty ? Placeholder : FormatMoistureOrDash(ringResult?.AvgMoisture), 2));

                if (ri == 0)
                    ringRow.Append(VRestart(isEmpty ? Placeholder : FormatMoistureOrDash(result?.AvgMoisture), rowsPerBlock));
                else
                    ringRow.Append(VContinue());

                ringRow.Append(VRestart(isEmpty ? Placeholder : FormatDensityOrDash(ringResult?.DryDensity), 2));

                if (ri == 0)
                    ringRow.Append(VRestart(isEmpty ? Placeholder : FormatDensityOrDash(result?.AvgDryDensity ?? result?.DryDensity), rowsPerBlock));
                else
                    ringRow.Append(VContinue());

                if (isGroup3)
                {
                    var compaction = isEmpty
                        ? Placeholder
                        : isPercent
                            ? FormatPercentOrDash(ringResult?.CompactionPercent)
                            : FormatCoeffOrDash(ringResult?.CompactionCoeff);
                    ringRow.Append(VRestart(compaction, 2));
                }
                else if (ri == 0)
                {
                    var compaction = isEmpty
                        ? Placeholder
                        : isPercent
                            ? FormatPercentOrDash(result?.CompactionPercent)
                            : FormatCoeffOrDash(result?.CompactionCoeff);
                    ringRow.Append(VRestart(compaction, rowsPerBlock));
                }
                else
                {
                    ringRow.Append(VContinue());
                }

                table.Append(ringRow);

                var boxRow = CreateRow();
                boxRow.Append(VContinue(), VContinue(), VContinue(), VContinue());
                boxRow.Append(VContinue(), VContinue(), VContinue(), VContinue(), VContinue());
                boxRow.Append(VContinue());
                boxRow.Append(
                    Cell(isEmpty ? Placeholder : DashIfEmpty(box2?.BoxNo)),
                    Cell(isEmpty ? Placeholder : FmtOrDash(box2?.BoxMass)),
                    Cell(isEmpty ? Placeholder : FmtOrDash(box2?.WetSampleMass), colspan: 2),
                    Cell(isEmpty ? Placeholder : FmtOrDash(box2?.DrySampleMass)),
                    Cell(isEmpty ? Placeholder : FormatMoistureOrDash(ringResult?.MoistureRates.ElementAtOrDefault(1))),
                    VContinue());
                boxRow.Append(VContinue());
                boxRow.Append(VContinue());
                boxRow.Append(VContinue());
                boxRow.Append(VContinue());
                table.Append(boxRow);
            }
        }

        private static TableRow CreateHeaderRow1(string resultHeader)
        {
            var row = CreateRow();
            row.Append(
                Header("样品编号", rowspan: 2),
                Header("测点标高\n（mm）", rowspan: 2),
                Header("日    期", colspan: 2),
                Header("土样湿密度（g/cm³）", colspan: 6),
                Header("含水率（%）", colspan: 8),
                Header("土样干密度\n（g/cm³）", rowspan: 2),
                Header("干密度平均值\n（g/cm³）", rowspan: 2),
                Header(resultHeader, rowspan: 2));
            return row;
        }

        private static TableRow CreateHeaderRow2()
        {
            var row = CreateRow();
            row.Append(
                VContinue(),
                VContinue(),
                Header("取样"),
                Header("检测"),
                Header("环刀和样质量\n（g）"),
                Header("环刀质量\n（g）"),
                Header("环刀容积\n（cm³）"),
                Header("湿土质量\n（g）"),
                Header("湿密度\n（g/cm³）"),
                Header("平均湿密度\n（g/cm³）"),
                Header("铝盒号"),
                Header("铝盒质量\n（g）"),
                Header("湿样+铝盒\n质量（g）", colspan: 2),
                Header("干样+铝盒\n质量（g）"),
                Header("含水率\n（%）"),
                Header("平均值\n(%)"),
                Header("含水率\n平均值(%)"),
                VContinue(),
                VContinue(),
                VContinue());
            return row;
        }

        private static void AppendFooterRows(Table table)
        {
            var equipmentText = $"天平：□{string.Join("  □", DefaultBalances)}\n烘箱：□{string.Join("  □", DefaultOvens)}";
            var locationText = "检测地点：河滨北路370号\n1#楼102室、105室";

            table.Append(CreateRow(
                SpanCell("样品测前", 2, JustificationValues.Center),
                SpanCell("□无异常   □", 5, JustificationValues.Center),
                SpanCell("检测设备", 3, JustificationValues.Center),
                SpanCell(equipmentText, 5, JustificationValues.Center),
                SpanCell(locationText, 6, JustificationValues.Center)));

            table.Append(CreateRow(
                SpanCell(
                    "备注：1、含水率测试时间：         ~          ；烘箱温度：     ；含水率检测方法：□烘干法  □酒精燃烧法",
                    21,
                    JustificationValues.Left)));
        }

        private static string BuildStandardsText(RecordParams p)
        {
            var basis = string.Join(" ", p.Standards.Concat(new[] { p.TestBasis, p.JudgeBasis }));
            var parts = StandardOptions.Select(s =>
                (ContainsStandard(basis, s) ? "☑" : "□") + s);
            return string.Join("  ", parts);
        }

        private static bool ContainsStandard(string haystack, string standard) =>
            haystack.Contains(standard, StringComparison.OrdinalIgnoreCase);

        private static string DashIfEmpty(string? value) =>
            string.IsNullOrWhiteSpace(value) ? Placeholder : value.Trim();

        private static string FmtOrDash(decimal? v) =>
            v.HasValue ? Fmt(v) : Placeholder;

        private static string FormatMassOrDash(decimal? v) =>
            v.HasValue ? v.Value.ToString("F2") : Placeholder;

        private static string FormatDensityOrDash(decimal? v)
        {
            var s = CompactionFormat.FormatDensity(v);
            return string.IsNullOrEmpty(s) ? Placeholder : s;
        }

        private static string FormatMoistureOrDash(decimal? v)
        {
            var s = CompactionFormat.FormatMoisture(v);
            return string.IsNullOrEmpty(s) ? Placeholder : s;
        }

        private static string FormatPercentOrDash(decimal? v)
        {
            var s = CompactionFormat.FormatPercent(v);
            return string.IsNullOrEmpty(s) ? Placeholder : s;
        }

        private static string FormatCoeffOrDash(decimal? v)
        {
            var s = CompactionFormat.FormatCoeff(v);
            return string.IsNullOrEmpty(s) ? Placeholder : s;
        }

        private static Paragraph CreateTitleParagraph(string text)
        {
            var para = new Paragraph();
            para.AppendChild(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
            AppendRuns(para, text, TitleFontSize, bold: true);
            return para;
        }

        private static Table CreateTable(int[] colWidths)
        {
            var table = new Table();
            var totalWidth = colWidths.Sum();
            var grid = new TableGrid();
            foreach (var w in colWidths)
                grid.Append(new GridColumn { Width = w.ToString() });

            table.AppendChild(new TableProperties(
                new TableWidth { Width = totalWidth.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableIndentation { Width = 0, Type = TableWidthUnitValues.Dxa },
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableJustification { Val = TableRowAlignmentValues.Center },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4 },
                    new BottomBorder { Val = BorderValues.Single, Size = 4 },
                    new LeftBorder { Val = BorderValues.Single, Size = 4 },
                    new RightBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));
            table.AppendChild(grid);
            return table;
        }

        private static TableRow CreateRow() => new(new TableRowProperties(
            new CantSplit(),
            new TableRowHeight { Val = MinRowHeightTwips, HeightType = HeightRuleValues.AtLeast }));

        private static TableRow CreateRow(params TableCell[] cells)
        {
            var row = CreateRow();
            foreach (var cell in cells)
                row.Append(cell);
            return row;
        }

        private static TableCell Header(string text, int colspan = 1, int rowspan = 1) =>
            CreateCell(text, colspan, rowspan, align: JustificationValues.Center);

        private static TableCell Cell(string text, int colspan = 1) =>
            CreateCell(text, colspan, 1, align: JustificationValues.Center);

        private static TableCell VRestart(string text, int rowspan) =>
            CreateCell(text, 1, rowspan, align: JustificationValues.Center);

        private static TableCell VContinue() => ContinueCell();

        private static TableCell SpanCell(
            string text,
            int colspan,
            JustificationValues align) =>
            CreateCell(text, colspan, 1, align: align);

        private static TableCell CreateCell(
            string text,
            int colspan = 1,
            int rowspan = 1,
            JustificationValues? align = null)
        {
            var cell = new TableCell();
            var tcp = new TableCellProperties();
            if (colspan > 1)
                tcp.Append(new GridSpan { Val = colspan });
            if (rowspan > 1)
                tcp.Append(new VerticalMerge { Val = MergedCellValues.Restart });
            tcp.TableCellVerticalAlignment = new TableCellVerticalAlignment
            {
                Val = TableVerticalAlignmentValues.Center
            };
            cell.Append(tcp);
            AppendCellParagraphs(cell, text, align ?? JustificationValues.Center);
            return cell;
        }

        private static void AppendCellParagraphs(TableCell cell, string text, JustificationValues align)
        {
            var lines = string.IsNullOrEmpty(text) ? Array.Empty<string>() : text.Split('\n');
            if (lines.Length == 0)
            {
                var empty = new Paragraph();
                empty.Append(CreateCellParagraphProperties(align));
                cell.Append(empty);
                return;
            }

            foreach (var line in lines)
            {
                var para = new Paragraph();
                para.Append(CreateCellParagraphProperties(align));
                AppendRuns(para, line, BodyFontSize);
                cell.Append(para);
            }
        }

        private static ParagraphProperties CreateCellParagraphProperties(JustificationValues align)
        {
            var pPr = new ParagraphProperties();
            pPr.Justification = new Justification { Val = align };
            pPr.SpacingBetweenLines = new SpacingBetweenLines
            {
                Before = "0",
                After = "0",
                Line = "240",
                LineRule = LineSpacingRuleValues.Auto
            };
            pPr.Append(new ContextualSpacing());
            return pPr;
        }

        private static TableCell ContinueCell() => ContinueSpanCell(1);

        private static TableCell ContinueSpanCell(int colspan)
        {
            var cell = new TableCell();
            var tcp = new TableCellProperties(
                new VerticalMerge { Val = MergedCellValues.Continue },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            if (colspan > 1)
                tcp.Append(new GridSpan { Val = colspan });
            cell.Append(tcp);
            var para = new Paragraph();
            para.Append(CreateCellParagraphProperties(JustificationValues.Center));
            cell.Append(para);
            return cell;
        }

        private static void AppendRuns(Paragraph para, string text, string fontSize, bool bold = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var ch in text)
            {
                var run = new Run();
                var rPr = new RunProperties();
                rPr.Append(new RunFonts { Ascii = EnFont, HighAnsi = EnFont, EastAsia = CnFont });
                rPr.Append(new FontSize { Val = fontSize });
                if (bold)
                    rPr.Append(new Bold());
                run.Append(rPr);
                run.Append(new Text(ch.ToString()) { Space = SpaceProcessingModeValues.Preserve });
                para.Append(run);
            }
        }

        private static string Fmt(decimal? v) =>
            v.HasValue ? v.Value.ToString(v.Value % 1 == 0 ? "0" : "0.####################") : string.Empty;
    }
}
