# Script to build TorrentClient for different platforms
# Usage: .\Build\build.ps1 [--all] [--platform win-x64|win-x86|win-arm64] [--self-contained|--framework-dependent]

param(
    [switch]$All,
    [string]$Platform = "win-x64",
    [switch]$SelfContained,
    [switch]$FrameworkDependent,
    [string]$OutputDir = "..\publish"
)

$ErrorActionPreference = "Stop"

# Output colors
function Write-Info {
    Write-Host "$args" -ForegroundColor Cyan
}

function Write-Success {
    Write-Host "$args" -ForegroundColor Green
}

function Write-Error {
    Write-Host "$args" -ForegroundColor Red
}

# Clean previous builds
function Clean-Build {
    Write-Info "Cleaning previous builds..."
    if (Test-Path $OutputDir) {
        Remove-Item -Path $OutputDir -Recurse -Force
    }
    dotnet clean -c Release
    Write-Success "Cleanup completed"
}

# Build for specific platform
function Build-Platform {
    param(
        [string]$Rid,
        [bool]$IsSelfContained,
        [string]$Suffix = ""
    )
    
    $runtimeName = $Rid
    $containedText = if ($IsSelfContained) { "Self-Contained" } else { "Framework-Dependent" }
    $outputPath = Join-Path $OutputDir "$Rid$Suffix"
    
    Write-Info "`n=========================================="
    Write-Info "Building: $Rid ($containedText)"
    Write-Info "Output path: $outputPath"
    Write-Info "=========================================="
    
    $publishArgs = @(
        "publish",
        "..\TorrentClient.csproj",
        "-c", "Release",
        "-r", $Rid,
        "-o", $outputPath,
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:DebugType=none",
        "-p:DebugSymbols=false"
    )
    
    if ($IsSelfContained) {
        $publishArgs += "-p:SelfContained=true"
    } else {
        $publishArgs += "-p:SelfContained=false"
    }
    
    try {
        $result = dotnet @publishArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            $errorOutput = $result | Where-Object { $_ -match "error" } | Select-Object -First 3
            throw "Build error for $Rid`n$($errorOutput -join "`n")"
        }
        
        # Get file size
        $exePath = Get-ChildItem -Path $outputPath -Filter "*.exe" | Select-Object -First 1
        if ($exePath) {
            $sizeMB = [math]::Round($exePath.Length / 1MB, 2)
            Write-Success "[OK] Build completed: $($exePath.Name) ($sizeMB MB)"
        } else {
            Write-Success "[OK] Build completed"
        }
    } catch {
        Write-Error "[ERROR] Build failed for $Rid : $_"
        return $false
    }
    
    return $true
}

# Main logic
Write-Info "=========================================="
Write-Info "  TorrentClient - Release Build"
Write-Info "=========================================="

# Cleanup
Clean-Build

$buildResults = @()

if ($All) {
    Write-Info "`nBuilding all versions..."
    
    # win-x64
    $buildResults += @{
        Platform = "win-x64"
        SelfContained = $true
        Success = (Build-Platform -Rid "win-x64" -IsSelfContained $true -Suffix "-self-contained")
    }
    
    $buildResults += @{
        Platform = "win-x64"
        SelfContained = $false
        Success = (Build-Platform -Rid "win-x64" -IsSelfContained $false -Suffix "-framework-dependent")
    }
    
    # win-x86
    $buildResults += @{
        Platform = "win-x86"
        SelfContained = $true
        Success = (Build-Platform -Rid "win-x86" -IsSelfContained $true -Suffix "-self-contained")
    }
    
    # win-arm64
    $buildResults += @{
        Platform = "win-arm64"
        SelfContained = $true
        Success = (Build-Platform -Rid "win-arm64" -IsSelfContained $true -Suffix "-self-contained")
    }
} else {
    # Build single version
    $isSelfContained = if ($FrameworkDependent) { $false } else { $true }
    $suffix = if ($isSelfContained) { "-self-contained" } else { "-framework-dependent" }
    
    $buildResults += @{
        Platform = $Platform
        SelfContained = $isSelfContained
        Success = (Build-Platform -Rid $Platform -IsSelfContained $isSelfContained -Suffix $suffix)
    }
}

# Summary
Write-Info "`n=========================================="
Write-Info "  Build Summary"
Write-Info "=========================================="

$successCount = ($buildResults | Where-Object { $_.Success }).Count
$totalCount = $buildResults.Count

foreach ($result in $buildResults) {
    $status = if ($result.Success) { "[OK]" } else { "[FAIL]" }
    $type = if ($result.SelfContained) { "Self-Contained" } else { "Framework-Dependent" }
    Write-Host "$status $($result.Platform) ($type)" -ForegroundColor $(if ($result.Success) { "Green" } else { "Red" })
}

Write-Info "`nSuccessfully built: $successCount of $totalCount"
Write-Info "Results saved to: $OutputDir"

if ($successCount -eq $totalCount) {
    Write-Success "`n[SUCCESS] All builds completed successfully!"
    exit 0
} else {
    Write-Error "`n[FAILURE] Some builds failed"
    exit 1
}
