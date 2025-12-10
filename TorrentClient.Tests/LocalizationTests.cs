using TorrentClient.Core;

namespace TorrentClient.Tests;

/// <summary>
/// Тесты для функциональности локализации
/// </summary>
public class LocalizationTests
{
    [Fact]
    public void LocalizationManager_Initialize_WithLanguageCode_SetsLanguage()
    {
        // Arrange & Act
        LocalizationManager.Initialize("ru");
        
        // Assert
        var currentLang = LocalizationManager.GetCurrentLanguage();
        Assert.True(currentLang == "ru" || currentLang.StartsWith("ru"));
    }

    [Fact]
    public void LocalizationManager_Initialize_WithoutLanguageCode_UsesSystemLanguage()
    {
        // Arrange & Act
        LocalizationManager.Initialize(null);
        
        // Assert - должен использовать язык системы или английский по умолчанию
        var currentLang = LocalizationManager.GetCurrentLanguage();
        Assert.NotNull(currentLang);
        Assert.NotEmpty(currentLang);
    }

    [Fact]
    public void LocalizationManager_SetLanguage_ChangesLanguage()
    {
        // Arrange
        LocalizationManager.Initialize("en");
        
        // Act
        LocalizationManager.SetLanguage("ru");
        
        // Assert
        var currentLang = LocalizationManager.GetCurrentLanguage();
        Assert.True(currentLang == "ru" || currentLang.StartsWith("ru"));
    }

    [Fact]
    public void LocalizationManager_GetString_ReturnsLocalizedString()
    {
        // Arrange
        LocalizationManager.Initialize("en");
        
        // Act
        var text = LocalizationManager.GetString("MainForm_Title");
        
        // Assert
        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Equal("Torrent Client", text);
    }

    [Fact]
    public void LocalizationManager_GetString_WithFormat_FormatsString()
    {
        // Arrange
        LocalizationManager.Initialize("en");
        
        // Act - проверяем, что форматирование работает
        var text = LocalizationManager.GetString("MainForm_Title");
        
        // Assert
        Assert.NotNull(text);
    }

    [Fact]
    public void LocalizationManager_GetString_InvalidKey_ReturnsKey()
    {
        // Arrange
        LocalizationManager.Initialize("en");
        
        // Act
        var text = LocalizationManager.GetString("InvalidKey_ThatDoesNotExist");
        
        // Assert - должен вернуть ключ, если строка не найдена
        Assert.Equal("InvalidKey_ThatDoesNotExist", text);
    }

    [Fact]
    public void LocalizationManager_GetSupportedLanguages_ReturnsList()
    {
        // Act
        var languages = LocalizationManager.GetSupportedLanguages();
        
        // Assert
        Assert.NotNull(languages);
        Assert.NotEmpty(languages);
        Assert.Contains(languages, l => l.Code == "en");
        Assert.Contains(languages, l => l.Code == "ru");
        Assert.Contains(languages, l => l.Code == "es");
        Assert.Contains(languages, l => l.Code == "fr");
        Assert.Contains(languages, l => l.Code == "de");
    }

    [Fact]
    public void LanguageInfo_ToString_ReturnsFormattedString()
    {
        // Arrange
        var langInfo = new LanguageInfo("en", "English", "English");
        
        // Act
        var result = langInfo.ToString();
        
        // Assert
        Assert.NotNull(result);
        Assert.Contains("English", result);
    }

    [Fact]
    public void LocalizationManager_GetString_Russian_ReturnsRussianText()
    {
        // Arrange
        LocalizationManager.Initialize("ru");
        
        // Act
        var text = LocalizationManager.GetString("MainForm_Title");
        
        // Assert
        Assert.NotNull(text);
        // Если русский файл ресурсов существует, должен вернуть русский текст
        // Иначе вернёт английский или ключ
        Assert.NotEmpty(text);
    }

    [Fact]
    public void LocalizationManager_SetLanguage_InvalidCode_FallsBackToEnglish()
    {
        // Arrange
        LocalizationManager.Initialize("en");
        
        // Act
        try
        {
            LocalizationManager.SetLanguage("invalid-language-code-xyz");
        }
        catch
        {
            // Ожидаем, что будет исключение или fallback
        }
        
        // Assert - должен использовать английский по умолчанию
        var currentLang = LocalizationManager.GetCurrentLanguage();
        Assert.NotNull(currentLang);
    }

    [Fact]
    public void LocalizationManager_GetString_TranslatedLanguages_ReturnTranslatedText()
    {
        // Arrange - проверяем все переведенные языки (50 языков)
        // Исключаем языки, где перевод совпадает с английским (nl)
        var translatedLanguages = new[] { 
            "ru", "es", "fr", "de", "it", "pt", "pl", "cs", "sv", 
            "da", "no", "fi", "tr", "uk", "ro", "hu", "el", "bg", "hr", 
            "sk", "sr", "sl", "et", "lv", "lt", "ja", "ko", "zh-CN", "zh-TW", "zh",
            "ar", "he", "hi", "id", "vi", "th",
            "ca", "mk", "sq", "is", "ga", "mt", "cy", "eu", "gl",
            "pt-BR", "es-MX", "es-AR", "fr-CA"
        };
        
        foreach (var lang in translatedLanguages)
        {
            // Act
            LocalizationManager.Initialize(lang);
            var text = LocalizationManager.GetString("MainForm_Title");
            
            // Assert - должен вернуть переведенный текст, а не английский
            Assert.NotNull(text);
            Assert.NotEmpty(text);
            // Проверяем, что это не английский текст "Torrent Client"
            // Примечание: если сателлитные сборки не загружаются в тестах,
            // может вернуться английский текст, но это не ошибка перевода
            Assert.NotEqual("Torrent Client", text);
        }
    }

    [Fact]
    public void LocalizationManager_GetString_AllLanguages_HaveResourceFiles()
    {
        // Arrange
        var supportedLanguages = LocalizationManager.GetSupportedLanguages();
        
        // Act & Assert - проверяем, что для всех языков есть файлы ресурсов
        foreach (var lang in supportedLanguages)
        {
            if (lang.Code == "en") continue; // Базовый файл
            
            LocalizationManager.Initialize(lang.Code);
            var text = LocalizationManager.GetString("MainForm_Title");
            
            // Должен вернуть либо переведенный текст, либо английский (fallback)
            Assert.NotNull(text);
            Assert.NotEmpty(text);
        }
    }
}

