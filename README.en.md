# TorrentClient

BitTorrent client for Windows, written in C# using .NET 9.0 and Windows Forms.

## 🚀 Features

- ✅ **Full BitTorrent Protocol Support**
  - HTTP/HTTPS trackers
  - UDP trackers
  - TCP trackers
  - DHT (Distributed Hash Table)
  - Peer Exchange (PEX)
  - Local Service Discovery

- ✅ **Download Management**
  - Multiple simultaneous downloads
  - Torrent prioritization (low, normal, high priority)
  - File prioritization (low, normal, high priority)
  - Automatic download resumption
  - Torrent state saving
  - Automatic torrent sorting by priority

- ✅ **Speed Control**
  - Global speed limits for download/upload (shared across all torrents)
  - Individual limits for each torrent (in Mbps or MB/s for small values)
  - Token Bucket algorithm for precise speed control
  - Visual indicators (⚡) for torrents with active limits
  - Display of current speed and set limits in the interface
  - Accurate speed calculations using decimal system (1 Mbps = 1,000,000 bits/s)
  - Limit display: Mbps for values ≥ 1 Mbps, MB/s for values < 1 Mbps

- ✅ **User Interface**
  - Modern Windows Forms interface
  - **Full interface localization** - support for 50+ world languages
  - Automatic language detection from Windows settings
  - Language selection in global settings with persistence after restart
  - System tray with download/upload speed notifications
  - Real-time progress and statistics display
  - Visual indicators for torrents with speed limits
  - Display of current speed and limits in the "Speed" column
  - Detailed tooltips for "Speed" and "Peers" columns
  - Context menu for each torrent (start, pause, stop, speed limit, set priority, remove)
  - Torrent management (add, remove, pause)
  - Configuration of global and individual limits through convenient forms

- ✅ **Additional Features**
  - **Multilingual support** - interface in 50+ languages
  - Cookie and header support for trackers
  - Operation logging
  - Automatic state saving
  - Thread-safe architecture

## 📋 Requirements

- **.NET 9.0 SDK** or higher
- **Windows 10/11** (x64)
- **Visual Studio 2022** or **Visual Studio Code** (for development)

## 🛠️ Building

### Cloning the repository

```bash
git clone https://github.com/yourusername/TorrentClient.git
cd TorrentClient
```

### Building the project

```bash
dotnet build
```

### Running tests

```bash
dotnet test
```

### Publishing

#### Quick build of one version

```powershell
# Windows x64 (self-contained)
.\Build\build.ps1 -Platform win-x64 -SelfContained

# Windows x64 (framework-dependent, requires installed .NET 9.0)
.\Build\build.ps1 -Platform win-x64 -FrameworkDependent

# Windows x86 (32-bit)
.\Build\build.ps1 -Platform win-x86 -SelfContained

# Windows ARM64
.\Build\build.ps1 -Platform win-arm64 -SelfContained
```

#### Building all versions

```powershell
.\Build\build-all.ps1
# or
.\Build\build.ps1 -All
```

**Note**: If PowerShell script execution is disabled, use `Build\build.bat win-x64` or see [Build/BUILD.md](Build/BUILD.md) for detailed instructions.

This will create builds for all supported platforms:
- **win-x64-self-contained** - Windows x64 (includes .NET Runtime, ~70-100 MB)
- **win-x64-framework-dependent** - Windows x64 (requires installed .NET 9.0, ~5-10 MB)
- **win-x86-self-contained** - Windows x86 32-bit (includes .NET Runtime)

All builds will be saved in the `publish/` folder.

#### Manual publishing

