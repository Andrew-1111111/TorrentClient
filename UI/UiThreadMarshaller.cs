using System.Windows.Forms;

namespace TorrentClient.UI
{
    /// <summary>
    /// Унифицированный помощник для работы с UI потоком
    /// Предотвращает накопление BeginInvoke и упрощает работу с потоками
    /// </summary>
    public static class UiThreadMarshaller
    {
        /// <summary>
        /// Выполняет действие в UI потоке с защитой от накопления вызовов
        /// </summary>
        public static void InvokeSafe(Control control, Action action)
        {
            if (control == null || action == null)
                return;

            // КРИТИЧНО: Проверяем, не disposed ли Control для предотвращения утечки памяти
            if (control.IsDisposed || !control.IsHandleCreated)
                return;

            if (control.InvokeRequired)
            {
                try
                {
                    control.BeginInvoke(action);
                }
                catch (ObjectDisposedException)
                {
                    // Игнорируем, если Control уже disposed
                }
                catch (InvalidOperationException)
                {
                    // Игнорируем, если Handle не создан
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Выполняет действие в UI потоке синхронно
        /// </summary>
        public static void Invoke(Control control, Action action)
        {
            if (control == null || action == null)
                return;

            // КРИТИЧНО: Проверяем, не disposed ли Control для предотвращения утечки памяти
            if (control.IsDisposed || !control.IsHandleCreated)
                return;

            if (control.InvokeRequired)
            {
                try
                {
                    control.Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                    // Игнорируем, если Control уже disposed
                }
                catch (InvalidOperationException)
                {
                    // Игнорируем, если Handle не создан
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Выполняет действие в UI потоке с защитой от накопления (с ключом)
        /// </summary>
        public static void InvokeSafeWithKey(Control control, string key, Action action, HashSet<string> pendingKeys)
        {
            if (control == null || action == null)
                return;

            // КРИТИЧНО: Проверяем, не disposed ли Control для предотвращения утечки памяти
            if (control.IsDisposed || !control.IsHandleCreated)
            {
                // Очищаем ключ из pendingKeys, если Control disposed
                lock (pendingKeys)
                {
                    pendingKeys.Remove(key);
                }
                return;
            }

            if (control.InvokeRequired)
            {
                lock (pendingKeys)
                {
                    if (pendingKeys.Contains(key))
                        return; // Уже есть ожидающее обновление
                    pendingKeys.Add(key);
                }

                try
                {
                    control.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // КРИТИЧНО: Проверяем еще раз перед выполнением
                            if (!control.IsDisposed && control.IsHandleCreated)
                            {
                                action();
                            }
                        }
                        finally
                        {
                            lock (pendingKeys)
                            {
                                pendingKeys.Remove(key);
                            }
                        }
                    }));
                }
                catch (ObjectDisposedException)
                {
                    // Очищаем ключ при ошибке
                    lock (pendingKeys)
                    {
                        pendingKeys.Remove(key);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Очищаем ключ при ошибке
                    lock (pendingKeys)
                    {
                        pendingKeys.Remove(key);
                    }
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Выполняет асинхронное действие в UI потоке
        /// </summary>
        public static async Task InvokeAsync(Control control, Func<Task> asyncAction)
        {
            if (control == null || asyncAction == null)
                return;

            if (control.InvokeRequired)
            {
                await Task.Run(async () =>
                {
                    await control.Invoke(async () => await asyncAction());
                });
            }
            else
            {
                await asyncAction();
            }
        }
    }
}

