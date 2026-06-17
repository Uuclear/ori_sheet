using RingKnifeDetector.Models;
using RingKnifeDetector.Services;
using Xunit;

namespace RingKnifeDetector.Tests;

public class RemarkParserTests
{
    private const string SampleRemark =
        "工程部位:二跑道 R3联络道 2SREL41-48跑道进入灯,一次电缆沟回填第二层，灯箱基底\n" +
        "取样点 P293+11.234/H257+19.296\n" +
        "标高-300mm  厚度200mm  品种:粉细砂\n" +
        "委托组数:1组压实度≥90% 最大干密度1.64g/cm³, 最佳含水率13.8%";

    [Fact]
    public void FillMissing_ExtractsFieldsFromRemark()
    {
        var project = new ProjectInfo();
        var parameters = new RecordParams();
        var samples = new List<RingKnifeSample> { new() };

        RemarkParser.FillMissing(project, parameters, samples, SampleRemark);

        Assert.Contains("二跑道", project.ProjectSection);
        Assert.Equal("粉细砂", parameters.MaterialType);
        Assert.Equal("P293+11.234/H257+19.296", parameters.TestLocation);
        Assert.Equal(1.64m, parameters.MaxDryDensity);
        Assert.Equal(13.8m, parameters.OptimalMoisture);
        Assert.Equal(90m, parameters.DesignRequirement);
        Assert.Equal("≥90%", parameters.DesignRequirementText);
        Assert.Equal("compaction_percent", parameters.ResultType);
        Assert.Equal("-300", samples[0].Elevation);
        Assert.Equal("200", samples[0].Thickness);
    }

    [Fact]
    public void FillMissing_DoesNotOverwriteExistingValues()
    {
        var project = new ProjectInfo { ProjectSection = "已有部位" };
        var parameters = new RecordParams { MaterialType = "已有品种" };
        var samples = new List<RingKnifeSample> { new() { Elevation = "0" } };

        RemarkParser.FillMissing(project, parameters, samples, SampleRemark);

        Assert.Equal("已有部位", project.ProjectSection);
        Assert.Equal("已有品种", parameters.MaterialType);
        Assert.Equal("0", samples[0].Elevation);
    }

    [Fact]
    public void FillMissing_TreatsRemarkPlaceholderAsEmpty()
    {
        var project = new ProjectInfo { SupervisionUnit = "见备注" };
        var parameters = new RecordParams();
        var samples = new List<RingKnifeSample> { new() };

        RemarkParser.FillMissing(project, parameters, samples,
            "见证单位:某某监理公司\n施工单位:某某建设");

        Assert.Equal("某某监理公司", project.SupervisionUnit);
    }

    [Fact]
    public void FillMissing_ExtractsCompactionCoeff()
    {
        var project = new ProjectInfo();
        var parameters = new RecordParams();
        var samples = new List<RingKnifeSample> { new() };

        RemarkParser.FillMissing(project, parameters, samples, "压实系数≥0.93");

        Assert.Equal(0.93m, parameters.DesignRequirement);
        Assert.Equal("≥0.93", parameters.DesignRequirementText);
        Assert.Equal("compaction_coeff", parameters.ResultType);
    }

    [Fact]
    public void FillMissing_ExtractsOptimalMoistureSynonym()
    {
        var project = new ProjectInfo();
        var parameters = new RecordParams();
        var samples = new List<RingKnifeSample> { new() };

        RemarkParser.FillMissing(project, parameters, samples, "最优含水率15.2%");

        Assert.Equal(15.2m, parameters.OptimalMoisture);
    }

    [Fact]
    public void FillMissing_RemovesChinesePeriodsFromExtractedFields()
    {
        var project = new ProjectInfo();
        var parameters = new RecordParams();
        var samples = new List<RingKnifeSample> { new() };

        RemarkParser.FillMissing(project, parameters, samples, "工程部位:某跑道部位。\n品种:粉细砂。");

        Assert.Equal("某跑道部位", project.ProjectSection);
        Assert.Equal("粉细砂", parameters.MaterialType);
    }

    [Fact]
    public void FillMissing_DesignRequirementIncludesPercentWhenMatched()
    {
        var project = new ProjectInfo();
        var parameters = new RecordParams();
        var samples = new List<RingKnifeSample> { new() };

        RemarkParser.FillMissing(project, parameters, samples, "压实度≥90%");

        Assert.Equal("≥90%", parameters.DesignRequirementText);
    }

