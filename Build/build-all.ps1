# Quick script to build all versions
# Usage: .\Build\build-all.ps1
# Установка кодировки UTF-8 для вывода
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

& .\Build\build.ps1 -All
