@echo off
setlocal
set "GAME=%~dp0..\unity\CoffeeGame\Builds\Windows\CoffeeGAME.exe"
if not exist "%GAME%" (
  echo CoffeeGAME Windows build not found:
  echo   %GAME%
  echo Build it with CoffeeGAME ^> Build ^> Windows development build.
  exit /b 1
)
start "CoffeeGAME SnowKimono Default" "%GAME%" -useSnowKimonoDefault %*