    [Fact]
    public void FillMissing_DesignRequirementOmitsPercentWhenNotMatched()
    {
        var project = new ProjectInfo();
        var parameters = new RecordParams();
        var samples = new List<RingKnifeSample> { new() };

        RemarkParser.FillMissing(project, parameters, samples, "压实度≥90");

        Assert.Equal("≥90", parameters.DesignRequirementText);
    }

    [Fact]
    public void FillMissing_ExtractsCementStabilizedRemark()
    {
        const string remark =
            "水泥稳定碎石底基层\n" +
            "桩号：P217+29-P218+31/H178+1.72-H179+11.52（1911.6m2）\n" +
            "最大干密度2.296g/cm3；最佳含水量5.4%；检测组数： 3组\n" +
            "点桩号：P217+32.7/H178+9.52；P218+14.66/H178+35.24；P218+26.75/H179+6.33；\n" +
            "设计要求：压实度≥97%（高程：4.22m，厚度200mm）";

        var project = new ProjectInfo();
        var parameters = new RecordParams();
        var samples = new List<RingKnifeSample> { new() };

        RemarkParser.FillMissing(project, parameters, samples, remark);

        Assert.Equal(2.296m, parameters.MaxDryDensity);
        Assert.Equal(5.4m, parameters.OptimalMoisture);
        Assert.Equal(97m, parameters.DesignRequirement);
        Assert.Equal("≥97%", parameters.DesignRequirementText);
        Assert.Equal("compaction_percent", parameters.ResultType);
        Assert.Equal("水泥稳定碎石底基层", parameters.MaterialType);
        Assert.Equal("200", samples[0].Thickness);
    }

    [Fact]
    public void FillMissing_ExtractsMaxDryDensityWithColon()
    {
        const string remark =
            "最佳含水率:14.1%;最大干密度:1.65g/cm³; 设计要求:压实度≥88%\n粉细砂";

        var project = new ProjectInfo();
        var parameters = new RecordParams();
        var samples = new List<RingKnifeSample> { new() };

        RemarkParser.FillMissing(project, parameters, samples, remark);

        Assert.Equal(1.65m, parameters.MaxDryDensity);
        Assert.Equal(14.1m, parameters.OptimalMoisture);
        Assert.Equal("≥88%", parameters.DesignRequirementText);
        Assert.Equal("粉细砂", parameters.MaterialType);
    }

    [Fact]
    public void FillMissing_ExtractsInlineMaterialAfterDryDensity()
    {
        const string remark =
            "压实度>85%(环刀)\n" +
            "新片区 消防阀门井X6-3素土回填第七层:\n" +
            "点桩号: P356+28.91/H303+35.8\n" +
            "最大干密度:1.68g/cm³  素土(环刀) 标高:3.5m最佳含水率:16.1%委托组数:1组\n" +
            "厚度:30cm";

        var project = new ProjectInfo();
        var parameters = new RecordParams();
        var samples = new List<RingKnifeSample> { new() };

        var parseResult = RemarkParser.FillMissing(project, parameters, samples, remark);

        Assert.Equal("素土(环刀)", parameters.MaterialType);
        Assert.Equal(1.68m, parameters.MaxDryDensity);
        Assert.Equal(16.1m, parameters.OptimalMoisture);
        Assert.Equal(85m, parameters.DesignRequirement);
        Assert.Equal("P356+28.91/H303+35.8", parameters.TestLocation);
        Assert.Contains(parseResult.Highlights, h => h.Label == "材料种类");
        Assert.Contains(parseResult.Highlights, h => h.Label == "最大干密度");
    }

    [Fact]
    public void AnalyzeHighlights_OnlyMarksExtractedValues()
    {
        const string remark =
            "工程部位:某跑道部位\n" +
            "品种:粉细砂\n" +
            "压实度≥90% 最大干密度1.64g/cm³, 最佳含水率13.8%";

        var result = RemarkParser.AnalyzeHighlights(remark);

        var section = result.Highlights.Single(h => h.Label == "工程部位");
        Assert.Equal("某跑道部位", remark.Substring(section.Start, section.Length));

        var material = result.Highlights.Single(h => h.Label == "材料种类");
        Assert.Equal("粉细砂", remark.Substring(material.Start, material.Length));

        var density = result.Highlights.Single(h => h.Label == "最大干密度");
        Assert.Equal("1.64g/cm³", remark.Substring(density.Start, density.Length));

        var moisture = result.Highlights.Single(h => h.Label == "最优含水率");
        Assert.Equal("13.8%", remark.Substring(moisture.Start, moisture.Length));

        var design = result.Highlights.Single(h => h.Label == "设计要求/压实度");
        Assert.Equal("≥90%", remark.Substring(design.Start, design.Length));
    }

