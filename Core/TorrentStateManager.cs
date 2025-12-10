using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TorrentClient.Core.Interfaces;
using TorrentClient.Models;
using TorrentClient.Utilities;

namespace TorrentClient.Core
{
    /// <summary>
    /// Менеджер состояния торрентов - сохранение и загрузка прогресса
    /// </summary>
    public class TorrentStateManager : ITorrentStateStorage, IDisposable
    {
        #region Поля

        private readonly string _statePath;
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private bool _disposed;

        #endregion

        #region Конструктор

        public TorrentStateManager(string statePath)
        {
            if (string.IsNullOrWhiteSpace(statePath))
                throw new ArgumentException("Путь к директории состояний не может быть пустым", nameof(statePath));
            
            _statePath = Path.GetFullPath(statePath);
            
            try
            {
                // Создаем папку, если её нет
                if (!Directory.Exists(_statePath))
                {
                    Directory.CreateDirectory(_statePath);
                }
                
                // Проверяем, что папка действительно создана
                if (!Directory.Exists(_statePath))
                {
                    throw new InvalidOperationException($"Не удалось создать директорию состояний: {_statePath}");
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем выполнение
                System.Diagnostics.Debug.WriteLine($"Ошибка создания директории состояний: {_statePath}, Ошибка: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Сохранение состояния торрента

        /// <summary>Асинхронно сохраняет состояние торрента</summary>
        public async Task SaveTorrentStateAsync(Torrent torrent)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await SaveTorrentStateInternalAsync(torrent).ConfigureAwait(false);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>Синхронно сохраняет состояние торрента</summary>
        public void SaveTorrentState(Torrent torrent)
        {
            _fileLock.Wait();
            try
            {
                SaveTorrentStateInternalAsync(torrent).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task SaveTorrentStateInternalAsync(Torrent torrent)
        {
            try
            {
                string stateFile = Path.Combine(_statePath, $"{torrent.Info.InfoHash}.state");
                var state = new TorrentStateData
                {
                    InfoHash = torrent.Info.InfoHash,
                    TorrentFilePath = torrent.TorrentFilePath,
                    DownloadPath = torrent.DownloadPath,
                    DownloadedBytes = torrent.DownloadedBytes,
                    UploadedBytes = torrent.UploadedBytes,
                    BitField = torrent.BitField?.ToByteArray(),
                    FileInfos = torrent.FileInfos,
                    MaxDownloadSpeed = torrent.MaxDownloadSpeed,
                    MaxUploadSpeed = torrent.MaxUploadSpeed,
                    Priority = torrent.Priority
                };

                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(stateFile, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[StateManager] Ошибка сохранения состояния: {ex.Message}");
            }
        }

        #endregion

        #region Сохранение списка торрентов

        /// <summary>Асинхронно сохраняет список торрентов</summary>
        public async Task SaveTorrentListAsync(List<Torrent> torrents)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await SaveTorrentListInternalAsync(torrents).ConfigureAwait(false);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>Синхронно сохраняет список торрентов</summary>
        public void SaveTorrentList(List<Torrent> torrents)
        {
            _fileLock.Wait();
            try
            {
                SaveTorrentListInternalAsync(torrents).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task SaveTorrentListInternalAsync(List<Torrent> torrents)
        {
            try
            {
                string listFile = Path.Combine(_statePath, "torrents.json");
                var listData = new TorrentListData
                {
                    Torrents = torrents.Select(t => new TorrentStateData
                    {
                        InfoHash = t.Info.InfoHash,
                        TorrentFilePath = t.TorrentFilePath,
                        DownloadPath = t.DownloadPath
                    }).ToList()
                };

                string json = JsonSerializer.Serialize(listData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(listFile, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[StateManager] Ошибка сохранения списка торрентов: {ex.Message}");
            }
        }

        #endregion

        #region Загрузка списка торрентов

        /// <summary>Асинхронно загружает список торрентов</summary>
        public async Task<List<TorrentStateData>> LoadTorrentListAsync()
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await LoadTorrentListInternalAsync().ConfigureAwait(false);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>Синхронно загружает список торрентов</summary>
        public List<TorrentStateData> LoadTorrentList()
        {
            _fileLock.Wait();
            try
            {
                return LoadTorrentListInternalAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task<List<TorrentStateData>> LoadTorrentListInternalAsync()
        {
            try
            {
                string listFile = Path.Combine(_statePath, "torrents.json");
                if (!File.Exists(listFile))
                    return [];

                string json = await File.ReadAllTextAsync(listFile).ConfigureAwait(false);
                var listData = JsonSerializer.Deserialize<TorrentListData>(json);
                return listData?.Torrents ?? [];
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[StateManager] Ошибка загрузки списка торрентов: {ex.Message}");
                return [];
            }
        }

        #endregion

        #region Загрузка состояния торрента

        /// <summary>Асинхронно загружает состояние торрента</summary>
        public async Task<TorrentStateData?> LoadTorrentStateAsync(string infoHash)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await LoadTorrentStateInternalAsync(infoHash).ConfigureAwait(false);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>Синхронно загружает состояние торрента</summary>
        public TorrentStateData? LoadTorrentState(string infoHash)
        {
            _fileLock.Wait();
            try
            {
                return LoadTorrentStateInternalAsync(infoHash).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task<TorrentStateData?> LoadTorrentStateInternalAsync(string infoHash)
        {
            try
            {
                string stateFile = Path.Combine(_statePath, $"{infoHash}.state");
                if (!File.Exists(stateFile))
                    return null;

                string json = await File.ReadAllTextAsync(stateFile).ConfigureAwait(false);
                return JsonSerializer.Deserialize<TorrentStateData>(json);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[StateManager] Ошибка загрузки состояния: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Удаление состояния торрента

        /// <summary>Асинхронно удаляет файл состояния торрента</summary>
        public async Task DeleteTorrentStateAsync(string infoHash)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                DeleteTorrentStateInternal(infoHash);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>Синхронно удаляет файл состояния торрента</summary>
        public void DeleteTorrentState(string infoHash)
        {
            _fileLock.Wait();
            try
            {
                DeleteTorrentStateInternal(infoHash);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private void DeleteTorrentStateInternal(string infoHash)
        {
            try
            {
                string stateFile = Path.Combine(_statePath, $"{infoHash}.state");
                if (File.Exists(stateFile))
                    File.Delete(stateFile);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[StateManager] Ошибка удаления состояния: {ex.Message}");
            }
        }

        #endregion

        #region Восстановление состояния

        /// <summary>Восстанавливает состояние торрента из сохранённых данных</summary>
        public void RestoreTorrentState(Torrent torrent, TorrentStateData? state)
        {
            if (state == null)
                return;

            torrent.DownloadedBytes = state.DownloadedBytes;
            torrent.UploadedBytes = state.UploadedBytes;

            // Восстанавливаем BitField
            if (state.BitField != null && torrent.BitField != null)
            {
                torrent.BitField.FromByteArray(state.BitField);
            }

            // Восстанавливаем информацию о файлах
            if (state.FileInfos != null && torrent.FileInfos != null)
            {
                foreach (var fileState in state.FileInfos)
                {
                    var fileInfo = torrent.FileInfos.FirstOrDefault(f => f.Path == fileState.Path);
                    if (fileInfo != null)
                    {
                        fileInfo.Downloaded = fileState.Downloaded;
                        fileInfo.MaxSpeed = fileState.MaxSpeed;
                        fileInfo.IsSelected = fileState.IsSelected;
                    }
                }
            }

            // Восстанавливаем ограничения скорости
            torrent.MaxDownloadSpeed = state.MaxDownloadSpeed;
            torrent.MaxUploadSpeed = state.MaxUploadSpeed;
            
            // Восстанавливаем приоритет
            torrent.Priority = state.Priority;
        }

        #endregion
        
        #region IDisposable
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            _fileLock.Dispose();
            GC.SuppressFinalize(this);
        }
        
        #endregion
    }

    #region Модели данных

    /// <summary>Данные состояния торрента</summary>
    public class TorrentStateData
    {
        /// <summary>InfoHash торрента</summary>
        public string InfoHash { get; set; } = string.Empty;

        /// <summary>Путь к .torrent файлу</summary>
        public string TorrentFilePath { get; set; } = string.Empty;

        /// <summary>Путь загрузки</summary>
        public string DownloadPath { get; set; } = string.Empty;

        /// <summary>Загружено байт</summary>
        public long DownloadedBytes { get; set; }

        /// <summary>Отдано байт</summary>
        public long UploadedBytes { get; set; }

        /// <summary>Битовое поле загруженных кусков</summary>
        public byte[]? BitField { get; set; }

        /// <summary>Информация о файлах</summary>
        public List<FileDownloadInfo>? FileInfos { get; set; }

        /// <summary>Ограничение скорости загрузки</summary>
        public long? MaxDownloadSpeed { get; set; }

        /// <summary>Ограничение скорости отдачи</summary>
        public long? MaxUploadSpeed { get; set; }
        
        /// <summary>Приоритет торрента (0 = низкий, 1 = нормальный, 2 = высокий)</summary>
        public int Priority { get; set; } = 1;
    }

    /// <summary>Список торрентов для сохранения</summary>
    public class TorrentListData
    {
        /// <summary>Список торрентов</summary>
        public List<TorrentStateData> Torrents { get; set; } = new();
    }

    #endregion
}
