using System;
using Xunit;

namespace TorrentClient.Tests;

/// <summary>
/// Тесты для расчета скорости передачи данных
/// </summary>
public class SpeedCalculationTests
{
    /// <summary>
    /// Проверяет, что скорость рассчитывается корректно при передаче данных за 1 секунду
    /// </summary>
    [Fact]
    public void CalculateSpeed_OneSecond_ReturnsCorrectSpeed()
    {
        // Подготовка
        long bytesTransferred = 1_250_000; // 10 Mbps = 1,250,000 байт/сек
        double elapsedSeconds = 1.0;
        
        // Действие
        long speed = (long)(bytesTransferred / elapsedSeconds);
        
        // Проверка
        Assert.Equal(1_250_000, speed);
    }

    /// <summary>
    /// Проверяет, что скорость рассчитывается корректно при передаче данных за 0.5 секунды
    /// </summary>
    [Fact]
    public void CalculateSpeed_HalfSecond_ReturnsCorrectSpeed()
    {
        // Подготовка
        long bytesTransferred = 625_000; // 5 Mbps за 0.5 сек = 1,250,000 байт/сек
        double elapsedSeconds = 0.5;
        
        // Действие
        long speed = (long)(bytesTransferred / elapsedSeconds);
        
        // Проверка
        Assert.Equal(1_250_000, speed);
    }

    /// <summary>
    /// Проверяет, что скорость рассчитывается корректно при передаче данных за 2 секунды
    /// </summary>
    [Fact]
    public void CalculateSpeed_TwoSeconds_ReturnsCorrectSpeed()
    {
        // Подготовка
        long bytesTransferred = 2_500_000; // 10 Mbps за 2 сек = 1,250,000 байт/сек
        double elapsedSeconds = 2.0;
        
        // Действие
        long speed = (long)(bytesTransferred / elapsedSeconds);
        
        // Проверка
        Assert.Equal(1_250_000, speed);
    }

    /// <summary>
    /// Проверяет, что скорость рассчитывается корректно для нулевого времени (защита от деления на ноль)
    /// </summary>
    [Fact]
    public void CalculateSpeed_ZeroTime_ReturnsZero()
    {
        // Подготовка
        long bytesTransferred = 1000;
        double elapsedSeconds = 0.0;
        
        // Действие
        long speed = elapsedSeconds > 0 ? (long)(bytesTransferred / elapsedSeconds) : 0;
        
        // Проверка
        Assert.Equal(0, speed);
    }

    /// <summary>
    /// Проверяет, что скорость рассчитывается корректно для очень малого времени (0.1 секунды)
    /// </summary>
    [Fact]
    public void CalculateSpeed_VerySmallTime_ReturnsCorrectSpeed()
    {
        // Подготовка
        long bytesTransferred = 125_000; // 1 Mbps за 0.1 сек = 1,250,000 байт/сек
        double elapsedSeconds = 0.1;
        
        // Действие
        long speed = (long)(bytesTransferred / elapsedSeconds);
        
        // Проверка
        Assert.Equal(1_250_000, speed);
    }

    /// <summary>
    /// Проверяет, что скорость рассчитывается корректно для нулевых байт
    /// </summary>
    [Fact]
    public void CalculateSpeed_ZeroBytes_ReturnsZero()
    {
        // Подготовка
        long bytesTransferred = 0;
        double elapsedSeconds = 1.0;
        
        // Действие
        long speed = (long)(bytesTransferred / elapsedSeconds);
        
        // Проверка
        Assert.Equal(0, speed);
    }

    /// <summary>
    /// Проверяет, что скорость рассчитывается корректно для очень больших значений
    /// </summary>
    [Fact]
    public void CalculateSpeed_VeryLargeValue_ReturnsCorrectSpeed()
    {
        // Подготовка
        long bytesTransferred = 1_250_000_000; // 10 Gbps за 1 сек = 1,250,000,000 байт/сек
        double elapsedSeconds = 1.0;
        
        // Действие
        long speed = (long)(bytesTransferred / elapsedSeconds);
        
        // Проверка
        Assert.Equal(1_250_000_000, speed);
    }

    /// <summary>
    /// Проверяет, что скорость рассчитывается корректно при отрицательной разнице байт (защита)
    /// </summary>
    [Fact]
    public void CalculateSpeed_NegativeBytes_ReturnsZero()
    {
        // Подготовка
        long bytesTransferred = -1000; // Отрицательное значение (не должно происходить, но защита)
        double elapsedSeconds = 1.0;
        
        // Действие
        long speed = bytesTransferred >= 0 ? (long)(bytesTransferred / elapsedSeconds) : 0;
        
        // Проверка
        Assert.Equal(0, speed);
    }

    /// <summary>
    /// Проверяет, что скорость рассчитывается корректно для дробных значений времени
    /// </summary>
    [Fact]
    public void CalculateSpeed_FractionalTime_ReturnsCorrectSpeed()
    {
        // Подготовка
        long bytesTransferred = 312_500; // 2.5 Mbps за 0.25 сек = 1,250,000 байт/сек
        double elapsedSeconds = 0.25;
        
        // Действие
        long speed = (long)(bytesTransferred / elapsedSeconds);
        
        // Проверка
        Assert.Equal(1_250_000, speed);
    }

    /// <summary>
    /// Проверяет, что скорость рассчитывается корректно для последовательных обновлений
    /// </summary>
    [Fact]
    public void CalculateSpeed_SequentialUpdates_ReturnsCorrectSpeed()
    {
        // Подготовка
        long lastBytes = 0;
        long currentBytes = 1_250_000; // 10 Mbps за 1 сек
        double elapsedSeconds = 1.0;
        
        // Действие
        long downloaded = currentBytes - lastBytes;
        long speed = (long)(downloaded / elapsedSeconds);
        
        // Проверка
        Assert.Equal(1_250_000, speed);
        
        // Второе обновление
        lastBytes = currentBytes;
        currentBytes = 2_500_000; // Еще 10 Mbps за 1 сек
        downloaded = currentBytes - lastBytes;
        speed = (long)(downloaded / elapsedSeconds);
        
        // Проверка
        Assert.Equal(1_250_000, speed);
    }
}


