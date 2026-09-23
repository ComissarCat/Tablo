#!/bin/sh
# Watchdog-цикл для Tablo.Client — аналог deploy/windows/run-client-loop.bat:
# перезапускает процесс при аварийном завершении, но НЕ перезапускает при
# штатном выходе по Esc (Tablo.Client в этом случае завершается с кодом 0 —
# см. Program.cs).
#
# Запускается из XDG-автозапуска графической сессии (см.
# install-client-autostart.sh), поэтому DISPLAY/XAUTHORITY уже заданы —
# в отличие от systemd --user сервиса, где их нет и Avalonia падает с
# "XOpenDisplay failed".

cd "$(dirname "$0")" || exit 1

while true; do
    ./Tablo.Client
    [ $? -eq 0 ] && exit 0
    sleep 3
done
