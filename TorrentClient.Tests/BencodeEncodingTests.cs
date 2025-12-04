using System.Linq;
using System.Text;
using TorrentClient.Utilities;
using Xunit;

namespace TorrentClient.Tests;

public class BencodeEncodingTests
{
    /// <summary>
    /// Проверяет, что BString корректно кодирует простую строку в формат Bencode
    /// </summary>
    [Fact]
    public void BString_Encode_SimpleString_ReturnsCorrectBencode()
    {
        // Arrange
        var bstring = new BString("test");

        // Act
        var encoded = bstring.EncodeAsBytes();
        var encodedStr = Encoding.UTF8.GetString(encoded);

        // Assert
        Assert.Equal("4:test", encodedStr);
    }

    /// <summary>
    /// Проверяет, что BString корректно кодирует пустую строку в формат Bencode
    /// </summary>
    [Fact]
    public void BString_Encode_EmptyString_ReturnsCorrectBencode()
    {
        // Arrange
        var bstring = new BString("");

        // Act
        var encoded = bstring.EncodeAsBytes();
        var encodedStr = Encoding.UTF8.GetString(encoded);

        // Assert
        Assert.Equal("0:", encodedStr);
    }

    /// <summary>
    /// Проверяет, что кодирование и декодирование BString работает корректно (round-trip)
    /// </summary>
    [Fact]
    public void BString_EncodeDecode_RoundTrip_Works()
    {
        // Arrange
        var original = "Hello, World!";
        var bstring = new BString(original);

        // Act
        var encoded = bstring.EncodeAsBytes();
        var parser = new BencodeParser();
        var decoded = parser.Parse<BString>(encoded);

        // Assert
        Assert.Equal(original, decoded.ToString());
    }

    /// <summary>
    /// Проверяет, что BNumber корректно кодирует положительное число в формат Bencode
    /// </summary>
    [Fact]
    public void BNumber_Encode_PositiveNumber_ReturnsCorrectBencode()
    {
        // Arrange
        var bnumber = new BNumber(42);

        // Act
        var encoded = bnumber.EncodeAsBytes();
        var encodedStr = Encoding.UTF8.GetString(encoded);

        // Assert
        Assert.Equal("i42e", encodedStr);
    }

    /// <summary>
    /// Проверяет, что BNumber корректно кодирует отрицательное число в формат Bencode
    /// </summary>
    [Fact]
    public void BNumber_Encode_NegativeNumber_ReturnsCorrectBencode()
    {
        // Arrange
        var bnumber = new BNumber(-42);

        // Act
        var encoded = bnumber.EncodeAsBytes();
        var encodedStr = Encoding.UTF8.GetString(encoded);

        // Assert
        Assert.Equal("i-42e", encodedStr);
    }

    /// <summary>
    /// Проверяет, что кодирование и декодирование BNumber работает корректно (round-trip)
    /// </summary>
    [Fact]
    public void BNumber_EncodeDecode_RoundTrip_Works()
    {
        // Arrange
        var original = 12345L;
        var bnumber = new BNumber(original);

        // Act
        var encoded = bnumber.EncodeAsBytes();
        var parser = new BencodeParser();
        var decoded = parser.Parse<BNumber>(encoded);

        // Assert
        Assert.Equal(original, decoded.Value);
    }

    /// <summary>
    /// Проверяет, что BList корректно кодирует пустой список в формат Bencode
    /// </summary>
    [Fact]
    public void BList_Encode_EmptyList_ReturnsCorrectBencode()
    {
        // Arrange
        var blist = new BList();

        // Act
        var encoded = blist.EncodeAsBytes();
        var encodedStr = Encoding.UTF8.GetString(encoded);

        // Assert
        Assert.Equal("le", encodedStr);
    }

    /// <summary>
    /// Проверяет, что BList корректно кодирует список с элементами в формат Bencode
    /// </summary>
    [Fact]
    public void BList_Encode_ListWithItems_ReturnsCorrectBencode()
    {
        // Arrange
        var blist = new BList
        {
            new BString("test"),
            new BNumber(42)
        };

        // Act
        var encoded = blist.EncodeAsBytes();
        var encodedStr = Encoding.UTF8.GetString(encoded);

        // Assert
        Assert.Equal("l4:testi42ee", encodedStr);
    }

