@echo off
setlocal
rem Genera una version lista para usar en .\dist (no necesita el SDK para ejecutarse,
rem solo el runtime de .NET Desktop 8, que ya viene con Windows 11 actualizado).
set "DOTNET=%LOCALAPPDATA%\dotnet-sdk8\dotnet.exe"

if not exist "%DOTNET%" (
  echo No se encontro el SDK en "%DOTNET%".
  exit /b 1
)

"%DOTNET%" publish "%~dp0BarDock.csproj" -c Release -r win-x64 --self-contained false -o "%~dp0dist"
echo.
echo Listo: %~dp0dist\BarDock.exe
