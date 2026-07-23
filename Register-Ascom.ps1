$ErrorActionPreference = 'Stop'
$driver = Join-Path $PSScriptRoot 'dist\ascom\ASCOM.AstroDeviceHub.LocalServer.exe'
if (-not (Test-Path -LiteralPath $driver)) {
    throw '请先运行 Build.ps1。'
}

$isAdministrator = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdministrator) {
    throw '请在管理员 PowerShell 中运行 Register-Ascom.ps1。'
}

& $driver /regserver
