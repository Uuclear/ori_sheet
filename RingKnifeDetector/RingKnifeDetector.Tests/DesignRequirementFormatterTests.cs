using RingKnifeDetector.Helpers;
using RingKnifeDetector.Models;
using Xunit;

namespace RingKnifeDetector.Tests;

public class DesignRequirementFormatterTests
{
    [Theory]
    [InlineData("compaction_percent", "≥94%", "压实度≥94%")]
    [InlineData("compaction_coeff", "≥0.90", "压实系数≥0.90")]
    [InlineData("compaction_percent", "压实度（环刀）≥94%", "压实度（环刀）≥94%")]
    public void FormatForWord_AddsPrefixWhenMissing(string resultType, string text, string expected)
    {
        var parameters = new RecordParams
        {
            ResultType = resultType,
            DesignRequirementText = text,
            DesignRequirement = 94
        };

        Assert.Equal(expected, DesignRequirementFormatter.FormatForWord(parameters));
    }
}
