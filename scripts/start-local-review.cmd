@echo off
setlocal
set "MWABU_REVIEW_ROOT=%~dp0.."
curl.exe --silent --fail https://localhost:7042/health/ready >nul 2>nul
if not errorlevel 1 (
  echo Mwabu Learn is already running at https://localhost:7042
  exit /b 0
)
set "MWABU_REVIEW_BUILD=%MWABU_REVIEW_ROOT%\.local\access-api"
set "MWABU_REVIEW_WEB=%MWABU_REVIEW_ROOT%\.local\access-web"
if /I "%~1"=="next" (
  echo The obsolete next build is retired. Run this script without next.
  exit /b 1
)
if not exist "%MWABU_REVIEW_BUILD%\MwabuLearn.Api.dll" (
  echo The local review build is missing. Follow docs\sprint4-review-handoff.md.
  exit /b 1
)
if not exist "%MWABU_REVIEW_WEB%\index.html" (
  echo The local frontend build is missing. Follow docs\sprint4-review-handoff.md.
  exit /b 1
)
set "ASPNETCORE_ENVIRONMENT=Development"
set "ASPNETCORE_URLS=https://localhost:7042;http://localhost:5101"
set "ASPNETCORE_WEBROOT=%MWABU_REVIEW_WEB%"
set "ContentImports__Enabled=true"
set "BootstrapAdministrator__Enabled=false"
set "DevelopmentSeed__Enabled=false"
cd /d "%MWABU_REVIEW_ROOT%\backend\src\MwabuLearn.Api"
echo Starting the existing local application at https://localhost:7042
echo Keep this window open. Press Ctrl+C to stop the application.
dotnet "%MWABU_REVIEW_BUILD%\MwabuLearn.Api.dll"
exit /b %errorlevel%
