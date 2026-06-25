using System.IO;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RingKnifeDetector.Helpers;
using RingKnifeDetector.Models;

namespace RingKnifeDetector.Services
{
    public class WordExportService
    {
        private const int ReportTotalPages = 1;
        private const string CnFont = "仿宋_GB2312";
        private const string EnFont = "Times New Roman";
        private const string BodyFontSizeHalfPoints = "21"; // 10.5pt 五号
        private const string TitleFontSizeHalfPoints = "44"; // 22pt 二号

        private static readonly Regex CjkRegex = new(
            @"[\u2e80-\u9fff\u3400-\u4dbf\uf900-\ufaff\u3000-\u303f\uff00-\uffef]",
            RegexOptions.Compiled);

        public string ExportToWord(
            ProjectInfo project,
            RecordParams p,
            List<SamplePointResult> results,
            string overallConclusion,
            string reportRemarks,
            string approverName,
            string reviewerName,
            string inspectorName,
            string filePath)
        {
            var templatePath = ResolveTemplatePath();
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"报告模板不存在: {templatePath}");

            ResultTypeHelper.SyncFromDesignText(p);

            var workPath = Path.Combine(Path.GetTempPath(), $"ring_report_{Guid.NewGuid():N}.docx");
            try
            {
                File.Copy(templatePath, workPath, true);

                using (var doc = WordprocessingDocument.Open(workPath, true))
                {
                    var body = doc.MainDocumentPart?.Document?.Body;
                    if (body == null) throw new InvalidOperationException("无法读取报告模板");

                    var mapping = BuildMapping(project, p, overallConclusion);
                    ReplacePlaceholders(body, mapping);
                    FillHeaderParagraphs(body, project);
                    FillInspectorParagraph(body, approverName, reviewerName, inspectorName);

                    var table = body.Elements<Table>().FirstOrDefault();
                    if (table != null)
                    {
                        var (sampleStart, conclusionRow, remarksRow) = EnsureProjectTableLayout(table, project);
                        FillProjectInfo(table, project, p);
                        UpdateCompactionHeader(table, sampleStart - 1, p.ResultType);

                        var ringsPerBlock = GetRingsPerBlock(p);
                        var useMultiRingRows = p.RecordTemplate == "group3" && ringsPerBlock > 1;
                        var requiredDataRows = useMultiRingRows
                            ? Math.Max(1, results.Count) * ringsPerBlock
                            : Math.Max(1, results.Count);
                        (conclusionRow, remarksRow) = EnsureSampleDataRows(
                            table, sampleStart, conclusionRow, remarksRow, requiredDataRows);

                        FillSampleRows(table, results, p, sampleStart, ringsPerBlock, useMultiRingRows);
                        SetPhysicalCellText(table, conclusionRow, 1, overallConclusion);
                        SetRemarksRow(table, remarksRow, reportRemarks);
                    }

                    StyleDocument(body);
                }

                return WordFileCommitHelper.CommitFile(workPath, filePath);
            }
            catch (IOException ex)
            {
                if (File.Exists(workPath))
                {
                    try { File.Delete(workPath); } catch { /* ignore */ }
                }
                throw new IOException(TranslateIoMessage(ex, filePath), ex);
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

        private static string TranslateIoMessage(IOException ex, string destinationPath)
        {
            if (ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("正在由另一进程使用", StringComparison.OrdinalIgnoreCase))
            {
                return $"无法写入报告文件，请关闭正在打开的 Word 文档后重试：{destinationPath}";
            }
            return ex.Message;
        }

        private static string ResolveTemplatePath()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Resources", "report_template.docx"),
                Path.Combine(AppContext.BaseDirectory, "report_template.docx"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "环刀300.docx"),
            };
            foreach (var path in candidates)
            {
                var full = Path.GetFullPath(path);
                if (File.Exists(full)) return full;
            }
            return candidates[0];
        }

