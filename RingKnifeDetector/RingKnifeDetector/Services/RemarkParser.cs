using System.Text.RegularExpressions;
using RingKnifeDetector.Helpers;
using RingKnifeDetector.Models;

namespace RingKnifeDetector.Services
{
    /// <summary>
    /// 从 LIMIS 原始记录备注中补全空字段
    /// </summary>
    public static class RemarkParser
    {
        private const string CompareSymbol = @"[≥＞>＝=]+";
        private const string PercentSuffix = @"[%％]";
        private const string OptionalPercentSuffix = @"[%％]?";
        private const string MoistureLabel = @"(?:最佳含水率|最优含水率|最佳含水量|最优含水量)";
        private const string DryDensityUnit = @"g\s*/\s*cm\s*[³3³]?";
        private const string UnitFieldBoundary = @"(?=\s*[;；]?\s*(?:监理单位|见证单位|建设单位|设计单位|施工单位)|\n|$)";
        private const string ChineseMaterialRun = @"[\p{IsCJKUnifiedIdeographs}（）()]+";
        private const string FollowingFieldLabels =
            "材料种类|材料种美|品种|取样时间|取样标高|厚度|最大干密度|毛体积密度|最佳含水率|最优含水率|设计要求|夯实方式|监理单位|见证单位|施工单位|委托组数|检测组数";
        private const string LocationFieldBoundary =
            $@"(?=\s*(?:{FollowingFieldLabels})(?:\s*[:：]|\s|$)|\n|标高|高程|取[样祥]层厚度|填筑厚度|取样点厚度|委托组数|检测组数|最佳|最优|设计|$)";
        private const string GluedAfterDigitLabels =
            "最佳含水率|最优含水率|最佳含水量|最优含水量|材料种类|材料种美|品种|设计要求|取样部位|取样层厚度|取祥层厚度|填筑厚度|取样时间|取样标高|最大干密度|毛体积密度|压实度|压实系数|委托组数|检测组数|固体体积率";
        private const string GluedAfterCjkLabels =
            "材料种类|材料种美|品种|设计要求|取样部位|最佳含水率|最优含水率|最大干密度";
        private const string LabelSep = @"\s*[:：]?\s*";

