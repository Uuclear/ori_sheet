using RingKnifeDetector.Helpers;
using Xunit;

namespace RingKnifeDetector.Tests;

public class TestBasisNormalizerTests
{
    [Fact]
    public void Normalize_KeepsStandardTitleOnly()
    {
        var raw = "TG11-260350-01: GB/T 50123-2019《土工试验方法标准》( ): (压实系数)";
        Assert.Equal("GB/T 50123-2019《土工试验方法标准》", TestBasisNormalizer.Normalize(raw));
    }

    [Fact]
    public void ExtractCodeOnly_StripsBookTitleByRegex()
    {
        Assert.Equal("GB/T 50123-2019", TestBasisNormalizer.ExtractCodeOnly("GB/T 50123-2019《土工试验方法标准》"));
        Assert.Equal("JTG 3450-2019", TestBasisNormalizer.ExtractCodeOnly("JTG 3450-2019"));
    }

    [Fact]
    public void ToDisplay_TogglesBookTitleByRegex()
    {
        const string full = "GB/T 50123-2019《土工试验方法标准》";
        Assert.Equal(full, TestBasisNormalizer.ToDisplay(full, showBookTitle: true));
        Assert.Equal("GB/T 50123-2019", TestBasisNormalizer.ToDisplay(full, showBookTitle: false));
    }
}
