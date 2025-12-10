# Скрипт для генерации переводов
# Этот скрипт создает переводы для всех языков на основе базового файла
# Установка кодировки UTF-8 для вывода
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

$translations = @{
    "de" = @{
        "MainForm_Title" = "Torrent-Client"
        "MainForm_AddTorrent" = "Torrent Hinzufügen"
        "MainForm_Remove" = "Entfernen"
        "MainForm_Start" = "Starten"
        "MainForm_Pause" = "Pausieren"
        "MainForm_Stop" = "Stoppen"
        "MainForm_SpeedLimit" = "Geschwindigkeitsbegrenzung"
        "MainForm_DownloadFolder" = "Download-Ordner"
        "MainForm_Settings" = "Einstellungen"
        "MainForm_Column_Name" = "Name"
        "MainForm_Column_Size" = "Größe"
        "MainForm_Column_Progress" = "Fortschritt"
        "MainForm_Column_Speed" = "Geschwindigkeit"
        "MainForm_Column_Downloaded" = "Heruntergeladen"
        "MainForm_Column_Peers" = "Peers"
        "MainForm_Column_Priority" = "Priorität"
        "MainForm_Column_Status" = "Status"
        "MainForm_Status_Ready" = "Bereit"
        "MainForm_ContextMenu_Start" = "Starten"
        "MainForm_ContextMenu_Pause" = "Pausieren"
        "MainForm_ContextMenu_Stop" = "Stoppen"
        "MainForm_ContextMenu_SpeedLimit" = "Geschwindigkeitsbegrenzung..."
        "MainForm_ContextMenu_Priority" = "Priorität"
        "MainForm_ContextMenu_Priority_High" = "Hoch"
        "MainForm_ContextMenu_Priority_Normal" = "Normal"
        "MainForm_ContextMenu_Priority_Low" = "Niedrig"
        "MainForm_ContextMenu_Remove" = "Entfernen"
        "Priority_High" = "Hoch"
        "Priority_Normal" = "Normal"
        "Priority_Low" = "Niedrig"
        "Status_Stopped" = "Gestoppt"
        "Status_Checking" = "Prüfung"
        "Status_Paused" = "Pausiert"
        "Status_Downloading" = "Lädt Herunter"
        "Status_Seeding" = "Seeding"
        "Status_Error" = "Fehler"
        "GlobalSettings_Title" = "Globale Einstellungen"
        "GlobalSettings_Language" = "Sprache"
        "GlobalSettings_MaxConnections" = "Maximale Verbindungen"
        "GlobalSettings_MaxHalfOpen" = "Maximale Halb-Offene Verbindungen"
        "GlobalSettings_MaxPieces" = "Maximale Anzufordernde Teile"
        "GlobalSettings_MaxRequestsPerPeer" = "Maximale Anfragen pro Peer"
        "GlobalSettings_EnableLogging" = "Protokollierung Aktivieren"
        "GlobalSettings_MinimizeToTray" = "Beim Schließen in Systemleiste Minimieren"
        "GlobalSettings_AutoStartOnLaunch" = "Automatischer Start beim Öffnen"
        "GlobalSettings_AutoStartOnAdd" = "Automatischer Start beim Hinzufügen"
        "GlobalSettings_CopyTorrentFile" = "Torrent-Datei in Download-Ordner Kopieren"
        "GlobalSettings_GlobalDownloadSpeed" = "Globale Download-Geschwindigkeitsbegrenzung"
        "GlobalSettings_GlobalUploadSpeed" = "Globale Upload-Geschwindigkeitsbegrenzung"
        "GlobalSettings_Unlimited" = "Unbegrenzt"
        "GlobalSettings_OK" = "OK"
        "GlobalSettings_Cancel" = "Abbrechen"
        "MessageBox_Error" = "Fehler"
        "MessageBox_Information" = "Information"
        "MessageBox_Warning" = "Warnung"
        "MessageBox_Question" = "Frage"
        "MainForm_Status_DragDrop" = "Ziehen Sie .torrent-Dateien hierher"
        "MainForm_Status_TorrentsLoaded" = "Torrents geladen und gestartet"
        "MainForm_Status_LoadError" = "Fehler beim Laden gespeicherter Torrents"
        "MainForm_Status_TorrentAdded" = "Torrent hinzugefügt: {0}"
        "MainForm_Status_TorrentsAdded" = "Torrents hinzugefügt: {0}"
        "MainForm_Status_Added" = "Hinzugefügt: {0}"
        "MainForm_Status_Skipped" = "Übersprungen: {0}"
        "MainForm_Status_Errors" = "Fehler: {0}"
        "MainForm_Status_FailedToAdd" = "Fehler beim Hinzufügen von Torrents"
        "MainForm_Status_DownloadFolderChanged" = "Download-Ordner geändert: {0}"
        "MainForm_Status_SettingsSaved" = "Einstellungen gespeichert: {0} Verbindungen, {1} Teile"
        "MessageBox_AddTorrentResult_Title" = "Ergebnis des Hinzufügens von Torrents"
        "MessageBox_AddTorrentResult_Added" = "✓ Torrents hinzugefügt: {0}"
        "MessageBox_AddTorrentResult_Skipped" = "⚠ Übersprungen wegen unzureichendem Speicherplatz: {0}"
        "MessageBox_AddTorrentResult_Failed" = "✗ Fehler: {0}"
        "MessageBox_AddTorrentResult_Warnings" = "⚠ Festplattenspeicher-Warnungen:"
        "MessageBox_AddTorrentResult_Errors" = "✗ Fehler:"
        "MessageBox_AddTorrentResult_MoreErrors" = "... und {0} weitere Fehler"
        "MessageBox_AddTorrentError" = "Fehler beim Hinzufügen von Torrents: {0}"
        "MessageBox_RemoveTorrentError" = "Fehler beim Entfernen von Torrents: {0}"
        "MessageBox_StartTorrentError" = "Fehler beim Starten von Torrents: {0}"
        "MessageBox_PauseTorrentError" = "Fehler beim Pausieren von Torrents: {0}"
        "MessageBox_StopTorrentError" = "Fehler beim Stoppen von Torrents: {0}"
        "MessageBox_SaveAsDefault_Title" = "Standardordner"
        "MessageBox_SaveAsDefault_Message" = "Diesen Ordner als Standardordner für zukünftige Torrents speichern?"
        "MessageBox_CriticalError_Title" = "Fehler"
        "MessageBox_CriticalError_Message" = "Kritischer Anwendungsfehler:\n{0}\n\nDetails in der Protokolldatei."
        "MessageBox_UnhandledError_Title" = "Fehler"
        "MessageBox_UnhandledError_Message" = "Ein Fehler ist aufgetreten:\n{0}\n\nDetails in der Protokolldatei."
        "GlobalSettings_Info" = "Erhöhung der Parallelität kann die Geschwindigkeit verbessern, erzeugt aber mehr Systemlast."
    }
}

# Функция для обновления файла перевода
function Update-TranslationFile {
    param(
        [string]$FilePath,
        [hashtable]$Translations
    )
    
    $content = Get-Content $FilePath -Raw -Encoding UTF8
    $xml = [xml]$content
    
    foreach ($key in $Translations.Keys) {
        $dataNode = $xml.SelectSingleNode("//data[@name='$key']")
        if ($dataNode -ne $null) {
            $valueNode = $dataNode.SelectSingleNode("value")
            if ($valueNode -ne $null) {
                $valueNode.InnerText = $Translations[$key]
            }
        }
    }
    
    # Сохранение XML с кодировкой UTF-8
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    $writer = New-Object System.IO.StreamWriter($FilePath, $false, $utf8NoBom)
    $xml.Save($writer)
    $writer.Close()
}

# Обновляем немецкий файл
$deFile = "Strings.de.resx"
if (Test-Path $deFile) {
    Update-TranslationFile -FilePath $deFile -Translations $translations["de"]
    Write-Host "Updated: $deFile"
}

