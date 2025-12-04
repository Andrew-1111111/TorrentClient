using System;
using System.Threading.Tasks;

namespace TorrentClient.Utilities
{
    /// <summary>
    /// Утилита для безопасного запуска fire-and-forget задач
    /// Обрабатывает задачи с безопасной обработкой исключений
    /// </summary>
    public static class SafeTaskRunner
    {
        /// <summary>
        /// Безопасно запускает fire-and-forget задачу с обработкой исключений
        /// </summary>
        public static void RunSafe(Func<Task> taskFactory)
        {
            if (taskFactory == null)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await taskFactory().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // через накопление необработанных исключений в TaskScheduler
                    Logger.LogError("[SafeTaskRunner] Необработанное исключение в fire-and-forget задаче", ex);
                }
            });
        }

        /// <summary>
        /// Безопасно запускает fire-and-forget задачу с обработкой исключений
        /// </summary>
        public static void RunSafe(Action action)
        {
            if (action == null)
                return;

            _ = Task.Run(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Logger.LogError("[SafeTaskRunner] Необработанное исключение в fire-and-forget задаче", ex);
                }
            });
        }
    }
}

