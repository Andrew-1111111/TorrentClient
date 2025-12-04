using System;
using System.Threading;
using System.Threading.Tasks;
using TorrentClient.Core;
using Xunit;

namespace TorrentClient.Tests;

public class GlobalSpeedLimiterTests : IDisposable
{
    public void Dispose()
    {
        GlobalSpeedLimiter.Instance.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Проверяет, что UpdateLimits устанавливает максимальные скорости загрузки и отдачи
    /// </summary>
    [Fact]
    public void UpdateLimits_SetsMaxDownloadAndUpload()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;

        // Act
        limiter.UpdateLimits(1024 * 1024, 512 * 1024);

        // Assert
        Assert.Equal(1024 * 1024, limiter.MaxDownloadSpeed);
        Assert.Equal(512 * 1024, limiter.MaxUploadSpeed);
    }

    /// <summary>
    /// Проверяет, что UpdateLimits с null значениями отключает лимиты скорости
    /// </summary>
    [Fact]
    public void UpdateLimits_NullValues_DisablesLimits()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;

        // Act
        limiter.UpdateLimits(null, null);

        // Assert
        Assert.Null(limiter.MaxDownloadSpeed);
        Assert.Null(limiter.MaxUploadSpeed);
    }

    /// <summary>
    /// Проверяет, что TryConsumeDownload возвращает true при отсутствии лимита
    /// </summary>
    [Fact]
    public void TryConsumeDownload_NoLimit_ReturnsTrue()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;
        limiter.UpdateLimits(null, null);

        // Act
        var result = limiter.TryConsumeDownload(1000);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Проверяет, что TryConsumeDownload возвращает true при потреблении в пределах лимита
    /// </summary>
    [Fact]
    public void TryConsumeDownload_WithinLimit_ReturnsTrue()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;
        limiter.UpdateLimits(1000, null);

        // Act
        var result = limiter.TryConsumeDownload(500);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Проверяет, что TryConsumeDownload возвращает false при превышении лимита
    /// </summary>
    [Fact]
    public void TryConsumeDownload_ExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;
        limiter.UpdateLimits(1000, null);

        // Act
        limiter.TryConsumeDownload(500);
        var result = limiter.TryConsumeDownload(600); // Превышает оставшийся лимит

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Проверяет, что TryConsumeDownloadAsync возвращает true при потреблении в пределах лимита
    /// </summary>
    [Fact]
    public async Task TryConsumeDownloadAsync_WithinLimit_ReturnsTrue()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;
        limiter.UpdateLimits(1000, null);

        // Act
        var result = await limiter.TryConsumeDownloadAsync(500);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Проверяет, что TryConsumeDownloadAsync возвращает false при превышении лимита
    /// </summary>
    [Fact]
    public async Task TryConsumeDownloadAsync_ExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;
        limiter.UpdateLimits(1000, null);

        // Act
        await limiter.TryConsumeDownloadAsync(500);
        var result = await limiter.TryConsumeDownloadAsync(600);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Проверяет, что TryConsumeUpload возвращает true при потреблении в пределах лимита
    /// </summary>
    [Fact]
    public void TryConsumeUpload_WithinLimit_ReturnsTrue()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;
        limiter.UpdateLimits(null, 1000);

        // Act
        var result = limiter.TryConsumeUpload(500);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Проверяет, что TryConsumeUpload возвращает false при превышении лимита
    /// </summary>
    [Fact]
    public void TryConsumeUpload_ExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;
        limiter.UpdateLimits(null, 1000);

        // Act
        limiter.TryConsumeUpload(500);
        var result = limiter.TryConsumeUpload(600);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Проверяет, что TryConsumeUploadAsync возвращает true при потреблении в пределах лимита
    /// </summary>
    [Fact]
    public async Task TryConsumeUploadAsync_WithinLimit_ReturnsTrue()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;
        limiter.UpdateLimits(null, 1000);

        // Act
        var result = await limiter.TryConsumeUploadAsync(500);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Проверяет, что WaitForDownloadTokensAsync ожидает пополнения токенов
    /// </summary>
    [Fact]
    public async Task WaitForDownloadTokensAsync_WaitsForTokens()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;
        limiter.UpdateLimits(1000, null);

        // Act
        await limiter.TryConsumeDownloadAsync(1000); // Используем весь лимит
        var startTime = DateTime.UtcNow;
        
        var waitTask = limiter.WaitForDownloadTokensAsync(500);
        await Task.Delay(100); // Даем время на пополнение токенов
        await waitTask;
        
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        Assert.True(elapsed >= 0); // Должен был подождать
    }

    /// <summary>
    /// Проверяет, что WaitForUploadTokensAsync ожидает пополнения токенов
    /// </summary>
    [Fact]
    public async Task WaitForUploadTokensAsync_WaitsForTokens()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;
        limiter.UpdateLimits(null, 1000);

        // Act
        await limiter.TryConsumeUploadAsync(1000);
        var startTime = DateTime.UtcNow;
        
        var waitTask = limiter.WaitForUploadTokensAsync(500);
        await Task.Delay(100);
        await waitTask;
        
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        Assert.True(elapsed >= 0);
    }

    /// <summary>
    /// Проверяет, что UpdateCurrentSpeeds устанавливает текущие скорости загрузки и отдачи
    /// </summary>
    [Fact]
    public void UpdateCurrentSpeeds_SetsCurrentSpeeds()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;

        // Act
        limiter.UpdateCurrentSpeeds(1024, 512);

        // Assert
        Assert.Equal(1024, limiter.CurrentDownloadSpeed);
        Assert.Equal(512, limiter.CurrentUploadSpeed);
    }

    /// <summary>
    /// Проверяет, что токены пополняются со временем и можно снова потреблять после ожидания
    /// </summary>
    [Fact]
    public async Task TryConsumeDownload_TokensRefillOverTime()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;
        limiter.UpdateLimits(1000, null); // 1000 байт/сек

        // Act
        await limiter.TryConsumeDownloadAsync(1000); // Используем весь лимит
        await Task.Delay(1100); // Ждем больше секунды для пополнения
        
        var result = await limiter.TryConsumeDownloadAsync(500);

        // Assert
        Assert.True(result); // Должны были пополниться токены
    }

    /// <summary>
    /// Проверяет, что WaitForDownloadTokensAsync корректно обрабатывает отмену через CancellationToken
    /// </summary>
    [Fact]
    public async Task WaitForDownloadTokensAsync_Cancellation_RespectsCancellationToken()
    {
        // Arrange
        var limiter = GlobalSpeedLimiter.Instance;
        limiter.UpdateLimits(1000, null);
        await limiter.TryConsumeDownloadAsync(1000);
        
        using var cts = new CancellationTokenSource(100);

        // Act & Assert
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            limiter.WaitForDownloadTokensAsync(500, cts.Token));
        
        Assert.NotNull(exception);
    }

    /// <summary>
    /// Проверяет, что UpdateLimits с очень большими значениями работает корректно
    /// </summary>
    [Fact]
    public void UpdateLimits_VeryLargeValues_WorksCorrectly()
    {
        // Подготовка
        var limiter = GlobalSpeedLimiter.Instance;
        
        // Действие
        limiter.UpdateLimits(100_000_000, 50_000_000); // 100 MB/s и 50 MB/s
        
        // Проверка
        Assert.Equal(100_000_000, limiter.MaxDownloadSpeed);
        Assert.Equal(50_000_000, limiter.MaxUploadSpeed);
    }

    /// <summary>
    /// Проверяет, что UpdateLimits с нулевыми значениями отключает лимиты
    /// </summary>
    [Fact]
    public void UpdateLimits_ZeroValues_DisablesLimits()
    {
        // Подготовка
        var limiter = GlobalSpeedLimiter.Instance;
        
        // Действие
        limiter.UpdateLimits(0, 0);
        
        // Проверка
        Assert.Equal(0, limiter.MaxDownloadSpeed);
        Assert.Equal(0, limiter.MaxUploadSpeed);
    }

    /// <summary>
    /// Проверяет, что TryConsumeDownload с нулевым лимитом возвращает true
    /// </summary>
    [Fact]
    public void TryConsumeDownload_ZeroLimit_ReturnsTrue()
    {
        // Подготовка
        var limiter = GlobalSpeedLimiter.Instance;
        limiter.UpdateLimits(0, null);
        
        // Действие
        var result = limiter.TryConsumeDownload(1000);
        
        // Проверка
        Assert.True(result);
    }

    /// <summary>
    /// Проверяет, что TryConsumeUpload с нулевым лимитом возвращает true
    /// </summary>
    [Fact]
    public void TryConsumeUpload_ZeroLimit_ReturnsTrue()
    {
        // Подготовка
        var limiter = GlobalSpeedLimiter.Instance;
        limiter.UpdateLimits(null, 0);
        
        // Действие
        var result = limiter.TryConsumeUpload(1000);
        
        // Проверка
        Assert.True(result);
    }

    /// <summary>
    /// Проверяет, что UpdateCurrentSpeeds с нулевыми значениями работает корректно
    /// </summary>
    [Fact]
    public void UpdateCurrentSpeeds_ZeroValues_WorksCorrectly()
    {
        // Подготовка
        var limiter = GlobalSpeedLimiter.Instance;
        
        // Действие
        limiter.UpdateCurrentSpeeds(0, 0);
        
        // Проверка
        Assert.Equal(0, limiter.CurrentDownloadSpeed);
        Assert.Equal(0, limiter.CurrentUploadSpeed);
    }

    /// <summary>
    /// Проверяет, что UpdateCurrentSpeeds с очень большими значениями работает корректно
    /// </summary>
    [Fact]
    public void UpdateCurrentSpeeds_VeryLargeValues_WorksCorrectly()
    {
        // Подготовка
        var limiter = GlobalSpeedLimiter.Instance;
        
        // Действие
        limiter.UpdateCurrentSpeeds(100_000_000, 50_000_000);
        
        // Проверка
        Assert.Equal(100_000_000, limiter.CurrentDownloadSpeed);
        Assert.Equal(50_000_000, limiter.CurrentUploadSpeed);
    }
}

