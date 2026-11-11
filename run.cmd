@echo off
setlocal
rem Ejecuta BarDock con el SDK local instalado a nivel de usuario.
set "DOTNET=%LOCALAPPDATA%\dotnet-sdk8\dotnet.exe"

if not exist "%DOTNET%" (
  echo No se encontro el SDK en "%DOTNET%".
  echo Instalalo con:  winget install Microsoft.DotNet.SDK.8
  exit /b 1
)

"%DOTNET%" run --project "%~dp0BarDock.csproj" -c Release
