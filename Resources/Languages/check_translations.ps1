# Скрипт для проверки переводов всех языков
# Установка кодировки UTF-8 для вывода
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

$baseFile = "..\Strings.resx"
$baseContent = Get-Content $baseFile -Raw -Encoding UTF8
$baseTitle = [regex]::Match($baseContent, '<data name="MainForm_Title"[^>]*>\s*<value>([^<]+)</value>').Groups[1].Value

Write-Host "=== Translation Check ===" -ForegroundColor Cyan
Write-Host "Base title (English): $baseTitle" -ForegroundColor Yellow
Write-Host ""

$translated = @()
$notTranslated = @()
$totalKeys = 0

# Count keys in base file
$baseKeys = ([regex]::Matches($baseContent, '<data name="([^"]+)"')).Count
Write-Host "Total keys in base file: $baseKeys" -ForegroundColor Yellow
Write-Host ""

Get-ChildItem -Filter "*.resx" | ForEach-Object {
    $lang = $_.Name -replace "Strings\.", "" -replace "\.resx", ""
    $content = Get-Content $_.FullName -Raw -Encoding UTF8
    $titleMatch = [regex]::Match($content, '<data name="MainForm_Title"[^>]*>\s*<value>([^<]+)</value>')
    
    if ($titleMatch.Success) {
        $title = $titleMatch.Groups[1].Value
        $keysCount = ([regex]::Matches($content, '<data name="([^"]+)"')).Count
        
        if ($title -ne $baseTitle) {
            $translated += $lang
            Write-Host "OK $lang - TRANSLATED ($keysCount keys)" -ForegroundColor Green
        } else {
            $notTranslated += $lang
            Write-Host "NO $lang - NEEDS TRANSLATION ($keysCount keys)" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "Translated: $($translated.Count) languages ($([math]::Round($translated.Count/50*100, 1))%)" -ForegroundColor Green
Write-Host "Remaining: $($notTranslated.Count) languages" -ForegroundColor Red
Write-Host ""
Write-Host "Translated: $($translated -join ', ')" -ForegroundColor Green
Write-Host ""
Write-Host "Need translation: $($notTranslated -join ', ')" -ForegroundColor Red