```bash
# Self-contained (includes .NET Runtime)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Framework-dependent (requires installed .NET 9.0)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## 📦 Project Structure

```
TorrentClient/
├── Build/                   # Build scripts
│   ├── build.ps1           # PowerShell build script
│   ├── build-all.ps1       # Script to build all versions
│   ├── build.bat           # Batch build script
│   └── BUILD.md            # Build instructions
├── Core/                    # Core business logic
│   ├── TorrentManager.cs   # Torrent manager
│   ├── TorrentDownloader.cs # Torrent downloader
│   ├── AppSettingsManager.cs # Application settings manager
│   ├── GlobalSpeedLimiter.cs # Global speed limiter
│   └── SpeedLimiter.cs     # Speed limiter for torrent
├── Engine/                  # BitTorrent engine
│   ├── TorrentClient.cs    # Main client
│   ├── Swarm.cs            # Peer management
│   ├── Wire.cs             # Data exchange protocol
│   └── Storage.cs          # File management
├── Protocol/                # Protocols and networking
│   ├── TrackerClient.cs    # Tracker client
│   ├── PeerConnection.cs   # Peer connections
│   ├── DhtNode.cs          # DHT node
│   └── PiecePicker.cs      # Piece selection with file priority consideration
├── UI/                      # User interface
│   ├── MainForm.cs         # Main form
│   ├── TorrentListViewUpdater.cs # Torrent list update
│   └── Services/           # UI services
│       ├── TrayIconManager.cs # System tray management
│       └── MainFormPresenter.cs # Main form presenter
├── Resources/               # Localization resources
│   ├── Strings.resx        # English (default)
│   ├── Strings.ru.resx     # Russian
│   ├── Strings.es.resx     # Spanish
│   ├── Strings.fr.resx     # French
│   └── ...                 # Other languages
├── GlobalSettingsForm.cs   # Global settings form
├── SpeedSettingsForm.cs    # Torrent speed settings form
├── Utilities/               # Utilities
│   ├── Bencode.cs          # Bencode parsing
│   └── Logger.cs           # Logging
└── TorrentClient.Tests/     # Tests
    ├── BencodeParserTests.cs
    ├── SpeedLimiterTests.cs
    └── ...
