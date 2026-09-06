@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0restore-pre-forest-v7-player.ps1" -Restore
pause
