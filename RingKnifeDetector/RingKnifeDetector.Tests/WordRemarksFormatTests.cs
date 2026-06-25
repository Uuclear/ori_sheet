using RingKnifeDetector.Services;
using Xunit;

namespace RingKnifeDetector.Tests;

public class WordRemarksFormatTests
{
    [Fact]
    public void NormalizeRemarkLines_AlignsNumberedItems()
    {
        var raw =
            "备注：1.第一条；\n" +
            "           2.第二条；\n" +
            "      3.第三条；";

        var lines = WordExportService.NormalizeRemarkLines(raw);

        Assert.Equal(3, lines.Count);
        Assert.Equal("备注： 1.第一条；", lines[0]);
        Assert.Equal("2.第二条；", lines[1]);
        Assert.Equal("3.第三条；", lines[2]);
    }
}
