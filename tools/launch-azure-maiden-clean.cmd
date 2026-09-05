@echo off
setlocal
set "APP=%~dp0..\unity\CoffeeGame\Builds\Windows-AzureCleanV3\CoffeeGAME-AzureCleanV3.exe"
if not exist "%APP%" (
  echo The Azure Clean V3 development build is missing.
  exit /b 1
)
start "" "%APP%" -azureMaidenUpgraded3D %*
endlocal
