@echo off
rem Deletes the demo database and creates the Fiktivdalen demo again. Double-click this file.
rem Only the dedicated demo database is deleted, never the ordinary one.
title SameieHub: nullstill demo
echo This deletes the SameieHub DEMO database, including changes you made in the demo,
echo and creates it again. The ordinary SameieHub database is not touched.
echo Stop the demo first if it is running.
echo.
choice /C JN /M "Vil du nullstille demoen"
if errorlevel 2 goto :cancelled
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0demo.ps1" reset
echo.
echo Press any key to close this window.
pause >nul
goto :eof

:cancelled
echo Cancelled. Nothing was changed.
pause >nul
