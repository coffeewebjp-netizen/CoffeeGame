@echo off
setlocal
set "GAME=%~dp0..\unity\CoffeeGame\Builds\Windows-AzureCleanV3\CoffeeGAME-AzureCleanV3.exe"
if not exist "%GAME%" (
  echo CoffeeGAME Azure Clean V3 development build not found:
  echo   %GAME%
  echo Use the BuildAzureCleanV3DiagnosticNoSetup build method.
  exit /b 1
)
start "CoffeeGAME Upgraded Azure Maiden Trial" "%GAME%" -azureMaidenUpgraded3D %*
