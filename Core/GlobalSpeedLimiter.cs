namespace TorrentClient.Core
{
    /// <summary>
    /// Глобальный ограничитель скорости для всех торрентов
    /// Использует алгоритм Token Bucket
    /// </summary>
    public sealed class GlobalSpeedLimiter : IDisposable
    {
        #region Singleton
        
        private static GlobalSpeedLimiter? _instance;
        private static readonly Lock _instanceLock = new();
        
        /// <summary>
        /// Получает единственный экземпляр GlobalSpeedLimiter (Singleton)
        /// </summary>
        public static GlobalSpeedLimiter Instance
        {
            get
            {
                if (_instance == null)
                {
                    using (_instanceLock.EnterScope())
                    {
                        _instance ??= new GlobalSpeedLimiter();
                    }
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Поля
        
        private readonly SemaphoreSlim _lock = new(1, 1);
        private long _downloadTokens;
        private long _uploadTokens;
        private DateTime _lastRefill = DateTime.UtcNow;
        
        #endregion
        
        #region Свойства
        
        /// <summary>Максимальная скорость загрузки (байт/сек, null = без ограничений)</summary>
        public long? MaxDownloadSpeed { get; set; }
        
        /// <summary>Максимальная скорость отдачи (байт/сек, null = без ограничений)</summary>
        public long? MaxUploadSpeed { get; set; }
        
        /// <summary>Текущая общая скорость загрузки</summary>
        public long CurrentDownloadSpeed { get; private set; }
        
        /// <summary>Текущая общая скорость отдачи</summary>
        public long CurrentUploadSpeed { get; private set; }
        
        #endregion
        
        #region Конструктор
        
        private GlobalSpeedLimiter()
        {
        }
        
        #endregion
        
        #region Публичные методы
        
        /// <summary>Обновляет лимиты скорости</summary>
        /// <param name="maxDownload">Максимальная скорость загрузки в байтах в секунду (null = без ограничений)</param>
        /// <param name="maxUpload">Максимальная скорость отдачи в байтах в секунду (null = без ограничений)</param>
        public void UpdateLimits(long? maxDownload, long? maxUpload)
        {
            _lock.Wait();
            try
            {
                MaxDownloadSpeed = maxDownload;
                MaxUploadSpeed = maxUpload;
                _downloadTokens = maxDownload ?? 0;
                _uploadTokens = maxUpload ?? 0;
                _lastRefill = DateTime.UtcNow;
            }
            finally
            {
                _lock.Release();
            }
        }
        
        /// <summary>Запрашивает токены для загрузки</summary>
        /// <param name="bytes">Количество байт для загрузки</param>
        /// <returns>true если токены доступны и загрузка может продолжаться, false если лимит превышен</returns>
        public bool TryConsumeDownload(int bytes)
        {
            if (MaxDownloadSpeed == null || MaxDownloadSpeed.Value <= 0)
                return true;
            
            _lock.Wait();
            try
            {
                RefillTokens();
                
                if (_downloadTokens >= bytes)
                {
                    _downloadTokens -= bytes;
                    return true;
                }
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }
        
        /// <summary>Асинхронно запрашивает токены для загрузки</summary>
        /// <param name="bytes">Количество байт для загрузки</param>
        /// <param name="ct">Токен отмены операции</param>
        /// <returns>true если токены доступны и загрузка может продолжаться, false если лимит превышен</returns>
        public async Task<bool> TryConsumeDownloadAsync(int bytes, CancellationToken ct = default)
        {
            if (MaxDownloadSpeed == null || MaxDownloadSpeed.Value <= 0)
                return true;
            
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                RefillTokens();
                
                if (_downloadTokens >= bytes)
                {
                    _downloadTokens -= bytes;
                    return true;
                }
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }
        
        /// <summary>Запрашивает токены для отдачи</summary>
        /// <param name="bytes">Количество байт для отдачи</param>
        /// <returns>true если токены доступны и отдача может продолжаться, false если лимит превышен</returns>
        public bool TryConsumeUpload(int bytes)
        {
            if (MaxUploadSpeed == null || MaxUploadSpeed.Value <= 0)
                return true;
            
            _lock.Wait();
            try
            {
                RefillTokens();
                
                if (_uploadTokens >= bytes)
                {
                    _uploadTokens -= bytes;
                    return true;
                }
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }
        
        /// <summary>Асинхронно запрашивает токены для отдачи</summary>
        /// <param name="bytes">Количество байт для отдачи</param>
        /// <param name="ct">Токен отмены операции</param>
        /// <returns>true если токены доступны и отдача может продолжаться, false если лимит превышен</returns>
        public async Task<bool> TryConsumeUploadAsync(int bytes, CancellationToken ct = default)
        {
            if (MaxUploadSpeed == null || MaxUploadSpeed.Value <= 0)
                return true;
            
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                RefillTokens();
                
                if (_uploadTokens >= bytes)
                {
                    _uploadTokens -= bytes;
                    return true;
                }
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }
        
        /// <summary>Ожидает доступность токенов для загрузки</summary>
        /// <param name="bytes">Количество байт для загрузки</param>
        /// <param name="ct">Токен отмены операции</param>
        public async Task WaitForDownloadTokensAsync(int bytes, CancellationToken ct = default)
        {
            if (MaxDownloadSpeed == null || MaxDownloadSpeed.Value <= 0)
                return;
            
            while (!ct.IsCancellationRequested)
            {
                if (await TryConsumeDownloadAsync(bytes, ct).ConfigureAwait(false))
                    return;
                await Task.Delay(10, ct).ConfigureAwait(false);
            }
        }
        
        /// <summary>Ожидает доступность токенов для отдачи</summary>
        /// <param name="bytes">Количество байт для отдачи</param>
        /// <param name="ct">Токен отмены операции</param>
        public async Task WaitForUploadTokensAsync(int bytes, CancellationToken ct = default)
        {
            if (MaxUploadSpeed == null || MaxUploadSpeed.Value <= 0)
                return;
            
            while (!ct.IsCancellationRequested)
            {
                if (await TryConsumeUploadAsync(bytes, ct).ConfigureAwait(false))
                    return;
                await Task.Delay(10, ct).ConfigureAwait(false);
            }
        }
        
        /// <summary>Обновляет текущую скорость (вызывается из UI)</summary>
        /// <param name="downloadSpeed">Текущая скорость загрузки в байтах в секунду</param>
        /// <param name="uploadSpeed">Текущая скорость отдачи в байтах в секунду</param>
        public void UpdateCurrentSpeeds(long downloadSpeed, long uploadSpeed)
        {
            CurrentDownloadSpeed = downloadSpeed;
            CurrentUploadSpeed = uploadSpeed;
        }
        
        #endregion
        
        #region Приватные методы
        
        private void RefillTokens()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastRefill).TotalSeconds;
            
            if (elapsed > 0)
            {
                if (MaxDownloadSpeed.HasValue && MaxDownloadSpeed.Value > 0)
                {
                    _downloadTokens += (long)(MaxDownloadSpeed.Value * elapsed);
                    _downloadTokens = Math.Min(_downloadTokens, MaxDownloadSpeed.Value * 2); // Буфер на 2 секунды
                }
                
                if (MaxUploadSpeed.HasValue && MaxUploadSpeed.Value > 0)
                {
                    _uploadTokens += (long)(MaxUploadSpeed.Value * elapsed);
                    _uploadTokens = Math.Min(_uploadTokens, MaxUploadSpeed.Value * 2);
                }
                
                _lastRefill = now;
            }
        }
        
        #endregion
        
        #region IDisposable
        
        /// <summary>
        /// Освобождает ресурсы, используемые GlobalSpeedLimiter
        /// </summary>
        public void Dispose()
        {
            _lock.Dispose();
            _instance = null;
        }
        
        #endregion
    }
}

