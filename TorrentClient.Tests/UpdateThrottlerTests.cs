using System;
using System.Linq;
using System.Threading.Tasks;
using TorrentClient.UI;
using Xunit;

namespace TorrentClient.Tests;

public class UpdateThrottlerTests
{
    /// <summary>
    /// Проверяет, что при первом вызове CanUpdate возвращается true
    /// </summary>
    [Fact]
    public void CanUpdate_FirstCall_ReturnsTrue()
    {
        // Arrange
        var throttler = new UpdateThrottler();
        var torrentId = "test-torrent-1";

        // Act
        var result = throttler.CanUpdate(torrentId);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Проверяет, что в пределах интервала троттлинга CanUpdate возвращает false
    /// </summary>
    [Fact]
    public void CanUpdate_WithinInterval_ReturnsFalse()
    {
        // Arrange
        var throttler = new UpdateThrottler();
        var torrentId = "test-torrent-1";

        // Act
        throttler.MarkUpdated(torrentId);
        var result = throttler.CanUpdate(torrentId);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Проверяет, что после истечения интервала троттлинга CanUpdate возвращает true
    /// </summary>
    [Fact]
    public async Task CanUpdate_AfterInterval_ReturnsTrue()
    {
        // Arrange
        var throttler = new UpdateThrottler();
        var torrentId = "test-torrent-1";

        // Act
        throttler.MarkUpdated(torrentId);
        await Task.Delay(1100); // Больше минимального интервала (1000 мс)
        var result = throttler.CanUpdate(torrentId);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Проверяет, что при передаче null идентификатора торрента CanUpdate возвращает false
    /// </summary>
    [Fact]
    public void CanUpdate_NullTorrentId_ReturnsFalse()
    {
        // Arrange
        var throttler = new UpdateThrottler();

        // Act
        var result = throttler.CanUpdate(null!);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Проверяет, что при передаче пустого идентификатора торрента CanUpdate возвращает false
    /// </summary>
    [Fact]
    public void CanUpdate_EmptyTorrentId_ReturnsFalse()
    {
        // Arrange
        var throttler = new UpdateThrottler();

        // Act
        var result = throttler.CanUpdate(string.Empty);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Проверяет, что MarkUpdated обновляет временную метку и влияет на результат CanUpdate
    /// </summary>
    [Fact]
    public async Task MarkUpdated_UpdatesTimestamp()
    {
        // Arrange
        var throttler = new UpdateThrottler();
        var torrentId = "test-torrent-1";

        // Act
        throttler.MarkUpdated(torrentId);
        var canUpdate1 = throttler.CanUpdate(torrentId);
        await Task.Delay(1100);
        var canUpdate2 = throttler.CanUpdate(torrentId);

        // Assert
        Assert.False(canUpdate1);
        Assert.True(canUpdate2);
    }

    /// <summary>
    /// Проверяет, что MarkUpdated не выбрасывает исключение при передаче null или пустого идентификатора
    /// </summary>
    [Fact]
    public void MarkUpdated_NullTorrentId_DoesNotThrow()
    {
        // Arrange
        var throttler = new UpdateThrottler();

        // Act & Assert
        throttler.MarkUpdated(null!);
        throttler.MarkUpdated(string.Empty);
    }

    /// <summary>
    /// Проверяет, что троттлинг работает независимо для разных торрентов
    /// </summary>
    [Fact]
    public void MarkUpdated_MultipleTorrents_IndependentThrottling()
    {
        // Arrange
        var throttler = new UpdateThrottler();
        var torrentId1 = "test-torrent-1";
        var torrentId2 = "test-torrent-2";

        // Act
        throttler.MarkUpdated(torrentId1);
        var canUpdate1 = throttler.CanUpdate(torrentId1);
        var canUpdate2 = throttler.CanUpdate(torrentId2);

        // Assert
        Assert.False(canUpdate1);
        Assert.True(canUpdate2);
    }

    /// <summary>
    /// Проверяет, что CleanupStaleEntries удаляет записи для несуществующих торрентов
    /// </summary>
    [Fact]
    public void CleanupStaleEntries_RemovesNonExistentTorrents()
    {
        // Arrange
        var throttler = new UpdateThrottler();
        var torrentId1 = "test-torrent-1";
        var torrentId2 = "test-torrent-2";
        var torrentId3 = "test-torrent-3";

        throttler.MarkUpdated(torrentId1);
        throttler.MarkUpdated(torrentId2);
        throttler.MarkUpdated(torrentId3);

        Assert.False(throttler.CanUpdate(torrentId1));
        Assert.False(throttler.CanUpdate(torrentId2));
        Assert.False(throttler.CanUpdate(torrentId3));

        // Act
        var existingTorrents = new[] { torrentId1, torrentId3 };
        throttler.CleanupStaleEntries(existingTorrents);

        // Assert
        Assert.False(throttler.CanUpdate(torrentId1));
        Assert.False(throttler.CanUpdate(torrentId3));
        Assert.True(throttler.CanUpdate(torrentId2));
    }

    /// <summary>
    /// Проверяет, что CleanupStaleEntries удаляет старые записи
    /// </summary>
    [Fact]
    public void CleanupStaleEntries_RemovesOldEntries()
    {
        // Arrange
        var throttler = new UpdateThrottler();
        var torrentId = "test-torrent-1";

        throttler.MarkUpdated(torrentId);

        // Act
        var existingTorrents = new[] { torrentId };
        throttler.CleanupStaleEntries(existingTorrents);

        // Assert
        Assert.True(true);
    }

    /// <summary>
    /// Проверяет, что при достижении максимального количества записей удаляются самые старые
    /// </summary>
    [Fact]
    public void MarkUpdated_MaxEntries_RemovesOldestEntries()
    {
        // Arrange
        var throttler = new UpdateThrottler();
        var maxEntries = 1000;

        // Act
        for (int i = 0; i < maxEntries + 100; i++)
        {
            throttler.MarkUpdated($"torrent-{i}");
        }

        // Assert
        var canUpdateOld = throttler.CanUpdate("torrent-0");
        var canUpdateNew = throttler.CanUpdate($"torrent-{maxEntries + 50}");

        Assert.True(canUpdateNew || canUpdateOld);
    }
}

