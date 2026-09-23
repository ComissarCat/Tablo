<#
    Настраивает автозапуск табло через папку "Автозагрузка" текущего
    пользователя — вместо Планировщика заданий. Работает на ЛЮБОЙ версии
    Windows (включая 7 и Server 2008), где нет модуля ScheduledTasks и,
    соответственно, командлетов New-ScheduledTaskAction/-Trigger (они
    появились только в PowerShell 3.0 / Windows 8 / Server 2012).

    Что делает:
      1. Копирует run-client-loop.bat (watchdog: перезапускает Tablo.Client
         при падении, но не при штатном выходе по Esc) рядом с exe.
      2. Кладёт ярлык на этот bat в папку "Автозагрузка" текущего
         пользователя — запустится при следующем входе в систему.

    Запускать на самой машине табло, под той учёткой, что автологинится в
    зале (не обязательно от администратора — папка "Автозагрузка" своя у
    каждого пользователя):

        .\install-client-autostart.ps1 -ClientDir "C:\tablo-client"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ClientDir
)

$exePath = Join-Path $ClientDir "Tablo.Client.exe"
if (-not (Test-Path $exePath)) {
    throw "Tablo.Client.exe не найден в $ClientDir"
}

# $PSScriptRoot появился только в PowerShell 3.0 — на Windows 7 с PS 2.0
# он пустой, и Join-Path с ним падает с "путь равен null". MyInvocation
# работает с PowerShell 1.0.
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$loopSource = Join-Path $ScriptDir "run-client-loop.bat"
$loopDest = Join-Path $ClientDir "run-client-loop.bat"
Copy-Item $loopSource $loopDest -Force

$startupDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup"
$shortcutPath = Join-Path $startupDir "TabloClient.lnk"

# WScript.Shell — работает на любой версии PowerShell/Windows без
# дополнительных модулей.
$wshShell = New-Object -ComObject WScript.Shell
$shortcut = $wshShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $loopDest
$shortcut.WorkingDirectory = $ClientDir
$shortcut.WindowStyle = 7  # свёрнутое окно консоли
$shortcut.Save()

Write-Host "Готово. Ярлык добавлен: $shortcutPath"
Write-Host "Табло запустится автоматически при следующем входе в систему."
Write-Host ""
Write-Host "Проверить сейчас, не дожидаясь перезагрузки:"
Write-Host "    & `"$loopDest`""
Write-Host ""
Write-Host "Esc по-прежнему закрывает табло штатно (цикл это распознаёт и не"
Write-Host "перезапускает). А чтобы отключить автозапуск совсем — удалите"
Write-Host "или переименуйте ярлык: $shortcutPath"
