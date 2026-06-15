using ClosedXML.Excel;
using RingKnifeDetector.Models;

namespace RingKnifeDetector.Services
{
    /// <summary>
    /// Excel导出服务
    /// </summary>
    public class ExcelExportService
    {
        /// <summary>
        /// 导出环刀法压实度检测记录到Excel
        /// </summary>
        /// <param name="project">工程信息</param>
        /// <param name="params">检测参数</param>
        /// <param name="results">计算结果</param>
        /// <param name="overallConclusion">总体结论</param>
        /// <param name="filePath">输出文件路径</param>
        public void ExportToExcel(
            ProjectInfo project,
            RecordParams @params,
            List<SamplePointResult> results,
            string overallConclusion,
            string filePath)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("环刀法压实度检测记录");

            // 设置表头样式
            var headerStyle = worksheet.Workbook.Style;
            headerStyle.Font.Bold = true;
            headerStyle.Font.FontSize = 11;
            headerStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerStyle.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerStyle.Fill.BackgroundColor = XLColor.LightGray;

            // 工程信息区域
            var row = 1;
            worksheet.Cell(row, 1).Value = "委托编号";
            worksheet.Cell(row, 2).Value = project.EntrustNo;
            worksheet.Cell(row, 3).Value = "报告编号";
            worksheet.Cell(row, 4).Value = project.ReportNo;

            row = 2;
            worksheet.Cell(row, 1).Value = "委托单位";
            worksheet.Cell(row, 2).Value = project.EntrustUnit;
            worksheet.Cell(row, 3).Value = "联系人";
            worksheet.Cell(row, 4).Value = project.Contact;

            row = 3;
            worksheet.Cell(row, 1).Value = "工程名称";
            worksheet.Cell(row, 2).Value = project.ProjectName;
            worksheet.Cell(row, 3).Value = "工程地址";
            worksheet.Cell(row, 4).Value = project.ProjectAddress;

            row = 4;
            worksheet.Cell(row, 1).Value = "监理单位";
            worksheet.Cell(row, 2).Value = project.SupervisionUnit;
            worksheet.Cell(row, 3).Value = "施工单位";
            worksheet.Cell(row, 4).Value = project.ConstructionUnit;

            row = 5;
            worksheet.Cell(row, 1).Value = "委托日期";
            worksheet.Cell(row, 2).Value = project.EntrustDate;
            worksheet.Cell(row, 3).Value = "报告日期";
            worksheet.Cell(row, 4).Value = project.ReportDate;

            row = 6;
            worksheet.Cell(row, 1).Value = "工程部位";
            worksheet.Cell(row, 2).Value = project.ProjectSection;
            worksheet.Cell(row, 3).Value = "检测性质";
            worksheet.Cell(row, 4).Value = project.TestNature;

            // 检测参数区域
            row = 8;
            worksheet.Cell(row, 1).Value = "土类型";
            worksheet.Cell(row, 2).Value = @params.SoilType;
            worksheet.Cell(row, 3).Value = "最大干密度(g/cm³)";
            worksheet.Cell(row, 4).Value = @params.MaxDryDensity?.ToString() ?? string.Empty;

            row = 9;
            worksheet.Cell(row, 1).Value = "压实方法";
            worksheet.Cell(row, 2).Value = @params.CompactionMethod;
            worksheet.Cell(row, 3).Value = "最优含水率(%)";
            worksheet.Cell(row, 4).Value = @params.OptimalMoisture?.ToString() ?? string.Empty;

            row = 10;
            worksheet.Cell(row, 1).Value = "环刀规格";
            worksheet.Cell(row, 2).Value = @params.RingSpec;
            worksheet.Cell(row, 3).Value = "设计要求";
            worksheet.Cell(row, 4).Value = @params.DesignRequirement?.ToString() ?? string.Empty;

            row = 11;
            worksheet.Cell(row, 1).Value = "样品名称";
            worksheet.Cell(row, 2).Value = @params.SampleName;
            worksheet.Cell(row, 3).Value = "检测依据";
            worksheet.Cell(row, 4).Value = @params.TestBasis;

