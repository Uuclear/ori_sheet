using RingKnifeDetector.Services;
using Xunit;

namespace RingKnifeDetector.Tests;

public class WordHeaderFormatTests
{
    [Fact]
    public void HeaderSectionGap_HasTwentyOneSpaces()
    {
        Assert.Equal(21, WordExportService.HeaderSectionGap.Length);
    }
}
