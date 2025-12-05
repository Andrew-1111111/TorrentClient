@echo off
REM Simple batch file to build a single version
REM Usage: Build\build.bat [win-x64|win-x86|win-arm64]

set PLATFORM=%1
if "%PLATFORM%"=="" set PLATFORM=win-x64

echo ==========================================
echo   TorrentClient - Release Build
echo ==========================================
echo.
echo Platform: %PLATFORM%
echo.

REM Cleanup
echo Cleaning previous builds...
cd ..
dotnet clean -c Release
if exist publish rmdir /s /q publish
cd Build

REM Build
echo.
echo Building %PLATFORM% (self-contained)...
cd ..
dotnet publish -c Release -r %PLATFORM% -o publish\%PLATFORM%-self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:SelfContained=true -p:DebugType=none -p:DebugSymbols=false
cd Build

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ==========================================
    echo   Build completed successfully!
    echo ==========================================
    echo Result: publish\%PLATFORM%-self-contained\
) else (
    echo.
    echo ==========================================
    echo   Build error!
    echo ==========================================
    exit /b 1
)

pause
