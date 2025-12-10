using System.Globalization;
using System.Resources;
using System.Reflection;

namespace TorrentClient.Core
{
    /// <summary>
    /// Менеджер локализации приложения.
    /// Управляет загрузкой и использованием локализованных строк из ресурсных файлов.
    /// Поддерживает 50 языков, включая региональные варианты.
    /// </summary>
    public static class LocalizationManager
    {
        private static ResourceManager? _resourceManager;
        private static CultureInfo _currentCulture = CultureInfo.CurrentCulture;
        private static readonly object _lock = new();

        /// <summary>
        /// Инициализирует менеджер локализации.
        /// Если язык не указан, автоматически определяет язык из настроек Windows.
        /// </summary>
        /// <param name="languageCode">Код языка (например, "ru", "en", "pt-BR"). Если null или пустая строка, используется язык Windows.</param>
        /// <remarks>
        /// Ресурсные файлы находятся в папке Resources/Languages/.
        /// Базовый файл: Resources/Languages/Strings.resx (английский).
        /// Локализованные файлы: Resources/Languages/Strings.{код_языка}.resx
        /// </remarks>
        public static void Initialize(string? languageCode = null)
        {
            lock (_lock)
            {
                try
                {
                    // Определяем язык
                    // Если languageCode не задан (null) или пустая строка, используем язык из Windows
                    if (string.IsNullOrWhiteSpace(languageCode))
                    {
                        // Пытаемся получить язык из Windows
                        try
                        {
                            _currentCulture = CultureInfo.CurrentUICulture;
                            // Логируем для отладки (если Logger доступен)
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"[LocalizationManager] Язык не задан, используется язык Windows: {_currentCulture.Name}");
                            }
                            catch { }
                        }
                        catch
                        {
                            // Если не удалось, используем английский по умолчанию
                            _currentCulture = new CultureInfo("en");
                            try
                            {
                                System.Diagnostics.Debug.WriteLine("[LocalizationManager] Не удалось получить язык Windows, используется английский по умолчанию");
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        // Используем заданный язык
                        _currentCulture = new CultureInfo(languageCode);
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"[LocalizationManager] Используется заданный язык: {languageCode}");
                        }
                        catch { }
                    }

                    // Загружаем ресурсы
                    var assembly = Assembly.GetExecutingAssembly();
                    _resourceManager = new ResourceManager("TorrentClient.Resources.Languages.Strings", assembly);
                }
                catch
                {
                    // В случае ошибки используем английский
                    _currentCulture = new CultureInfo("en");
                    var assembly = Assembly.GetExecutingAssembly();
                    _resourceManager = new ResourceManager("TorrentClient.Resources.Languages.Strings", assembly);
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("[LocalizationManager] Ошибка инициализации, используется английский по умолчанию");
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Устанавливает язык приложения.
        /// Изменяет текущий язык без переинициализации ResourceManager.
        /// </summary>
        /// <param name="languageCode">Код языка (например, "ru", "en", "pt-BR").</param>
        /// <remarks>
        /// Если указан неверный код языка, используется английский по умолчанию.
        /// </remarks>
        public static void SetLanguage(string languageCode)
        {
            lock (_lock)
            {
                try
                {
                    _currentCulture = new CultureInfo(languageCode);
                }
                catch
                {
                    _currentCulture = new CultureInfo("en");
                }
            }
        }

        /// <summary>
        /// Получает текущий язык приложения.
        /// </summary>
        /// <returns>Код текущего языка (например, "ru", "en", "pt-BR").</returns>
        /// <remarks>
        /// Возвращает полное имя культуры (например, "pt-BR"), если доступно,
        /// иначе возвращает двухбуквенный код языка (например, "pt").
        /// </remarks>
        public static string GetCurrentLanguage()
        {
            lock (_lock)
            {
                // Возвращаем полное имя культуры (например, "pt-BR"), если есть, иначе двухбуквенный код
                return string.IsNullOrEmpty(_currentCulture.Name) 
                    ? _currentCulture.TwoLetterISOLanguageName 
                    : _currentCulture.Name;
            }
        }

        /// <summary>
        /// Получает локализованную строку по ключу.
        /// Если строка не найдена для текущего языка, используется английский (fallback).
        /// Если строка не найдена и в английском, возвращается ключ.
        /// </summary>
        /// <param name="key">Ключ локализованной строки (например, "MainForm_Title").</param>
        /// <param name="args">Аргументы для форматирования строки (опционально).</param>
        /// <returns>Локализованная строка или ключ, если строка не найдена.</returns>
        /// <remarks>
        /// Метод автоматически инициализирует менеджер, если он еще не инициализирован.
        /// Поддерживает форматирование строк через string.Format.
        /// </remarks>
        /// <example>
        /// <code>
        /// var title = LocalizationManager.GetString("MainForm_Title");
        /// var message = LocalizationManager.GetString("MessageBox_Error", errorCount);
        /// </code>
        /// </example>
        public static string GetString(string key, params object[] args)
        {
            lock (_lock)
            {
                if (_resourceManager == null)
                {
                    Initialize();
                }

                try
                {
                    var value = _resourceManager?.GetString(key, _currentCulture);
                    if (string.IsNullOrEmpty(value))
                    {
                        // Если не найдено, пытаемся получить из английского
                        value = _resourceManager?.GetString(key, new CultureInfo("en"));
                        if (string.IsNullOrEmpty(value))
                        {
                            return key; // Возвращаем ключ, если не найдено
                        }
                    }

                    return args.Length > 0 ? string.Format(value, args) : value;
                }
                catch
                {
                    return key;
                }
            }
        }

        /// <summary>
        /// Получает список всех поддерживаемых языков приложения.
        /// </summary>
        /// <returns>Список из 50 языков, включая региональные варианты.</returns>
        /// <remarks>
        /// Возвращает список языков с их кодами, нативными названиями и английскими названиями.
        /// Включает основные языки и региональные варианты (pt-BR, zh-CN, zh-TW, es-MX, es-AR, fr-CA).
        /// </remarks>
        public static List<LanguageInfo> GetSupportedLanguages()
        {
            return new List<LanguageInfo>
            {
                new("en", "English", "English"),
                new("ru", "Русский", "Russian"),
                new("es", "Español", "Spanish"),
                new("fr", "Français", "French"),
                new("de", "Deutsch", "German"),
                new("it", "Italiano", "Italian"),
                new("pt", "Português", "Portuguese"),
                new("zh", "中文", "Chinese"),
                new("ja", "日本語", "Japanese"),
                new("ko", "한국어", "Korean"),
                new("ar", "العربية", "Arabic"),
                new("hi", "हिन्दी", "Hindi"),
                new("tr", "Türkçe", "Turkish"),
                new("pl", "Polski", "Polish"),
                new("nl", "Nederlands", "Dutch"),
                new("sv", "Svenska", "Swedish"),
                new("cs", "Čeština", "Czech"),
                new("uk", "Українська", "Ukrainian"),
                new("vi", "Tiếng Việt", "Vietnamese"),
                new("th", "ไทย", "Thai"),
                new("id", "Bahasa Indonesia", "Indonesian"),
                new("he", "עברית", "Hebrew"),
                new("ro", "Română", "Romanian"),
                new("hu", "Magyar", "Hungarian"),
                new("fi", "Suomi", "Finnish"),
                new("da", "Dansk", "Danish"),
                new("no", "Norsk", "Norwegian"),
                new("el", "Ελληνικά", "Greek"),
                new("bg", "Български", "Bulgarian"),
                new("hr", "Hrvatski", "Croatian"),
                new("sk", "Slovenčina", "Slovak"),
                new("sr", "Српски", "Serbian"),
                new("sl", "Slovenščina", "Slovenian"),
                new("et", "Eesti", "Estonian"),
                new("lv", "Latviešu", "Latvian"),
                new("lt", "Lietuvių", "Lithuanian"),
                new("mk", "Македонски", "Macedonian"),
                new("sq", "Shqip", "Albanian"),
                new("is", "Íslenska", "Icelandic"),
                new("ga", "Gaeilge", "Irish"),
                new("mt", "Malti", "Maltese"),
                new("cy", "Cymraeg", "Welsh"),
                new("eu", "Euskara", "Basque"),
                new("ca", "Català", "Catalan"),
                new("gl", "Galego", "Galician"),
                new("pt-BR", "Português (Brasil)", "Portuguese (Brazil)"),
                new("zh-CN", "中文 (简体)", "Chinese (Simplified)"),
                new("zh-TW", "中文 (繁體)", "Chinese (Traditional)"),
                new("es-MX", "Español (México)", "Spanish (Mexico)"),
                new("es-AR", "Español (Argentina)", "Spanish (Argentina)"),
                new("fr-CA", "Français (Canada)", "French (Canada)"),
            };
        }
    }