        public static RemarkParseResult FillMissing(
            ProjectInfo project,
            RecordParams parameters,
            IList<RingKnifeSample> samples,
            string? remark)
        {
            var result = new RemarkParseResult();
            if (string.IsNullOrWhiteSpace(remark)) return result;

            var text = NormalizeRemarkText(remark);
            var ctx = new ParseContext(text, result);

            if (IsMissingValue(project.SupervisionUnit))
            {
                var v = MatchField(ctx, $@"监理单位{LabelSep}(.+?){UnitFieldBoundary}", "project.supervisionUnit", "监理单位")
                        ?? MatchField(ctx, $@"见证单位{LabelSep}(.+?){UnitFieldBoundary}", "project.supervisionUnit", "监理单位");
                if (!string.IsNullOrEmpty(v)) project.SupervisionUnit = v;
            }

            if (IsMissingValue(project.ConstructionUnit))
            {
                var v = MatchField(ctx, $@"施工单位{LabelSep}(.+?){UnitFieldBoundary}", "project.constructionUnit", "施工单位");
                if (!string.IsNullOrEmpty(v)) project.ConstructionUnit = v;
            }

            if (IsMissingValue(project.ProjectSection))
            {
                var v = MatchField(ctx, $@"工程部位{LabelSep}(.+?)(?=\n|[;；]|取样点|取样部位|桩号|$)", "project.projectSection", "工程部位")
                        ?? MatchField(ctx, $@"桩号{LabelSep}(.+?)(?=\n|最大干密度|点桩号|检测点桩号|$)", "project.projectSection", "桩号");
                if (!string.IsNullOrEmpty(v)) project.ProjectSection = v;
            }

            if (IsMissingValue(parameters.MaterialType))
            {
                var v = TryMaterialLabel(ctx)
                        ?? TryMaterialFromRemarkSegments(ctx)
                        ?? TryInlineMaterialAfterDryDensity(ctx)
                        ?? TryFirstLineMaterial(ctx)
                        ?? TryLastStandaloneLine(ctx, "params.materialType", "材料种类");
                if (!string.IsNullOrEmpty(v)) parameters.MaterialType = v;
            }

            if (IsMissingValue(parameters.TestLocation))
            {
                var v = MatchField(ctx, $@"取样部位{LabelSep}(.+?){LocationFieldBoundary}", "params.testLocation", "取样部位")
                        ?? MatchField(ctx, $@"检测点桩号{LabelSep}(.+?)(?=\n|最佳|最大|设计|最优|$)", "params.testLocation", "检测点桩号")
                        ?? MatchField(ctx, $@"取样点(?!部){LabelSep}(.+?){LocationFieldBoundary}", "params.testLocation", "取样点")
                        ?? MatchField(ctx, $@"取样点(?!部)\s+(.+?){LocationFieldBoundary}", "params.testLocation", "取样点")
                        ?? MatchField(ctx, $@"点桩号{LabelSep}(.+?)(?=\n|设计要求|最佳|最大|$)", "params.testLocation", "点桩号");
                if (!string.IsNullOrEmpty(v)) parameters.TestLocation = v;
            }

            if (!parameters.MaxDryDensity.HasValue)
                TryExtractDryDensity(text, ctx, parameters);

            if (!parameters.OptimalMoisture.HasValue)
            {
                var m = Regex.Match(
                    text,
                    $@"{MoistureLabel}\s*[:：]?\s*(\d+(?:\.\d+)?)\s*{OptionalPercentSuffix}",
                    RegexOptions.IgnoreCase);
                if (m.Success && decimal.TryParse(m.Groups[1].Value, out var v))
                {
                    parameters.OptimalMoisture = v;
                    ctx.RecordFromGroupToMatchEnd(m, 1, "params.optimalMoisture", "最优含水率");
                }
            }

            if (ShouldExtractDesign(parameters))
                TryExtractDesignRequirement(ctx, parameters);

            if (samples.Count > 0)
            {
                var sample = samples[0];
                if (string.IsNullOrWhiteSpace(sample.Elevation))
                {
                    var m = Regex.Match(text, @"(?:标高|高程|取样标高)\s*[:：]?\s*([-\d]+(?:\.\d+)?)\s*(?:mm|m)?", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        sample.Elevation = m.Groups[1].Value;
                        ctx.RecordFromGroupToMatchEnd(m, 1, "sample.elevation", "标高/高程");
                    }
                }

                if (string.IsNullOrWhiteSpace(sample.Thickness))
                {
                    var m = Regex.Match(
                        text,
                        @"(?:取样点厚度|取样层厚度|取祥层厚度|填筑厚度|厚度)\s*[:：]?\s*(\d+)\s*(mm|cm|m)?",
                        RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        sample.Thickness = ConvertThicknessToMm(m.Groups[1].Value, m.Groups[2].Value);
                        ctx.RecordFromGroupToMatchEnd(m, 1, "sample.thickness", "厚度");
                    }
                }

                TryExtractSamplingDate(text, ctx, samples);
            }

            TextSanitizer.SanitizeProject(project);
            TextSanitizer.SanitizeParams(parameters);
            ApplyUnfilledRemarkFieldDefaults(result, parameters);
            return result;
        }

        private static void ApplyUnfilledRemarkFieldDefaults(RemarkParseResult result, RecordParams parameters)
        {
            if (!result.ExtractedFieldKeys.Contains("params.compactionMethod")
                && string.IsNullOrWhiteSpace(parameters.CompactionMethod))
            {
                parameters.CompactionMethod = ReportDefaults.MissingFieldPlaceholder;
            }

            if (!result.ExtractedFieldKeys.Contains("params.judgeBasis")
                && (string.IsNullOrWhiteSpace(parameters.JudgeBasis)
                    || parameters.JudgeBasis == "JTG 3450-2019"
                    || parameters.JudgeBasis == ReportDefaults.MissingFieldPlaceholder))
            {
                parameters.JudgeBasis = ReportDefaults.DefaultJudgeBasis;
            }
        }

