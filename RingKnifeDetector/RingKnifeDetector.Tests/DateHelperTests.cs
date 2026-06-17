using RingKnifeDetector.Helpers;
using Xunit;

namespace RingKnifeDetector.Tests;

public class DateHelperTests
{
    [Theory]
    [InlineData("2024-01-05", "2024/1/5")]
    [InlineData("2024-12-31", "2024/12/31")]
    public void FormatWordDate_SingleDate(string input, string expected)
    {
        Assert.Equal(expected, DateHelper.FormatWordDate(input));
    }

    [Fact]
    public void FormatWordDate_Range()
    {
        Assert.Equal(
            "2024/1/5~2024/1/10",
            DateHelper.FormatWordDate("2024-01-05~2024-01-10"));
    }
}
