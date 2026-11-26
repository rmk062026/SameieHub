@echo off
rem SameieHub, intern testversjon. Dobbeltklikk denne filen eller SameieHub.exe.
rem Programmet bruker databasen i %LOCALAPPDATA%\SameieHub. Bruk bare fiktive data.
title SameieHub
cd /d "%~dp0"
SameieHub.exe
if errorlevel 1 (
    echo.
    echo SameieHub stoppet med en feil. Se meldingen over eller loggfilene i %LOCALAPPDATA%\SameieHub\logs.
    pause
)
