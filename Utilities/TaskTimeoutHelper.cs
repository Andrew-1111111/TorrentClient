namespace TorrentClient.Utilities
{
    /// <summary>
    /// Утилитный класс для работы с таймаутами задач
    /// </summary>
    internal static class TaskTimeoutHelper
    {
        /// <summary>
        /// Выполняет задачу с таймаутом. Если задача не завершается в указанное время, выбрасывает TimeoutException.
        /// </summary>
        /// <param name="task">Задача для выполнения</param>
        /// <param name="timeout">Таймаут</param>
        /// <exception cref="ArgumentNullException">Если task равен null</exception>
        /// <exception cref="TimeoutException">Если задача не завершилась в указанное время</exception>
        internal static async Task TimeoutAsync(Task? task, TimeSpan timeout)
        {
            ArgumentNullException.ThrowIfNull(task);

            using var timeoutCTS = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCTS.Token));
            if (completedTask == task)
            {
                timeoutCTS.Cancel();
                await task;  // Очень важно для распространения исключений
            }
            else
            {
                throw new TimeoutException("Операция превысила время ожидания");
            }
        }

        /// <summary>
        /// Выполняет задачу с таймаутом и возвращает результат. Если задача не завершается в указанное время, выбрасывает TimeoutException.
        /// </summary>
        /// <typeparam name="TResult">Тип результата задачи</typeparam>
        /// <param name="task">Задача для выполнения</param>
        /// <param name="timeout">Таймаут</param>
        /// <returns>Результат выполнения задачи</returns>
        /// <exception cref="ArgumentNullException">Если task равен null</exception>
        /// <exception cref="TimeoutException">Если задача не завершилась в указанное время</exception>
        internal static async Task<TResult> TimeoutAsync<TResult>(Task<TResult> task, TimeSpan timeout)
        {
            ArgumentNullException.ThrowIfNull(task);

            using var timeoutCTS = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCTS.Token));
            if (completedTask == task)
            {
                timeoutCTS.Cancel();
                return await task;  // Очень важно для распространения исключений
            }
            else
            {
                throw new TimeoutException("Операция превысила время ожидания");
            }
        }

        /// <summary>
        /// Выполняет ValueTask с таймаутом. Если задача не завершается в указанное время, выбрасывает TimeoutException.
        /// </summary>
        /// <param name="valueTask">ValueTask для выполнения</param>
        /// <param name="timeout">Таймаут</param>
        /// <exception cref="ArgumentNullException">Если valueTask равен null</exception>
        /// <exception cref="TimeoutException">Если задача не завершилась в указанное время</exception>
        internal static async Task TimeoutAsync(ValueTask valueTask, TimeSpan timeout)
        {
            var task = valueTask.AsTask();
            await TimeoutAsync(task, timeout);
        }

        /// <summary>
        /// Выполняет ValueTask с таймаутом и возвращает результат. Если задача не завершается в указанное время, выбрасывает TimeoutException.
        /// </summary>
        /// <typeparam name="TResult">Тип результата задачи</typeparam>
        /// <param name="valueTask">ValueTask для выполнения</param>
        /// <param name="timeout">Таймаут</param>
        /// <returns>Результат выполнения задачи</returns>
        /// <exception cref="ArgumentNullException">Если valueTask равен null</exception>
        /// <exception cref="TimeoutException">Если задача не завершилась в указанное время</exception>
        internal static async Task<TResult> TimeoutAsync<TResult>(ValueTask<TResult> valueTask, TimeSpan timeout)
        {
            var task = valueTask.AsTask();
            return await TimeoutAsync(task, timeout);
        }
    }
}

