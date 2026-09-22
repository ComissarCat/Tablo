# Развёртывание Tablo

Инструкция для тех, кто ставит сервер или табло на месте. Предполагается,
что сборка (`publish.ps1`) уже выполнена на машине с интернетом и .NET SDK —
см. `../CLAUDE.md`, раздел «Рантайм на целевых машинах». Все exe
self-contained — ставить отдельно .NET на целевых машинах не нужно.

## 1. Сервер (один раз на всю систему)

Сервер — центральный компонент: опрашивает Firebird и раздаёт данные всем
табло по сети. Разворачивается один раз на машине с сетевым доступом к БД.

### 1.1 Скопировать

Скопируйте папку `publish/server-win-x64` (или `server-linux-x64`) на
сервер, например в `C:\tablo-server`.

### 1.2 Настроить `appsettings.json`

Откройте `appsettings.json` рядом с `Tablo.Server.exe`, поправьте:

```json
{
  "ConnectionStrings": {
    "Firebird": "DataSource=<хост_БД>;Database=<путь_к_gdb>;User=SYSDBA;Password=<пароль>;Charset=WIN1251;ServerType=0;Pooling=true"
  },
  "Tablo": {
    "PollIntervalSeconds": 20
  },
  "Kestrel": {
    "Endpoints": { "Http": { "Url": "http://0.0.0.0:5000" } }
  }
}
```

- `DataSource` / `Database` / `Password` — под вашу боевую БД.
- `Charset=WIN1251` менять не нужно — без него/без другого значения
  соединение с БД не откроется (см. `CLAUDE.md`).
- Порт `5000` в `Kestrel:Endpoints:Http:Url` — можно сменить, если занят.

### 1.3 Открыть порт в файрволе

Windows Server 2012 и новее (штатный командлет):
```powershell
New-NetFirewallRule -DisplayName "Tablo Server" -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow
```

Более старые версии / если командлет недоступен:
```
netsh advfirewall firewall add rule name="Tablo Server" dir=in action=allow protocol=TCP localport=5000
```

Убрать правило (если сервер переезжает на другую машину):
```
netsh advfirewall firewall delete rule name="Tablo Server"
```

### 1.4 Запустить и проверить

Запустите `Tablo.Server.exe` (Linux: `./Tablo.Server`) — окно/консоль
оставьте открытой, там видны логи в реальном времени. Проверьте (можно
прямо в браузере на сервере):

```
http://localhost:5000/health
http://localhost:5000/api/cases
http://localhost:5000/api/halls
```

`/health` должен показать `"hasEverSucceeded": true` — если долго остаётся
`"starting"`, смотрите лог в папке `logs/` рядом с exe (там же, где вы его
запустили). `/api/halls` пригодится, чтобы взять точные названия залов для
настройки клиентов (см. ниже).

С другой машины в той же сети — то же самое, но с адресом/именем сервера
вместо `localhost`. Если не отвечает — сначала чините сеть/файрвол, не exe.

### 1.5 (когда всё проверено) Автозапуск сервера

Пока сервис работает вручную в открытой консоли — это нормально для
тестирования. Когда пора оставить его работать постоянно:

Windows — зарегистрировать как службу (от администратора):
```powershell
.\deploy\windows\register-server-service.ps1 -ExePath "C:\tablo-server\Tablo.Server.exe"
Start-Service -Name TabloServer
```
Автоперезапуск при сбое уже настроен внутри скрипта (`sc.exe failure`).

Linux — systemd (от root):
```bash
sudo cp deploy/linux/tablo-server.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now tablo-server.service
```

## 2. Клиент (на каждый монитор/зал)

### 2.1 Скопировать

Скопируйте `publish/client-win-x64` (или `client-linux-x64`) на машину
табло, например в `C:\tablo-client`.

### 2.2 Настроить `tablo.config.json`

Скопируйте `tablo.config.sample.json` рядом с `Tablo.Client.exe`,
переименуйте в `tablo.config.json`, отредактируйте:

```json
{
  "ApiBaseUrl": "http://<адрес-сервера>:5000",
  "HallName": null,
  "ShowCivil": true,
  "ShowAdministrative": true,
  "ShowCriminal": true,
  "UiScale": 1.0,
  "PollIntervalSeconds": 20
}
```

| Поле | Что означает |
|---|---|
| `ApiBaseUrl` | адрес сервера из шага 1 |
| `HallName` | точное название зала (взять из `GET /api/halls` на сервере), без учёта регистра/пробелов; `null` — показывать все залы сразу |
| `ShowCivil` / `ShowAdministrative` / `ShowCriminal` | любая комбинация `true`/`false` |
| `UiScale` | масштаб интерфейса, `1.0` = 100% |
| `PollIntervalSeconds` | как часто опрашивать сервер |

### 2.3 Запустить и проверить

Запустите `Tablo.Client.exe` (Linux: `./Tablo.Client`) — развернётся на
весь экран. Через несколько секунд должны появиться дела, сверху —
заголовок с датой (и залом, если настроен), внизу — время последнего
обновления. **Esc** — закрыть окно (для тестов и обслуживания на месте).

Если пусто/висит "Загрузка..." — смотрите `logs/` рядом с
`Tablo.Client.exe`: обычно это неверный `ApiBaseUrl`, файрвол на машине
табло (сервер недоступен по сети) или опечатка в `tablo.config.json`.

### 2.4 (когда всё проверено) Автозапуск клиента

Windows — задача в Планировщике при входе в систему + автоперезапуск при
сбое (от администратора, на самой машине табло, под учёткой с автологином
в зале):
```powershell
.\deploy\windows\register-task.ps1 -ExePath "C:\tablo-client\Tablo.Client.exe"
```

Linux — на выбор:
- `systemd --user` (есть автоперезапуск при падении, `Restart=always`):
  ```bash
  mkdir -p ~/.config/systemd/user
  cp deploy/linux/tablo-client.service ~/.config/systemd/user/
  systemctl --user daemon-reload
  systemctl --user enable --now tablo-client.service
  ```
- XDG autostart (проще, без автоперезапуска при падении):
  ```bash
  mkdir -p ~/.config/autostart
  cp deploy/linux/tablo-client.desktop ~/.config/autostart/
  sed -i 's#/home/tablo#'"$HOME"'#' ~/.config/autostart/tablo-client.desktop
  ```

## 3. Обновление уже развёрнутых машин

1. На машине сборки: `.\deploy\publish.ps1`
2. Остановить процесс/службу на целевой машине.
3. Заменить файлы новой публикацией (конфиги `appsettings.json` /
   `tablo.config.json` не трогать — они не пересоздаются публикацией,
   лежат отдельно от кода).
4. Запустить заново.

## 4. Куда смотреть при проблемах

- **Сервер**: `logs/tablo-server-<дата>.log` рядом с `Tablo.Server.exe`.
- **Клиент**: `logs/tablo-client-<дата>.log` рядом с `Tablo.Client.exe`.
- Проверка сети сервер↔клиент: `curl http://<сервер>:5000/health` (или
  `Invoke-RestMethod` в PowerShell, если `curl` недоступен) с машины табло.
- Известные особенности и уже исправленные грабли — `../CLAUDE.md`.
