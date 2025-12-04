using System;
using System.Collections.Generic;
using System.Linq;
using TorrentClient.Models;

namespace TorrentClient.Protocol
{
    /// <summary>
    /// Выбор кусков для загрузки - стратегия Rarest First (редкие куски в приоритете)
    /// </summary>
    public class PiecePicker
    {
        #region Поля

        private readonly BitField _ourBitField;
        private readonly int _totalPieces;
        private readonly Dictionary<int, int> _pieceAvailability = new();
        private readonly HashSet<int> _downloadingPieces = new();
        private readonly Random _random = new();

        #endregion

        #region Конструктор

        public PiecePicker(BitField ourBitField, int totalPieces)
        {
            _ourBitField = ourBitField;
            _totalPieces = totalPieces;
        }

        #endregion

        #region Публичные методы

        /// <summary>
        /// Выбирает куски для загрузки (редкие в приоритете)
        /// </summary>
        public List<int> PickPieces(int maxPieces, HashSet<int>? excludePieces = null)
        {
            excludePieces ??= [];

            // Получаем доступные куски
            List<int> availablePieces = [];
            for (int i = 0; i < _totalPieces; i++)
            {
                if (!_ourBitField[i] && 
                    !_downloadingPieces.Contains(i) && 
                    !excludePieces.Contains(i) &&
                    _pieceAvailability.ContainsKey(i) && 
                    _pieceAvailability[i] > 0)
                {
                    availablePieces.Add(i);
                }
            }

            if (availablePieces.Count == 0)
                return [];

            // Сортировка: сначала редкие куски
            return availablePieces
                .OrderBy(p => _pieceAvailability[p])
                .ThenBy(_ => _random.Next())
                .Take(maxPieces)
                .ToList();
        }

        /// <summary>
        /// Обновляет доступность куска
        /// </summary>
        public void UpdatePieceAvailability(int pieceIndex, bool hasPiece)
        {
            if (pieceIndex < 0 || pieceIndex >= _totalPieces)
                return;

            if (hasPiece)
            {
                _pieceAvailability.TryGetValue(pieceIndex, out var count);
                _pieceAvailability[pieceIndex] = count + 1;
            }
            else
            {
                if (_pieceAvailability.ContainsKey(pieceIndex) && _pieceAvailability[pieceIndex] > 0)
                {
                    _pieceAvailability[pieceIndex]--;
                    if (_pieceAvailability[pieceIndex] == 0)
                        _pieceAvailability.Remove(pieceIndex);
                }
            }
        }

        /// <summary>
        /// Обновляет BitField пира
        /// </summary>
        public void UpdatePeerBitField(BitField peerBitField)
        {
            if (peerBitField == null || peerBitField.Length != _totalPieces)
                return;

            for (int i = 0; i < _totalPieces; i++)
                UpdatePieceAvailability(i, peerBitField[i]);
        }

        /// <summary>
        /// Обновляет доступность при получении Have сообщения
        /// </summary>
        public void UpdateHave(int pieceIndex) => 
            UpdatePieceAvailability(pieceIndex, true);

        /// <summary>
        /// Помечает кусок как загружаемый
        /// </summary>
        public void MarkDownloading(int pieceIndex)
        {
            if (pieceIndex >= 0 && pieceIndex < _totalPieces)
                _downloadingPieces.Add(pieceIndex);
        }

        /// <summary>
        /// Снимает метку загрузки с куска
        /// </summary>
        public void UnmarkDownloading(int pieceIndex) => 
            _downloadingPieces.Remove(pieceIndex);

        /// <summary>
        /// Проверяет, загружается ли кусок
        /// </summary>
        public bool IsDownloading(int pieceIndex) => 
            _downloadingPieces.Contains(pieceIndex);

        #endregion
    }
}
