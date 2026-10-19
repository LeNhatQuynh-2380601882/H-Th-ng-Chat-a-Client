@echo off
chcp 65001 > nul
title DESKTOP CLIENT (C# WINFORMS)
echo ===================================================
echo   HỆ THỐNG CHAT ĐA CLIENT - KHỞI CHẠY DESKTOP CLIENT
echo ===================================================
echo.

cd /d "%~dp0desktop-client\ChatDesktopApp"

dotnet run --no-build

if %ERRORLEVEL% NEQ 0 (
    echo [*] Dang build va chay ung dung...
    dotnet run
)

pause
