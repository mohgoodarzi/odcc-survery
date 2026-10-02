@echo off
setlocal enableextensions enabledelayedexpansion

title ODCC Survey - Frontend (Vite)

echo ============================================================
echo  ODCC Survey - Frontend launcher
echo ============================================================
echo.
echo  Frees the Vite port first, so a previous dev server still
echo  holding it cannot cause an "address already in use" error.
echo.
echo  NOTE: this dev server proxies /api to https://localhost:7208,
echo  so start run-backend.cmd first.
echo.

echo [1/3] Freeing port 5174 (Vite dev server) ...
call :KillPort 5174

echo.
echo [2/3] Checking dependencies ...
if not exist "%~dp0web\node_modules" (
    echo      node_modules not found - running "npm install" ...
    pushd "%~dp0web"
    call npm install
    popd
)

echo.
echo [3/3] Starting the frontend ...
echo      URL: http://localhost:5174
echo      Close this window (or Ctrl+C) to stop it.
echo.

pushd "%~dp0web"
call npm run dev
popd

echo.
echo Frontend has stopped. Press any key to close this window.
pause >nul
goto :eof

REM --- Kill every process LISTENING on the port given in %1 -------
REM A single process listens on both IPv4 and IPv6, so consecutive
REM duplicate PIDs (from netstat) are skipped.
:KillPort
set "killed="
for /f "tokens=5" %%p in ('netstat -aon ^| findstr /L ":%~1 " ^| findstr LISTENING') do (
    if /i not "%%p"=="!killed!" (
        echo      port %~1 -^> killing PID %%p
        taskkill /F /PID %%p >nul 2>&1
        set "killed=%%p"
    )
)
goto :eof
