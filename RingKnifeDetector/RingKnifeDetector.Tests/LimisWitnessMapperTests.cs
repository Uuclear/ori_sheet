using RingKnifeDetector.Models;
using RingKnifeDetector.Services;
using Xunit;

namespace RingKnifeDetector.Tests;

public class LimisWitnessMapperTests
{
    [Fact]
    public void Map_AppliesWitnessFieldKeys()
    {
        var project = new ProjectInfo { TestNature = "见证送样" };
        var order = new Dictionary<string, object>
        {
            ["witnessUnitName"] = "某某监理公司",
            ["witnessPersonName"] = "张三",
            ["samplingUnitName"] = "某某施工单位",
            ["samplingPersonName"] = "李四",
            ["witnessLinkMan"] = "王五",
            ["witnessTel"] = "13800000000",
            ["typeSpecification"] = "C30",
            ["testBasisName"] = "GB/T 50081-2019",
            ["sampleName"] = "混凝土试块",
        };

        var fields = LimisWitnessMapper.Map(project, order, null, null);
        LimisWitnessMapper.ApplyToProject(project, fields);

        Assert.Contains("某某监理公司", project.SupervisionUnit);
        Assert.Contains("某某施工单位", project.ConstructionUnit);
        Assert.Equal("王五 13800000000", project.Contact);
        Assert.Equal("混凝土试块", fields.SampleName);
        Assert.Equal("C30", fields.TypeSpecification);
        Assert.Equal("GB/T 50081-2019", fields.TestBasis);
    }

    [Fact]
    public void Map_ReturnsEmptyForFieldDetection()
    {
        var project = new ProjectInfo { TestNature = "现场检测" };
        var order = new Dictionary<string, object> { ["witnessUnitName"] = "不应采用" };

        var fields = LimisWitnessMapper.Map(project, order, null, null);

        Assert.Equal(string.Empty, fields.SupervisionWitness);
    }
}
