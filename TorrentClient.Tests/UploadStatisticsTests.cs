using System;
using System.Threading.Tasks;
using TorrentClient.Core;
using TorrentClient.Models;
using Xunit;

namespace TorrentClient.Tests;

/// <summary>
/// Тесты для статистики отдачи
/// </summary>
public class UploadStatisticsTests
{
    /// <summary>
    /// Проверяет, что SpeedLimiter правильно ограничивает скорость отдачи
    /// </summary>
    [Fact]
    public async Task SpeedLimiter_UploadLimit_RespectsLimit()
    {
        // Подготовка
        const long maxUploadSpeed = 1024 * 1024; // 1 MB/s
        using var limiter = new SpeedLimiter(maxUploadSpeed);

        // Действие - передаем данные порциями, чтобы превысить лимит
        var startTime = DateTime.Now;
        // Первая порция 1MB - должна пройти без задержки
        await limiter.WaitIfNeededAsync(1024 * 1024);
        // Вторая порция 1MB - должна вызвать задержку ~1 секунду
        await limiter.WaitIfNeededAsync(1024 * 1024);
        var elapsed = DateTime.Now - startTime;

        // Проверка
        // Должна быть задержка примерно 1 секунда (избыток 1MB / 1 MB/s = 1 сек)
        Assert.True(elapsed.TotalSeconds >= 0.8 && elapsed.TotalSeconds <= 2.0, 
            $"Ожидалась задержка ~1 секунда, получено {elapsed.TotalSeconds} секунд");
    }

    /// <summary>
    /// Проверяет, что SpeedLimiter не создает задержку при отсутствии лимита отдачи
    /// </summary>
    [Fact]
    public async Task SpeedLimiter_NoUploadLimit_DoesNotWait()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(null);

        // Действие
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(1024 * 1024);
        var elapsed = DateTime.Now - startTime;

        // Проверка
        Assert.True(elapsed.TotalMilliseconds < 100, 
            $"Не должно быть задержки, получено {elapsed.TotalMilliseconds} мс");
    }

    /// <summary>
    /// Проверяет, что SpeedLimiter правильно обрабатывает нулевой лимит отдачи
    /// </summary>
    [Fact]
    public async Task SpeedLimiter_ZeroUploadLimit_DoesNotWait()
    {
        // Подготовка
        using var limiter = new SpeedLimiter(0);

        // Действие
        var startTime = DateTime.Now;
        await limiter.WaitIfNeededAsync(1024 * 1024);
        var elapsed = DateTime.Now - startTime;

        // Проверка
        Assert.True(elapsed.TotalMilliseconds < 100, 
            $"Не должно быть задержки при нулевом лимите, получено {elapsed.TotalMilliseconds} мс");
    }

    /// <summary>
    /// Проверяет, что SpeedLimiter правильно обрабатывает разные размеры данных
    /// </summary>
    [Fact]
    public async Task SpeedLimiter_DifferentDataSizes_RespectsLimit()
    {
        // Подготовка
        const long maxUploadSpeed = 512 * 1024; // 512 KB/s
        using var limiter = new SpeedLimiter(maxUploadSpeed);

        // Действие - передаем данные порциями, чтобы превысить лимит
        var startTime = DateTime.Now;
        // Первая порция 512KB - должна пройти без задержки
        await limiter.WaitIfNeededAsync(512 * 1024);
        // Вторая порция 512KB - должна вызвать задержку ~1 секунду
        await limiter.WaitIfNeededAsync(512 * 1024);
        var elapsed = DateTime.Now - startTime;

        // Проверка
        // Должна быть задержка примерно 1 секунда (избыток 512KB / 512 KB/s = 1 сек)
        Assert.True(elapsed.TotalSeconds >= 0.8 && elapsed.TotalSeconds <= 2.0, 
            $"Ожидалась задержка ~1 секунда, получено {elapsed.TotalSeconds} секунд");
    }

    /// <summary>
    /// Проверяет, что статистика отдачи обновляется при отправке данных
    /// </summary>
    [Fact]
    public void UploadStatistics_UpdateUploadedBytes_IncrementsCorrectly()
    {
        // Подготовка
        var torrent = CreateTestTorrent();
        var initialUploaded = torrent.UploadedBytes;

        // Действие
        torrent.UploadedBytes += 1024 * 1024; // 1 MB

        // Проверка
        Assert.Equal(initialUploaded + 1024 * 1024, torrent.UploadedBytes);
    }

    /// <summary>
    /// Проверяет, что скорость отдачи рассчитывается корректно
    /// </summary>
    [Fact]
    public void UploadStatistics_CalculateUploadSpeed_ReturnsCorrectSpeed()
    {
        // Подготовка
        var torrent = CreateTestTorrent();
        torrent.UploadedBytes = 0;
        torrent.UploadSpeed = 0;

        // Действие
        torrent.UploadedBytes = 1024 * 1024; // 1 MB за 1 секунду
        torrent.UploadSpeed = 1024 * 1024; // 1 MB/s

        // Проверка
        Assert.Equal(1024 * 1024, torrent.UploadSpeed);
    }

    /// <summary>
    /// Проверяет, что скорость отдачи может быть нулевой
    /// </summary>
    [Fact]
    public void UploadStatistics_ZeroUploadSpeed_IsValid()
    {
        // Подготовка
        var torrent = CreateTestTorrent();

        // Действие
        torrent.UploadSpeed = 0;

        // Проверка
        Assert.Equal(0, torrent.UploadSpeed);
    }

    /// <summary>
    /// Проверяет, что скорость отдачи может быть очень большой
    /// </summary>
    [Fact]
    public void UploadStatistics_LargeUploadSpeed_IsValid()
    {
        // Подготовка
        var torrent = CreateTestTorrent();

        // Действие
        torrent.UploadSpeed = 100 * 1024 * 1024; // 100 MB/s

        // Проверка
        Assert.Equal(100 * 1024 * 1024, torrent.UploadSpeed);
    }

    /// <summary>
    /// Создает тестовый торрент для тестирования
    /// </summary>
    private Torrent CreateTestTorrent()
    {
        var torrentInfo = new TorrentInfo
        {
            Name = "Test Torrent",
            TotalSize = 1024 * 1024 * 100, // 100 MB
            PieceCount = 100,
            PieceLength = 1024 * 1024, // 1 MB per piece
            InfoHash = "test-info-hash"
        };

        var torrent = new Torrent
        {
            Info = torrentInfo,
            DownloadPath = "test-download-path"
        };

        return torrent;
    }
}

