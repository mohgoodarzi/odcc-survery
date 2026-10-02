@echo off
setlocal enableextensions enabledelayedexpansion

title ODCC Survey - Backend API

echo ============================================================
echo  ODCC Survey - Backend API launcher
echo ============================================================
echo.
echo  Frees the API ports first, so a previous instance still
echo  holding them cannot cause a Kestrel bind failure. It starts
echo  the "https" profile so BOTH ports are bound:
echo    5030 (http)  <- direct access
echo    7208 (https) <- the Vite dev server proxies /api here
echo.

echo [1/3] Freeing ports 5030 (http) and 7208 (https) ...
call :KillPort 5030
call :KillPort 7208

echo.
echo [2/3] Waiting for the ports to be released ...
call :WaitFree 5030
call :WaitFree 7208

echo.
echo [3/3] Starting the API (https profile) ...
echo      HTTP :  http://localhost:5030
echo      HTTPS:  https://localhost:7208   (Vite proxies here)
echo      Close this window (or Ctrl+C) to stop it.
echo.

dotnet run --project "%~dp0src\ODCC.Api" --launch-profile https

echo.
echo Backend has stopped. Press any key to close this window.
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

REM --- Block until nothing is LISTENING on port %1 (max ~15s) -----
:WaitFree
set "tries=0"
:WaitFreeLoop
set "busy="
for /f "tokens=5" %%p in ('netstat -aon ^| findstr /L ":%~1 " ^| findstr LISTENING') do set "busy=1"
if not defined busy goto :eof
set /a "tries+=1"
if !tries! geq 15 goto :eof
timeout /t 1 /nobreak >nul
goto WaitFreeLoop
