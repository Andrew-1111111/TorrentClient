using System.IO;
using System.Text;
using TorrentClient.Utilities;
using Xunit;

namespace TorrentClient.Tests;

public class BencodeParserTests
{
    private readonly BencodeParser _parser = new();

    /// <summary>
    /// Проверяет, что парсер корректно разбирает простую Bencode строку формата "длина:строка"
    /// </summary>
    [Fact]
    public void ParseString_SimpleString_ReturnsCorrectValue()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("4:test");

        // Act
        var result = _parser.Parse<BString>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test", result.ToString());
    }

    /// <summary>
    /// Проверяет, что парсер корректно обрабатывает пустую Bencode строку
    /// </summary>
    [Fact]
    public void ParseString_EmptyString_ReturnsEmpty()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("0:");

        // Act
        var result = _parser.Parse<BString>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("", result.ToString());
    }

    /// <summary>
    /// Проверяет, что парсер корректно обрабатывает длинные строки (1000 символов)
    /// </summary>
    [Fact]
    public void ParseString_LongString_ReturnsCorrectValue()
    {
        // Arrange
        var longString = new string('a', 1000);
        var data = Encoding.UTF8.GetBytes($"{longString.Length}:{longString}");

        // Act
        var result = _parser.Parse<BString>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(longString, result.ToString());
    }

    /// <summary>
    /// Проверяет, что парсер корректно разбирает положительное число в формате Bencode
    /// </summary>
    [Fact]
    public void ParseNumber_PositiveNumber_ReturnsCorrectValue()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("i42e");

        // Act
        var result = _parser.Parse<BNumber>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(42, result.Value);
    }

    /// <summary>
    /// Проверяет, что парсер корректно разбирает отрицательное число в формате Bencode
    /// </summary>
    [Fact]
    public void ParseNumber_NegativeNumber_ReturnsCorrectValue()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("i-42e");

        // Act
        var result = _parser.Parse<BNumber>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(-42, result.Value);
    }

    /// <summary>
    /// Проверяет, что парсер корректно обрабатывает ноль в формате Bencode
    /// </summary>
    [Fact]
    public void ParseNumber_Zero_ReturnsZero()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("i0e");

        // Act
        var result = _parser.Parse<BNumber>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Value);
    }

    /// <summary>
    /// Проверяет, что парсер корректно обрабатывает пустой Bencode список
    /// </summary>
    [Fact]
    public void ParseList_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("le");

        // Act
        var result = _parser.Parse<BList>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    /// <summary>
    /// Проверяет, что парсер корректно разбирает Bencode список, содержащий строки
    /// </summary>
    [Fact]
    public void ParseList_ListWithStrings_ReturnsCorrectList()
    {
        // Arrange
        var blist = new BList
        {
            new BString("test"),
            new BString("hello"),
            new BString("bye")
        };
        var data = blist.EncodeAsBytes();

        // Act
        var result = _parser.Parse<BList>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("test", result[0].ToString());
        Assert.Equal("hello", result[1].ToString());
        Assert.Equal("bye", result[2].ToString());
    }

    /// <summary>
    /// Проверяет, что парсер корректно разбирает Bencode список, содержащий числа
    /// </summary>
    [Fact]
    public void ParseList_ListWithNumbers_ReturnsCorrectList()
    {
        // Arrange
        var blist = new BList
        {
            new BNumber(1),
            new BNumber(2),
            new BNumber(3)
        };
        var data = blist.EncodeAsBytes();

        // Act
        var result = _parser.Parse<BList>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(1, ((BNumber)result[0]).Value);
        Assert.Equal(2, ((BNumber)result[1]).Value);
        Assert.Equal(3, ((BNumber)result[2]).Value);
    }

    /// <summary>
    /// Проверяет, что парсер корректно обрабатывает пустой Bencode словарь
    /// </summary>
    [Fact]
    public void ParseDictionary_EmptyDictionary_ReturnsEmptyDictionary()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("de");

        // Act
        var result = _parser.Parse<BDictionary>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    /// <summary>
    /// Проверяет, что парсер корректно разбирает простой Bencode словарь с ключами и значениями
    /// </summary>
    [Fact]
    public void ParseDictionary_SimpleDictionary_ReturnsCorrectDictionary()
    {
        // Arrange
        var bdict = new BDictionary
        {
            ["name"] = new BString("test"),
            ["age"] = new BNumber(25)
        };
        var data = bdict.EncodeAsBytes();

        // Act
        var result = _parser.Parse<BDictionary>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("name"));
        Assert.True(result.ContainsKey("age"));
        Assert.Equal("test", result["name"].ToString());
        Assert.Equal(25, ((BNumber)result["age"]).Value);
    }

    /// <summary>
    /// Проверяет, что парсер корректно разбирает вложенные Bencode словари
    /// </summary>
    [Fact]
    public void ParseDictionary_NestedDictionary_ReturnsCorrectDictionary()
    {
        // Arrange
        var nestedDict = new BDictionary
        {
            ["name"] = new BString("test"),
            ["age"] = new BNumber(25)
        };
        var outerDict = new BDictionary
        {
            ["user"] = nestedDict
        };
        var data = outerDict.EncodeAsBytes();

        // Act
        var result = _parser.Parse<BDictionary>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.True(result.ContainsKey("user"));
        var userDict = (BDictionary)result["user"];
        Assert.Equal(2, userDict.Count);
        Assert.Equal("test", userDict["name"].ToString());
        Assert.Equal(25, ((BNumber)userDict["age"]).Value);
    }

    /// <summary>
    /// Проверяет, что парсер выбрасывает исключение при попытке разобрать невалидные данные
    /// </summary>
    [Fact]
    public void Parse_InvalidData_ThrowsException()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("xyz123");

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => _parser.Parse<BString>(data));
        Assert.NotNull(exception);
    }

    /// <summary>
    /// Проверяет, что парсер корректно обрабатывает максимальное значение long
    /// </summary>
    [Fact]
    public void ParseNumber_LargeNumber_ReturnsCorrectValue()
    {
        // Arrange
        var largeNumber = long.MaxValue;
        var data = Encoding.UTF8.GetBytes($"i{largeNumber}e");

        // Act
        var result = _parser.Parse<BNumber>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(largeNumber, result.Value);
    }

    /// <summary>
    /// Проверяет, что парсер корректно обрабатывает минимальное значение long
    /// </summary>
    [Fact]
    public void ParseNumber_SmallNumber_ReturnsCorrectValue()
    {
        // Arrange
        var smallNumber = long.MinValue;
        var data = Encoding.UTF8.GetBytes($"i{smallNumber}e");

        // Act
        var result = _parser.Parse<BNumber>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(smallNumber, result.Value);
    }

    /// <summary>
    /// Проверяет, что парсер корректно обрабатывает Unicode строки, включая эмодзи
    /// </summary>
    [Fact]
    public void ParseString_UnicodeString_ReturnsCorrectValue()
    {
        // Arrange
        var unicodeString = "Привет, мир! 🌍";
        var bstring = new BString(unicodeString);
        var data = bstring.EncodeAsBytes();

        // Act
        var result = _parser.Parse<BString>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(unicodeString, result.ToString());
    }

    /// <summary>
    /// Проверяет, что парсер корректно разбирает Bencode список, содержащий элементы разных типов (строки и числа)
    /// </summary>
    [Fact]
    public void ParseList_ListWithMixedTypes_ReturnsCorrectList()
    {
        // Arrange
        var blist = new BList
        {
            new BString("test"),
            new BNumber(42),
            new BString("hello"),
            new BNumber(-10)
        };
        var data = blist.EncodeAsBytes();

        // Act
        var result = _parser.Parse<BList>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Count);
        Assert.Equal("test", result[0].ToString());
        Assert.Equal(42, ((BNumber)result[1]).Value);
        Assert.Equal("hello", result[2].ToString());
        Assert.Equal(-10, ((BNumber)result[3]).Value);
    }

    /// <summary>
    /// Проверяет, что парсер корректно разбирает Bencode словарь, содержащий список в качестве значения
    /// </summary>
    [Fact]
    public void ParseDictionary_DictionaryWithList_ReturnsCorrectDictionary()
    {
        // Arrange
        var blist = new BList { new BString("item1"), new BString("item2") };
        var bdict = new BDictionary
        {
            ["list"] = blist,
            ["name"] = new BString("test")
        };
        var data = bdict.EncodeAsBytes();

        // Act
        var result = _parser.Parse<BDictionary>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("list"));
        Assert.True(result.ContainsKey("name"));
        var list = (BList)result["list"];
        Assert.Equal(2, list.Count);
    }

    /// <summary>
    /// Проверяет, что парсер выбрасывает исключение при несоответствии заявленной длины строки и фактической длины
    /// </summary>
    [Fact]
    public void ParseString_InvalidLength_ThrowsException()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("10:short");

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => _parser.Parse<BString>(data));
    }

    /// <summary>
    /// Проверяет, что парсер выбрасывает исключение при попытке разобрать число с невалидным форматом
    /// </summary>
    [Fact]
    public void ParseNumber_InvalidFormat_ThrowsException()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("iabcde");

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => _parser.Parse<BNumber>(data));
    }

    /// <summary>
    /// Проверяет, что парсер выбрасывает исключение при отсутствии маркера окончания числа 'e'
    /// </summary>
    [Fact]
    public void ParseNumber_NoEndMarker_ThrowsException()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("i42");

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => _parser.Parse<BNumber>(data));
    }

    /// <summary>
    /// Проверяет, что парсер корректно разбирает Bencode список, содержащий словарь в качестве элемента
    /// </summary>
    [Fact]
    public void ParseList_ListWithDictionary_ReturnsCorrectList()
    {
        // Arrange
        var nestedDict = new BDictionary { ["key"] = new BString("value") };
        var blist = new BList { nestedDict, new BString("test") };
        var data = blist.EncodeAsBytes();

        // Act
        var result = _parser.Parse<BList>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        var dict = (BDictionary)result[0];
        Assert.True(dict.ContainsKey("key"));
        Assert.Equal("value", dict["key"].ToString());
    }

    /// <summary>
    /// Проверяет, что парсер корректно разбирает сложную вложенную структуру (словарь со списком внутри другого словаря)
    /// </summary>
    [Fact]
    public void ParseDictionary_ComplexNestedStructure_ReturnsCorrectDictionary()
    {
        // Arrange
        var innerList = new BList { new BNumber(1), new BNumber(2) };
        var innerDict = new BDictionary { ["numbers"] = innerList };
        var outerDict = new BDictionary
        {
            ["inner"] = innerDict,
            ["name"] = new BString("test")
        };
        var data = outerDict.EncodeAsBytes();

        // Act
        var result = _parser.Parse<BDictionary>(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        var inner = (BDictionary)result["inner"];
        var numbers = (BList)inner["numbers"];
        Assert.Equal(2, numbers.Count);
    }
}

