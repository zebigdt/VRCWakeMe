@echo off
rem Local development only: runs the tests, then launches a debug build.
rem Nothing here is meant for distribution -- use build-release.ps1 for that.
setlocal
cd /d "%~dp0.."

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
dotnet run --project src/VRCWakeMe.csproj
exit /b %ERRORLEVEL%
