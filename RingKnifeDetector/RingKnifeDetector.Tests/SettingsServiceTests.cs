using System.Text.Json;
using RingKnifeDetector.Models;
using RingKnifeDetector.Services;
using Xunit;

namespace RingKnifeDetector.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void LoadSettings_UpgradesLegacyReportRemarksTemplate()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rkd_settings_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var settingsPath = Path.Combine(dir, "settings.json");

        var legacy = new
        {
            baseUrl = "http://example.com",
            defaultReportRemarks = "备注：1.旧版备注；",
            reportRemarksTemplateVersion = 0
        };
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(legacy));

        try
        {
            var service = new SettingsService(dir);
            var settings = service.LoadSettings();

            Assert.Equal(ReportDefaults.ReportRemarksTemplateVersion, settings.ReportRemarksTemplateVersion);
            Assert.Equal(ReportDefaults.DefaultReportRemarks, settings.DefaultReportRemarks);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }
}
