@echo off
setlocal
set "DRAGON_GAME=%~dp0..\unity\CoffeeGame\Builds\Windows-DragonGirl\CoffeeGAME-DragonGirl.exe"
if not exist "%DRAGON_GAME%" (
  echo Dragon Girl build was not found. Run CoffeeGame.Editor.DragonGirlAssetSetup.Build in Unity first.
  pause
  exit /b 1
)
start "" "%DRAGON_GAME%" -dragonTrial -azureMaidenUpgraded3D -noaudio -screen-fullscreen 0 -screen-width 1600 -screen-height 900