        private static void TryExtractSamplingDate(string text, ParseContext ctx, IList<RingKnifeSample> samples)
        {
            var m = Regex.Match(
                text,
                @"取样时间\s*[:：]?\s*(\d{4}[-/.年]\d{1,2}[-/.月]\d{1,2}日?)",
                RegexOptions.IgnoreCase);
            if (!m.Success) return;

            var normalized = DateHelper.Normalize(m.Groups[1].Value);
            if (string.IsNullOrEmpty(normalized)) return;

            foreach (var sample in samples)
                sample.SamplingDate = normalized;

            ctx.RecordFromGroupToMatchEnd(m, 1, "sample.samplingDate", "取样时间");
        }

        public static RemarkParseResult AnalyzeHighlights(string? remark)
        {
            var project = new ProjectInfo();
            var parameters = new RecordParams();
            var samples = new List<RingKnifeSample> { new() };
            return FillMissing(project, parameters, samples, remark);
        }

        public static bool IsMissingValue(string? value) =>
            string.IsNullOrWhiteSpace(value) || value.Contains("备注", StringComparison.Ordinal);

        private static bool ShouldExtractDesign(RecordParams parameters) =>
            IsMissingValue(parameters.DesignRequirementText)
            || !parameters.DesignRequirement.HasValue
            || (parameters.ResultType == "compaction_percent"
                && !parameters.DesignRequirementText.Contains('%'));

        private static void TryExtractDesignRequirement(ParseContext ctx, RecordParams parameters)
        {
            var percentPatterns = new[]
            {
                $@"设计要求{LabelSep}压实度\s*[（(]环刀[）)]\s*(?:{CompareSymbol})?\s*(\d+(?:\.\d+)?)\s*{OptionalPercentSuffix}",
                $@"设计要求{LabelSep}压实度\s*[（(][^）)]*[）)]\s*(?:{CompareSymbol})?\s*(\d+(?:\.\d+)?)\s*{OptionalPercentSuffix}",
                $@"设计要求{LabelSep}压实度\s*({CompareSymbol}\s*\d+(?:\.\d+)?{OptionalPercentSuffix})",
                $@"压实度\s*({CompareSymbol}\s*\d+(?:\.\d+)?{OptionalPercentSuffix})",
            };

            foreach (var pattern in percentPatterns)
            {
                var m = Regex.Match(ctx.Text, pattern, RegexOptions.IgnoreCase);
                if (!m.Success) continue;

                if (!TryParseCompactionPercentToken(m.Groups[1].Value, m.Value, out var percentValue, out var displayToken))
                    continue;

                parameters.DesignRequirement = percentValue;
                parameters.ResultType = "compaction_percent";
                parameters.DesignRequirementText = displayToken;
                ctx.RecordGroup(m.Groups[1], "params.designRequirement", "设计要求/压实度");
                return;
            }

            var coeffMatch = Regex.Match(
                ctx.Text,
                $@"(?:设计要求{LabelSep})?压实系数\s*({CompareSymbol}\s*\d+(?:\.\d+)?)",
                RegexOptions.IgnoreCase);
            if (!coeffMatch.Success)
            {
                TryExtractSolidVolumeRate(ctx, parameters);
                return;
            }

            var coeffNum = Regex.Match(coeffMatch.Groups[1].Value, @"\d+(?:\.\d+)?");
            if (!coeffNum.Success || !decimal.TryParse(coeffNum.Value, out var coeffValue))
            {
                TryExtractSolidVolumeRate(ctx, parameters);
                return;
            }

            parameters.DesignRequirement = coeffValue;
            parameters.ResultType = "compaction_coeff";
            parameters.DesignRequirementText = coeffMatch.Groups[1].Value.Trim();
            ctx.RecordGroup(coeffMatch.Groups[1], "params.designRequirement", "设计要求/压实系数");
        }

        private static void TryExtractDryDensity(string text, ParseContext ctx, RecordParams parameters)
        {
            var patterns = new (string pattern, string label)[]
            {
                ($@"最大干密度\s*[:：]?\s*(\d+(?:\.\d+)?)\s*{DryDensityUnit}", "最大干密度"),
                ($@"毛体积密度\s*[:：]?\s*(\d+(?:\.\d+)?)\s*{DryDensityUnit}", "毛体积密度"),
            };

            foreach (var (pattern, label) in patterns)
            {
                var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (!m.Success || !decimal.TryParse(m.Groups[1].Value, out var value))
                    continue;

                parameters.MaxDryDensity = value;
                ctx.RecordFromGroupToMatchEnd(m, 1, "params.maxDryDensity", label);
                return;
            }
        }