            row = 12;
            worksheet.Cell(row, 1).Value = "结果类型";
            worksheet.Cell(row, 2).Value = @params.ResultType == "compaction_percent" ? "压实度" : "压实系数";
            worksheet.Cell(row, 3).Value = "判定依据";
            worksheet.Cell(row, 4).Value = @params.JudgeBasis;

            // 数据表头
            row = 14;
            var headers = new[]
            {
                "样品编号", "高程", "厚度", "取样日期", "检测日期",
                "湿密度(g/cm³)", "平均湿密度(g/cm³)", "含水率(%)", "平均含水率(%)",
                "干密度(g/cm³)", "平均干密度(g/cm³)", "压实系数", "压实度(%)", "结论"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(row, i + 1).Value = headers[i];
                worksheet.Cell(row, i + 1).Style = headerStyle;
            }

            // 数据行
            row = 15;
            foreach (var result in results)
            {
                worksheet.Cell(row, 1).Value = result.SampleNo;
                worksheet.Cell(row, 2).Value = result.Elevation;
                worksheet.Cell(row, 3).Value = result.Thickness;
                worksheet.Cell(row, 4).Value = result.SamplingDate;
                worksheet.Cell(row, 5).Value = result.TestDate;

                worksheet.Cell(row, 6).Value = result.WetDensity?.ToString() ?? string.Empty;
                worksheet.Cell(row, 7).Value = result.AvgWetDensity?.ToString() ?? string.Empty;

                // 含水率（显示第一个）
                worksheet.Cell(row, 8).Value = result.MoistureRates.FirstOrDefault()?.ToString() ?? string.Empty;
                worksheet.Cell(row, 9).Value = result.AvgMoisture?.ToString() ?? string.Empty;

                worksheet.Cell(row, 10).Value = result.DryDensity?.ToString() ?? string.Empty;
                worksheet.Cell(row, 11).Value = result.AvgDryDensity?.ToString() ?? string.Empty;

                worksheet.Cell(row, 12).Value = result.CompactionCoeff?.ToString() ?? string.Empty;
                worksheet.Cell(row, 13).Value = result.CompactionPercent?.ToString() ?? string.Empty;
                worksheet.Cell(row, 14).Value = result.Conclusion;

                row++;
            }

            // 总体结论
            row += 1;
            worksheet.Cell(row, 1).Value = "总体结论";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = overallConclusion;
            worksheet.Range(row, 2, row, 14).Merge();

            // 设置列宽
            worksheet.Columns().AdjustToContents();

            // 设置边框
            var usedRange = worksheet.RangeUsed();
            if (usedRange != null)
            {
                usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            // 保存文件
            workbook.SaveAs(filePath);
        }

        /// <summary>
        /// 导出环刀法压实度检测记录到Excel（简化版本）
        /// </summary>
        /// <param name="params">检测参数</param>
        /// <param name="results">计算结果</param>
        /// <param name="overallConclusion">总体结论</param>
        /// <param name="filePath">输出文件路径</param>
        public void ExportToExcel(
            RecordParams @params,
            List<SamplePointResult> results,
            string overallConclusion,
            string filePath)
        {
            var project = new ProjectInfo();
            ExportToExcel(project, @params, results, overallConclusion, filePath);
        }

        /// <summary>
        /// 导出原始记录数据到Excel
        /// </summary>
        /// <param name="samples">样品数据</param>
        /// <param name="filePath">输出文件路径</param>
        public void ExportRawDataToExcel(List<RingKnifeSample> samples, string filePath)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("原始记录数据");

            // 设置表头样式
            var headerStyle = worksheet.Workbook.Style;
            headerStyle.Font.Bold = true;
            headerStyle.Font.FontSize = 11;
            headerStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerStyle.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerStyle.Fill.BackgroundColor = XLColor.LightGray;

