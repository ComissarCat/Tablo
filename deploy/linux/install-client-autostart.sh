#!/bin/sh
# Настраивает автозапуск табло при входе в графическую сессию (XDG
# autostart, ~/.config/autostart) — аналог
# deploy/windows/install-client-autostart.ps1.
#
# Что делает:
#   1. Выставляет права на исполнение Tablo.Client — они теряются, когда
#      публикация собрана на Windows и скопирована флешкой/по сети.
#   2. Копирует run-client-loop.sh (watchdog: перезапуск при падении, но не
#      при штатном выходе по Esc) в папку клиента.
#   3. Создаёт ~/.config/autostart/tablo-client.desktop, запускающий этот
#      цикл, с абсолютными путями под текущего пользователя.
#
# Запускать на самой машине табло, под той учёткой, что автологинится в
# зале (без sudo):
#
#     sh install-client-autostart.sh ~/tablo-client
#
# Без аргумента папкой клиента считается папка, где лежит этот скрипт
# (publish.ps1 кладёт его в publish/client-linux-x64/).

set -e

script_dir=$(cd "$(dirname "$0")" && pwd)
client_dir=$(cd "${1:-$script_dir}" && pwd)

if [ ! -f "$client_dir/Tablo.Client" ]; then
    echo "Tablo.Client не найден в $client_dir" >&2
    exit 1
fi

# tr -d '\r' — на случай, если скрипт прошёл через Windows и получил CRLF:
# с ним /bin/sh не запустится.
if [ "$script_dir/run-client-loop.sh" != "$client_dir/run-client-loop.sh" ]; then
    tr -d '\r' < "$script_dir/run-client-loop.sh" > "$client_dir/run-client-loop.sh"
fi
chmod +x "$client_dir/Tablo.Client" "$client_dir/run-client-loop.sh"

autostart_dir="$HOME/.config/autostart"
desktop_file="$autostart_dir/tablo-client.desktop"
mkdir -p "$autostart_dir"
cat > "$desktop_file" <<EOF
[Desktop Entry]
Type=Application
Name=Tablo Client
Comment=Табло судебных заседаний
Exec="$client_dir/run-client-loop.sh"
Path=$client_dir
X-GNOME-Autostart-enabled=true
NoDisplay=false
Terminal=false
EOF

echo "Готово. Автозапуск добавлен: $desktop_file"
echo "Табло запустится автоматически при следующем входе в систему."
echo ""
echo "Проверить сейчас, не дожидаясь перезагрузки:"
echo "    \"$client_dir/run-client-loop.sh\" &"
echo ""
echo "Esc по-прежнему закрывает табло штатно (цикл это распознаёт и не"
echo "перезапускает). Отключить автозапуск — удалить $desktop_file"