        private static void TryExtractSolidVolumeRate(ParseContext ctx, RecordParams parameters)
        {
            var directPatterns = new[]
            {
                $@"设计要求{LabelSep}固体体积率\s*({CompareSymbol}\s*\d+(?:\.\d+)?)",
                $@"固体体积率\s*({CompareSymbol}\s*\d+(?:\.\d+)?)",
            };

            foreach (var pattern in directPatterns)
            {
                var m = Regex.Match(ctx.Text, pattern, RegexOptions.IgnoreCase);
                if (!m.Success || !TryParseDesignToken(m.Groups[1].Value, out var value, out var display))
                    continue;

                ApplySolidVolumeRate(ctx, parameters, value, display, m.Groups[1]);
                return;
            }

            var wordPatterns = new[]
            {
                $@"设计要求{LabelSep}固体体积率\s*(?:不小于|不低于|不少于|大于等于)\s*(\d+(?:\.\d+)?)",
                @"固体体积率\s*(?:不小于|不低于|不少于|大于等于)\s*(\d+(?:\.\d+)?)",
                $@"设计要求{LabelSep}.+?(?:不小于|不低于|不少于|大于等于)\s*(\d+(?:\.\d+)?)",
            };

            foreach (var pattern in wordPatterns)
            {
                var m = Regex.Match(ctx.Text, pattern, RegexOptions.IgnoreCase);
                if (!m.Success || !decimal.TryParse(m.Groups[1].Value, out var value))
                    continue;

                var display = $"≥{m.Groups[1].Value}";
                ApplySolidVolumeRate(ctx, parameters, value, display, m.Groups[1]);
                return;
            }
        }

        private static void ApplySolidVolumeRate(
            ParseContext ctx,
            RecordParams parameters,
            decimal value,
            string display,
            Group highlightGroup)
        {
            parameters.DesignRequirement = value;
            parameters.ResultType = "compaction_coeff";
            parameters.DesignRequirementText = display;
            ctx.RecordGroup(highlightGroup, "params.designRequirement", "设计要求/固体体积率");
        }

        private static bool TryParseDesignToken(string token, out decimal value, out string display)
        {
            value = 0;
            display = token.Trim().Replace(" ", string.Empty);
            var numMatch = Regex.Match(display, @"\d+(?:\.\d+)?");
            if (!numMatch.Success || !decimal.TryParse(numMatch.Value, out value))
                return false;

            if (!display.Contains('≥') && !display.Contains('>'))
                display = $"≥{numMatch.Value}";
            return true;
        }

        private static string ConvertThicknessToMm(string number, string unit)
        {
            if (!int.TryParse(number, out var value))
                return number;

            return unit.ToLowerInvariant() switch
            {
                "cm" => (value * 10).ToString(),
                "m" => (value * 1000).ToString(),
                _ => value.ToString(),
            };
        }

        private static bool TryParseCompactionPercentToken(string token, string fullMatch, out decimal value, out string display)
        {
            value = 0;
            display = string.Empty;
            var numMatch = Regex.Match(token, @"\d+(?:\.\d+)?");
            if (!numMatch.Success || !decimal.TryParse(numMatch.Value, out value))
                return false;

            if (value is > 100 and < 1000)
                value = value % 100;

            var hasPercent = token.Contains('%') || fullMatch.Contains('%');
            display = hasPercent ? $"≥{TrimDecimal(value)}%" : $"≥{TrimDecimal(value)}";
            return true;
        }

        private static string TrimDecimal(decimal value) =>
            value.ToString(value % 1 == 0 ? "0" : "0.####################");

        private static string BuildDesignRequirementText(string number, string percentGroup, bool isPercent)
        {
            var hasPercent = !string.IsNullOrEmpty(percentGroup) && percentGroup.Contains('%');
            if (isPercent && hasPercent)
                return $"≥{number}%";
            if (isPercent)
                return $"≥{number}";
            return $"≥{number}";
        }

