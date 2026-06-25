namespace RingKnifeDetector.Helpers
{
    public static class TestNatureHelper
    {
        public static bool IsWitnessSampling(string? testNature) =>
            !string.IsNullOrWhiteSpace(testNature)
            && testNature.Contains("见证送样", StringComparison.Ordinal);
    }
}
