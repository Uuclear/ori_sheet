using RingKnifeDetector.Helpers;
using Xunit;

namespace RingKnifeDetector.Tests;

public class DateHelperTests
{
    [Theory]
    [InlineData("2024-01-05", "2024年01月05日")]
    [InlineData("2024-12-31", "2024年12月31日")]
    public void FormatWordDate_SingleDate(string input, string expected)
    {
        Assert.Equal(expected, DateHelper.FormatWordDate(input));
    }

    [Fact]
    public void FormatWordDate_Range()
    {
        Assert.Equal(
            "2024年01月05日~2024年01月10日",
            DateHelper.FormatWordDate("2024-01-05~2024-01-10"));
    }

    [Theory]
    [InlineData("2024年01月05日", "2024年01月05日")]
    [InlineData("2024-01-05", "2024年01月05日")]
    public void Format_NormalizesToChineseDate(string input, string expected)
    {
        Assert.Equal(expected, DateHelper.Format(DateHelper.TryParse(input)));
    }

    [Fact]
    public void EnsureRangeFormat_ConvertsSingleDateToRange()
    {
        Assert.Equal(
            "2024年01月05日~2024年01月05日",
            DateHelper.EnsureRangeFormat("2024-01-05"));
    }

    [Fact]
    public void FormatRangeMultiline_SplitsStartAndEnd()
    {
        Assert.Equal(
            "2024年01月05日\n2024年01月10日",
            DateHelper.FormatRangeMultiline("2024-01-05~2024-01-10"));
    }
}
