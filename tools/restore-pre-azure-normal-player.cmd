@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0restore-pre-azure-normal-player.ps1" -Restore
if errorlevel 1 pause
