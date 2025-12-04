using System.Collections.Generic;
using TorrentClient.Models;
using TorrentClient.Protocol;
using Xunit;

namespace TorrentClient.Tests;

public class PiecePickerTests
{
    /// <summary>
    /// Проверяет, что при отсутствии доступных кусков PickPieces возвращает пустой список
    /// </summary>
    [Fact]
    public void PickPieces_NoAvailablePieces_ReturnsEmptyList()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);

        // Act
        var result = picker.PickPieces(5);

        // Assert
        Assert.Empty(result);
    }

    /// <summary>
    /// Проверяет, что PickPieces возвращает доступные куски при их наличии
    /// </summary>
    [Fact]
    public void PickPieces_WithAvailablePieces_ReturnsPieces()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);
        
        picker.UpdatePieceAvailability(0, true);
        picker.UpdatePieceAvailability(1, true);
        picker.UpdatePieceAvailability(2, true);

        // Act
        var result = picker.PickPieces(5);

        // Assert
        Assert.NotEmpty(result);
        Assert.True(result.Count <= 5);
        Assert.All(result, piece => Assert.True(piece >= 0 && piece < 10));
    }

    /// <summary>
    /// Проверяет, что PickPieces использует стратегию Rarest First и возвращает самые редкие куски первыми
    /// </summary>
    [Fact]
    public void PickPieces_RarestFirst_ReturnsRarestPiecesFirst()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);
        
        picker.UpdatePieceAvailability(0, true);
        picker.UpdatePieceAvailability(0, true);
        picker.UpdatePieceAvailability(0, true);
        picker.UpdatePieceAvailability(1, true);
        picker.UpdatePieceAvailability(2, true);
        picker.UpdatePieceAvailability(2, true);

        // Act
        var result = picker.PickPieces(3);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(1, result[0]);
    }

    /// <summary>
    /// Проверяет, что PickPieces исключает указанные куски из результата
    /// </summary>
    [Fact]
    public void PickPieces_ExcludesSpecifiedPieces()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);
        
        picker.UpdatePieceAvailability(0, true);
        picker.UpdatePieceAvailability(1, true);
        picker.UpdatePieceAvailability(2, true);
        
        var excludePieces = new HashSet<int> { 1 };

        // Act
        var result = picker.PickPieces(5, excludePieces);

        // Assert
        Assert.DoesNotContain(1, result);
    }

    /// <summary>
    /// Проверяет, что PickPieces исключает куски, помеченные как загружающиеся
    /// </summary>
    [Fact]
    public void PickPieces_ExcludesDownloadingPieces()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);
        
        picker.UpdatePieceAvailability(0, true);
        picker.UpdatePieceAvailability(1, true);
        picker.MarkDownloading(1);

        // Act
        var result = picker.PickPieces(5);

        // Assert
        Assert.DoesNotContain(1, result);
    }

    /// <summary>
    /// Проверяет, что UpdatePieceAvailability добавляет доступность куска
    /// </summary>
    [Fact]
    public void UpdatePieceAvailability_AddsAvailability()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);

        // Act
        picker.UpdatePieceAvailability(0, true);
        picker.UpdatePieceAvailability(0, true);
        
        var result = picker.PickPieces(5);

        // Assert
        Assert.Contains(0, result);
    }

    /// <summary>
    /// Проверяет, что UpdatePieceAvailability удаляет доступность куска
    /// </summary>
    [Fact]
    public void UpdatePieceAvailability_RemovesAvailability()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);
        
        picker.UpdatePieceAvailability(0, true);
        picker.UpdatePieceAvailability(0, true);

        // Act
        picker.UpdatePieceAvailability(0, false);
        picker.UpdatePieceAvailability(0, false);
        
        var result = picker.PickPieces(5);

        // Assert
        Assert.DoesNotContain(0, result);
    }

    /// <summary>
    /// Проверяет, что UpdatePieceAvailability не выбрасывает исключение при невалидном индексе
    /// </summary>
    [Fact]
    public void UpdatePieceAvailability_InvalidIndex_DoesNotThrow()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);

        // Act & Assert
        picker.UpdatePieceAvailability(-1, true);
        picker.UpdatePieceAvailability(10, true);
        picker.UpdatePieceAvailability(100, true);
    }

    /// <summary>
    /// Проверяет, что UpdatePeerBitField обновляет доступность кусков на основе битового поля пира
    /// </summary>
    [Fact]
    public void UpdatePeerBitField_UpdatesAvailability()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);
        var peerBitField = new BitField(10);
        peerBitField[0] = true;
        peerBitField[1] = true;
        peerBitField[2] = true;

        // Act
        picker.UpdatePeerBitField(peerBitField);
        
        var result = picker.PickPieces(5);

        // Assert
        Assert.Contains(0, result);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
    }

    /// <summary>
    /// Проверяет, что UpdatePeerBitField не обновляет доступность при невалидной длине битового поля
    /// </summary>
    [Fact]
    public void UpdatePeerBitField_InvalidLength_DoesNotUpdate()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);
        var peerBitField = new BitField(5); // Неправильная длина

        // Act
        picker.UpdatePeerBitField(peerBitField);
        
        var result = picker.PickPieces(5);

        // Assert
        Assert.Empty(result);
    }

    /// <summary>
    /// Проверяет, что UpdateHave обновляет доступность куска при получении сообщения Have от пира
    /// </summary>
    [Fact]
    public void UpdateHave_UpdatesAvailability()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);

        // Act
        picker.UpdateHave(0);
        
        var result = picker.PickPieces(5);

        // Assert
        Assert.Contains(0, result);
    }

    /// <summary>
    /// Проверяет, что MarkDownloading помечает кусок как загружающийся и исключает его из выбора
    /// </summary>
    [Fact]
    public void MarkDownloading_ExcludesPiece()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);
        
        picker.UpdatePieceAvailability(0, true);
        picker.MarkDownloading(0);

        // Act
        var result = picker.PickPieces(5);

        // Assert
        Assert.DoesNotContain(0, result);
        Assert.True(picker.IsDownloading(0));
    }

    /// <summary>
    /// Проверяет, что UnmarkDownloading снимает пометку загружающегося куска и позволяет его выбрать снова
    /// </summary>
    [Fact]
    public void UnmarkDownloading_AllowsPiece()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);
        
        picker.UpdatePieceAvailability(0, true);
        picker.MarkDownloading(0);
        picker.UnmarkDownloading(0);

        // Act
        var result = picker.PickPieces(5);

        // Assert
        Assert.Contains(0, result);
        Assert.False(picker.IsDownloading(0));
    }

    /// <summary>
    /// Проверяет, что IsDownloading корректно возвращает статус загрузки куска
    /// </summary>
    [Fact]
    public void IsDownloading_ReturnsCorrectStatus()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);

        // Act
        picker.MarkDownloading(0);
        var isDownloading = picker.IsDownloading(0);
        var isNotDownloading = picker.IsDownloading(1);

        // Assert
        Assert.True(isDownloading);
        Assert.False(isNotDownloading);
    }

    /// <summary>
    /// Проверяет, что PickPieces учитывает максимальное количество кусков в результате
    /// </summary>
    [Fact]
    public void PickPieces_RespectsMaxPieces()
    {
        // Arrange
        var bitField = new BitField(10);
        var picker = new PiecePicker(bitField, 10);
        
        for (int i = 0; i < 10; i++)
        {
            picker.UpdatePieceAvailability(i, true);
        }

        // Act
        var result = picker.PickPieces(3);

        // Assert
        Assert.True(result.Count <= 3);
    }
}

