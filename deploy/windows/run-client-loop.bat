@echo off
rem Watchdog-цикл для Tablo.Client: перезапускает процесс при аварийном
rem завершении, но НЕ перезапускает при штатном выходе по Esc (Tablo.Client
rem в этом случае завершается с кодом 0 — см. Program.cs).
rem Работает на любой версии Windows через обычный cmd.exe, без зависимости
rem от модулей PowerShell (нужно было для Windows 7 / Server 2008, где нет
rem New-ScheduledTask*).

cd /d "%~dp0"

:loop
Tablo.Client.exe
if %errorlevel%==0 goto end
timeout /t 3 /nobreak >nul
goto loop

:end
