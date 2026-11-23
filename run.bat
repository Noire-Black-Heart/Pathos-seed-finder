@echo off
setlocal

rem Check local user dotnet 10 path
if exist "%LocalAppData%\Microsoft\dotnet\dotnet.exe" (
    set "PATH=%LocalAppData%\Microsoft\dotnet;%PATH%"
    set "DOTNET_ROOT=%LocalAppData%\Microsoft\dotnet"
)

cd /d "%~dp0PathosSeedFinder"

if exist "bin\Debug\net10.0-windows\PathosSyntaxTest.exe" (
    dotnet run --project PathosSeedFinder.csproj --no-build
) else (
    dotnet run --project PathosSeedFinder.csproj
)
pause
