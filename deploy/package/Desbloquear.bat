@echo off
chcp 65001 >nul
title MAMDesk - Desbloquear
echo.
echo  Desbloqueando arquivos MAMDesk...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%~dp0' -Recurse -File | Unblock-File -ErrorAction SilentlyContinue"
echo  Pronto! Agora execute o .exe desta pasta.
echo.
pause