```

## 🧪 Testing

The project includes a comprehensive set of unit tests using xUnit:

- **BencodeParserTests** - Bencode parser tests
- **SpeedLimiterTests** - Torrent speed limiter tests
- **SpeedLimiterEdgeCasesTests** - Speed limiter edge case tests
- **SpeedLimiterUpdateLimitTests** - Speed limit update tests
- **GlobalSpeedLimiterTests** - Global speed limiter tests
- **SpeedConversionTests** - Speed unit conversion tests
- **SpeedCalculationTests** - Download/upload speed calculation tests
- **UpdateThrottlerTests** - UI update throttling tests
- **PiecePickerTests** - Piece selection tests
- **AppSettingsManagerTests** - Settings manager tests (including limit saving)
- **TaskTimeoutHelperTests** - Timeout utility tests
- **UploadStatisticsTests** - Upload statistics tests
- **TorrentPriorityTests** - Torrent priority functionality tests
- **LocalizationTests** - Localization functionality tests

Run all tests:

```bash
dotnet test --verbosity normal
```

## 🏗️ Architecture

The project follows SOLID principles and uses:

- **SRP (Single Responsibility Principle)** - each class has a single responsibility
- **DIP (Dependency Inversion Principle)** - dependency on abstractions through interfaces
- **Asynchronous programming** - use of async/await for non-blocking operations
- **Thread safety** - use of `Lock`, `SemaphoreSlim` and other synchronization primitives
- **Memory management** - preventing memory leaks through proper use of `IDisposable`

### Speed Limiting System

The project uses a two-level speed limiting system:

1. **GlobalSpeedLimiter** (Singleton) - global limiter applied to all torrents
2. **SpeedLimiter** - individual limiter for each torrent

Both use the **Token Bucket** algorithm for precise speed control:
- Tokens accumulate at a given rate
- Operations consume tokens before data transfer
- When tokens are insufficient, operations wait for their accumulation

### Speed Calculation

All speed calculations use the **decimal system** (according to the [Data-rate units](https://en.wikipedia.org/wiki/Data-rate_units) standard):
- 1 Mbps = 1,000,000 bits/second (not 1,048,576)
- 1 MB/s = 1,000,000 bytes/second
- 1 Kbps = 1,000 bits/second
- Conversion: `bytes/second * 8.0 / 1_000_000.0 = Mbps`
- Conversion: `bytes/second / 1_000_000.0 = MB/s`

This ensures compliance with standard internet connection speed measurement units.

**Speed limit display:**
- For values ≥ 1 Mbps: displayed in **Mbps** (e.g., "10.0 Mbps")
- For values < 1 Mbps: displayed in **MB/s** (e.g., "0.13 MB/s")

## 📝 Usage

1. Launch the application
2. Add a `.torrent` file via menu or drag-and-drop
3. Select download path
4. Configure speed limits:
   - **Global limits**: "Global Settings" button → set shared limits for all torrents
   - **Torrent limits**: Right-click on torrent → "Speed Limit" → set individual limits
5. Track progress in the main window:
   - Torrents with active limits are marked with ⚡ icon
   - The "Speed" column displays current speed and set limits (Mbps or MB/s)
   - The "Priority" column displays torrent priority (Low, Normal, High)
   - Torrents are automatically sorted by priority (high → normal → low)
   - Hover over torrent or "Speed"/"Peers" columns for detailed information
6. Torrent management:
   - Right-click on torrent → context menu with options: start, pause, stop, speed limit, set priority, remove
   - Priority can be set via "Priority" menu → High/Normal/Low

## ⚙️ Settings

The application saves settings in `appsettings.json`:

- **Interface language** - language selection from 50+ supported languages (saved after restart)
- **Default download path** - automatically saved
- **Global speed limits** - download and upload limits (in bytes/sec), applied to all torrents
- **Individual torrent limits** - saved separately for each torrent (in Mbps)
- **Cookies and headers for trackers** - settings for tracker authentication
- **Maximum connection count** - limit on simultaneous connections
- **Enable/disable logging** - control log detail level

### Localization

The application supports full interface localization in 50+ languages:
- **Automatic language detection** - on first launch, language is detected from Windows settings
- **Language selection** - Settings → Language → select desired language
- **Language persistence** - selected language is saved and applied on next launch
- **Supported languages**: English, Russian, Spanish, French, German, Italian, Portuguese, Chinese, Japanese, Korean, Arabic, Hindi, Turkish, Polish, Dutch, Swedish, Czech, Ukrainian, Vietnamese, Thai, Indonesian, Hebrew, Romanian, Hungarian, Finnish, Danish, Norwegian, Greek, Bulgarian, Croatian, Slovak, Serbian, Slovenian, Estonian, Latvian, Lithuanian, Macedonian, Albanian, Icelandic, Irish, Maltese, Welsh, Basque, Catalan, Galician and many others, including regional variants (pt-BR, zh-CN, zh-TW, es-MX, es-AR, fr-CA)

### Speed Limit Format

- **Global limits**: Saved in bytes per second (bytes/second)
- **Torrent limits**: Set in Mbps (megabits per second), displayed in Mbps or MB/s
- **Limit display**:
  - Values ≥ 1 Mbps: displayed in **Mbps** (e.g., "10.0 Mbps")
  - Values < 1 Mbps: displayed in **MB/s** (e.g., "0.13 MB/s")
- **Conversion**: Uses decimal system (1 Mbps = 1,000,000 bits/s = 125,000 bytes/s, 1 MB/s = 1,000,000 bytes/s)

All settings are automatically saved when changed and loaded when the application starts.

## 🚀 Releases

### Automatic build via GitHub Actions

When creating a version tag (e.g., `v1.0.0`), a build for all platforms is automatically triggered:

```bash
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions will automatically:
1. Run all tests
2. Build versions for all platforms
3. Create a release with artifacts

### Manual build trigger

You can also trigger a build manually via GitHub Actions:
1. Go to the "Actions" section
2. Select the "Build and Release" workflow
3. Click "Run workflow"
4. Specify the release version

### Available versions

Each release includes the following builds:

- **TorrentClient-{version}-win-x64-self-contained.zip** - Windows x64 (includes .NET Runtime)
- **TorrentClient-{version}-win-x64-framework-dependent.zip** - Windows x64 (requires .NET 9.0)
- **TorrentClient-{version}-win-x86-self-contained.zip** - Windows x86 32-bit
- **TorrentClient-{version}-win-arm64-self-contained.zip** - Windows ARM64

## 🔧 Development

### Adding new features

1. Create an interface in the appropriate `Interfaces/` folder
2. Implement the interface in the corresponding module
3. Add unit tests
4. Update documentation if necessary

### Code style

- Use `async/await` for asynchronous operations
- Follow C# naming conventions
- Add XML comments for public APIs
- Use `using` for resource management

