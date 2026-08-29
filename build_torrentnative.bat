@echo off
cd /d D:\VS_Projects\Cursor\TorrentNative
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe" "D:\VS_Projects\Cursor\TorrentNative\TorrentNative.sln" /p:Configuration=Release /p:Platform=x64 /t:Rebuild

