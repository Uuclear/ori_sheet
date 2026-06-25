using System.Text.Json;
using RingKnifeDetector.Services;

namespace RingKnifeDetector.Tests;

/// <summary>内网探测：设置 LIMIS_PROBE=1 及 LIMIS_ENTRUST / LIMIS_BASE_URL / LIMIS_USER / LIMIS_PASS。</summary>
public class LimisProbeRunner
{
    public static async Task<string> RunAsync(
        string entrustNo,
        string baseUrl,
        string username,
        string password,
        string? orderIdOverride = null)
    {
        using var service = new LimisService();
        var dump = await service.DumpRawEntrustJsonAsync(entrustNo, baseUrl, username, password, orderIdOverride);
        var alt = await service.ProbeAlternativeApisAsync(
            entrustNo, orderIdOverride, baseUrl, username, password);
        using var doc = JsonDocument.Parse(dump);
        var merged = new Dictionary<string, object?>
        {
            ["entrustProbe"] = JsonSerializer.Deserialize<object>(dump),
            ["alternativeApis"] = JsonSerializer.Deserialize<object>(alt)
        };
        return JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true });
    }
}