            // 表头
            var headers = new[]
            {
                "样品编号", "高程", "取样日期", "检测日期", "厚度",
                "环刀加湿土质量(g)", "环刀质量(g)", "环刀体积(cm³)",
                "铝盒1编号", "铝盒1质量(g)", "铝盒1湿样质量(g)", "铝盒1干样质量(g)",
                "铝盒2编号", "铝盒2质量(g)", "铝盒2湿样质量(g)", "铝盒2干样质量(g)"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
                worksheet.Cell(1, i + 1).Style = headerStyle;
            }

            // 数据行
            int row = 2;
            foreach (var sample in samples)
            {
                worksheet.Cell(row, 1).Value = sample.SampleNo;
                worksheet.Cell(row, 2).Value = sample.Elevation;
                worksheet.Cell(row, 3).Value = sample.SamplingDate;
                worksheet.Cell(row, 4).Value = sample.TestDate;
                worksheet.Cell(row, 5).Value = sample.Thickness;

                worksheet.Cell(row, 6).Value = sample.RingSampleMass?.ToString() ?? string.Empty;
                worksheet.Cell(row, 7).Value = sample.RingMass?.ToString() ?? string.Empty;
                worksheet.Cell(row, 8).Value = sample.RingVolume?.ToString() ?? string.Empty;

                // 铝盒数据
                if (sample.Boxes.Count >= 1)
                {
                    worksheet.Cell(row, 9).Value = sample.Boxes[0].BoxNo;
                    worksheet.Cell(row, 10).Value = sample.Boxes[0].BoxMass?.ToString() ?? string.Empty;
                    worksheet.Cell(row, 11).Value = sample.Boxes[0].WetSampleMass?.ToString() ?? string.Empty;
                    worksheet.Cell(row, 12).Value = sample.Boxes[0].DrySampleMass?.ToString() ?? string.Empty;
                }

                if (sample.Boxes.Count >= 2)
                {
                    worksheet.Cell(row, 13).Value = sample.Boxes[1].BoxNo;
                    worksheet.Cell(row, 14).Value = sample.Boxes[1].BoxMass?.ToString() ?? string.Empty;
                    worksheet.Cell(row, 15).Value = sample.Boxes[1].WetSampleMass?.ToString() ?? string.Empty;
                    worksheet.Cell(row, 16).Value = sample.Boxes[1].DrySampleMass?.ToString() ?? string.Empty;
                }

                row++;
            }

            // 设置列宽
            worksheet.Columns().AdjustToContents();

            // 设置边框
            var usedRange = worksheet.RangeUsed();
            if (usedRange != null)
            {
                usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            // 保存文件
            workbook.SaveAs(filePath);
        }

