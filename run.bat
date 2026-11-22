@echo off
chcp 65001 > nul
cd /d "%~dp0PathosSeedFinder"
dotnet run --project PathosSeedFinder.csproj --no-build
if errorlevel 1 (
    dotnet run --project PathosSeedFinder.csproj
)
pause
