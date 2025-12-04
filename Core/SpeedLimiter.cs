using System;
using System.Threading;
using System.Threading.Tasks;

namespace TorrentClient.Core
{
    /// <summary>
    /// Ограничитель скорости передачи данных (индивидуальное ограничение для торрента)
    /// </summary>
    public class SpeedLimiter : IDisposable
    {
        #region Поля

        private long? _maxBytesPerSecond;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private long _bytesTransferred;
        private DateTime _lastResetTime = DateTime.Now;

        #endregion

        #region Конструктор

        /// <summary>
        /// Создает новый экземпляр ограничителя скорости
        /// </summary>
        /// <param name="maxBytesPerSecond">Максимальная скорость в байтах в секунду (null = без ограничений)</param>
        public SpeedLimiter(long? maxBytesPerSecond)
        {
            _maxBytesPerSecond = maxBytesPerSecond;
        }

        #endregion

        #region Публичные методы

        /// <summary>
        /// Ожидает, если превышен лимит скорости
        /// </summary>
        /// <param name="bytesToTransfer">Количество байт для передачи</param>
        /// <param name="ct">Токен отмены операции</param>
        public async Task WaitIfNeededAsync(int bytesToTransfer, CancellationToken ct = default)
        {
            if (_maxBytesPerSecond == null || _maxBytesPerSecond.Value <= 0)
                return;

            await _lock.WaitAsync(ct);
            try
            {
                var elapsed = DateTime.Now - _lastResetTime;

                // Сброс счетчика каждую секунду
                if (elapsed.TotalSeconds >= 1.0)
                {
                    _bytesTransferred = 0;
                    _lastResetTime = DateTime.Now;
                }

                _bytesTransferred += bytesToTransfer;

                // Если превышен лимит - ждем
                if (_bytesTransferred > _maxBytesPerSecond.Value)
                {
                    var excessBytes = _bytesTransferred - _maxBytesPerSecond.Value;
                    var waitMs = (int)((double)excessBytes / _maxBytesPerSecond.Value * 1000);

                    if (waitMs > 0)
                    {
                        await Task.Delay(waitMs, ct);
                        _bytesTransferred = _maxBytesPerSecond.Value;
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Сбрасывает счетчики передаваемых байт и время последнего сброса
        /// </summary>
        public void Reset()
        {
            _lock.Wait();
            try
            {
                _bytesTransferred = 0;
                _lastResetTime = DateTime.Now;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Обновляет лимит скорости (индивидуальное ограничение для торрента)
        /// </summary>
        /// <param name="newMaxBytesPerSecond">Новый лимит скорости в байтах в секунду (null = без ограничений)</param>
        public void UpdateLimit(long? newMaxBytesPerSecond)
        {
            _lock.Wait();
            try
            {
                _maxBytesPerSecond = newMaxBytesPerSecond;
                _bytesTransferred = 0; // Сбрасываем счетчик при изменении лимита
                _lastResetTime = DateTime.Now;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Освобождает ресурсы, используемые SpeedLimiter
        /// </summary>
        public void Dispose()
        {
            _lock?.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
