<#
    Регистрирует табло в Планировщике заданий Windows: запуск при входе в
    систему (на выделенной автологин-учётке в зале) + перезапуск при сбое
    (watchdog).

    Запускать от имени администратора на машине с табло:
        .\register-task.ps1 -ExePath "C:\tablo-client\Tablo.Client.exe"

    Требования к учётке в зале: автовход в Windows включён, рабочий стол
    поднимается сразу при загрузке — задача триггерится "при входе" именно
    под этой учёткой.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [string]$TaskName = "TabloClient"
)

if (-not (Test-Path $ExePath)) {
    throw "Файл не найден: $ExePath"
}

$workingDir = Split-Path -Parent $ExePath

$action = New-ScheduledTaskAction -Execute $ExePath -WorkingDirectory $workingDir
$trigger = New-ScheduledTaskTrigger -AtLogOn

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -RestartCount 999 `
    -RestartInterval (New-TimeSpan -Minutes 1)

Register-ScheduledTask -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Description "Табло судебных заседаний — автозапуск и автоперезапуск при сбое" `
    -Force

Write-Host "Задача '$TaskName' зарегистрирована. Запустится при следующем входе в систему."
Write-Host "Проверить/запустить прямо сейчас: Start-ScheduledTask -TaskName '$TaskName'"
