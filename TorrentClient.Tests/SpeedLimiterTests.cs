using System;
using TorrentClient.Core;
using Xunit;

namespace TorrentClient.Tests;

public class SpeedLimiterTests
{
    /// <summary>
    /// Проверяет, что при отсутствии лимита скорости метод не создает задержку
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_NoLimit_DoesNotWait()
    {
        // Arrange
        using var limiter = new SpeedLimiter(null);

        // Act
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(1000);
        var elapsed = DateTime.Now - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds < 100); // Не должно быть задержки
    }

    /// <summary>
    /// Проверяет, что при нулевом лимите скорости метод не создает задержку
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_ZeroLimit_DoesNotWait()
    {
        // Arrange
        using var limiter = new SpeedLimiter(0);

        // Act
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(1000);
        var elapsed = DateTime.Now - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds < 100); // Не должно быть задержки
    }

    /// <summary>
    /// Проверяет, что при передаче данных в пределах лимита метод не создает задержку
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_WithinLimit_DoesNotWait()
    {
        // Arrange
        using var limiter = new SpeedLimiter(1000); // 1000 байт/сек

        // Act
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(500); // 500 байт - в пределах лимита
        var elapsed = DateTime.Now - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds < 100); // Не должно быть задержки
    }

    /// <summary>
    /// Проверяет, что при превышении лимита скорости метод создает задержку
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_ExceedsLimit_Waits()
    {
        // Arrange
        using var limiter = new SpeedLimiter(1000); // 1000 байт/сек = 1 KB/s

        // Act
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(2000); // 2000 байт - превышает лимит
        var elapsed = DateTime.Now - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds >= 500, $"Expected at least 500ms, got {elapsed.TotalMilliseconds}ms");
        Assert.True(elapsed.TotalMilliseconds < 3000); // Но не слишком долго
    }

    /// <summary>
    /// Проверяет, что метод Reset очищает счетчики и позволяет продолжить передачу данных
    /// </summary>
    [Fact]
    public async Task Reset_ClearsCounters()
    {
        // Arrange
        using var limiter = new SpeedLimiter(1000);

        // Act
        await limiter.WaitIfNeededAsync(800);
        await limiter.WaitIfNeededAsync(800); // Всего 1600 байт - превышает лимит 1000
        limiter.Reset();
        
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(500); // После сброса должно быть в пределах лимита
        var elapsed = DateTime.Now - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds < 200); // Не должно быть значительной задержки после сброса
    }

    /// <summary>
    /// Проверяет, что множественные вызовы метода учитывают общий лимит скорости
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_MultipleCalls_RespectsLimit()
    {
        // Arrange
        using var limiter = new SpeedLimiter(1000); // 1000 байт/сек

        // Act
        var startTime = DateTime.Now;
        
        await limiter.WaitIfNeededAsync(400);
        await limiter.WaitIfNeededAsync(400);
        await limiter.WaitIfNeededAsync(400); // Всего 1200 байт
        
        var elapsed = DateTime.Now - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds >= 50, $"Expected at least 50ms delay, got {elapsed.TotalMilliseconds}ms");
    }

    /// <summary>
    /// Проверяет, что метод Dispose корректно освобождает ресурсы и может быть вызван несколько раз
    /// </summary>
    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        var limiter = new SpeedLimiter(1000);

        // Act & Assert
        limiter.Dispose();
        limiter.Dispose();
    }

    /// <summary>
    /// Проверяет, что при очень высоком лимите скорости метод не создает задержку
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_VeryHighLimit_DoesNotWait()
    {
        // Arrange
        using var limiter = new SpeedLimiter(100 * 1024 * 1024); // 100 MB/s

        // Act
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(1024 * 1024); // 1 MB
        var elapsed = DateTime.Now - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds < 100);
    }

    /// <summary>
    /// Проверяет, что последовательные вызовы метода учитывают лимит скорости
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_SequentialCalls_RespectsLimit()
    {
        // Arrange
        using var limiter = new SpeedLimiter(1000); // 1000 байт/сек

        // Act
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(300);
        await limiter.WaitIfNeededAsync(300);
        await limiter.WaitIfNeededAsync(300);
        await limiter.WaitIfNeededAsync(300); // Всего 1200 байт
        var elapsed = DateTime.Now - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds >= 50);
    }

    /// <summary>
    /// Проверяет, что после превышения лимита и вызова Reset можно сразу продолжить передачу данных
    /// </summary>
    [Fact]
    public async Task Reset_AfterExceedingLimit_AllowsImmediateUse()
    {
        // Arrange
        using var limiter = new SpeedLimiter(1000);

        // Act
        await limiter.WaitIfNeededAsync(1500); // Превышает лимит
        limiter.Reset();
        
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(500); // После сброса должно быть в пределах лимита
        var elapsed = DateTime.Now - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds < 200);
    }

    /// <summary>
    /// Проверяет, что при передаче данных точно на лимите метод не создает значительной задержки
    /// </summary>
    [Fact]
    public async Task WaitIfNeededAsync_ExactLimit_DoesNotWait()
    {
        // Arrange
        using var limiter = new SpeedLimiter(1000);

        // Act
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(1000); // Точно лимит
        var elapsed = DateTime.Now - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds < 500);
    }
}

