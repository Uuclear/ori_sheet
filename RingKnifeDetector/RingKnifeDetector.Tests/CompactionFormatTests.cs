using RingKnifeDetector.Helpers;

namespace RingKnifeDetector.Tests;

public class CompactionFormatTests
{
    [Fact]
    public void RoundPercent_UsesOneDecimal()
    {
        Assert.Equal(95.3m, CompactionFormat.RoundPercent(95.26m));
        Assert.Equal("95.3", CompactionFormat.FormatPercent(95.26m));
    }

    [Fact]
    public void RoundCoeff_UsesTwoDecimals()
    {
        Assert.Equal(0.95m, CompactionFormat.RoundCoeff(0.953m));
        Assert.Equal("0.95", CompactionFormat.FormatCoeff(0.953m));
    }
}