    [Fact]
    public void FillMissing_MaterialType_RejectsNonChineseCharacters()
    {
        var project = new ProjectInfo();
        var parameters = new RecordParams();
        var samples = new List<RingKnifeSample> { new() };

        RemarkParser.FillMissing(project, parameters, samples,
            "品种:粉细砂A\n" +
            "压实度>85%(环刀)\n" +
            "最大干密度:1.68g/cm³  素土(环刀) 标高:3.5m");

        Assert.Equal("素土(环刀)", parameters.MaterialType);
    }

    [Fact]
    public void FillMissing_MaterialType_AllowsChineseWithParens()
    {
        var project = new ProjectInfo();
        var parameters = new RecordParams();
        var samples = new List<RingKnifeSample> { new() };

        RemarkParser.FillMissing(project, parameters, samples, "品种:素土（环刀）");

        Assert.Equal("素土（环刀）", parameters.MaterialType);
    }

    [Fact]
    public void FillMissing_ExtractsCableWellRemark()
    {
        const string remark =
            "工程部位：RJ16电缆井；\n" +
            "标高：1.321m；\n" +
            "点桩号：P7120.500/H8659.250;\n" +
            "委托组数：1组；\n" +
            "设计要求：固体体积率不小于0.83；\n" +
            "厚度：500mm，材料种类：0-200mm山皮石;\n" +
            "毛体积密度：2.405g/cm3，测点由委托方现场指定；\n" +
            "建设单位：上海机场集团有限公司，设计单位：上海民航新时代机场设计研究院公司；\n" +
            "施工单位：北京京航安机场工程有限公司；监理单位：上海华东民航机场建设监理有限公司";

        var project = new ProjectInfo();
        var parameters = new RecordParams();
        var samples = new List<RingKnifeSample> { new() };

        var result = RemarkParser.FillMissing(project, parameters, samples, remark);

        Assert.Equal("RJ16电缆井", project.ProjectSection);
        Assert.Equal(0.83m, parameters.DesignRequirement);
        Assert.Equal("≥0.83", parameters.DesignRequirementText);
        Assert.Equal("compaction_coeff", parameters.ResultType);
        Assert.Equal("山皮石", parameters.MaterialType);
        Assert.Equal("500", samples[0].Thickness);
        Assert.Equal("P7120.500/H8659.250", parameters.TestLocation);
        Assert.Equal("北京京航安机场工程有限公司", project.ConstructionUnit);
        Assert.Equal("上海华东民航机场建设监理有限公司", project.SupervisionUnit);

        var materialHighlight = result.Highlights.Single(h => h.Label == "材料种类");
        Assert.Equal("山皮石", remark.Substring(materialHighlight.Start, materialHighlight.Length));
    }

    [Fact]
    public void FillMissing_ExtractsValveWellRemark()
    {
        const string remark =
            "工程部位：F-03阀门井换填\n" +
            "标高：-2.65m；\n" +
            "桩号：P218+34.5/H177+30\n" +
            "检测点桩号：P218+35.91/H177+29.12\n" +
            "委托组数：1组；\n" +
            "设计要求：固体体积率≥0.80；\n" +
            "0-20cm山皮石;填筑厚度500mm\n" +
            "毛体积密度：2.269g/cm3，测点由委托方现场指定；\n" +
            "建设单位：上海机场集团有限公司，设计单位：上海民航新时代机场设计研究院公司；\n" +
            "施工单位：上海公路桥梁（集团）有限公司；监理单位：上海华东民航机场建设监理有限公司";

        var project = new ProjectInfo();
        var parameters = new RecordParams();
        var samples = new List<RingKnifeSample> { new() };

        var result = RemarkParser.FillMissing(project, parameters, samples, remark);

        Assert.Equal(0.80m, parameters.DesignRequirement);
        Assert.Equal("≥0.80", parameters.DesignRequirementText);
        Assert.Equal(2.269m, parameters.MaxDryDensity);
        Assert.Equal("山皮石", parameters.MaterialType);
        Assert.Equal("500", samples[0].Thickness);
        Assert.Equal("上海公路桥梁（集团）有限公司", project.ConstructionUnit);

        var designHighlight = result.Highlights.Single(h => h.Label == "设计要求/固体体积率");
        Assert.Equal("≥0.80", remark.Substring(designHighlight.Start, designHighlight.Length));

        var densityHighlight = result.Highlights.Single(h => h.Label == "毛体积密度");
        Assert.Equal("2.269g/cm3", remark.Substring(densityHighlight.Start, densityHighlight.Length));
    }
}
