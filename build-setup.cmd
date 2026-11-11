@echo off
setlocal
rem Genera dist\BarDockSetup.exe:
rem   1. Publica BarDock autocontenido en un solo .exe (no necesita .NET en el destino).
rem   2. Lo incrusta en el instalador y publica el instalador tambien autocontenido.
set "DOTNET=%LOCALAPPDATA%\dotnet-sdk8\dotnet.exe"
set "ROOT=%~dp0"

if not exist "%DOTNET%" (
  echo No se encontro el SDK en "%DOTNET%".
  exit /b 1
)

echo [1/3] Publicando BarDock autocontenido...
"%DOTNET%" publish "%ROOT%BarDock.csproj" -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false ^
  -o "%ROOT%Setup\payload"
if errorlevel 1 exit /b 1

echo [2/3] Compilando el instalador...
"%DOTNET%" publish "%ROOT%Setup\BarDockSetup.csproj" -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false ^
  -o "%ROOT%Setup\out"
if errorlevel 1 exit /b 1

echo [3/3] Copiando a dist...
if not exist "%ROOT%dist" mkdir "%ROOT%dist"
copy /y "%ROOT%Setup\out\BarDockSetup.exe" "%ROOT%dist\BarDockSetup.exe" >nul
if errorlevel 1 exit /b 1

echo.
echo Listo: %ROOT%dist\BarDockSetup.exe
