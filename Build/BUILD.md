# Инструкции по сборке TorrentClient

## Быстрый старт

### Windows (PowerShell)

Если у вас включено выполнение PowerShell скриптов:

```powershell
# Сборка всех версий
.\Build\build-all.ps1

# Сборка одной версии
.\Build\build.ps1 -Platform win-x64 -SelfContained
```

Если выполнение скриптов запрещено, используйте batch файл:

```cmd
Build\build.bat win-x64
```

### Ручная сборка через dotnet CLI

```bash
# Windows x64 (self-contained)
dotnet publish -c Release -r win-x64 -o publish/win-x64-self-contained -p:PublishSingleFile=true -p:SelfContained=true

# Windows x64 (framework-dependent)
dotnet publish -c Release -r win-x64 -o publish/win-x64-framework-dependent -p:PublishSingleFile=true -p:SelfContained=false

# Windows x86 (32-bit)
dotnet publish -c Release -r win-x86 -o publish/win-x86-self-contained -p:PublishSingleFile=true -p:SelfContained=true

# Windows ARM64
dotnet publish -c Release -r win-arm64 -o publish/win-arm64-self-contained -p:PublishSingleFile=true -p:SelfContained=true
```

## Поддерживаемые платформы

- **win-x64** - Windows 64-bit (рекомендуется)
- **win-x86** - Windows 32-bit (для старых систем)
- **win-arm64** - Windows ARM64 (для новых ARM устройств)

## Типы сборок

### Self-Contained

Включает .NET Runtime, не требует установленного .NET SDK.
- Размер: ~70-100 MB
- Преимущества: Работает на любой Windows системе
- Недостатки: Больший размер файла

### Framework-Dependent

Требует установленный .NET 9.0 Runtime.
- Размер: ~5-10 MB
- Преимущества: Меньший размер
- Недостатки: Требует установленный .NET 9.0

## Разрешение выполнения PowerShell скриптов

Если при выполнении `.\Build\build.ps1` вы получаете ошибку о политике выполнения:

```powershell
# Проверить текущую политику
Get-ExecutionPolicy

# Временно разрешить выполнение (только для текущей сессии)
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process

# Или разрешить для текущего пользователя (постоянно)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

**Внимание**: Изменение политики выполнения требует прав администратора.

## Кодировка UTF-8

Все PowerShell скрипты сборки настроены на использование кодировки UTF-8 для корректного вывода:
- `$OutputEncoding = [System.Text.Encoding]::UTF8` - для пайпов
- `[Console]::OutputEncoding = [System.Text.Encoding]::UTF8` - для консоли
- `[Console]::InputEncoding = [System.Text.Encoding]::UTF8` - для ввода

Это обеспечивает корректное отображение многоязычного контента в консоли.

## GitHub Actions

При создании тега версии автоматически запускается сборка:

```bash
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions автоматически:
1. Запустит все тесты
2. Соберёт версии для всех платформ
3. Создаст релиз с артефактами

## Структура выходных файлов

После сборки структура будет следующей:

```
publish/
├── win-x64-self-contained/
│   └── TorrentClient.exe
├── win-x64-framework-dependent/
│   └── TorrentClient.exe
├── win-x86-self-contained/
│   └── TorrentClient.exe
└── win-arm64-self-contained/
    └── TorrentClient.exe
```

## Создание ZIP архивов

Для распространения можно создать ZIP архивы:

```powershell
# Windows PowerShell
Compress-Archive -Path "publish\win-x64-self-contained\*" -DestinationPath "TorrentClient-win-x64.zip" -Force
```

```bash
# Git Bash / Linux
cd publish/win-x64-self-contained
zip -r ../../TorrentClient-win-x64.zip .
```

## Требования для сборки

- .NET 9.0 SDK или выше
- Windows 10/11 (для сборки Windows версий)
- Visual Studio 2022 или Visual Studio Code (опционально)

## Устранение проблем

### Ошибка "The specified RuntimeIdentifier 'win-x86' is not recognized"

Убедитесь, что у вас установлен .NET 9.0 SDK с поддержкой всех платформ:

```bash
dotnet --list-sdks
```

Если нужной платформы нет, установите соответствующий workload:

```bash
dotnet workload install
```

### Ошибка компиляции

Убедитесь, что все зависимости восстановлены:

```bash
dotnet restore
dotnet build
```

### Тесты не проходят

Перед сборкой релиза убедитесь, что все тесты проходят:

```bash
dotnet test
```