        private static string? TryMaterialLabel(ParseContext ctx)
        {
            var m = Regex.Match(
                ctx.Text,
                $@"(?:品种|材料种类|材料种美){LabelSep}(.+?)(?=\n|[;；]|委托组数|检测组数|设计要求|取样|$)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!m.Success) return null;

            var raw = NormalizeMaterialText(m.Groups[1].Value);
            var value = ExtractChineseMaterialName(raw);
            if (string.IsNullOrEmpty(value) || IsMissingValue(value) || !IsValidMaterialText(value))
                return null;

            var rawInText = m.Groups[1].Value.Trim();
            var valueOffset = rawInText.LastIndexOf(value, StringComparison.Ordinal);
            var highlightStart = valueOffset >= 0 ? m.Groups[1].Index + valueOffset : m.Groups[1].Index;
            ctx.Record(highlightStart, value.Length, "params.materialType", "材料种类");
            return value;
        }

        private static string? TryMaterialFromRemarkSegments(ParseContext ctx)
        {
            foreach (var line in ctx.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var segment in line.Split(';', '；'))
                {
                    var trimmed = segment.Trim();
                    if (string.IsNullOrEmpty(trimmed))
                        continue;
                    if (trimmed.Contains('：') || trimmed.Contains(':'))
                        continue;
                    if (LooksLikeCompactionRequirement(trimmed))
                        continue;
                    if (Regex.IsMatch(trimmed, @"密度|桩号|单位|组数|设计要求|标高|厚度|工程部位", RegexOptions.IgnoreCase))
                        continue;

                    var value = ExtractChineseMaterialName(trimmed);
                    if (string.IsNullOrEmpty(value) || !IsValidMaterialText(value))
                        continue;
                    if (!Regex.IsMatch(trimmed, @"^[\d\.\-~～]+(?:mm|cm|m|MM|CM|M)", RegexOptions.IgnoreCase)
                        && !IsValidMaterialText(trimmed))
                        continue;

                    var start = ctx.Text.IndexOf(value, StringComparison.Ordinal);
                    if (start >= 0)
                        ctx.Record(start, value.Length, "params.materialType", "材料种类");
                    return value;
                }
            }

            return null;
        }

        private static string? ExtractChineseMaterialName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var text = NormalizeMaterialText(raw);
            var sized = Regex.Match(
                text,
                $@"^(?:[\d\.\-~～]+(?:mm|cm|m|MM|CM|M)?)+(?<name>{ChineseMaterialRun})$",
                RegexOptions.IgnoreCase);
            if (sized.Success)
                return sized.Groups["name"].Value;

            if (IsValidMaterialText(text))
                return text;

            return null;
        }

        private static string? TryInlineMaterialAfterDryDensity(ParseContext ctx)
        {
            var m = Regex.Match(
                ctx.Text,
                $@"最大干密度\s*[:：]?\s*\d+(?:\.\d+)?\s*{DryDensityUnit}\s+(.+?)(?=\s*标高|\s*最佳|\s*最优|\s*委托组数|\s*检测组数|$)",
                RegexOptions.IgnoreCase);
            if (!m.Success) return null;

            var value = NormalizeMaterialText(m.Groups[1].Value);
            if (string.IsNullOrEmpty(value) || !LooksLikeMaterialLine(value)) return null;

            ctx.Record(m.Groups[1].Index, value.Length, "params.materialType", "材料种类");
            return value;
        }

        private static string? TryFirstLineMaterial(ParseContext ctx)
        {
            var line = ctx.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (line == null) return null;
            line = NormalizeMaterialText(line);
            if (!LooksLikeMaterialLine(line)) return null;

            var start = ctx.Text.IndexOf(line, StringComparison.Ordinal);
            if (start >= 0)
                ctx.Record(start, line.Length, "params.materialType", "材料种类");
            return line;
        }

