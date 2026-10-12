<#
    Собирает self-contained публикации сервера и клиента для Windows и Linux.
    Машины, где будет работать Tablo, без интернета — self-contained значит,
    что .NET-рантайм целиком упакован рядом с exe, отдельно ставить рантайм
    на целевой машине не нужно: просто скопировать папку публикации и
    запустить.

    Запускать на машине со сборочным окружением (интернет нужен только
    здесь, для скачивания NuGet-пакетов и рантайм-паков при первой сборке).

    Сборка single-file: в папке публикации один исполняемый файл
    (Tablo.Server(.exe) / Tablo.Client(.exe)) плюс конфиги рядом с ним —
    appsettings*.json у сервера, tablo.config.sample.json у клиента, и
    скрипты автозапуска у клиента для Linux. Нативные библиотеки (Skia,
    HarfBuzz и т.п.) тоже упакованы внутрь exe и при первом запуске
    распаковываются во временную папку пользователя (%TEMP%\.net на
    Windows, ~/.net на Linux) — она должна быть доступна на запись.

    Результат — в publish/<server|client>-<win-x64|linux-x64>/ (папка
    пересоздаётся при каждой сборке, чтобы не оставались файлы от прошлых
    публикаций). Дальше эти папки целиком копируются на целевые машины
    (флешкой/по внутренней сети).
#>
param(
    [string[]]$Runtimes = @("win-x64", "linux-x64"),
    [string]$Configuration = "Release"
)

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$projects = @{
    "server" = Join-Path $root "src/Tablo.Server"
    "client" = Join-Path $root "src/Tablo.Client"
}

foreach ($rid in $Runtimes) {
    foreach ($name in $projects.Keys) {
        $outDir = Join-Path $root "publish/$name-$rid"
        Write-Host "==> Публикация $name для $rid в $outDir"
        if (Test-Path $outDir) {
            Remove-Item $outDir -Recurse -Force
        }
        # DebugType=embedded — pdb внутри exe: отдельного файла нет, а номера
        # строк в стектрейсах логов сохраняются.
        dotnet publish $projects[$name] `
            -c $Configuration `
            -r $rid `
            --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:DebugType=embedded `
            -o $outDir
        if ($LASTEXITCODE -ne 0) {
            throw "Публикация $name/$rid завершилась с ошибкой"
        }

        # Мусор, который не нужен для работы и только раздувает папку:
        # отладочные pdb нативных библиотек из NuGet-пакетов SkiaSharp/
        # HarfBuzz (~100 МБ), web.config для IIS и манифест статических
        # веб-ресурсов (у сервера их нет).
        Get-ChildItem $outDir -Include "*.pdb", "web.config", "*.staticwebassets.endpoints.json" -Recurse |
            Remove-Item -Force

        # Скрипты автозапуска кладём прямо в публикацию клиента для Linux —
        # на машину табло копируется одна папка, и install-client-
        # autostart.sh запускается из неё без аргументов.
        if ($name -eq "client" -and $rid -like "linux-*") {
            Copy-Item (Join-Path $root "deploy/linux/*.sh") $outDir -Force
        }
    }
}

Write-Host ""
Write-Host "Готово. Перед копированием на зал не забудьте:"
Write-Host "  - положить tablo.config.json рядом с Tablo.Client(.exe) (см. tablo.config.sample.json)"
Write-Host "  - для сервера проверить ConnectionStrings:Firebird в appsettings.json"
