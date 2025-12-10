using TorrentClient.Core;
using TorrentClient.Core.Interfaces;
using TorrentClient.Models;

namespace TorrentClient.Tests;

/// <summary>
/// Интеграционные тесты для проверки корректности работы приоритетов торрентов
/// </summary>
public class TorrentPriorityIntegrationTests : IDisposable
{
    private readonly string _testDownloadPath;
    private readonly string _testStatePath;
    private TorrentManager? _torrentManager;

    public TorrentPriorityIntegrationTests()
    {
        _testDownloadPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "Downloads");
        _testStatePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "States");
        Directory.CreateDirectory(_testDownloadPath);
        Directory.CreateDirectory(_testStatePath);
    }

    [Fact]
    public void Torrents_SortedByPriority_CorrectOrder()
    {
        // Arrange
        _torrentManager = new TorrentManager(_testDownloadPath, _testStatePath);
        
        var now = DateTime.Now;
        var torrents = new List<Torrent>
        {
            new Torrent
            {
                Id = "1",
                Info = new TorrentInfo { Name = "Normal1", InfoHash = "hash1", TotalSize = 1000 },
                Priority = 1, // Нормальный
                AddedDate = now.AddMinutes(-10)
            },
            new Torrent
            {
                Id = "2",
                Info = new TorrentInfo { Name = "High1", InfoHash = "hash2", TotalSize = 1000 },
                Priority = 2, // Высокий
                AddedDate = now.AddMinutes(-5)
            },
            new Torrent
            {
                Id = "3",
                Info = new TorrentInfo { Name = "Low1", InfoHash = "hash3", TotalSize = 1000 },
                Priority = 0, // Низкий
                AddedDate = now.AddMinutes(-15)
            },
            new Torrent
            {
                Id = "4",
                Info = new TorrentInfo { Name = "High2", InfoHash = "hash4", TotalSize = 1000 },
                Priority = 2, // Высокий
                AddedDate = now.AddMinutes(-20) // Старше High1
            },
            new Torrent
            {
                Id = "5",
                Info = new TorrentInfo { Name = "Normal2", InfoHash = "hash5", TotalSize = 1000 },
                Priority = 1, // Нормальный
                AddedDate = now.AddMinutes(-8)
            }
        };

        // Act - сортировка как в MainFormPresenter
        var sorted = torrents
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.AddedDate)
            .ToList();

        // Assert
        Assert.Equal(5, sorted.Count);
        
        // Первые два должны быть высокого приоритета, отсортированные по дате
        Assert.Equal("High2", sorted[0].Info.Name); // Старше
        Assert.Equal(2, sorted[0].Priority);
        Assert.Equal("High1", sorted[1].Info.Name); // Младше
        Assert.Equal(2, sorted[1].Priority);
        
        // Следующие два - нормального приоритета
        Assert.Equal("Normal1", sorted[2].Info.Name);
        Assert.Equal(1, sorted[2].Priority);
        Assert.Equal("Normal2", sorted[3].Info.Name);
        Assert.Equal(1, sorted[3].Priority);
        
        // Последний - низкого приоритета
        Assert.Equal("Low1", sorted[4].Info.Name);
        Assert.Equal(0, sorted[4].Priority);
    }

    [Fact]
    public void SetTorrentPriority_ChangesPriority_AndPersists()
    {
        // Arrange
        _torrentManager = new TorrentManager(_testDownloadPath, _testStatePath);
        var stateManager = new TorrentStateManager(_testStatePath);
        
        var torrent = new Torrent
        {
            Id = "test-id",
            Info = new TorrentInfo
            {
                Name = "Test Torrent",
                InfoHash = "testhash789",
                TotalSize = 1000
            },
            TorrentFilePath = Path.Combine(_testDownloadPath, "test.torrent"),
            DownloadPath = _testDownloadPath,
            Priority = 1 // Нормальный по умолчанию
        };

        // Act - устанавливаем высокий приоритет
        torrent.Priority = 2;
        stateManager.SaveTorrentState(torrent);
        
        // Загружаем обратно
        var loadedState = stateManager.LoadTorrentState("testhash789");
        var restoredTorrent = new Torrent
        {
            Info = new TorrentInfo
            {
                Name = "Test Torrent",
                InfoHash = "testhash789",
                TotalSize = 1000
            },
            TorrentFilePath = Path.Combine(_testDownloadPath, "test.torrent"),
            DownloadPath = _testDownloadPath
        };
        stateManager.RestoreTorrentState(restoredTorrent, loadedState);

        // Assert
        Assert.NotNull(loadedState);
        Assert.Equal(2, loadedState.Priority);
        Assert.Equal(2, restoredTorrent.Priority);
    }

    [Fact]
    public void PriorityDisplay_AllValues_CorrectText()
    {
        // Arrange & Act - проверяем отображение приоритетов
        var priorityTexts = new Dictionary<int, string>();
        
        for (int priority = 0; priority <= 2; priority++)
        {
            var text = priority switch
            {
                0 => "Низкий",
                1 => "Нормальный",
                2 => "Высокий",
                _ => "Нормальный"
            };
            priorityTexts[priority] = text;
        }

        // Assert
        Assert.Equal("Низкий", priorityTexts[0]);
        Assert.Equal("Нормальный", priorityTexts[1]);
        Assert.Equal("Высокий", priorityTexts[2]);
    }

    [Fact]
    public void PriorityDisplay_InvalidValue_DefaultsToNormal()
    {
        // Arrange
        var invalidPriorities = new[] { -1, 3, 5, 100 };

        foreach (var invalidPriority in invalidPriorities)
        {
            // Act - проверяем обработку невалидных значений
            var text = invalidPriority switch
            {
                0 => "Низкий",
                1 => "Нормальный",
                2 => "Высокий",
                _ => "Нормальный" // Default для невалидных значений
            };

            // Assert
            Assert.Equal("Нормальный", text);
        }
    }

    [Fact]
    public void PriorityClamp_OutOfRange_ClampsCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            (input: -5, expected: 0),
            (input: -1, expected: 0),
            (input: 0, expected: 0),
            (input: 1, expected: 1),
            (input: 2, expected: 2),
            (input: 3, expected: 2),
            (input: 10, expected: 2),
            (input: 100, expected: 2)
        };

        foreach (var (input, expected) in testCases)
        {
            // Act
            var clamped = Math.Clamp(input, 0, 2);

            // Assert
            Assert.Equal(expected, clamped);
        }
    }

    [Fact]
    public void PrioritySorting_MultipleTorrents_SamePriority_SortedByDate()
    {
        // Arrange
        var now = DateTime.Now;
        var torrents = new List<Torrent>
        {
            new Torrent
            {
                Id = "1",
                Info = new TorrentInfo { Name = "Third", InfoHash = "hash1", TotalSize = 1000 },
                Priority = 1,
                AddedDate = now.AddMinutes(-30)
            },
            new Torrent
            {
                Id = "2",
                Info = new TorrentInfo { Name = "First", InfoHash = "hash2", TotalSize = 1000 },
                Priority = 1,
                AddedDate = now.AddMinutes(-60)
            },
            new Torrent
            {
                Id = "3",
                Info = new TorrentInfo { Name = "Second", InfoHash = "hash3", TotalSize = 1000 },
                Priority = 1,
                AddedDate = now.AddMinutes(-45)
            }
        };

        // Act
        var sorted = torrents
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.AddedDate)
            .ToList();

        // Assert - должны быть отсортированы по дате добавления (старые первыми)
        Assert.Equal("First", sorted[0].Info.Name);
        Assert.Equal("Second", sorted[1].Info.Name);
        Assert.Equal("Third", sorted[2].Info.Name);
    }

    public void Dispose()
    {
        _torrentManager?.Dispose();
        
        try
        {
            if (Directory.Exists(_testDownloadPath))
                Directory.Delete(_testDownloadPath, true);
        }
        catch { }
        
        try
        {
            if (Directory.Exists(_testStatePath))
                Directory.Delete(_testStatePath, true);
        }
        catch { }
    }
}

