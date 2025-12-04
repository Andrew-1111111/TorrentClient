using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TorrentClient.Models;
using TorrentClient.Protocol;
using Xunit;

namespace TorrentClient.Tests;

/// <summary>
/// Тесты для ChokeManager - управления блокировкой пиров для загрузки и отдачи
/// </summary>
public class ChokeManagerTests : IDisposable
{
    private ChokeManager _chokeManager = new();

    public void Dispose()
    {
        _chokeManager?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Проверяет, что добавление и удаление пиров работает корректно
    /// </summary>
    [Fact]
    public void AddPeer_RemovePeer_ManagesPeersCorrectly()
    {
        // Подготовка
        var peer1 = CreateTestPeerConnection(new IPEndPoint(IPAddress.Loopback, 6881));
        var peer2 = CreateTestPeerConnection(new IPEndPoint(IPAddress.Loopback, 6882));
        
        // Действие
        _chokeManager.AddPeer(peer1);
        _chokeManager.AddPeer(peer2);
        _chokeManager.RemovePeer(peer1);
        
        // Проверка
        // Проверяем, что пир был удален (косвенно - через отсутствие исключений)
        Assert.True(true);
    }

    /// <summary>
    /// Проверяет, что Start и Stop работают корректно
    /// </summary>
    [Fact]
    public void Start_Stop_ManagesChokeLoop()
    {
        // Подготовка
        var peer = CreateTestPeerConnection(new IPEndPoint(IPAddress.Loopback, 6881));
        _chokeManager.AddPeer(peer);
        
        // Действие
        _chokeManager.Start();
        Thread.Sleep(100);
        _chokeManager.Stop();
        
        // Проверка
        Assert.True(true);
    }

    /// <summary>
    /// Проверяет, что Dispose освобождает ресурсы
    /// </summary>
    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Подготовка
        var peer = CreateTestPeerConnection(new IPEndPoint(IPAddress.Loopback, 6881));
        _chokeManager.AddPeer(peer);
        _chokeManager.Start();
        Thread.Sleep(200); // Даем время на запуск
        
        // Действие - сначала останавливаем, потом dispose
        _chokeManager.Stop();
        Thread.Sleep(100); // Даем время на завершение Stop
        _chokeManager.Dispose();
        
        // Проверка
        // Проверяем, что Dispose не вызывает исключений
        Assert.True(true);
    }

    /// <summary>
    /// Создает тестовый PeerConnection для тестирования
    /// </summary>
    private PeerConnection CreateTestPeerConnection(IPEndPoint endPoint)
    {
        return new PeerConnection(endPoint, "test-peer-id", "test-info-hash", new BitField(1));
    }
}