    /// <summary>
    /// Проверяет, что кодирование и декодирование BList работает корректно (round-trip)
    /// </summary>
    [Fact]
    public void BList_EncodeDecode_RoundTrip_Works()
    {
        // Arrange
        var blist = new BList
        {
            new BString("hello"),
            new BNumber(123),
            new BString("world")
        };

        // Act
        var encoded = blist.EncodeAsBytes();
        var parser = new BencodeParser();
        var decoded = parser.Parse<BList>(encoded);

        // Assert
        Assert.Equal(3, decoded.Count);
        Assert.Equal("hello", decoded[0].ToString());
        Assert.Equal(123, ((BNumber)decoded[1]).Value);
        Assert.Equal("world", decoded[2].ToString());
    }

    /// <summary>
    /// Проверяет, что BDictionary корректно кодирует пустой словарь в формат Bencode
    /// </summary>
    [Fact]
    public void BDictionary_Encode_EmptyDictionary_ReturnsCorrectBencode()
    {
        // Arrange
        var bdict = new BDictionary();

        // Act
        var encoded = bdict.EncodeAsBytes();
        var encodedStr = Encoding.UTF8.GetString(encoded);

        // Assert
        Assert.Equal("de", encodedStr);
    }

    /// <summary>
    /// Проверяет, что BDictionary корректно кодирует словарь с элементами в формат Bencode
    /// </summary>
    [Fact]
    public void BDictionary_Encode_DictionaryWithItems_ReturnsCorrectBencode()
    {
        // Arrange
        var bdict = new BDictionary
        {
            ["name"] = new BString("test"),
            ["age"] = new BNumber(25)
        };

        // Act
        var encoded = bdict.EncodeAsBytes();
        var parser = new BencodeParser();
        var decoded = parser.Parse<BDictionary>(encoded);

        // Assert
        Assert.Equal(2, decoded.Count);
        Assert.True(decoded.ContainsKey("age"));
        Assert.True(decoded.ContainsKey("name"));
        Assert.Equal("test", decoded["name"].ToString());
        Assert.Equal(25, ((BNumber)decoded["age"]).Value);
    }

    /// <summary>
    /// Проверяет, что кодирование и декодирование BDictionary работает корректно (round-trip)
    /// </summary>
    [Fact]
    public void BDictionary_EncodeDecode_RoundTrip_Works()
    {
        // Arrange
        var bdict = new BDictionary
        {
            ["name"] = new BString("John"),
            ["age"] = new BNumber(30),
            ["city"] = new BString("New York")
        };

        // Act
        var encoded = bdict.EncodeAsBytes();
        var parser = new BencodeParser();
        var decoded = parser.Parse<BDictionary>(encoded);

        // Assert
        Assert.Equal(3, decoded.Count);
        Assert.Equal("John", decoded["name"].ToString());
        Assert.Equal(30, ((BNumber)decoded["age"]).Value);
        Assert.Equal("New York", decoded["city"].ToString());
    }

    /// <summary>
    /// Проверяет, что BDictionary сортирует ключи в лексикографическом порядке при кодировании
    /// </summary>
    [Fact]
    public void BDictionary_Encode_KeysAreSorted()
    {
        // Arrange
        var bdict = new BDictionary
        {
            ["zebra"] = new BString("last"),
            ["apple"] = new BString("first"),
            ["banana"] = new BString("middle")
        };

        // Act
        var encoded = bdict.EncodeAsBytes();
        var encodedStr = Encoding.UTF8.GetString(encoded);

        // Assert
        var appleIndex = encodedStr.IndexOf("5:apple");
        var bananaIndex = encodedStr.IndexOf("6:banana");
        var zebraIndex = encodedStr.IndexOf("5:zebra");
        
        Assert.True(appleIndex >= 0, "Apple key not found");
        Assert.True(bananaIndex >= 0, "Banana key not found");
        Assert.True(zebraIndex >= 0, "Zebra key not found");
        Assert.True(appleIndex < bananaIndex, $"Expected apple before banana, but apple at {appleIndex}, banana at {bananaIndex}");
        Assert.True(bananaIndex < zebraIndex, $"Expected banana before zebra, but banana at {bananaIndex}, zebra at {zebraIndex}");
    }

    /// <summary>
    /// Проверяет, что BString корректно кодирует и декодирует Unicode строки, включая эмодзи
    /// </summary>
    [Fact]
    public void BString_Encode_UnicodeString_EncodesCorrectly()
    {
        // Arrange
        var unicodeString = "Привет, мир! 🌍";
        var bstring = new BString(unicodeString);

        // Act
        var encoded = bstring.EncodeAsBytes();
        var parser = new BencodeParser();
        var decoded = parser.Parse<BString>(encoded);

        // Assert
        Assert.Equal(unicodeString, decoded.ToString());
    }

