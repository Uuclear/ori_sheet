using RingKnifeDetector.Models;

namespace RingKnifeDetector.Helpers
{
    public static class MoistureValidation
    {
        public const decimal MaxBoxMoistureDiffPercent = 1m;

        public static bool HasExcessiveBoxMoistureSpread(RingPointResult? ring)
        {
            if (ring == null) return false;

            var rates = ring.MoistureRates
                .Where(m => m.HasValue)
                .Select(m => m!.Value)
                .Take(2)
                .ToList();

            if (rates.Count < 2) return false;
            return Math.Abs(rates[0] - rates[1]) > MaxBoxMoistureDiffPercent;
        }

        public static bool HasExcessiveBoxMoistureSpread(SamplePointResult? result) =>
            result?.Rings.Any(HasExcessiveBoxMoistureSpread) == true;

        public static IEnumerable<string> CollectBoxMoistureWarnings(IReadOnlyList<SamplePointResult> results)
        {
            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var label = string.IsNullOrWhiteSpace(result.SampleNo) ? $"测点{i + 1}" : result.SampleNo;

                for (var ringIndex = 0; ringIndex < result.Rings.Count; ringIndex++)
                {
                    var ring = result.Rings[ringIndex];
                    var rates = ring.MoistureRates
                        .Where(m => m.HasValue)
                        .Select(m => m!.Value)
                        .Take(2)
                        .ToList();

                    if (rates.Count < 2) continue;

                    var spread = Math.Abs(rates[0] - rates[1]);
                    if (spread <= MaxBoxMoistureDiffPercent) continue;

                    yield return $"{label}·环刀{ringIndex + 1}：两个盒号含水率相差 {spread:0.#}%（超过1%）";
                }
            }
        }
    }
}
