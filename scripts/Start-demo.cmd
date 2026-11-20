@echo off
rem Starts the SameieHub demo (Fiktivdalen Sameie). Double-click this file.
rem The execution policy is only relaxed for this one PowerShell process. Nothing permanent is changed,
rem and the ordinary database is never used.
title SameieHub demo
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0demo.ps1" launch
echo.
echo The demo has stopped. Press any key to close this window.
pause >nul
