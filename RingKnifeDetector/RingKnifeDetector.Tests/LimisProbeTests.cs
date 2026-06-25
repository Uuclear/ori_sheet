using System.Text.Json;
using RingKnifeDetector.Services;
using Xunit;

namespace RingKnifeDetector.Tests;

public class LimisProbeTests
{
    [Fact]
    public async Task Probe_TG11_260327_DumpEntrustFields()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LIMIS_PROBE")))
            return;

        var baseUrl = Environment.GetEnvironmentVariable("LIMIS_BASE_URL") ?? "http://10.1.228.22";
        var username = Environment.GetEnvironmentVariable("LIMIS_USER")
            ?? Environment.GetEnvironmentVariable("LIMIS_USERNAME");
        var password = Environment.GetEnvironmentVariable("LIMIS_PASS")
            ?? Environment.GetEnvironmentVariable("LIMIS_PASSWORD");
        var entrust = Environment.GetEnvironmentVariable("LIMIS_ENTRUST") ?? "TG11-260327";
        var orderId = Environment.GetEnvironmentVariable("LIMIS_ORDER_ID");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RingKnifeDetector", "settings.json");
            if (!File.Exists(settingsPath)) return;
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
            username = doc.RootElement.GetProperty("username").GetString();
            password = doc.RootElement.GetProperty("password").GetString();
            baseUrl = doc.RootElement.GetProperty("baseUrl").GetString() ?? baseUrl;
        }

        var dump = await LimisProbeRunner.RunAsync(entrust, baseUrl, username!, password!, orderId);
        var safeName = entrust.Replace('-', '_');
        var outPath = Path.Combine(Path.GetTempPath(), $"limis-probe-{safeName}.json");
        await File.WriteAllTextAsync(outPath, dump);
        Assert.Contains(entrust, dump);
    }

    [Fact]
    public async Task Probe_TG11_260350_DumpEntrustFields()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LIMIS_PROBE")))
            return;

        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RingKnifeDetector", "settings.json");
        Assert.True(File.Exists(settingsPath), $"Missing settings: {settingsPath}");

        var json = await File.ReadAllTextAsync(settingsPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var baseUrl = root.GetProperty("baseUrl").GetString()!;
        var username = root.GetProperty("username").GetString()!;
        var password = root.GetProperty("password").GetString()!;

        using var service = new LimisService();
        var dump = await service.DumpRawEntrustJsonAsync(
            "TG11-260350", baseUrl: baseUrl, username: username, password: password);

        var outPath = Path.Combine(Path.GetTempPath(), "limis-probe-TG11-260350.json");
        await File.WriteAllTextAsync(outPath, dump);
        Assert.Contains("TG11-260350", dump);
    }
}
