using RingKnifeDetector.Services;
using Xunit;

namespace RingKnifeDetector.Tests;

public class LimisOrderHtmlParserTests
{
    private const string SampleHtml = """
        <table>
        <tr><th>联系方式</th><td> 021-63245336 </td></tr>
        </table>
        <table id="tbInfo">
        <tr class="apendTr">
            <th>工程见证</th><td colspan="3"> 上海信达工程建设监理有限公司,叶绍清,18556,15900826288 </td>
            <th>样品取样</th><td colspan="3"> 中国建筑第二工程局有限公司,孟献龙,31875,15257554555 </td>
        </tr>
        <tr class="tbSamples"><th>序号</th><th>样品名称</th><th>型号规格</th></tr>
        <tr class="tbSamples">
            <td>1</td>
            <td>回填土（环刀）</td>
            <td>200cm<sup>3</sup></td>
        </tr>
        <tr class="tbSamples">
            <th>检验依据<br />及项目</th>
            <td colspan="7">TG11-260350-01: <br/>GB/T 50123-2019《土工试验方法标准》( ): (压实系数)</td>
        </tr>
        </table>
        """;

    [Fact]
    public void Parse_TG11_260350_ExtractsWitnessFields()
    {
        var fields = LimisOrderHtmlParser.Parse(SampleHtml);

        Assert.Equal("021-63245336", fields.Contact);
        Assert.Equal("上海信达工程建设监理有限公司,叶绍清,18556,15900826288", fields.SupervisionWitness);
        Assert.Equal("中国建筑第二工程局有限公司,孟献龙,31875,15257554555", fields.SampleSampling);
        Assert.Equal("回填土（环刀）", fields.SampleName);
        Assert.Equal("200cm³", fields.TypeSpecification);
        Assert.Equal("GB/T 50123-2019《土工试验方法标准》", fields.TestBasis);
    }

    [Fact]
    public void FormatWitnessParty_PreservesCommaSeparatedFormat()
    {
        var formatted = LimisOrderHtmlParser.FormatWitnessParty("某某公司,张三,12345,13800001111");
        Assert.Equal("某某公司,张三,12345,13800001111", formatted);
    }
}
