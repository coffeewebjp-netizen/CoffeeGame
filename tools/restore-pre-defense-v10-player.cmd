@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0restore-pre-defense-v10-player.ps1" -Restore
pause
