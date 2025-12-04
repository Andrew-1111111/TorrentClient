namespace TorrentClient
{
    internal static class Program
    {
        private const string MutexName = "TorrentClient_SingleInstance_Mutex";
        private static Mutex? _mutex;

        /// <summary>
        /// Главная точка входа приложения
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Включаем поддержку высокого DPI с поддержкой нескольких мониторов (до инициализации Application)
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            
            // КРИТИЧНО: Проверяем, не запущен ли уже экземпляр приложения
            bool createdNew = false;
            try
            {
                _mutex = new Mutex(true, MutexName, out createdNew);
            }
            catch (Exception ex)
            {
                // Ошибка при создании Mutex - продолжаем работу
                Debug.WriteLine($"Ошибка создания Mutex: {ex.Message}");
                createdNew = true;
            }
            
            if (!createdNew)
            {
                // Приложение уже запущено - закрываем предыдущий экземпляр
                Debug.WriteLine("Обнаружен предыдущий экземпляр приложения, закрываем его...");
                CloseExistingInstance();
                
                // Ждём немного, чтобы предыдущий экземпляр успел закрыться
                Thread.Sleep(2000);
                
                // Пытаемся создать Mutex снова
                _mutex?.Dispose();
                _mutex = null;
                try
                {
                    _mutex = new Mutex(true, MutexName, out createdNew);
                    if (!createdNew)
                    {
                        Debug.WriteLine("Предыдущий экземпляр всё ещё запущен, продолжаем работу...");
                    }
                }
                catch (Exception ex)
                {
                    // Ошибка - продолжаем работу
                    Debug.WriteLine($"Ошибка повторного создания Mutex: {ex.Message}");
                    createdNew = true;
                }
            }
            
            try
            {
                // Явно создаем папки приложения при старте
                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var settingsDir = Path.Combine(appDirectory, "Settings");
                var statesDir = Path.Combine(appDirectory, "States");
                var downloadsDir = Path.Combine(appDirectory, "Downloads");
                
                try
                {
                    if (!Directory.Exists(settingsDir))
                        Directory.CreateDirectory(settingsDir);
                    if (!Directory.Exists(statesDir))
                        Directory.CreateDirectory(statesDir);
                    if (!Directory.Exists(downloadsDir))
                        Directory.CreateDirectory(downloadsDir);
                }
                catch (Exception dirEx)
                {
                    Debug.WriteLine($"Предупреждение: не удалось создать папки приложения: {dirEx.Message}");
                }
                
                // Загружаем настройки и применяем настройку логирования
                var settingsManager = new AppSettingsManager();
                var settings = settingsManager.LoadSettings();
                
                // Инициализация логирования с учетом настроек
                Logger.Initialize();
                Logger.SetEnabled(settings.EnableLogging);

                // Включаем режим уплотнения кучи сборщика мусора (GC) в .NET.
                AppContext.SetSwitch("System.GC.HeapCompactionMode", true);

                // Обработка необработанных исключений
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += Application_ThreadException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                try
                {
                    Logger.LogInfo("=== TorrentClient Приложение Запущено ===");

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new MainForm());
                }
                catch (Exception ex)
                {
                    Logger.LogError("Критическая ошибка в Main", ex);
                    MessageBox.Show($"Критическая ошибка приложения:\n{ex.Message}\n\nПодробности в лог-файле.", 
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Logger.LogInfo("=== TorrentClient Приложение Завершено ===");
                    // Освобождаем HttpClientService (синглтон)
                    try
                    {
                        HttpClientService.Instance.Dispose();
                    }
                    catch { }
                }
            }
            finally
            {
                // КРИТИЧНО: Освобождаем Mutex при выходе
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
        }

        /// <summary>
        /// Закрывает существующий экземпляр приложения
        /// </summary>
        private static void CloseExistingInstance()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var processName = currentProcess.ProcessName;
                var processes = Process.GetProcessesByName(processName);
                
                bool foundExisting = false;
                foreach (var process in processes)
                {
                    // Пропускаем текущий процесс
                    if (process.Id == currentProcess.Id)
                        continue;
                    
                    foundExisting = true;
                    
                    try
                    {
                        // Пытаемся корректно закрыть процесс
                        if (!process.HasExited)
                        {
                            // Сначала пытаемся закрыть главное окно (если есть)
                            if (process.MainWindowHandle != IntPtr.Zero)
                            {
                                // Отправляем сообщение WM_CLOSE для корректного закрытия
                                const int WM_CLOSE = 0x0010;
                                NativeMethods.SendMessage(process.MainWindowHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                                
                                // Ждём закрытия процесса (макс 3 секунды)
                                if (process.WaitForExit(3000))
                                {
                                    // Logger может быть ещё не инициализирован
                                    try { Logger.LogInfo($"Предыдущий экземпляр приложения закрыт корректно (PID: {process.Id})"); } catch { }
                                    continue;
                                }
                            }
                            
                            // Если не удалось закрыть корректно - принудительно завершаем
                            try { Logger.LogWarning($"Принудительное завершение предыдущего экземпляра (PID: {process.Id})"); } catch { }
                            process.Kill();
                            
                            // Ждём завершения
                            if (!process.HasExited)
                            {
                                process.WaitForExit(2000);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Logger может быть ещё не инициализирован
                        try { Logger.LogWarning($"Ошибка при закрытии процесса {process.Id}: {ex.Message}"); } catch { }
                    }
                    finally
                    {
                        process?.Dispose();
                    }
                }
                
                // Если не нашли существующий экземпляр - это нормально при первом запуске
                if (!foundExisting)
                {
                    // Это может быть первый запуск или Mutex был заблокирован по другой причине
                    // Продолжаем работу
                }
            }
            catch (Exception ex)
            {
                // Logger может быть ещё не инициализирован
                try { Logger.LogError("Ошибка при поиске и закрытии предыдущего экземпляра", ex); } catch { }
            }
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Logger.LogError("Необработанное исключение в потоке UI", e.Exception);
            MessageBox.Show($"Произошла ошибка:\n{e.Exception.Message}\n\nПодробности в лог-файле.", 
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Logger.LogError("Необработанное исключение в домене приложения", ex);
            }
            else
            {
                Logger.LogError($"Unhandled exception: {e.ExceptionObject}");
            }
        }
    }

    /// <summary>
    /// Нативные методы Windows API для работы с окнами
    /// </summary>
    internal static partial class NativeMethods
    {
        [LibraryImport("user32.dll")]
        internal static partial IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}