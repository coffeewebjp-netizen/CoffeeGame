@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0restore-pre-target-lock-v12-player.ps1" -Restore
pause
