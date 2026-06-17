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
            string inspectorName,
            string filePath)
        {
            var templatePath = ResolveTemplatePath();
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"报告模板不存在: {templatePath}");

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
                    FillInspectorParagraph(body, inspectorName);

                    var table = body.Elements<Table>().FirstOrDefault();
                    if (table != null)
                    {
                        var (sampleStart, conclusionRow, remarksRow) = EnsureProjectTableLayout(table);
                        FillProjectInfo(table, project, p);
                        UpdateCompactionHeader(table, sampleStart - 1, p.ResultType);
                        FillSampleRows(table, results, p, sampleStart);
                        SetPhysicalCellText(table, conclusionRow, 1, overallConclusion);
                        SetRemarksRow(table, remarksRow, reportRemarks);
                    }

                    StyleDocument(body);
                }

                return CommitReportFile(workPath, filePath);
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

        private static string CommitReportFile(string sourcePath, string destinationPath)
        {
            IOException? lastError = null;
            foreach (var targetPath in EnumerateSaveCandidates(destinationPath))
            {
                try
                {
                    WriteReportToPath(sourcePath, targetPath);
                    if (!string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(sourcePath); } catch { /* ignore */ }
                    }
                    return targetPath;
                }
                catch (IOException ex)
                {
                    lastError = ex;
                }
            }

            throw new IOException(
                TranslateIoMessage(lastError ?? new IOException("无法写入报告文件"), destinationPath),
                lastError);
        }

        private static void WriteReportToPath(string sourcePath, string destinationPath)
        {
            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(destinationPath))
            {
                File.SetAttributes(destinationPath, FileAttributes.Normal);
                File.Delete(destinationPath);
            }

            File.Copy(sourcePath, destinationPath, true);
        }

        private static IEnumerable<string> EnumerateSaveCandidates(string destinationPath)
        {
            yield return destinationPath;

            var dir = Path.GetDirectoryName(destinationPath) ?? ".";
            var ext = Path.GetExtension(destinationPath);
            var name = Path.GetFileNameWithoutExtension(destinationPath);
            var baseName = Regex.Replace(name, @"\(\d+\)$", string.Empty);

            for (var i = 2; i <= 99; i++)
                yield return Path.Combine(dir, $"{baseName}({i}){ext}");
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
            return new Dictionary<string, string>
            {
                ["委托编号"] = project.EntrustNo,
                ["报告编号"] = project.ReportNo,
                ["委托单位"] = project.EntrustUnit,
                ["联系方式"] = project.Contact,
                ["监理单位"] = project.SupervisionUnit,
                ["施工单位"] = project.ConstructionUnit,
                ["工程名称"] = project.ProjectName,
                ["单位地址"] = project.UnitAddress,
                ["工程地址"] = project.ProjectAddress,
                ["委托日期"] = DateHelper.FormatWordDate(project.EntrustDate),
                ["工程部位"] = project.ProjectSection,
                ["报告日期"] = DateHelper.FormatWordDate(project.ReportDate),
                ["检测性质"] = project.TestNature,
                ["样品名称"] = p.SampleName,
                ["材料种类"] = p.MaterialType,
                ["环刀规格"] = p.RingSpec,
                ["夯实方式"] = p.CompactionMethod,
                ["设计要求"] = design,
                ["最大干密度"] = FmtRaw(p.MaxDryDensity),
                ["最优含水率"] = FmtRaw(p.OptimalMoisture),
                ["检测依据"] = p.TestBasis,
                ["判定依据"] = p.JudgeBasis,
                ["检测结论"] = conclusion,
            };
        }

        private const int HeaderRightTabPosition = 9360;

        private static void FillHeaderParagraphs(Body body, ProjectInfo project)
        {
            var paragraphs = body.Elements<Paragraph>().ToList();
            if (paragraphs.Count < 4) return;

            SetParagraphText(paragraphs[2], $"委托编号：{project.EntrustNo}", JustificationValues.Right);
            ApplyParagraphTabLayout(paragraphs[3], project.TestNature, project.ReportNo);
        }

        private static void ApplyParagraphTabLayout(Paragraph para, string testNature, string reportNo)
        {
            para.RemoveAllChildren<Run>();
            var pPr = para.ParagraphProperties ?? para.PrependChild(new ParagraphProperties());
            pPr.Justification = new Justification { Val = JustificationValues.Left };
            pPr.SpacingBetweenLines = new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto };
            pPr.Tabs = new Tabs(
                new TabStop { Val = TabStopValues.Center, Position = 4680 },
                new TabStop { Val = TabStopValues.Right, Position = HeaderRightTabPosition });

            AppendStyledRun(para, $"检测性质：{testNature}", true, BodyFontSizeHalfPoints);
            para.Append(new Run(new TabChar()));
            AppendStyledRun(para, $"共{ReportTotalPages}页，第{ReportTotalPages}页", true, BodyFontSizeHalfPoints);
            para.Append(new Run(new TabChar()));
            AppendStyledRun(para, $"报告编号：{reportNo}", true, BodyFontSizeHalfPoints);
        }

        private static string FormatDesignRequirement(RecordParams p)
        {
            if (!string.IsNullOrWhiteSpace(p.DesignRequirementText))
                return p.DesignRequirementText.Trim();

            if (!p.DesignRequirement.HasValue)
                return string.Empty;

            var value = TrimDecimal(p.DesignRequirement.Value);
            return p.ResultType == "compaction_percent" ? $"≥{value}%" : $"≥{value}";
        }

        private static string TrimDecimal(decimal value) =>
            value.ToString(value % 1 == 0 ? "0" : "0.####################");

        private static void FillInspectorParagraph(Body body, string inspectorName)
        {
            var paragraphs = body.Elements<Paragraph>().ToList();
            if (paragraphs.Count < 5) return;
            var name = string.IsNullOrWhiteSpace(inspectorName) ? "" : inspectorName.Trim();
            SetParagraphText(
                paragraphs[4],
                $"检验单位（盖章）：         批准：                   审核：                      主检：{name}        ");
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
        private static (int sampleStart, int conclusionRow, int remarksRow) EnsureProjectTableLayout(Table table)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count < 3) return (10, 13, 14);

            if (GetPhysicalCellText(rows[2], 0).Contains("监理单位"))
                return (11, 14, 15);

            var newRow = (TableRow)rows[1].CloneNode(true);
            ClearRowText(newRow);
            rows[1].InsertAfterSelf(newRow);

            SetPhysicalCellText(newRow, 0, "监理单位", JustificationValues.Center, verticalCenter: true);
            SetPhysicalCellText(newRow, 2, "施工单位", JustificationValues.Center, verticalCenter: true);
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
        }

        private static void FillSupervisionConstructionRow(Table table, ProjectInfo project)
        {
            SetPhysicalCellText(table, 2, 0, "监理单位", JustificationValues.Center, verticalCenter: true);
            SetPhysicalCellText(table, 2, 1, project.SupervisionUnit, JustificationValues.Left, verticalCenter: true);
            SetPhysicalCellText(table, 2, 2, "施工单位", JustificationValues.Center, verticalCenter: true);
            SetPhysicalCellText(table, 2, 3, project.ConstructionUnit, JustificationValues.Left, verticalCenter: true);
        }

        private static void UpdateCompactionHeader(Table table, int headerRow, string resultType)
        {
            var label = resultType == "compaction_percent" ? "压实度%" : "压实系数";
            SetPhysicalCellText(table, headerRow, 8, label);
        }

        private static void FillSampleRows(Table table, List<SamplePointResult> results, RecordParams p, int startRow)
        {
            for (int i = 0; i < results.Count && i < 3; i++)
            {
                var r = results[i];
                var row = startRow + i;
                SetPhysicalCellText(table, row, 0, r.SampleNo);
                SetPhysicalCellText(table, row, 1, r.Elevation);
                SetPhysicalCellText(table, row, 2, r.Thickness);
                SetPhysicalCellText(table, row, 3, DateHelper.FormatWordDate(r.SamplingDate));
                SetPhysicalCellText(table, row, 4, DateHelper.FormatWordDate(r.TestDate));
                SetPhysicalCellText(table, row, 5, Fmt(r.AvgWetDensity ?? r.WetDensity));
                SetPhysicalCellText(table, row, 6, Fmt(r.AvgMoisture));
                SetPhysicalCellText(table, row, 7, Fmt(r.AvgDryDensity ?? r.DryDensity));
                var compaction = p.ResultType == "compaction_percent" ? Fmt(r.CompactionPercent) : Fmt(r.CompactionCoeff);
                SetPhysicalCellText(table, row, 8, compaction);
            }
        }

        private static void SetRemarksRow(Table table, int remarksRow, string reportRemarks)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (remarksRow < 0 || remarksRow >= rows.Count) return;
            var cell = rows[remarksRow].Elements<TableCell>().FirstOrDefault();
            if (cell == null) return;

            var text = string.IsNullOrWhiteSpace(reportRemarks)
                ? ReportDefaults.DefaultReportRemarks
                : reportRemarks.Trim();

            cell.RemoveAllChildren<Paragraph>();
            foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
            {
                var para = new Paragraph();
                para.Append(new ParagraphProperties(new Indentation { Left = "0", FirstLine = "0" }));
                AppendStyledRuns(para, line, BodyFontSizeHalfPoints);
                cell.Append(para);
            }
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

        private static string Fmt(decimal? v) => v.HasValue ? v.Value.ToString("F2") : "";

        private static string FmtRaw(decimal? v) =>
            v.HasValue ? TrimDecimal(v.Value) : string.Empty;

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
            para.RemoveAllChildren<Run>();
            if (alignment.HasValue)
            {
                var pPr = para.ParagraphProperties ?? para.PrependChild(new ParagraphProperties());
                pPr.Justification = new Justification { Val = alignment.Value };
            }
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