    /// <summary>
    /// Проверяет, что BString корректно кодирует и декодирует бинарные данные
    /// </summary>
    [Fact]
    public void BString_Encode_BinaryData_EncodesCorrectly()
    {
        // Arrange
        var binaryData = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD };
        var bstring = new BString(binaryData);

        // Act
        var encoded = bstring.EncodeAsBytes();
        var parser = new BencodeParser();
        var decoded = parser.Parse<BString>(encoded);

        // Assert
        Assert.Equal(binaryData, decoded.Value.ToArray());
    }

    /// <summary>
    /// Проверяет, что BNumber корректно кодирует и декодирует максимальное значение long
    /// </summary>
    [Fact]
    public void BNumber_Encode_LargeNumber_EncodesCorrectly()
    {
        // Arrange
        var largeNumber = long.MaxValue;
        var bnumber = new BNumber(largeNumber);

        // Act
        var encoded = bnumber.EncodeAsBytes();
        var parser = new BencodeParser();
        var decoded = parser.Parse<BNumber>(encoded);

        // Assert
        Assert.Equal(largeNumber, decoded.Value);
    }

    /// <summary>
    /// Проверяет, что BNumber корректно кодирует и декодирует минимальное значение long
    /// </summary>
    [Fact]
    public void BNumber_Encode_SmallNumber_EncodesCorrectly()
    {
        // Arrange
        var smallNumber = long.MinValue;
        var bnumber = new BNumber(smallNumber);

        // Act
        var encoded = bnumber.EncodeAsBytes();
        var parser = new BencodeParser();
        var decoded = parser.Parse<BNumber>(encoded);

        // Assert
        Assert.Equal(smallNumber, decoded.Value);
    }

    /// <summary>
    /// Проверяет, что BList корректно кодирует и декодирует большой список (100 элементов)
    /// </summary>
    [Fact]
    public void BList_Encode_LargeList_EncodesCorrectly()
    {
        // Arrange
        var blist = new BList();
        for (int i = 0; i < 100; i++)
        {
            blist.Add(new BNumber(i));
        }

        // Act
        var encoded = blist.EncodeAsBytes();
        var parser = new BencodeParser();
        var decoded = parser.Parse<BList>(encoded);

        // Assert
        Assert.Equal(100, decoded.Count);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(i, ((BNumber)decoded[i]).Value);
        }
    }

    /// <summary>
    /// Проверяет, что BDictionary корректно кодирует и декодирует большой словарь (50 элементов)
    /// </summary>
    [Fact]
    public void BDictionary_Encode_LargeDictionary_EncodesCorrectly()
    {
        // Arrange
        var bdict = new BDictionary();
        for (int i = 0; i < 50; i++)
        {
            bdict[$"key{i}"] = new BString($"value{i}");
        }

        // Act
        var encoded = bdict.EncodeAsBytes();
        var parser = new BencodeParser();
        var decoded = parser.Parse<BDictionary>(encoded);

        // Assert
        Assert.Equal(50, decoded.Count);
        for (int i = 0; i < 50; i++)
        {
            Assert.True(decoded.ContainsKey($"key{i}"));
            Assert.Equal($"value{i}", decoded[$"key{i}"].ToString());
        }
    }

    /// <summary>
    /// Проверяет, что неявное преобразование string и byte[] в BString работает корректно
    /// </summary>
    [Fact]
    public void BString_ImplicitConversion_Works()
    {
        // Arrange
        string str = "test";
        byte[] bytes = new byte[] { 1, 2, 3 };

        // Act
        BString bstring1 = str;
        BString bstring2 = bytes;

        // Assert
        Assert.Equal("test", bstring1.ToString());
        Assert.Equal(bytes, bstring2.Value.ToArray());
    }

    /// <summary>
    /// Проверяет, что неявное преобразование long и int в BNumber работает корректно
    /// </summary>
    [Fact]
    public void BNumber_ImplicitConversion_Works()
    {
        // Arrange
        long longValue = 42L;
        int intValue = 24;

        // Act
        BNumber bnumber1 = longValue;
        BNumber bnumber2 = intValue;

        // Assert
        Assert.Equal(42L, bnumber1.Value);
        Assert.Equal(24L, bnumber2.Value);
    }
}

