@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0restore-pre-mobile-v15-player.ps1" -Restore
pause
