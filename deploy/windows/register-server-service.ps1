<#
    Регистрирует Tablo.Server как службу Windows (Program.cs уже вызывает
    builder.Host.UseWindowsService(), поэтому приложение само умеет работать
    как служба). Разворачивается один раз в сети — это центральный сервис,
    а не табло в конкретном зале.

    Запускать от имени администратора:
        .\register-server-service.ps1 -ExePath "C:\tablo-server\Tablo.Server.exe"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [string]$ServiceName = "TabloServer"
)

if (-not (Test-Path $ExePath)) {
    throw "Файл не найден: $ExePath"
}

New-Service -Name $ServiceName `
    -BinaryPathName $ExePath `
    -DisplayName "Tablo Server" `
    -Description "Сервис данных для табло судебных заседаний" `
    -StartupType Automatic

# Автоперезапуск при сбое (watchdog на уровне SCM).
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000

Write-Host "Служба '$ServiceName' зарегистрирована. Запуск: Start-Service -Name '$ServiceName'"
