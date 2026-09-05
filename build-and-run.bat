@echo off
setlocal
cd /d "%~dp0"

echo Running tests...
dotnet test VRCWakeMe.sln
if errorlevel 1 (
    echo.
    echo Tests failed. The app was not started.
    pause
    exit /b 1
)

echo.
echo Starting VRCWakeMe...
dotnet run --project src/VRCWakeMe.App/VRCWakeMe.App.csproj
exit /b %ERRORLEVEL%