    /// <summary>
    /// Информация о языке локализации.
    /// Содержит код языка, нативное название и английское название.
    /// </summary>
    public class LanguageInfo
    {
        /// <summary>
        /// Получает код языка (например, "ru", "en", "pt-BR").
        /// </summary>
        public string Code { get; }
        
        /// <summary>
        /// Получает нативное название языка (например, "Русский", "English", "Português (Brasil)").
        /// </summary>
        public string NativeName { get; }
        
        /// <summary>
        /// Получает английское название языка (например, "Russian", "English", "Portuguese (Brazil)").
        /// </summary>
        public string EnglishName { get; }

        /// <summary>
        /// Инициализирует новый экземпляр класса LanguageInfo.
        /// </summary>
        /// <param name="code">Код языка (например, "ru", "en", "pt-BR").</param>
        /// <param name="nativeName">Нативное название языка.</param>
        /// <param name="englishName">Английское название языка.</param>
        public LanguageInfo(string code, string nativeName, string englishName)
        {
            Code = code;
            NativeName = nativeName;
            EnglishName = englishName;
        }

        /// <summary>
        /// Возвращает строковое представление информации о языке.
        /// </summary>
        /// <returns>Строка в формате "Нативное название (Английское название)" (например, "Русский (Russian)").</returns>
        public override string ToString() => $"{NativeName} ({EnglishName})";
    }
}