        private static string? TryLastStandaloneLine(ParseContext ctx, string fieldKey, string label)
        {
            var lines = ctx.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var line = NormalizeMaterialText(lines[i]);
                if (!LooksLikeMaterialLine(line))
                    continue;

                var start = ctx.Text.LastIndexOf(line, StringComparison.Ordinal);
                if (start >= 0)
                    ctx.Record(start, line.Length, fieldKey, label);
                return line;
            }
            return null;
        }

        private static bool LooksLikeCompactionRequirement(string line) =>
            Regex.IsMatch(line, @"压实度|压实系数", RegexOptions.IgnoreCase);

        private static bool LooksLikeMaterialLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.Length > 40)
                return false;
            if (LooksLikeCompactionRequirement(line))
                return false;
            if (line.Contains('：') || line.Contains(':'))
                return false;
            if (Regex.IsMatch(line, @"^P\d+\+", RegexOptions.IgnoreCase))
                return false;
            return IsValidMaterialText(line);
        }

        /// <summary>规则⑤：仅允许中文及括号（）（）。</summary>
        private static bool IsValidMaterialText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return NormalizeMaterialText(text).All(IsMaterialChar);
        }

        private static string NormalizeMaterialText(string text) =>
            text.Trim().TrimEnd('。', '，', '、', '.', ',', ';', '；');

        private static bool IsMaterialChar(char c) =>
            c is '（' or '）' or '(' or ')'
            || (c >= '\u4e00' && c <= '\u9fff');

        private static string? MatchField(ParseContext ctx, string pattern, string fieldKey, string label)
        {
            var m = Regex.Match(ctx.Text, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            var value = TrimAtFollowingFieldLabels(m.Groups[1].Value.Trim());
            if (string.IsNullOrEmpty(value) || IsMissingValue(value)) return null;
            value = NormalizeMaterialText(value);
            if (string.IsNullOrEmpty(value)) return null;
            ctx.RecordGroup(m.Groups[1], fieldKey, label);
            return value;
        }

        private static string TrimAtFollowingFieldLabels(string value)
        {
            var labels = new[]
            {
                "材料种类", "材料种美", "品种", "取样时间", "取样标高", "取样层厚度", "取祥层厚度",
                "填筑厚度", "厚度", "最大干密度", "毛体积密度", "最佳含水率", "最优含水率", "设计要求",
                "夯实方式", "监理单位", "见证单位", "施工单位", "委托组数", "检测组数"
            };

            var index = value.Length;
            foreach (var label in labels)
            {
                var pos = value.IndexOf(label, StringComparison.Ordinal);
                if (pos > 0)
                    index = Math.Min(index, pos);
            }

            return index < value.Length ? value[..index].Trim() : value.Trim();
        }

        private static string NormalizeRemarkText(string remark)
        {
            var text = remark.Replace("\r\n", "\n").Trim().Replace('％', '%');
            text = InsertMissingLabelSpaces(text);
            text = Regex.Replace(text, $@"(?<=[0-9%³3)])(?=(?:{GluedAfterDigitLabels}))", " ");
            text = Regex.Replace(text, $@"(?<=[\u4e00-\u9fff])(?=(?:{GluedAfterCjkLabels}))", " ");
            text = Regex.Replace(text, $@"(?<={DryDensityUnit})(?={MoistureLabel})", " ", RegexOptions.IgnoreCase);
            return text;
        }

        private static string InsertMissingLabelSpaces(string text) =>
            Regex.Replace(
                text,
                @"(?<![\u4e00-\u9fff])(监理单位|见证单位|施工单位|工程部位|取样部位|材料种类|材料种美|品种|设计要求|取样时间|取样标高|检测点桩号|点桩号|桩号)(?=[^\s:：;；\n])",
                "$1 ",
                RegexOptions.IgnoreCase);

        private sealed class ParseContext
        {
            public ParseContext(string text, RemarkParseResult result)
            {
                Text = text;
                Result = result;
            }

            public string Text { get; }
            public RemarkParseResult Result { get; }

            public void Record(int start, int length, string fieldKey, string label)
            {
                if (length <= 0) return;
                Result.Highlights.Add(new RemarkHighlight
                {
                    Start = start,
                    Length = length,
                    FieldKey = fieldKey,
                    Label = label
                });
                Result.ExtractedFieldKeys.Add(fieldKey);
            }

            public void RecordGroup(Group group, string fieldKey, string label)
            {
                if (!group.Success || group.Length <= 0) return;
                Record(group.Index, group.Length, fieldKey, label);
            }

            public void RecordFromGroupToMatchEnd(Match match, int groupIndex, string fieldKey, string label)
            {
                var group = match.Groups[groupIndex];
                if (!group.Success || group.Length <= 0) return;
                var length = match.Index + match.Length - group.Index;
                Record(group.Index, length, fieldKey, label);
            }
        }
    }
}
