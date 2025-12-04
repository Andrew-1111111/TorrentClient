using System.Text.Json;
using TorrentClient.Core;
using TorrentClient.Core.Interfaces;

namespace TorrentClient.Tests;

public class AppSettingsManagerTests : IDisposable
{
    private readonly string _testSettingsPath;
    private readonly string _testSettingsFile;
    private readonly TestableAppSettingsManager _settingsManager;

    public AppSettingsManagerTests()
    {
        _testSettingsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "Settings");
        Directory.CreateDirectory(_testSettingsPath);
        _testSettingsFile = Path.Combine(_testSettingsPath, "appsettings.json");
        
        _settingsManager = new TestableAppSettingsManager(_testSettingsFile);
    }

    /// <summary>
    /// Проверяет, что при отсутствии файла настроек возвращаются настройки по умолчанию
    /// </summary>
    [Fact]
    public void LoadSettings_FileNotExists_ReturnsDefaultSettings()
    {
        // Act
        var settings = _settingsManager.LoadSettings();

        // Assert
        Assert.NotNull(settings);
        Assert.NotNull(settings.DefaultDownloadPath);
        Assert.NotNull(settings.StatePath);
        Assert.True(settings.EnableLogging);
    }

    /// <summary>
    /// Проверяет, что сохраненные настройки корректно загружаются обратно
    /// </summary>
    [Fact]
    public void SaveSettings_ThenLoadSettings_ReturnsSavedSettings()
    {
        // Arrange
        var originalSettings = new AppSettings
        {
            DefaultDownloadPath = @"C:\Test\Downloads",
            StatePath = @"C:\Test\States",
            EnableLogging = false,
            MaxConnections = 150,
            GlobalMaxDownloadSpeed = 1024 * 1024, // 1 MB/s
            GlobalMaxUploadSpeed = 512 * 1024 // 512 KB/s
        };

        // Act
        _settingsManager.SaveSettings(originalSettings);
        var loadedSettings = _settingsManager.LoadSettings();

        // Assert
        Assert.NotNull(loadedSettings);
        Assert.Equal(originalSettings.DefaultDownloadPath, loadedSettings.DefaultDownloadPath);
        Assert.Equal(originalSettings.StatePath, loadedSettings.StatePath);
        Assert.Equal(originalSettings.EnableLogging, loadedSettings.EnableLogging);
        Assert.Equal(originalSettings.MaxConnections, loadedSettings.MaxConnections);
        Assert.Equal(originalSettings.GlobalMaxDownloadSpeed, loadedSettings.GlobalMaxDownloadSpeed);
        Assert.Equal(originalSettings.GlobalMaxUploadSpeed, loadedSettings.GlobalMaxUploadSpeed);
    }

    /// <summary>
    /// Проверяет, что настройки с cookies для трекеров корректно сохраняются и загружаются
    /// </summary>
    [Fact]
    public void SaveSettings_WithTrackerCookies_SavesAndLoadsCorrectly()
    {
        // Arrange
        var settings = new AppSettings
        {
            TrackerCookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["http://tracker1.com"] = "cookie1=value1",
                ["http://tracker2.com"] = "cookie2=value2"
            }
        };

        // Act
        _settingsManager.SaveSettings(settings);
        var loadedSettings = _settingsManager.LoadSettings();

        // Assert
        Assert.NotNull(loadedSettings.TrackerCookies);
        Assert.Equal(2, loadedSettings.TrackerCookies.Count);
        Assert.True(loadedSettings.TrackerCookies.ContainsKey("http://tracker1.com"));
        Assert.True(loadedSettings.TrackerCookies.ContainsKey("http://tracker2.com"));
        Assert.Equal("cookie1=value1", loadedSettings.TrackerCookies["http://tracker1.com"]);
        Assert.Equal("cookie2=value2", loadedSettings.TrackerCookies["http://tracker2.com"]);
    }

    /// <summary>
    /// Проверяет, что настройки с headers для трекеров корректно сохраняются и загружаются
    /// </summary>
    [Fact]
    public void SaveSettings_WithTrackerHeaders_SavesAndLoadsCorrectly()
    {
        // Arrange
        var settings = new AppSettings
        {
            TrackerHeaders = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["http://tracker1.com"] = new Dictionary<string, string>
                {
                    ["User-Agent"] = "TestAgent",
                    ["Authorization"] = "Bearer token123"
                }
            }
        };

        // Act
        _settingsManager.SaveSettings(settings);
        var loadedSettings = _settingsManager.LoadSettings();

        // Assert
        Assert.NotNull(loadedSettings.TrackerHeaders);
        Assert.Single(loadedSettings.TrackerHeaders);
        Assert.True(loadedSettings.TrackerHeaders.ContainsKey("http://tracker1.com"));
        var headers = loadedSettings.TrackerHeaders["http://tracker1.com"];
        Assert.Equal(2, headers.Count);
        Assert.Equal("TestAgent", headers["User-Agent"]);
        Assert.Equal("Bearer token123", headers["Authorization"]);
    }

    /// <summary>
    /// Проверяет, что при невалидном JSON в файле настроек возвращаются настройки по умолчанию
    /// </summary>
    [Fact]
    public void LoadSettings_InvalidJson_ReturnsDefaultSettings()
    {
        // Arrange
        File.WriteAllText(_testSettingsFile, "invalid json content");

        // Act
        var settings = _settingsManager.LoadSettings();

        // Assert
        Assert.NotNull(settings);
        Assert.NotNull(settings.DefaultDownloadPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testSettingsPath))
            {
                Directory.Delete(_testSettingsPath, true);
            }
        }
        catch (Exception)
        {
        }
        
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Тестовая версия AppSettingsManager, которая позволяет указать путь к файлу настроек
    /// </summary>
    private class TestableAppSettingsManager : IAppSettingsManager
    {
        private readonly string _settingsFilePath;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public TestableAppSettingsManager(string settingsFilePath)
        {
            _settingsFilePath = settingsFilePath;
            var directory = Path.GetDirectoryName(settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                    return GetDefaultSettings();

                var json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? GetDefaultSettings();
            }
            catch (Exception)
            {
                return GetDefaultSettings();
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception)
            {
            }
        }

        private static AppSettings GetDefaultSettings()
        {
            return new AppSettings
            {
                DefaultDownloadPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads", "Torrents"),
                StatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "States"),
                TrackerCookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                TrackerHeaders = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase),
                EnableLogging = true
            };
        }
    }
}

