@echo off
chcp 65001 > nul
title CORE SERVER (JAVA) - PORT 8888 & 8887
echo ===================================================
echo   HỆ THỐNG CHAT ĐA CLIENT - KHỞI CHẠY CORE SERVER
echo   Môn: Lập trình mạng máy tính - Nhóm 11
echo ===================================================
echo.

cd /d "%~dp0server"

set "JAVA_EXE=C:\Program Files\Java\jdk-23\bin\java.exe"
if not exist "%JAVA_EXE%" (
    set "JAVA_EXE=java"
)

set "CP=target\classes;%USERPROFILE%\.m2\repository\com\google\code\gson\gson\2.10.1\gson-2.10.1.jar;%USERPROFILE%\.m2\repository\org\java-websocket\Java-WebSocket\1.5.6\Java-WebSocket-1.5.6.jar;%USERPROFILE%\.m2\repository\org\slf4j\slf4j-api\2.0.6\slf4j-api-2.0.6.jar;%USERPROFILE%\.m2\repository\org\slf4j\slf4j-simple\2.0.9\slf4j-simple-2.0.9.jar"

echo [*] Dang khoi dong Core Server...
echo [*] TCP Socket:      Port 8888 (Desktop Client)
echo [*] WebSocket:       Port 8887 (Web Client)
echo ===================================================
echo.

"%JAVA_EXE%" -classpath "%CP%" com.chatsystem.ServerMain

pause