        private static Dictionary<string, string> BuildMapping(ProjectInfo project, RecordParams p, string conclusion)
        {
            var design = FormatDesignRequirement(p);
            var labels = GetFieldLabels(project);
            var mapping = new Dictionary<string, string>
            {
                ["委托编号"] = project.EntrustNo,
                ["报告编号"] = project.ReportNo,
                ["委托单位"] = project.EntrustUnit,
                ["联系方式"] = project.Contact,
                [labels.Supervision] = project.SupervisionUnit,
                [labels.Construction] = project.ConstructionUnit,
                ["工程名称"] = project.ProjectName,
                ["单位地址"] = project.UnitAddress,
                ["工程地址"] = project.ProjectAddress,
                ["委托日期"] = DateHelper.FormatWordDate(project.EntrustDate),
                ["工程部位"] = project.ProjectSection,
                ["报告日期"] = DateHelper.FormatWordDate(project.ReportDate),
                ["检测性质"] = project.TestNature,
                ["样品名称"] = p.SampleName,
                ["材料种类"] = p.MaterialType,
                [labels.RingSpec] = p.RingSpec,
                ["夯实方式"] = p.CompactionMethod,
                ["设计要求"] = design,
                ["最大干密度"] = FmtRaw(p.MaxDryDensity),
                ["最优含水率"] = FmtRaw(p.OptimalMoisture),
                ["检测依据"] = p.TestBasis,
                ["判定依据"] = p.JudgeBasis,
                ["检测结论"] = conclusion,
            };

            // 旧模板占位符仍可能使用现场检测字段名。
            mapping["监理单位"] = project.SupervisionUnit;
            mapping["施工单位"] = project.ConstructionUnit;
            mapping["环刀规格"] = p.RingSpec;
            if (TestNatureHelper.IsWitnessSampling(project.TestNature))
            {
                mapping["工程见证"] = project.SupervisionUnit;
                mapping["样品取样"] = project.ConstructionUnit;
                mapping["规格型号"] = p.RingSpec;
            }

            return mapping;
        }

        private readonly record struct ReportFieldLabels(string Supervision, string Construction, string RingSpec);

        private static ReportFieldLabels GetFieldLabels(ProjectInfo project) =>
            TestNatureHelper.IsWitnessSampling(project.TestNature)
                ? new ReportFieldLabels("工程见证", "样品取样", "规格型号")
                : new ReportFieldLabels("监理单位", "施工单位", "环刀规格");

        internal const string HeaderSectionGap = "                     ";
        /// <summary>委托编号行右缩进（约 1 个汉字），使「编号：」与下行报告编号冒号对齐。</summary>
        private const int EntrustNoRightIndentTwips = 210;
        /// <summary>与首行「备注：」等宽，使 2. 3. … 与 1. 对齐（约 3 个汉字 @10.5pt）。</summary>
        private const int RemarksNumberIndentTwips = 735;

        private static void FillHeaderParagraphs(Body body, ProjectInfo project)
        {
            var paragraphs = body.Elements<Paragraph>().ToList();
            if (paragraphs.Count < 4) return;

            SetEntrustNoParagraph(paragraphs[2], project.EntrustNo);
            ApplyParagraphTabLayout(paragraphs[3], project.TestNature, project.ReportNo);
        }

        private static void SetEntrustNoParagraph(Paragraph para, string entrustNo)
        {
            para.RemoveAllChildren();
            var pPr = new ParagraphProperties
            {
                Justification = new Justification { Val = JustificationValues.Right },
                SuppressAutoHyphens = new SuppressAutoHyphens(),
                Indentation = new Indentation { Right = EntrustNoRightIndentTwips.ToString() }
            };
            para.Append(pPr);

            var text = $"委托编号：{PreventWordBreak(entrustNo)}";
            var run = new Run();
            run.AppendChild(CreateRunProperties(isCjk: true, BodyFontSizeHalfPoints));
            run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            para.Append(run);
        }

        /// <summary>避免 Word 在连字符处断行（如 TG11-260350）。</summary>
        private static string PreventWordBreak(string? value) =>
            string.IsNullOrEmpty(value) ? string.Empty : value.Replace('-', '\u2011');

        private static void ApplyParagraphTabLayout(Paragraph para, string testNature, string reportNo)
        {
            para.RemoveAllChildren();
            var pPr = new ParagraphProperties
            {
                Justification = new Justification { Val = JustificationValues.Left },
                SpacingBetweenLines = new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }
            };
            para.Append(pPr);

