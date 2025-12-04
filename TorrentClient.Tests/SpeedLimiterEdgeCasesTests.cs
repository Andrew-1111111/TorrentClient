using System;
using System.Threading;
using System.Threading.Tasks;
using TorrentClient.Core;
using Xunit;

namespace TorrentClient.Tests;

/// <summary>
/// Тесты для граничных случаев и особых ситуаций в SpeedLimiter
/// </summary>
public class SpeedLimiterEdgeCasesTests
{
    /// <summary>
    /// Проверяет, что WaitIfNeededAsync с нулевым количеством байт не создает задержку
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_ZeroBytes_DoesNotWait()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000);
        
        // Действие
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(0);
        var elapsed = DateTime.Now - startTime;
        
        // Проверка
        Assert.True(elapsed.TotalMilliseconds < 100);
    }

    /// <summary>
    /// Проверяет, что WaitIfNeededAsync с отрицательным количеством байт не создает задержку
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_NegativeBytes_DoesNotWait()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000);
        
        // Действие
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(-100);
        var elapsed = DateTime.Now - startTime;
        
        // Проверка
        Assert.True(elapsed.TotalMilliseconds < 100);
    }

    /// <summary>
    /// Проверяет, что WaitIfNeededAsync с большим количеством байт создает задержку
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_VeryLargeBytes_Waits()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000); // 1000 байт/сек
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // Таймаут 2 секунды
        
        // Действие - передаем 5000 байт при лимите 1000 байт/сек (ожидаемая задержка ~4 секунды)
        // Но используем таймаут, чтобы тест не зависал
        var startTime = DateTime.Now;
        try
        {
            await limiter.WaitIfNeededAsync(5000, cts.Token); // 5 KB при лимите 1 KB/s
        }
        catch (OperationCanceledException)
        {
            // Ожидаем отмену из-за таймаута
        }
        var elapsed = DateTime.Now - startTime;
        
        // Проверка - должна быть задержка хотя бы 500мс до таймаута
        Assert.True(elapsed.TotalMilliseconds >= 500 && elapsed.TotalMilliseconds <= 2500);
    }

    /// <summary>
    /// Проверяет, что Reset можно вызывать несколько раз подряд
    /// </summary>
    [Fact]
    public void Reset_MultipleCalls_WorksCorrectly()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000);
        
        // Действие
        limiter.Reset();
        limiter.Reset();
        limiter.Reset();
        
        // Проверка - не должно быть исключений
        Assert.True(true);
    }

    /// <summary>
    /// Проверяет, что UpdateLimit можно вызывать сразу после Reset
    /// </summary>
    [Fact]
    public void UpdateLimit_AfterReset_WorksCorrectly()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000);
        
        // Действие
        limiter.Reset();
        limiter.UpdateLimit(2000);
        
        // Проверка - не должно быть исключений
        Assert.True(true);
    }

    /// <summary>
    /// Проверяет, что WaitIfNeededAsync работает корректно после Reset
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_AfterReset_WorksCorrectly()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000);
        
        // Действие
        await limiter.WaitIfNeededAsync(1500); // Превышает лимит
        limiter.Reset();
        
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(500); // После сброса должно быть в пределах лимита
        var elapsed = DateTime.Now - startTime;
        
        // Проверка
        Assert.True(elapsed.TotalMilliseconds < 500);
    }

    /// <summary>
    /// Проверяет, что WaitIfNeededAsync работает корректно при очень малом лимите
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_VerySmallLimit_Waits()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(100); // 100 байт/сек
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // Таймаут 2 секунды
        
        // Действие - передаем 500 байт при лимите 100 байт/сек (ожидаемая задержка ~4 секунды)
        var startTime = DateTime.Now;
        try
        {
            await limiter.WaitIfNeededAsync(500, cts.Token); // 500 байт при лимите 100 байт/сек
        }
        catch (OperationCanceledException)
        {
            // Ожидаем отмену из-за таймаута
        }
        var elapsed = DateTime.Now - startTime;
        
        // Проверка - должна быть задержка хотя бы 500мс до таймаута
        Assert.True(elapsed.TotalMilliseconds >= 500 && elapsed.TotalMilliseconds <= 2500);
    }

    /// <summary>
    /// Проверяет, что WaitIfNeededAsync работает корректно при очень малом количестве байт
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_VerySmallBytes_DoesNotWait()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000);
        
        // Действие
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(1); // 1 байт
        var elapsed = DateTime.Now - startTime;
        
        // Проверка
        Assert.True(elapsed.TotalMilliseconds < 100);
    }

    /// <summary>
    /// Проверяет, что UpdateLimit с отрицательным значением обрабатывается корректно
    /// </summary>
    [Fact]
    public async Task UpdateLimit_NegativeValue_DisablesLimit()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000);
        
        // Действие - отрицательное значение должно обрабатываться как отключение лимита
        limiter.UpdateLimit(-1000);
        
        // Проверка
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(10_000);
        var elapsed = DateTime.Now - startTime;
        
        // Если лимит отключен, не должно быть задержки
        Assert.True(elapsed.TotalMilliseconds < 500);
    }

    /// <summary>
    /// Проверяет, что множественные вызовы WaitIfNeededAsync с малыми значениями накапливают задержку
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_MultipleSmallCalls_AccumulatesDelay()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(1000); // 1000 байт/сек
        
        // Действие - передаем много маленьких порций
        var startTime = DateTime.Now;
        for (int i = 0; i < 10; i++)
        {
            await limiter.WaitIfNeededAsync(200); // 200 байт * 10 = 2000 байт
        }
        var elapsed = DateTime.Now - startTime;
        
        // Проверка - должна быть задержка из-за превышения лимита
        Assert.True(elapsed.TotalMilliseconds >= 200);
    }
}

