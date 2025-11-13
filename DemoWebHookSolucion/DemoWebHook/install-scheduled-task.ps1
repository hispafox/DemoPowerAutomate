# Script de PowerShell para instalar como Tarea Programada de Windows

param(
    [Parameter(Mandatory=$false)]
    [string]$TaskName = "FileWatcherWebhook",
    
    [Parameter(Mandatory=$false)]
    [string]$AppPath = $PSScriptRoot,
    
    [Parameter(Mandatory=$false)]
    [string]$ExecutableName = "DemoWebHook.exe"
)

Write-Host "?????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?   Instalador de Tarea Programada - FileWatcher           ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host ""

# Verificar si se ejecuta como Administrador
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: Este script debe ejecutarse como Administrador" -ForegroundColor Red
    Write-Host "Haga clic derecho en PowerShell y seleccione 'Ejecutar como administrador'" -ForegroundColor Yellow
    pause
    exit 1
}

$exePath = Join-Path $AppPath $ExecutableName

# Verificar que existe el ejecutable
if (-not (Test-Path $exePath)) {
    Write-Host "ERROR: No se encontró el ejecutable en: $exePath" -ForegroundColor Red
    pause
    exit 1
}

Write-Host "Configuración:" -ForegroundColor Yellow
Write-Host "  - Nombre de tarea: $TaskName"
Write-Host "  - Ejecutable: $exePath"
Write-Host ""

# Eliminar tarea existente si existe
$existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($existingTask) {
    Write-Host "Eliminando tarea existente..." -ForegroundColor Yellow
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
}

# Crear acción
$action = New-ScheduledTaskAction -Execute $exePath -WorkingDirectory $AppPath

# Crear trigger (al iniciar el sistema)
$trigger = New-ScheduledTaskTrigger -AtStartup

# Configurar para ejecutar con máximos privilegios
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

# Configuración adicional
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1)

# Registrar la tarea
Write-Host "Registrando tarea programada..." -ForegroundColor Green
Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Principal $principal `
    -Settings $settings `
    -Description "FileSystemWatcher que envía eventos a Power Automate via webhook"

Write-Host ""
Write-Host "? Tarea programada creada exitosamente" -ForegroundColor Green
Write-Host ""
Write-Host "La tarea se ejecutará automáticamente al iniciar el sistema." -ForegroundColor Cyan
Write-Host ""
Write-Host "Para iniciar la tarea manualmente ahora, ejecute:" -ForegroundColor Yellow
Write-Host "  Start-ScheduledTask -TaskName '$TaskName'" -ForegroundColor White
Write-Host ""
Write-Host "Para ver el estado:" -ForegroundColor Yellow
Write-Host "  Get-ScheduledTask -TaskName '$TaskName'" -ForegroundColor White
Write-Host ""
Write-Host "Para detener la tarea:" -ForegroundColor Yellow
Write-Host "  Stop-ScheduledTask -TaskName '$TaskName'" -ForegroundColor White
Write-Host ""
Write-Host "Para eliminar la tarea:" -ForegroundColor Yellow
Write-Host "  Unregister-ScheduledTask -TaskName '$TaskName'" -ForegroundColor White
Write-Host ""

pause
