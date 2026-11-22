@echo off
chcp 65001 > nul
setlocal

:: 优先检测当前用户的 .NET 本地安装路径
if exist "%LocalAppData%\Microsoft\dotnet\dotnet.exe" (
    set "PATH=%LocalAppData%\Microsoft\dotnet;%PATH%"
    set "DOTNET_ROOT=%LocalAppData%\Microsoft\dotnet"
)

cd /d "%~dp0PathosSeedFinder"
dotnet run --project PathosSeedFinder.csproj --no-build
if errorlevel 1 (
    dotnet run --project PathosSeedFinder.csproj
)
pause
