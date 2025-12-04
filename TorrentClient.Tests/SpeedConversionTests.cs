using System;
using Xunit;

namespace TorrentClient.Tests;

/// <summary>
/// Тесты для конвертации скорости между различными единицами измерения
/// </summary>
public class SpeedConversionTests
{
    /// <summary>
    /// Проверяет, что конвертация 1 Mbps в байты/сек дает правильный результат (125,000 байт/сек)
    /// </summary>
    [Fact]
    public void MbpsToBytesPerSecond_1Mbps_Returns125000()
    {
        // Подготовка
        int mbps = 1;
        
        // Действие
        long bytesPerSecond = (long)(mbps * 1_000_000.0 / 8.0);
        
        // Проверка
        Assert.Equal(125_000, bytesPerSecond);
    }

    /// <summary>
    /// Проверяет, что конвертация 10 Mbps в байты/сек дает правильный результат (1,250,000 байт/сек)
    /// </summary>
    [Fact]
    public void MbpsToBytesPerSecond_10Mbps_Returns1250000()
    {
        // Подготовка
        int mbps = 10;
        
        // Действие
        long bytesPerSecond = (long)(mbps * 1_000_000.0 / 8.0);
        
        // Проверка
        Assert.Equal(1_250_000, bytesPerSecond);
    }

    /// <summary>
    /// Проверяет, что конвертация 100 Mbps в байты/сек дает правильный результат (12,500,000 байт/сек)
    /// </summary>
    [Fact]
    public void MbpsToBytesPerSecond_100Mbps_Returns12500000()
    {
        // Подготовка
        int mbps = 100;
        
        // Действие
        long bytesPerSecond = (long)(mbps * 1_000_000.0 / 8.0);
        
        // Проверка
        Assert.Equal(12_500_000, bytesPerSecond);
    }

    /// <summary>
    /// Проверяет, что конвертация байтов/сек в Mbps работает корректно для 125,000 байт/сек (1 Mbps)
    /// </summary>
    [Fact]
    public void BytesPerSecondToMbps_125000Bytes_Returns1Mbps()
    {
        // Подготовка
        long bytesPerSecond = 125_000;
        
        // Действие
        double mbps = bytesPerSecond * 8.0 / 1_000_000.0;
        
        // Проверка
        Assert.Equal(1.0, mbps, 1);
    }

    /// <summary>
    /// Проверяет, что конвертация байтов/сек в Mbps работает корректно для 1,250,000 байт/сек (10 Mbps)
    /// </summary>
    [Fact]
    public void BytesPerSecondToMbps_1250000Bytes_Returns10Mbps()
    {
        // Подготовка
        long bytesPerSecond = 1_250_000;
        
        // Действие
        double mbps = bytesPerSecond * 8.0 / 1_000_000.0;
        
        // Проверка
        Assert.Equal(10.0, mbps, 1);
    }

    /// <summary>
    /// Проверяет, что конвертация байтов/сек в Mbps работает корректно для 12,500,000 байт/сек (100 Mbps)
    /// </summary>
    [Fact]
    public void BytesPerSecondToMbps_12500000Bytes_Returns100Mbps()
    {
        // Подготовка
        long bytesPerSecond = 12_500_000;
        
        // Действие
        double mbps = bytesPerSecond * 8.0 / 1_000_000.0;
        
        // Проверка
        Assert.Equal(100.0, mbps, 1);
    }

    /// <summary>
    /// Проверяет, что обратная конвертация (Mbps → bytes/sec → Mbps) дает исходное значение
    /// </summary>
    [Fact]
    public void RoundTripConversion_1Mbps_ReturnsOriginalValue()
    {
        // Подготовка
        int originalMbps = 1;
        
        // Действие - конвертируем туда и обратно
        long bytesPerSecond = (long)(originalMbps * 1_000_000.0 / 8.0);
        double mbps = bytesPerSecond * 8.0 / 1_000_000.0;
        
        // Проверка
        Assert.Equal(1.0, mbps, 1);
    }

    /// <summary>
    /// Проверяет, что обратная конвертация (Mbps → bytes/sec → Mbps) работает для 50 Mbps
    /// </summary>
    [Fact]
    public void RoundTripConversion_50Mbps_ReturnsOriginalValue()
    {
        // Подготовка
        int originalMbps = 50;
        
        // Действие - конвертируем туда и обратно
        long bytesPerSecond = (long)(originalMbps * 1_000_000.0 / 8.0);
        double mbps = bytesPerSecond * 8.0 / 1_000_000.0;
        
        // Проверка
        Assert.Equal(50.0, mbps, 1);
    }

    /// <summary>
    /// Проверяет, что обратная конвертация (Mbps → bytes/sec → Mbps) работает для 1000 Mbps
    /// </summary>
    [Fact]
    public void RoundTripConversion_1000Mbps_ReturnsOriginalValue()
    {
        // Подготовка
        int originalMbps = 1000;
        
        // Действие - конвертируем туда и обратно
        long bytesPerSecond = (long)(originalMbps * 1_000_000.0 / 8.0);
        double mbps = bytesPerSecond * 8.0 / 1_000_000.0;
        
        // Проверка
        Assert.Equal(1000.0, mbps, 1);
    }

    /// <summary>
    /// Проверяет, что конвертация малых значений (0.5 Mbps) работает корректно
    /// </summary>
    [Fact]
    public void BytesPerSecondToMbps_SmallValue_ReturnsCorrectMbps()
    {
        // Подготовка
        long bytesPerSecond = 62_500; // 0.5 Mbps
        
        // Действие
        double mbps = bytesPerSecond * 8.0 / 1_000_000.0;
        
        // Проверка
        Assert.Equal(0.5, mbps, 1);
    }

    /// <summary>
    /// Проверяет, что конвертация очень больших значений (10,000 Mbps) работает корректно
    /// </summary>
    [Fact]
    public void MbpsToBytesPerSecond_VeryLargeValue_ReturnsCorrectBytes()
    {
        // Подготовка
        int mbps = 10_000;
        
        // Действие
        long bytesPerSecond = (long)(mbps * 1_000_000.0 / 8.0);
        
        // Проверка
        Assert.Equal(1_250_000_000, bytesPerSecond);
    }

    /// <summary>
    /// Проверяет, что конвертация нулевого значения работает корректно
    /// </summary>
    [Fact]
    public void MbpsToBytesPerSecond_Zero_ReturnsZero()
    {
        // Подготовка
        int mbps = 0;
        
        // Действие
        long bytesPerSecond = (long)(mbps * 1_000_000.0 / 8.0);
        
        // Проверка
        Assert.Equal(0, bytesPerSecond);
    }

    /// <summary>
    /// Проверяет, что конвертация нулевого значения байтов/сек в Mbps дает 0
    /// </summary>
    [Fact]
    public void BytesPerSecondToMbps_Zero_ReturnsZero()
    {
        // Подготовка
        long bytesPerSecond = 0;
        
        // Действие
        double mbps = bytesPerSecond * 8.0 / 1_000_000.0;
        
        // Проверка
        Assert.Equal(0.0, mbps);
    }
}


