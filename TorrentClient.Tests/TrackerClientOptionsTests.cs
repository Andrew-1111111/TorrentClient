using System.Collections.Generic;
using TorrentClient.Protocol;
using Xunit;

namespace TorrentClient.Tests;

public class TrackerClientOptionsTests
{
    /// <summary>
    /// Проверяет, что конструктор без параметров создает пустые словари для cookies и headers
    /// </summary>
    [Fact]
    public void Constructor_NoParameters_CreatesEmptyDictionaries()
    {
        // Act
        var options = new TrackerClientOptions();

        // Assert
        Assert.NotNull(options.TrackerCookies);
        Assert.NotNull(options.TrackerHeaders);
        Assert.Empty(options.TrackerCookies);
        Assert.Empty(options.TrackerHeaders);
    }

    /// <summary>
    /// Проверяет, что конструктор с cookies создает словарь без учета регистра ключей
    /// </summary>
    [Fact]
    public void Constructor_WithCookies_CreatesCaseInsensitiveDictionary()
    {
        // Arrange
        var cookies = new Dictionary<string, string>
        {
            ["http://tracker1.com"] = "cookie1=value1",
            ["HTTP://TRACKER2.COM"] = "cookie2=value2"
        };

        // Act
        var options = new TrackerClientOptions(trackerCookies: cookies);

        // Assert
        Assert.Equal(2, options.TrackerCookies.Count);
        Assert.True(options.TrackerCookies.ContainsKey("http://tracker1.com"));
        Assert.True(options.TrackerCookies.ContainsKey("HTTP://TRACKER1.COM")); // Case-insensitive
        Assert.Equal("cookie1=value1", options.TrackerCookies["http://tracker1.com"]);
    }

    /// <summary>
    /// Проверяет, что конструктор с headers создает словарь без учета регистра ключей
    /// </summary>
    [Fact]
    public void Constructor_WithHeaders_CreatesCaseInsensitiveDictionary()
    {
        // Arrange
        var headers = new Dictionary<string, Dictionary<string, string>>
        {
            ["http://tracker1.com"] = new Dictionary<string, string>
            {
                ["User-Agent"] = "TestAgent",
                ["Authorization"] = "Bearer token"
            },
            ["HTTP://TRACKER2.COM"] = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json"
            }
        };

        // Act
        var options = new TrackerClientOptions(trackerHeaders: headers);

        // Assert
        Assert.Equal(2, options.TrackerHeaders.Count);
        Assert.True(options.TrackerHeaders.ContainsKey("http://tracker1.com"));
        Assert.True(options.TrackerHeaders.ContainsKey("HTTP://TRACKER1.COM")); // Case-insensitive
        
        var tracker1Headers = options.TrackerHeaders["http://tracker1.com"];
        Assert.True(tracker1Headers.ContainsKey("User-Agent"));
        Assert.True(tracker1Headers.ContainsKey("user-agent")); // Case-insensitive
        Assert.Equal("TestAgent", tracker1Headers["User-Agent"]);
    }

    /// <summary>
    /// Проверяет, что конструктор с cookies и headers создает оба словаря
    /// </summary>
    [Fact]
    public void Constructor_WithBothCookiesAndHeaders_CreatesBothDictionaries()
    {
        // Arrange
        var cookies = new Dictionary<string, string>
        {
            ["http://tracker1.com"] = "cookie1=value1"
        };
        var headers = new Dictionary<string, Dictionary<string, string>>
        {
            ["http://tracker1.com"] = new Dictionary<string, string>
            {
                ["User-Agent"] = "TestAgent"
            }
        };

        // Act
        var options = new TrackerClientOptions(cookies, headers);

        // Assert
        Assert.Single(options.TrackerCookies);
        Assert.Single(options.TrackerHeaders);
        Assert.True(options.TrackerCookies.ContainsKey("http://tracker1.com"));
        Assert.True(options.TrackerHeaders.ContainsKey("http://tracker1.com"));
    }

    /// <summary>
    /// Проверяет, что вложенные headers также не учитывают регистр ключей
    /// </summary>
    [Fact]
    public void Constructor_NestedHeadersAreCaseInsensitive()
    {
        // Arrange
        var headers = new Dictionary<string, Dictionary<string, string>>
        {
            ["http://tracker1.com"] = new Dictionary<string, string>
            {
                ["User-Agent"] = "TestAgent"
            }
        };

        // Act
        var options = new TrackerClientOptions(trackerHeaders: headers);

        // Assert
        var trackerHeaders = options.TrackerHeaders["http://tracker1.com"];
        Assert.True(trackerHeaders.ContainsKey("user-agent")); // Case-insensitive
        Assert.True(trackerHeaders.ContainsKey("USER-AGENT")); // Case-insensitive
        Assert.Equal("TestAgent", trackerHeaders["user-agent"]);
    }
}

