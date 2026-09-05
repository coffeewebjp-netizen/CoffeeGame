@echo off
setlocal
set "GAME=%~dp0..\unity\CoffeeGame\Builds\Windows-SnowKimono\CoffeeGAME-SnowKimono.exe"
if not exist "%GAME%" (
  echo Snow-kimono trial build not found:
  echo   %GAME%
  echo Build it with CoffeeGAME ^> Build ^> Windows snow-kimono trial.
  exit /b 1
)
start "CoffeeGAME Snow Kimono" "%GAME%" -snowKimono3D %*