        /// <summary>
        /// 导出计算结果到Excel（包含所有环刀数据）
        /// </summary>
        /// <param name="project">工程信息</param>
        /// <param name="params">检测参数</param>
        /// <param name="results">计算结果</param>
        /// <param name="overallConclusion">总体结论</param>
        /// <param name="filePath">输出文件路径</param>
        public void ExportDetailedToExcel(
            ProjectInfo project,
            RecordParams @params,
            List<SamplePointResult> results,
            string overallConclusion,
            string filePath)
        {
            using var workbook = new XLWorkbook();

            // 第一个工作表：汇总数据
            var summarySheet = workbook.Worksheets.Add("汇总数据");
            
            // 设置表头样式
            var headerStyle = summarySheet.Workbook.Style;
            headerStyle.Font.Bold = true;
            headerStyle.Font.FontSize = 11;
            headerStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerStyle.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerStyle.Fill.BackgroundColor = XLColor.LightGray;

            // 工程信息区域
            var row = 1;
            summarySheet.Cell(row, 1).Value = "委托编号";
            summarySheet.Cell(row, 2).Value = project.EntrustNo;
            summarySheet.Cell(row, 3).Value = "报告编号";
            summarySheet.Cell(row, 4).Value = project.ReportNo;

            row = 2;
            summarySheet.Cell(row, 1).Value = "委托单位";
            summarySheet.Cell(row, 2).Value = project.EntrustUnit;
            summarySheet.Cell(row, 3).Value = "联系人";
            summarySheet.Cell(row, 4).Value = project.Contact;

            row = 3;
            summarySheet.Cell(row, 1).Value = "工程名称";
            summarySheet.Cell(row, 2).Value = project.ProjectName;
            summarySheet.Cell(row, 3).Value = "工程地址";
            summarySheet.Cell(row, 4).Value = project.ProjectAddress;

            row = 4;
            summarySheet.Cell(row, 1).Value = "监理单位";
            summarySheet.Cell(row, 2).Value = project.SupervisionUnit;
            summarySheet.Cell(row, 3).Value = "施工单位";
            summarySheet.Cell(row, 4).Value = project.ConstructionUnit;

            row = 5;
            summarySheet.Cell(row, 1).Value = "委托日期";
            summarySheet.Cell(row, 2).Value = project.EntrustDate;
            summarySheet.Cell(row, 3).Value = "报告日期";
            summarySheet.Cell(row, 4).Value = project.ReportDate;

            row = 6;
            summarySheet.Cell(row, 1).Value = "工程部位";
            summarySheet.Cell(row, 2).Value = project.ProjectSection;
            summarySheet.Cell(row, 3).Value = "检测性质";
            summarySheet.Cell(row, 4).Value = project.TestNature;

            // 检测参数区域
            row = 8;
            summarySheet.Cell(row, 1).Value = "土类型";
            summarySheet.Cell(row, 2).Value = @params.SoilType;
            summarySheet.Cell(row, 3).Value = "最大干密度(g/cm³)";
            summarySheet.Cell(row, 4).Value = @params.MaxDryDensity?.ToString() ?? string.Empty;

            row = 9;
            summarySheet.Cell(row, 1).Value = "压实方法";
            summarySheet.Cell(row, 2).Value = @params.CompactionMethod;
            summarySheet.Cell(row, 3).Value = "最优含水率(%)";
            summarySheet.Cell(row, 4).Value = @params.OptimalMoisture?.ToString() ?? string.Empty;

            row = 10;
            summarySheet.Cell(row, 1).Value = "环刀规格";
            summarySheet.Cell(row, 2).Value = @params.RingSpec;
            summarySheet.Cell(row, 3).Value = "设计要求";
            summarySheet.Cell(row, 4).Value = @params.DesignRequirement?.ToString() ?? string.Empty;

            row = 11;
            summarySheet.Cell(row, 1).Value = "样品名称";
            summarySheet.Cell(row, 2).Value = @params.SampleName;
            summarySheet.Cell(row, 3).Value = "检测依据";
            summarySheet.Cell(row, 4).Value = @params.TestBasis;

            row = 12;
            summarySheet.Cell(row, 1).Value = "结果类型";
            summarySheet.Cell(row, 2).Value = @params.ResultType == "compaction_percent" ? "压实度" : "压实系数";
            summarySheet.Cell(row, 3).Value = "判定依据";
            summarySheet.Cell(row, 4).Value = @params.JudgeBasis;

            // 数据表头
            row = 14;
            var headers = new[]
            {
                "样品编号", "高程", "厚度", "取样日期", "检测日期",
                "湿密度(g/cm³)", "平均湿密度(g/cm³)", "含水率(%)", "平均含水率(%)",
                "干密度(g/cm³)", "平均干密度(g/cm³)", "压实系数", "压实度(%)", "结论"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                summarySheet.Cell(row, i + 1).Value = headers[i];
                summarySheet.Cell(row, i + 1).Style = headerStyle;
            }

            // 数据行
            row = 15;
            foreach (var result in results)
            {
                summarySheet.Cell(row, 1).Value = result.SampleNo;
                summarySheet.Cell(row, 2).Value = result.Elevation;
                summarySheet.Cell(row, 3).Value = result.Thickness;
                summarySheet.Cell(row, 4).Value = result.SamplingDate;
                summarySheet.Cell(row, 5).Value = result.TestDate;

                summarySheet.Cell(row, 6).Value = result.WetDensity?.ToString() ?? string.Empty;
                summarySheet.Cell(row, 7).Value = result.AvgWetDensity?.ToString() ?? string.Empty;

                // 含水率（显示第一个）
                summarySheet.Cell(row, 8).Value = result.MoistureRates.FirstOrDefault()?.ToString() ?? string.Empty;
                summarySheet.Cell(row, 9).Value = result.AvgMoisture?.ToString() ?? string.Empty;

                summarySheet.Cell(row, 10).Value = result.DryDensity?.ToString() ?? string.Empty;
                summarySheet.Cell(row, 11).Value = result.AvgDryDensity?.ToString() ?? string.Empty;

                summarySheet.Cell(row, 12).Value = result.CompactionCoeff?.ToString() ?? string.Empty;
                summarySheet.Cell(row, 13).Value = result.CompactionPercent?.ToString() ?? string.Empty;
                summarySheet.Cell(row, 14).Value = result.Conclusion;

                row++;
            }

            // 总体结论
            row += 1;
            summarySheet.Cell(row, 1).Value = "总体结论";
            summarySheet.Cell(row, 1).Style.Font.Bold = true;
            summarySheet.Cell(row, 2).Value = overallConclusion;
            summarySheet.Range(row, 2, row, 14).Merge();

            // 第二个工作表：详细数据
            var detailSheet = workbook.Worksheets.Add("详细数据");

            // 详细数据表头
            var detailHeaders = new[]
            {
                "样品编号", "环刀编号", "湿土质量(g)", "湿密度(g/cm³)",
                "铝盒1编号", "铝盒1含水率(%)", "铝盒2编号", "铝盒2含水率(%)",
                "平均含水率(%)", "干密度(g/cm³)"
            };

            for (int i = 0; i < detailHeaders.Length; i++)
            {
                detailSheet.Cell(1, i + 1).Value = detailHeaders[i];
                detailSheet.Cell(1, i + 1).Style = headerStyle;
            }

            // 详细数据行
            row = 2;
            foreach (var result in results)
            {
                for (int i = 0; i < result.Rings.Count; i++)
                {
                    var ring = result.Rings[i];
                    detailSheet.Cell(row, 1).Value = result.SampleNo;
                    detailSheet.Cell(row, 2).Value = i + 1;
                    detailSheet.Cell(row, 3).Value = ring.WetMass?.ToString() ?? string.Empty;
                    detailSheet.Cell(row, 4).Value = ring.WetDensity?.ToString() ?? string.Empty;

                    // 含水率数据
                    if (ring.MoistureRates.Count >= 1)
                    {
                        detailSheet.Cell(row, 5).Value = ring.MoistureRates[0]?.ToString() ?? string.Empty;
                        detailSheet.Cell(row, 6).Value = ring.MoistureRates[0]?.ToString() ?? string.Empty;
                    }
                    if (ring.MoistureRates.Count >= 2)
                    {
                        detailSheet.Cell(row, 7).Value = ring.MoistureRates[1]?.ToString() ?? string.Empty;
                        detailSheet.Cell(row, 8).Value = ring.MoistureRates[1]?.ToString() ?? string.Empty;
                    }

                    detailSheet.Cell(row, 9).Value = ring.AvgMoisture?.ToString() ?? string.Empty;
                    detailSheet.Cell(row, 10).Value = ring.DryDensity?.ToString() ?? string.Empty;

                    row++;
                }
            }

            // 设置列宽
            summarySheet.Columns().AdjustToContents();
            detailSheet.Columns().AdjustToContents();

            // 设置边框
            var usedRange = summarySheet.RangeUsed();
            if (usedRange != null)
            {
                usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            usedRange = detailSheet.RangeUsed();
            if (usedRange != null)
            {
                usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            // 保存文件
            workbook.SaveAs(filePath);
        }
    }
}