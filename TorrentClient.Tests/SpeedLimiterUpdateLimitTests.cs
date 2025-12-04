using System;
using System.Threading.Tasks;
using TorrentClient.Core;
using Xunit;

namespace TorrentClient.Tests;

/// <summary>
/// Тесты для метода UpdateLimit в SpeedLimiter
/// </summary>
public class SpeedLimiterUpdateLimitTests
{
    /// <summary>
    /// Проверяет, что UpdateLimit обновляет лимит скорости
    /// </summary>
    [Fact]
    public async Task UpdateLimit_ChangesLimit()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000); // Начальный лимит 1000 байт/сек
        
        // Действие
        limiter.UpdateLimit(2000); // Обновляем до 2000 байт/сек
        
        // Проверка - передаем 2000 байт, должно пройти без задержки
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(2000);
        var elapsed = DateTime.Now - startTime;
        
        Assert.True(elapsed.TotalMilliseconds < 500);
    }

    /// <summary>
    /// Проверяет, что UpdateLimit с null отключает лимит
    /// </summary>
    [Fact]
    public async Task UpdateLimit_Null_DisablesLimit()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000);
        
        // Действие
        limiter.UpdateLimit(null);
        
        // Проверка - передаем большое количество данных, не должно быть задержки
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(10_000);
        var elapsed = DateTime.Now - startTime;
        
        Assert.True(elapsed.TotalMilliseconds < 100);
    }

    /// <summary>
    /// Проверяет, что UpdateLimit с нулевым значением отключает лимит
    /// </summary>
    [Fact]
    public async Task UpdateLimit_Zero_DisablesLimit()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000);
        
        // Действие
        limiter.UpdateLimit(0);
        
        // Проверка
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(10_000);
        var elapsed = DateTime.Now - startTime;
        
        Assert.True(elapsed.TotalMilliseconds < 100);
    }

    /// <summary>
    /// Проверяет, что UpdateLimit сбрасывает счетчик при изменении лимита
    /// </summary>
    [Fact]
    public async Task UpdateLimit_ResetsCounter()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000);
        
        // Действие - используем весь лимит
        await limiter.WaitIfNeededAsync(1000);
        
        // Обновляем лимит - счетчик должен сброситься
        limiter.UpdateLimit(2000);
        
        // Проверка - после обновления можно сразу передать данные в пределах нового лимита
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(2000);
        var elapsed = DateTime.Now - startTime;
        
        Assert.True(elapsed.TotalMilliseconds < 500);
    }

    /// <summary>
    /// Проверяет, что UpdateLimit можно вызывать несколько раз подряд
    /// </summary>
    [Fact]
    public void UpdateLimit_MultipleCalls_WorksCorrectly()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000);
        
        // Действие
        limiter.UpdateLimit(2000);
        limiter.UpdateLimit(3000);
        limiter.UpdateLimit(4000);
        
        // Проверка - не должно быть исключений
        Assert.True(true);
    }

    /// <summary>
    /// Проверяет, что UpdateLimit с очень большим значением работает корректно
    /// </summary>
    [Fact]
    public async Task UpdateLimit_VeryLargeValue_WorksCorrectly()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000);
        
        // Действие
        limiter.UpdateLimit(100_000_000); // 100 MB/s
        
        // Проверка
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(10_000_000); // 10 MB
        var elapsed = DateTime.Now - startTime;
        
        Assert.True(elapsed.TotalMilliseconds < 500);
    }

    /// <summary>
    /// Проверяет, что UpdateLimit уменьшает лимит и применяет его сразу
    /// </summary>
    [Fact]
    public async Task UpdateLimit_DecreaseLimit_AppliesImmediately()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(10_000);
        
        // Действие - уменьшаем лимит
        limiter.UpdateLimit(1000);
        
        // Проверка - передаем 2000 байт, должно быть задержка
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(2000);
        var elapsed = DateTime.Now - startTime;
        
        Assert.True(elapsed.TotalMilliseconds >= 500);
    }
}


