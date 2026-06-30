using RingKnifeDetector.Helpers;
using RingKnifeDetector.Models;

namespace RingKnifeDetector.Services
{
    /// <summary>
    /// Word 导出前的数据提醒（仅提示，不阻断导出）。
    /// </summary>
    public static class WordExportReminderService
    {
        public static IReadOnlyList<string> CollectReminders(
            ProjectInfo project,
            RecordParams parameters,
            IReadOnlyList<SamplePointResult> results,
            string overallConclusion)
        {
            var reminders = new List<string>();
            reminders.AddRange(CollectBlankFieldReminders(project, parameters, results, overallConclusion));
            reminders.AddRange(CollectDateOrderReminders(project, results));
            reminders.AddRange(CollectConclusionReminders(results, overallConclusion));
            reminders.AddRange(CollectCompactionReminders(results));
            reminders.AddRange(MoistureValidation.CollectBoxMoistureWarnings(results));
            return reminders;
        }

        private static IEnumerable<string> CollectBlankFieldReminders(
            ProjectInfo project,
            RecordParams parameters,
            IReadOnlyList<SamplePointResult> results,
            string overallConclusion)
        {
            var blanks = new List<string>();

            void Check(string label, string? value)
            {
                if (IsBlank(value))
                    blanks.Add(label);
            }

            void CheckDecimal(string label, decimal? value)
            {
                if (value == null)
                    blanks.Add(label);
            }

            Check("委托编号", project.EntrustNo);
            Check("报告编号", project.ReportNo);
            Check("委托单位", project.EntrustUnit);
            Check("联系方式", project.Contact);
            Check(TestNatureHelper.IsWitnessSampling(project.TestNature) ? "工程见证" : "监理单位", project.SupervisionUnit);
            Check(TestNatureHelper.IsWitnessSampling(project.TestNature) ? "样品取样" : "施工单位", project.ConstructionUnit);
            Check("工程名称", project.ProjectName);
            Check("单位地址", project.UnitAddress);
            Check("工程地址", project.ProjectAddress);
            Check("委托日期", project.EntrustDate);
            Check("工程部位", project.ProjectSection);
            Check("报告日期", project.ReportDate);
            Check("检测性质", project.TestNature);
            Check("样品名称", parameters.SampleName);
            Check("材料种类", parameters.MaterialType);
            Check(TestNatureHelper.IsWitnessSampling(project.TestNature) ? "规格型号" : "环刀规格", parameters.RingSpec);
            Check("夯实方式", parameters.CompactionMethod);
            if (IsBlank(parameters.DesignRequirementText) && parameters.DesignRequirement == null)
                blanks.Add("设计要求");
            CheckDecimal("最大干密度", parameters.MaxDryDensity);
            Check("检测地点", parameters.TestLocation);
            CheckDecimal("最优含水率", parameters.OptimalMoisture);
            Check("检测依据", parameters.TestBasis);
            Check("判定依据", parameters.JudgeBasis);
            Check("检测结论", overallConclusion);

            for (var i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var prefix = $"测点{i + 1}";
                Check($"{prefix}·样品编号", r.SampleNo);
                Check($"{prefix}·高程", r.Elevation);
                Check($"{prefix}·厚度", r.Thickness);
                Check($"{prefix}·取样日期", r.SamplingDate);
                Check($"{prefix}·检测日期", r.TestDate);
                if (r.AvgWetDensity == null && r.WetDensity == null)
                    blanks.Add($"{prefix}·湿密度");
                CheckDecimal($"{prefix}·平均含水率", r.AvgMoisture);
                if (r.AvgDryDensity == null && r.DryDensity == null)
                    blanks.Add($"{prefix}·干密度");
                if (parameters.ResultType == "compaction_percent")
                    CheckDecimal($"{prefix}·压实度", r.CompactionPercent);
                else
                    CheckDecimal($"{prefix}·压实系数", r.CompactionCoeff);
            }

            if (blanks.Count == 0)
                yield break;

            yield return $"存在空白字段：{string.Join("、", blanks)}";
        }

        private static IEnumerable<string> CollectDateOrderReminders(
            ProjectInfo project,
            IReadOnlyList<SamplePointResult> results)
        {
            var entrust = DateHelper.TryParse(project.EntrustDate);
            var report = DateHelper.TryParse(project.ReportDate);

            if (entrust.HasValue && report.HasValue && entrust > report)
                yield return "委托日期晚于报告日期";

            for (var i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var prefix = string.IsNullOrWhiteSpace(r.SampleNo) ? $"测点{i + 1}" : r.SampleNo;
                var sampling = DateHelper.TryParse(r.SamplingDate);
                var (testStartText, testEndText) = DateHelper.ParseRange(r.TestDate);
                var testStart = DateHelper.TryParse(testStartText);
                var testEnd = DateHelper.TryParse(testEndText);

                if (entrust.HasValue && sampling.HasValue && entrust > sampling)
                    yield return $"{prefix}：委托日期晚于取样日期";

                if (sampling.HasValue && testStart.HasValue && sampling > testStart)
                    yield return $"{prefix}：取样日期晚于检测日期";

                if (testStart.HasValue && testEnd.HasValue && testStart > testEnd)
                    yield return $"{prefix}：检测日期范围起止颠倒";

                if (testEnd.HasValue && report.HasValue && testEnd > report)
                    yield return $"{prefix}：检测日期晚于报告日期";

                if (entrust.HasValue && testStart.HasValue && entrust > testStart)
                    yield return $"{prefix}：委托日期晚于检测日期";
            }
        }

        private static IEnumerable<string> CollectConclusionReminders(
            IReadOnlyList<SamplePointResult> results,
            string overallConclusion)
        {
            if (!string.IsNullOrWhiteSpace(overallConclusion)
                && overallConclusion.Contains("不符合", StringComparison.Ordinal))
            {
                yield return $"检测结论不合格：{overallConclusion.Trim()}";
            }

            foreach (var r in results)
            {
                if (!string.IsNullOrWhiteSpace(r.Conclusion)
                    && r.Conclusion.Contains("不符合", StringComparison.Ordinal))
                {
                    var label = string.IsNullOrWhiteSpace(r.SampleNo) ? "某测点" : r.SampleNo;
                    yield return $"{label} 单项结论不合格：{r.Conclusion}";
                }
            }
        }

        private static IEnumerable<string> CollectCompactionReminders(IReadOnlyList<SamplePointResult> results)
        {
            for (var i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var label = string.IsNullOrWhiteSpace(r.SampleNo) ? $"测点{i + 1}" : r.SampleNo;

                if (r.CompactionPercent is >= 100)
                    yield return $"{label}：压实度 {Fmt(r.CompactionPercent)}% ≥ 100%";

                if (r.CompactionCoeff is >= 1)
                    yield return $"{label}：压实系数 {Fmt(r.CompactionCoeff)} ≥ 1";
            }
        }

        private static bool IsBlank(string? value) =>
            string.IsNullOrWhiteSpace(value)
            || string.Equals(value.Trim(), "/", StringComparison.Ordinal)
            || RemarkParser.IsMissingValue(value);

        private static string Fmt(decimal? value) =>
            value?.ToString("0.##") ?? string.Empty;
    }
}
