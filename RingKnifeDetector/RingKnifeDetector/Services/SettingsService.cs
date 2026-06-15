using System;
using System.IO;
using System.Text.Json;
using RingKnifeDetector.Models;

namespace RingKnifeDetector.Services
{
    /// <summary>
    /// 设置管理服务
    /// </summary>
    public class SettingsService
    {
        private readonly string _settingsFilePath;

        public SettingsService(string? settingsDirectory = null)
        {
            var directory = settingsDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RingKnifeDetector");

            // 确保目录存在
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _settingsFilePath = Path.Combine(directory, "settings.json");
        }

        /// <summary>
        /// 加载设置
        /// </summary>
        /// <returns>应用程序设置</returns>
        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    return new AppSettings();
                }

                var json = File.ReadAllText(_settingsFilePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                return JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        /// <param name="settings">应用程序设置</param>
        /// <returns>是否成功</returns>
        public bool SaveSettings(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_settingsFilePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取设置响应（隐藏密码）
        /// </summary>
        /// <returns>设置响应</returns>
        public AppSettingsResponse GetSettingsResponse()
        {
            var settings = LoadSettings();
            return new AppSettingsResponse
            {
                BaseUrl = settings.BaseUrl,
                Username = settings.Username,
                PasswordSet = !string.IsNullOrEmpty(settings.Password)
            };
        }
    }
}