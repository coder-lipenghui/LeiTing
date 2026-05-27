@echo off
setlocal

set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR%..
set LUBAN_DLL=%SCRIPT_DIR%Luban\Luban.dll
set CONF_ROOT=%SCRIPT_DIR%.
set OUTPUT_DIR=%PROJECT_ROOT%\Assets\Resources\Luban
set CODE_DIR=%PROJECT_ROOT%\Assets\Scripts\Config\LubanGenerated
set DOTNET_ROLL_FORWARD=Major
set DOTNET_CMD=dotnet

where dotnet >nul 2>nul
if errorlevel 1 (
    if exist "%ProgramFiles%\JetBrains\JetBrains Rider 2026.1.2\lib\ReSharperHost\windows-x64\dotnet\dotnet.exe" (
        set DOTNET_CMD=%ProgramFiles%\JetBrains\JetBrains Rider 2026.1.2\lib\ReSharperHost\windows-x64\dotnet\dotnet.exe
    )
)

"%DOTNET_CMD%" "%LUBAN_DLL%" ^
    -t all ^
    -c cs-simple-json ^
    -d json ^
    --conf "%CONF_ROOT%\luban.conf" ^
    -x outputCodeDir="%CODE_DIR%" ^
    -x outputDataDir="%OUTPUT_DIR%"

endlocal
