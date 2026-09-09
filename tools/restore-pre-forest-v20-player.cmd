@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0restore-pre-forest-v20-player.ps1" -Restore
pause