            var text =
                $"检测性质：{testNature}{HeaderSectionGap}共{ReportTotalPages}页，第{ReportTotalPages}页{HeaderSectionGap}报告编号：{PreventWordBreak(reportNo)}";
            var run = new Run();
            run.AppendChild(CreateRunProperties(isCjk: true, BodyFontSizeHalfPoints));
            run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            para.Append(run);
        }

        private static string FormatDesignRequirement(RecordParams p) =>
            DesignRequirementFormatter.FormatForWord(p);

        /// <summary>
        /// 调整样品数据区行数（2 环刀/组或 3 环刀/组），返回更新后的结论行、备注行索引。
        /// </summary>
        internal static (int conclusionRow, int remarksRow) EnsureSampleDataRows(
            Table table,
            int sampleStartRow,
            int conclusionRow,
            int remarksRow,
            int requiredDataRows)
        {
            var currentDataRows = conclusionRow - sampleStartRow;
            if (requiredDataRows == currentDataRows)
                return (conclusionRow, remarksRow);

            var rows = table.Elements<TableRow>().ToList();
            if (requiredDataRows < currentDataRows)
            {
                var removeCount = currentDataRows - requiredDataRows;
                for (var i = 0; i < removeCount; i++)
                {
                    var rowToRemove = table.Elements<TableRow>().ElementAt(sampleStartRow + requiredDataRows);
                    rowToRemove.Remove();
                }

                return (conclusionRow - removeCount, remarksRow - removeCount);
            }

            var prototype = rows[sampleStartRow + currentDataRows - 1];
            var insertCount = requiredDataRows - currentDataRows;
            for (var i = 0; i < insertCount; i++)
            {
                var clone = (TableRow)prototype.CloneNode(true);
                ClearRowText(clone);
                table.Elements<TableRow>().ElementAt(conclusionRow + i).InsertBeforeSelf(clone);
            }

            return (conclusionRow + insertCount, remarksRow + insertCount);
        }

        private static void FillInspectorParagraph(Body body, string approverName, string reviewerName, string inspectorName)
        {
            var paragraphs = body.Elements<Paragraph>().ToList();
            if (paragraphs.Count < 5) return;
            var approver = string.IsNullOrWhiteSpace(approverName) ? "" : approverName.Trim();
            var reviewer = string.IsNullOrWhiteSpace(reviewerName) ? "" : reviewerName.Trim();
            var inspector = string.IsNullOrWhiteSpace(inspectorName) ? "" : inspectorName.Trim();
            SetParagraphText(
                paragraphs[4],
                $"检验单位（盖章）：         批准：{approver}                   审核：{reviewer}                      主检：{inspector}        ");
        }

        private static void ReplacePlaceholders(Body body, Dictionary<string, string> mapping)
        {
            foreach (var para in body.Descendants<Paragraph>())
            {
                var text = para.InnerText;
                foreach (var kv in mapping)
                {
                    var token = $"{{{{{kv.Key}}}}}";
                    if (text.Contains(token))
                        SetParagraphText(para, text.Replace(token, kv.Value));
                }
            }
            foreach (var cell in body.Descendants<TableCell>())
            {
                var text = cell.InnerText;
                foreach (var kv in mapping)
                {
                    var token = $"{{{{{kv.Key}}}}}";
                    if (text.Contains(token))
                        SetCellText(cell, text.Replace(token, kv.Value));
                }
            }
        }

        /// <summary>
        /// 在工程名称行下方插入监理/施工单位行。返回样品数据行、结论行、备注行索引。
        /// </summary>
        private static (int sampleStart, int conclusionRow, int remarksRow) EnsureProjectTableLayout(Table table, ProjectInfo project)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count < 3) return (10, 13, 14);

            var labels = GetFieldLabels(project);
            var headerText = GetPhysicalCellText(rows[2], 0);
            if (headerText.Contains("监理单位", StringComparison.Ordinal)
                || headerText.Contains("工程见证", StringComparison.Ordinal))
                return (11, 14, 15);

            var newRow = (TableRow)rows[1].CloneNode(true);
            ClearRowText(newRow);
            rows[1].InsertAfterSelf(newRow);

            SetPhysicalCellText(newRow, 0, labels.Supervision, JustificationValues.Center, verticalCenter: true);
            SetPhysicalCellText(newRow, 2, labels.Construction, JustificationValues.Center, verticalCenter: true);
            return (11, 14, 15);
        }

        private static void FillProjectInfo(Table table, ProjectInfo project, RecordParams p)
        {
            var design = FormatDesignRequirement(p);
            var pairs = new (int row, int valCol, string val)[]
            {
                (0, 1, project.EntrustUnit), (0, 3, project.Contact),
                (1, 1, project.ProjectName), (1, 3, project.UnitAddress),
                (2, 1, project.SupervisionUnit), (2, 3, project.ConstructionUnit),
                (3, 1, project.ProjectAddress), (3, 3, DateHelper.FormatWordDate(project.EntrustDate)),
                (4, 1, project.ProjectSection), (4, 3, DateHelper.FormatWordDate(project.ReportDate)),
                (5, 1, p.SampleName), (5, 3, p.MaterialType),
                (6, 1, p.RingSpec), (6, 3, p.CompactionMethod),
                (7, 1, design), (7, 3, FmtRaw(p.MaxDryDensity)),
                (8, 1, p.TestLocation), (8, 3, FmtRaw(p.OptimalMoisture)),
                (9, 1, p.TestBasis), (9, 3, p.JudgeBasis),
            };
            foreach (var (row, valCol, val) in pairs)
            {
                if (row == 2) continue;
                SetPhysicalCellText(table, row, valCol, val);
            }
            FillSupervisionConstructionRow(table, project);
            var labels = GetFieldLabels(project);
            SetPhysicalCellText(table, 6, 0, labels.RingSpec, JustificationValues.Center, verticalCenter: true);
        }

        private static void FillSupervisionConstructionRow(Table table, ProjectInfo project)
        {
            var labels = GetFieldLabels(project);
            SetPhysicalCellText(table, 2, 0, labels.Supervision, JustificationValues.Center, verticalCenter: true);
            SetPhysicalCellText(table, 2, 1, project.SupervisionUnit, JustificationValues.Left, verticalCenter: true);
            SetPhysicalCellText(table, 2, 2, labels.Construction, JustificationValues.Center, verticalCenter: true);
            SetPhysicalCellText(table, 2, 3, project.ConstructionUnit, JustificationValues.Left, verticalCenter: true);
        }

        private static void UpdateCompactionHeader(Table table, int headerRow, string resultType)
        {
            var label = resultType == "compaction_percent" ? "压实度/%" : "压实系数";
            SetPhysicalCellText(table, headerRow, 8, label, JustificationValues.Center, verticalCenter: true);
        }

        private static int GetRingsPerBlock(RecordParams p) =>
            p.RecordTemplate == "group3" ? 3 : 2;

        private static void FillSampleRows(
            Table table,
            List<SamplePointResult> results,
            RecordParams p,
            int startRow,
            int ringsPerBlock,
            bool useMultiRingRows)
        {
            if (useMultiRingRows)
            {
                FillSampleRowsMultiRing(table, results, p, startRow, ringsPerBlock);
                return;
            }

            FillSampleRowsSingle(table, results, p, startRow);
        }

        private static void FillSampleRowsSingle(
            Table table,
            List<SamplePointResult> results,
            RecordParams p,
            int startRow)
        {
            var rows = table.Elements<TableRow>().ToList();
            var dataRowCount = rows.Count > startRow + 1
                ? rows.Skip(startRow).TakeWhile(r => !IsConclusionOrRemarksRow(r)).Count()
                : Math.Max(1, results.Count);

            for (var i = 0; i < dataRowCount; i++)
            {
                var rowIndex = startRow + i;
                if (i >= results.Count)
                {
                    ClearSampleDataRow(table, rowIndex);
                    continue;
                }

                var r = results[i];
                var samplingDate = DateHelper.FormatWordDate(r.SamplingDate);
                var testDate = DateHelper.FormatWordDate(DateHelper.EnsureRangeFormat(r.TestDate));
                var compaction = p.ResultType == "compaction_percent"
                    ? CompactionFormat.FormatPercent(r.CompactionPercent)
                    : CompactionFormat.FormatCoeff(r.CompactionCoeff);

                SetSampleDataCell(table, rowIndex, 0, r.SampleNo);
                SetSampleDataCell(table, rowIndex, 1, r.Elevation);
                SetSampleDataCell(table, rowIndex, 2, r.Thickness);
                SetSampleDataCell(table, rowIndex, 3, samplingDate);
                SetSampleDataCell(table, rowIndex, 4, testDate);
                SetSampleDataCell(table, rowIndex, 5, CompactionFormat.FormatDensity(r.AvgWetDensity ?? r.WetDensity));
                SetSampleDataCell(table, rowIndex, 6, CompactionFormat.FormatMoisture(r.AvgMoisture));
                SetSampleDataCell(table, rowIndex, 7, CompactionFormat.FormatDensity(r.AvgDryDensity ?? r.DryDensity));
                SetSampleDataCell(table, rowIndex, 8, compaction);
            }
        }

        private static void FillSampleRowsMultiRing(
            Table table,
            List<SamplePointResult> results,
            RecordParams p,
            int startRow,
            int ringsPerBlock)
        {
            if (results.Count == 0)
            {
                ClearSampleBlock(table, startRow, ringsPerBlock);
                return;
            }

            for (var sampleIndex = 0; sampleIndex < results.Count; sampleIndex++)
            {
                var blockStart = startRow + sampleIndex * ringsPerBlock;
                FillSampleBlock(table, blockStart, ringsPerBlock, results[sampleIndex], p);
            }
        }

        private static void ClearSampleBlock(Table table, int blockStart, int ringsPerBlock)
        {
            for (var ringIndex = 0; ringIndex < ringsPerBlock; ringIndex++)
            {
                var row = blockStart + ringIndex;
                for (var col = 0; col <= 8; col++)
                {
                    var merge = col <= 4
                        ? ringIndex == 0 ? VerticalMergeMode.Restart : VerticalMergeMode.Continue
                        : VerticalMergeMode.None;
                    SetSampleCell(table, row, col, string.Empty, merge);
                }
            }
        }

        private static void FillSampleBlock(
            Table table,
            int blockStart,
            int ringsPerBlock,
            SamplePointResult result,
            RecordParams p)
        {
            var samplingDate = DateHelper.FormatWordDate(result.SamplingDate);
            var testDate = DateHelper.FormatWordDate(DateHelper.EnsureRangeFormat(result.TestDate));

            for (var ringIndex = 0; ringIndex < ringsPerBlock; ringIndex++)
            {
                var row = blockStart + ringIndex;
                var ring = result.Rings.ElementAtOrDefault(ringIndex);

                if (ringIndex == 0)
                {
                    SetSampleCell(table, row, 0, result.SampleNo, VerticalMergeMode.Restart);
                    SetSampleCell(table, row, 1, result.Elevation, VerticalMergeMode.Restart);
                    SetSampleCell(table, row, 2, result.Thickness, VerticalMergeMode.Restart);
                    SetSampleCell(table, row, 3, samplingDate, VerticalMergeMode.Restart);
                    SetSampleCell(table, row, 4, testDate, VerticalMergeMode.Restart);
                }
                else
                {
                    SetSampleCell(table, row, 0, string.Empty, VerticalMergeMode.Continue);
                    SetSampleCell(table, row, 1, string.Empty, VerticalMergeMode.Continue);
                    SetSampleCell(table, row, 2, string.Empty, VerticalMergeMode.Continue);
                    SetSampleCell(table, row, 3, string.Empty, VerticalMergeMode.Continue);
                    SetSampleCell(table, row, 4, string.Empty, VerticalMergeMode.Continue);
                }

                SetSampleCell(table, row, 5, CompactionFormat.FormatDensity(ring?.WetDensity), VerticalMergeMode.None);
                SetSampleCell(table, row, 6, CompactionFormat.FormatMoisture(ring?.AvgMoisture), VerticalMergeMode.None);
                SetSampleCell(table, row, 7, CompactionFormat.FormatDensity(ring?.DryDensity), VerticalMergeMode.None);

                var compaction = p.ResultType == "compaction_percent"
                    ? CompactionFormat.FormatPercent(ring?.CompactionPercent)
                    : CompactionFormat.FormatCoeff(ring?.CompactionCoeff);
                SetSampleCell(table, row, 8, compaction, VerticalMergeMode.None);
            }
        }

        private enum VerticalMergeMode
        {
            None,
            Restart,
            Continue
        }

        private static void SetSampleCell(
            Table table,
            int rowIndex,
            int physicalCol,
            string value,
            VerticalMergeMode mergeMode)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rowIndex < 0 || rowIndex >= rows.Count) return;
            var row = rows[rowIndex];
            var cells = row.Elements<TableCell>().ToList();
            if (physicalCol < 0 || physicalCol >= cells.Count) return;

            ApplySampleRowHeight(row);
            var cell = cells[physicalCol];
            if (mergeMode == VerticalMergeMode.None)
                ClearVerticalMerge(cell);
            SetCellText(cell, value ?? string.Empty, JustificationValues.Center, verticalCenter: true);
            ApplyVerticalMerge(cell, mergeMode);
        }

        private static void ApplySampleRowHeight(TableRow row)
        {
            var trPr = row.TableRowProperties ?? row.PrependChild(new TableRowProperties());
            trPr.RemoveAllChildren<TableRowHeight>();
            trPr.Append(new TableRowHeight
            {
                Val = (UInt32Value)360U,
                HeightType = HeightRuleValues.AtLeast
            });
        }

        private static void ApplyVerticalMerge(TableCell cell, VerticalMergeMode mergeMode)
        {
            if (mergeMode == VerticalMergeMode.None)
                return;

            var tcp = cell.TableCellProperties ?? cell.PrependChild(new TableCellProperties());
            tcp.TableCellVerticalAlignment = new TableCellVerticalAlignment
            {
                Val = TableVerticalAlignmentValues.Center
            };
            tcp.VerticalMerge = new VerticalMerge
            {
                Val = mergeMode == VerticalMergeMode.Restart
                    ? MergedCellValues.Restart
                    : MergedCellValues.Continue
            };
        }

        private static bool IsConclusionOrRemarksRow(TableRow row)
        {
            var text = string.Concat(row.Descendants<Text>().Select(t => t.Text));
            return text.Contains("检测结论") || text.StartsWith("备注", StringComparison.Ordinal);
        }

        private static void ClearSampleDataRow(Table table, int rowIndex)
        {
            for (var col = 0; col <= 8; col++)
                SetSampleDataCell(table, rowIndex, col, string.Empty);
        }

        private static void SetSampleDataCell(Table table, int rowIndex, int physicalCol, string value)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rowIndex < 0 || rowIndex >= rows.Count) return;
            var cells = rows[rowIndex].Elements<TableCell>().ToList();
            if (physicalCol < 0 || physicalCol >= cells.Count) return;

            ClearVerticalMerge(cells[physicalCol]);
            SetCellText(cells[physicalCol], value ?? string.Empty, JustificationValues.Center, verticalCenter: true);
        }

        private static void ClearVerticalMerge(TableCell cell)
        {
            var tcp = cell.TableCellProperties;
            tcp?.VerticalMerge?.Remove();
        }

        private static void SetRemarksRow(Table table, int remarksRow, string reportRemarks)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (remarksRow < 0 || remarksRow >= rows.Count) return;
            var row = rows[remarksRow];
            var cell = row.Elements<TableCell>().FirstOrDefault();
            if (cell == null) return;

            var text = string.IsNullOrWhiteSpace(reportRemarks)
                ? ReportDefaults.DefaultReportRemarks
                : reportRemarks.Trim();

            ApplyRemarksRowLayout(row, cell);

            var lines = NormalizeRemarkLines(text);
            cell.RemoveAllChildren<Paragraph>();
            for (var i = 0; i < lines.Count; i++)
            {
                var para = new Paragraph();
                var pPr = new ParagraphProperties
                {
                    SpacingBetweenLines = new SpacingBetweenLines
                    {
                        Before = "0",
                        After = "0",
                        Line = "240",
                        LineRule = LineSpacingRuleValues.Auto
                    }
                };
                if (i > 0)
                    pPr.Indentation = new Indentation { Left = RemarksNumberIndentTwips.ToString() };
                para.Append(pPr);
                AppendStyledRuns(para, lines[i], BodyFontSizeHalfPoints);
                cell.Append(para);
            }
        }

        private static void ApplyRemarksRowLayout(TableRow row, TableCell cell)
        {
            var trPr = row.TableRowProperties ?? row.PrependChild(new TableRowProperties());
            trPr.RemoveAllChildren<TableRowHeight>();

            var tcp = cell.TableCellProperties ?? cell.PrependChild(new TableCellProperties());
            tcp.TableCellVerticalAlignment = new TableCellVerticalAlignment
            {
                Val = TableVerticalAlignmentValues.Top
            };
            tcp.TableCellMargin = new TableCellMargin(
                new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "0", Type = TableWidthUnitValues.Dxa });
        }

        internal static List<string> NormalizeRemarkLines(string text)
        {
            var raw = text.Replace("\r\n", "\n").Split('\n')
                .Select(line => Regex.Replace(line.Trim(), @"[ \t]+", " "))
                .Where(line => !string.IsNullOrEmpty(line))
                .ToList();

            var result = new List<string>();
            for (var i = 0; i < raw.Count; i++)
            {
                var line = raw[i];
                if (i == 0)
                {
                    if (!line.StartsWith("备注", StringComparison.Ordinal))
                        line = "备注：" + line;
                    line = Regex.Replace(line, @"^备注：(\d)", "备注： $1");
                    result.Add(line);
                    continue;
                }

                line = Regex.Replace(line, @"^备注[:：]\s*", "");
                result.Add(line);
            }

            return result;
        }

        private static void StyleDocument(Body body)
        {
            var paragraphs = body.Elements<Paragraph>().ToList();
            if (paragraphs.Count > 0)
            {
                var p0 = paragraphs[0];
                var pPr0 = p0.ParagraphProperties ?? p0.PrependChild(new ParagraphProperties());
                pPr0.Justification = new Justification { Val = JustificationValues.Center };
                SetParagraphText(p0, p0.InnerText, JustificationValues.Center, TitleFontSizeHalfPoints, bold: true);
            }
            if (paragraphs.Count > 1)
                SetParagraphText(paragraphs[1], paragraphs[1].InnerText, JustificationValues.Center, TitleFontSizeHalfPoints, bold: true);

            for (int i = 4; i < paragraphs.Count; i++)
            {
                var para = paragraphs[i];
                if (string.IsNullOrEmpty(para.InnerText)) continue;
                var align = para.ParagraphProperties?.Justification?.Val?.Value;
                SetParagraphText(para, para.InnerText, align);
            }
        }

        private static string FmtRaw(decimal? v) =>
            v.HasValue ? v.Value.ToString(v.Value % 1 == 0 ? "0" : "0.####################") : string.Empty;

        private static string GetPhysicalCellText(TableRow row, int physicalCol)
        {
            var cells = row.Elements<TableCell>().ToList();
            return physicalCol < cells.Count ? cells[physicalCol].InnerText.Trim() : "";
        }

        private static void SetPhysicalCellText(Table table, int rowIndex, int physicalCol, string value,
            JustificationValues? alignment = null, bool verticalCenter = false)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rowIndex < 0 || rowIndex >= rows.Count) return;
            var cells = rows[rowIndex].Elements<TableCell>().ToList();
            if (physicalCol < 0 || physicalCol >= cells.Count) return;
            SetCellText(cells[physicalCol], value ?? "", alignment, verticalCenter);
        }

        private static void SetPhysicalCellText(Table table, int rowIndex, int physicalCol, string value)
            => SetPhysicalCellText(table, rowIndex, physicalCol, value, null, false);

        private static void SetPhysicalCellText(TableRow row, int physicalCol, string value)
        {
            var cells = row.Elements<TableCell>().ToList();
            if (physicalCol < 0 || physicalCol >= cells.Count) return;
            SetCellText(cells[physicalCol], value ?? "");
        }

        private static void SetPhysicalCellText(TableRow row, int physicalCol, string value,
            JustificationValues? alignment, bool verticalCenter)
        {
            var cells = row.Elements<TableCell>().ToList();
            if (physicalCol < 0 || physicalCol >= cells.Count) return;
            SetCellText(cells[physicalCol], value ?? "", alignment, verticalCenter);
        }

        private static void ClearRowText(TableRow row)
        {
            foreach (var cell in row.Elements<TableCell>())
                SetCellText(cell, "");
        }

        private static void SetCellText(TableCell cell, string value,
            JustificationValues? alignment = null, bool verticalCenter = false)
        {
            cell.RemoveAllChildren<Paragraph>();
            var para = new Paragraph();
            if (alignment.HasValue)
            {
                var pPr = new ParagraphProperties();
                pPr.Justification = new Justification { Val = alignment.Value };
                para.AppendChild(pPr);
            }
            AppendStyledRuns(para, value ?? "", BodyFontSizeHalfPoints);
            cell.AppendChild(para);

            if (verticalCenter)
            {
                var tcp = cell.TableCellProperties ?? cell.PrependChild(new TableCellProperties());
                tcp.TableCellVerticalAlignment = new TableCellVerticalAlignment
                {
                    Val = TableVerticalAlignmentValues.Center
                };
            }
        }

        private static void SetParagraphText(
            Paragraph para,
            string value,
            JustificationValues? alignment = null,
            string? fontSizeHalfPoints = null,
            bool bold = false)
        {
            para.RemoveAllChildren();
            var pPr = new ParagraphProperties();
            if (alignment.HasValue)
                pPr.Justification = new Justification { Val = alignment.Value };
            para.Append(pPr);
            AppendStyledRuns(para, value ?? "", fontSizeHalfPoints ?? BodyFontSizeHalfPoints, bold);
        }

        private static void AppendStyledRuns(Paragraph para, string text, string fontSizeHalfPoints, bool bold = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var (chunk, isCjk) in SplitTextSegments(text))
                AppendStyledRun(para, chunk, isCjk, fontSizeHalfPoints, bold);
        }

        private static void AppendStyledRun(
            Paragraph para,
            string chunk,
            bool isCjk,
            string fontSizeHalfPoints,
            bool bold = false)
        {
            var run = new Run();
            run.AppendChild(CreateRunProperties(isCjk, fontSizeHalfPoints, bold));
            run.AppendChild(new Text(chunk) { Space = SpaceProcessingModeValues.Preserve });
            para.AppendChild(run);
        }

        private static List<(string chunk, bool isCjk)> SplitTextSegments(string text)
        {
            var segments = new List<(string, bool)>();
            if (string.IsNullOrEmpty(text)) return segments;

            var buf = new System.Text.StringBuilder();
            bool? isCjk = null;
            foreach (var ch in text)
            {
                var cjk = CjkRegex.IsMatch(ch.ToString());
                if (isCjk == null)
                {
                    isCjk = cjk;
                    buf.Append(ch);
                    continue;
                }
                if (cjk == isCjk)
                    buf.Append(ch);
                else
                {
                    segments.Add((buf.ToString(), isCjk.Value));
                    buf.Clear();
                    buf.Append(ch);
                    isCjk = cjk;
                }
            }
            if (buf.Length > 0 && isCjk.HasValue)
                segments.Add((buf.ToString(), isCjk.Value));
            return segments;
        }

        private static RunProperties CreateRunProperties(bool isCjk, string fontSizeHalfPoints, bool bold = false)
        {
            var props = new RunProperties(
                new RunFonts
                {
                    Ascii = EnFont,
                    HighAnsi = EnFont,
                    ComplexScript = EnFont,
                    EastAsia = isCjk ? CnFont : EnFont
                },
                new FontSize { Val = fontSizeHalfPoints });
            if (bold)
                props.Append(new Bold());
            return props;
        }
    }
}
