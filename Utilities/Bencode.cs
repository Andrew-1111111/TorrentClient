using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TorrentClient.Utilities
{
    /// <summary>
    /// Базовый класс для Bencode объектов
    /// </summary>
    public abstract class BObject
    {
        /// <summary>Кодирует объект в Bencode формат</summary>
        public abstract byte[] EncodeAsBytes();
        
        /// <summary>Кодирует объект в Bencode строку</summary>
        public string Encode() => Encoding.UTF8.GetString(EncodeAsBytes());
    }

    /// <summary>
    /// Bencode строка (может содержать бинарные данные)
    /// </summary>
    public class BString : BObject
    {
        /// <summary>Значение как массив байт</summary>
        public ReadOnlyMemory<byte> Value { get; }

        /// <summary>
        /// Инициализирует новый экземпляр BString из массива байт
        /// </summary>
        /// <param name="value">Массив байт</param>
        public BString(byte[] value)
        {
            Value = value ?? Array.Empty<byte>();
        }

        /// <summary>
        /// Инициализирует новый экземпляр BString из строки
        /// </summary>
        /// <param name="value">Строка (будет преобразована в UTF-8 байты)</param>
        public BString(string value)
        {
            Value = Encoding.UTF8.GetBytes(value ?? string.Empty);
        }

        /// <summary>
        /// Преобразует значение в строку UTF-8
        /// </summary>
        /// <returns>Строковое представление значения</returns>
        public override string ToString() => Encoding.UTF8.GetString(Value.Span);

        /// <summary>
        /// Кодирует BString в формат Bencode
        /// </summary>
        /// <returns>Массив байт в формате Bencode</returns>
        public override byte[] EncodeAsBytes()
        {
            var lengthStr = Value.Length.ToString();
            var result = new byte[lengthStr.Length + 1 + Value.Length];
            Encoding.ASCII.GetBytes(lengthStr, result);
            result[lengthStr.Length] = (byte)':';
            Value.Span.CopyTo(result.AsSpan(lengthStr.Length + 1));
            return result;
        }

        /// <summary>
        /// Неявное преобразование строки в BString
        /// </summary>
        /// <param name="value">Строка для преобразования</param>
        public static implicit operator BString(string value) => new BString(value);
        
        /// <summary>
        /// Неявное преобразование массива байт в BString
        /// </summary>
        /// <param name="value">Массив байт для преобразования</param>
        public static implicit operator BString(byte[] value) => new BString(value);
    }

    /// <summary>
    /// Bencode число (целое)
    /// </summary>
    public class BNumber : BObject
    {
        /// <summary>Значение</summary>
        public long Value { get; }

        /// <summary>
        /// Инициализирует новый экземпляр BNumber
        /// </summary>
        /// <param name="value">Числовое значение</param>
        public BNumber(long value)
        {
            Value = value;
        }

        /// <summary>
        /// Преобразует значение в строку
        /// </summary>
        /// <returns>Строковое представление числа</returns>
        public override string ToString() => Value.ToString();

        /// <summary>
        /// Кодирует BNumber в формат Bencode
        /// </summary>
        /// <returns>Массив байт в формате Bencode</returns>
        public override byte[] EncodeAsBytes()
        {
            var valueStr = Value.ToString();
            var result = new byte[2 + valueStr.Length]; // i + число + e
            result[0] = (byte)'i';
            Encoding.ASCII.GetBytes(valueStr, result.AsSpan(1));
            result[^1] = (byte)'e';
            return result;
        }

        /// <summary>
        /// Неявное преобразование long в BNumber
        /// </summary>
        /// <param name="value">Число для преобразования</param>
        public static implicit operator BNumber(long value) => new BNumber(value);
        
        /// <summary>
        /// Неявное преобразование int в BNumber
        /// </summary>
        /// <param name="value">Число для преобразования</param>
        public static implicit operator BNumber(int value) => new BNumber(value);
    }

    /// <summary>
    /// Bencode список
    /// </summary>
    public class BList : BObject, IList<BObject>
    {
        private readonly List<BObject> _items = new();

        /// <summary>
        /// Инициализирует новый пустой экземпляр BList
        /// </summary>
        public BList() { }

        /// <summary>
        /// Инициализирует новый экземпляр BList с элементами
        /// </summary>
        /// <param name="items">Коллекция элементов для добавления</param>
        public BList(IEnumerable<BObject> items)
        {
            _items.AddRange(items);
        }

        public BObject this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }

        public int Count => _items.Count;
        public bool IsReadOnly => false;

        public void Add(BObject item) => _items.Add(item);
        public void Clear() => _items.Clear();
        public bool Contains(BObject item) => _items.Contains(item);
        public void CopyTo(BObject[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
        public IEnumerator<BObject> GetEnumerator() => _items.GetEnumerator();
        public int IndexOf(BObject item) => _items.IndexOf(item);
        public void Insert(int index, BObject item) => _items.Insert(index, item);
        public bool Remove(BObject item) => _items.Remove(item);
        public void RemoveAt(int index) => _items.RemoveAt(index);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Кодирует BList в формат Bencode
        /// </summary>
        /// <returns>Массив байт в формате Bencode</returns>
        public override byte[] EncodeAsBytes()
        {
            using var ms = new MemoryStream();
            ms.WriteByte((byte)'l');
            foreach (var item in _items)
            {
                var encoded = item.EncodeAsBytes();
                ms.Write(encoded, 0, encoded.Length);
            }
            ms.WriteByte((byte)'e');
            return ms.ToArray();
        }
    }

    /// <summary>
    /// Bencode словарь
    /// </summary>
    public class BDictionary : BObject, IDictionary<string, BObject>
    {
        private readonly Dictionary<string, BObject> _dict = new();

        public BObject this[string key]
        {
            get => _dict[key];
            set => _dict[key] = value;
        }

        public ICollection<string> Keys => _dict.Keys;
        public ICollection<BObject> Values => _dict.Values;
        public int Count => _dict.Count;
        public bool IsReadOnly => false;

        public void Add(string key, BObject value) => _dict.Add(key, value);
        public void Add(KeyValuePair<string, BObject> item) => _dict.Add(item.Key, item.Value);
        public void Clear() => _dict.Clear();
        public bool Contains(KeyValuePair<string, BObject> item) => _dict.ContainsKey(item.Key) && _dict[item.Key] == item.Value;
        public bool ContainsKey(string key) => _dict.ContainsKey(key);
        public void CopyTo(KeyValuePair<string, BObject>[] array, int arrayIndex)
        {
            var i = arrayIndex;
            foreach (var kvp in _dict)
                array[i++] = kvp;
        }
        public IEnumerator<KeyValuePair<string, BObject>> GetEnumerator() => _dict.GetEnumerator();
        public bool Remove(string key) => _dict.Remove(key);
        public bool Remove(KeyValuePair<string, BObject> item) => _dict.Remove(item.Key);
        public bool TryGetValue(string key, out BObject value) => _dict.TryGetValue(key, out value!);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Кодирует BDictionary в формат Bencode (ключи сортируются в лексикографическом порядке)
        /// </summary>
        /// <returns>Массив байт в формате Bencode</returns>
        public override byte[] EncodeAsBytes()
        {
            using var ms = new MemoryStream();
            ms.WriteByte((byte)'d');
            
            // Ключи должны быть отсортированы
            var sortedKeys = new List<string>(_dict.Keys);
            sortedKeys.Sort(StringComparer.Ordinal);
            
            foreach (var key in sortedKeys)
            {
                // Кодируем ключ как BString
                var keyBytes = new BString(key).EncodeAsBytes();
                ms.Write(keyBytes, 0, keyBytes.Length);
                
                // Кодируем значение
                var valueBytes = _dict[key].EncodeAsBytes();
                ms.Write(valueBytes, 0, valueBytes.Length);
            }
            
            ms.WriteByte((byte)'e');
            return ms.ToArray();
        }
    }

    /// <summary>
    /// Парсер Bencode данных
    /// </summary>
    public class BencodeParser
    {
        /// <summary>Парсит Bencode данные из файла</summary>
        /// <typeparam name="T">Тип Bencode объекта для парсинга</typeparam>
        /// <param name="filePath">Путь к файлу с Bencode данными</param>
        /// <returns>Распарсенный Bencode объект</returns>
        /// <exception cref="FileNotFoundException">Если файл не найден</exception>
        /// <exception cref="InvalidDataException">Если данные имеют неверный формат</exception>
        public T Parse<T>(string filePath) where T : BObject
        {
            var data = File.ReadAllBytes(filePath);
            return Parse<T>(data);
        }

        /// <summary>Парсит Bencode данные из потока</summary>
        /// <typeparam name="T">Тип Bencode объекта для парсинга</typeparam>
        /// <param name="stream">Поток с Bencode данными</param>
        /// <returns>Распарсенный Bencode объект</returns>
        /// <exception cref="InvalidDataException">Если данные имеют неверный формат</exception>
        public T Parse<T>(Stream stream) where T : BObject
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return Parse<T>(ms.ToArray());
        }

        /// <summary>Парсит Bencode данные из массива байт</summary>
        /// <typeparam name="T">Тип Bencode объекта для парсинга</typeparam>
        /// <param name="data">Массив байт с Bencode данными</param>
        /// <returns>Распарсенный Bencode объект</returns>
        /// <exception cref="InvalidDataException">Если данные имеют неверный формат</exception>
        public T Parse<T>(byte[] data) where T : BObject
        {
            var index = 0;
            var result = ParseObject(data, ref index);
            
            if (result is not T typed)
            {
                throw new InvalidDataException($"Ожидался тип {typeof(T).Name}, получен {result?.GetType().Name ?? "null"}");
            }
            
            return typed;
        }

        /// <summary>Парсит Bencode объект</summary>
        private BObject ParseObject(byte[] data, ref int index)
        {
            if (index >= data.Length)
                throw new InvalidDataException("Неожиданный конец данных");

            var b = data[index];

            return b switch
            {
                (byte)'d' => ParseDictionary(data, ref index),
                (byte)'l' => ParseList(data, ref index),
                (byte)'i' => ParseNumber(data, ref index),
                >= (byte)'0' and <= (byte)'9' => ParseString(data, ref index),
                _ => throw new InvalidDataException($"Неожиданный символ '{(char)b}' на позиции {index}")
            };
        }

        /// <summary>Парсит Bencode словарь</summary>
        private BDictionary ParseDictionary(byte[] data, ref int index)
        {
            if (data[index] != 'd')
                throw new InvalidDataException($"Ожидался 'd', получен '{(char)data[index]}'");
            
            index++; // Пропускаем 'd'
            var dict = new BDictionary();

            while (index < data.Length && data[index] != 'e')
            {
                // Ключ должен быть строкой
                var key = ParseString(data, ref index);
                var value = ParseObject(data, ref index);
                dict[key.ToString()] = value;
            }

            if (index >= data.Length)
                throw new InvalidDataException("Неожиданный конец словаря");
            
            index++; // Пропускаем 'e'
            return dict;
        }

        /// <summary>Парсит Bencode список</summary>
        private BList ParseList(byte[] data, ref int index)
        {
            if (data[index] != 'l')
                throw new InvalidDataException($"Ожидался 'l', получен '{(char)data[index]}'");
            
            index++; // Пропускаем 'l'
            var list = new BList();

            while (index < data.Length && data[index] != 'e')
            {
                list.Add(ParseObject(data, ref index));
            }

            if (index >= data.Length)
                throw new InvalidDataException("Неожиданный конец списка");
            
            index++; // Пропускаем 'e'
            return list;
        }

        /// <summary>Парсит Bencode число</summary>
        private BNumber ParseNumber(byte[] data, ref int index)
        {
            if (data[index] != 'i')
                throw new InvalidDataException($"Ожидался 'i', получен '{(char)data[index]}'");
            
            index++; // Пропускаем 'i'
            var start = index;

            while (index < data.Length && data[index] != 'e')
            {
                index++;
            }

            if (index >= data.Length)
                throw new InvalidDataException("Неожиданный конец числа");

            var numberStr = Encoding.ASCII.GetString(data, start, index - start);
            
            if (!long.TryParse(numberStr, out var value))
                throw new InvalidDataException($"Неверный формат числа: {numberStr}");
            
            index++; // Пропускаем 'e'
            return new BNumber(value);
        }

        /// <summary>Парсит Bencode строку</summary>
        private BString ParseString(byte[] data, ref int index)
        {
            var start = index;

            // Читаем длину
            while (index < data.Length && data[index] != ':')
            {
                if (data[index] < '0' || data[index] > '9')
                    throw new InvalidDataException($"Неожиданный символ '{(char)data[index]}' в длине строки");
                index++;
            }

            if (index >= data.Length)
                throw new InvalidDataException("Неожиданный конец данных при чтении длины строки");

            var lengthStr = Encoding.ASCII.GetString(data, start, index - start);
            
            if (!int.TryParse(lengthStr, out var length))
                throw new InvalidDataException($"Неверная длина строки: {lengthStr}");
            
            index++; // Пропускаем ':'

            if (index + length > data.Length)
                throw new InvalidDataException($"Строка выходит за пределы данных: нужно {length} байт, доступно {data.Length - index}");

            var value = new byte[length];
            Array.Copy(data, index, value, 0, length);
            index += length;

            return new BString(value);
        }
    }
}

