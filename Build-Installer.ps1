$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$compilerCandidates = @(@(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) })

if ($compilerCandidates.Count -eq 0) {
    throw 'Inno Setup 6 was not found. Install it and rerun Build-Installer.ps1.'
}

& (Join-Path $projectRoot 'Build.ps1')
& $compilerCandidates[0] (Join-Path $projectRoot 'installer\AstroDeviceHub.iss')

Write-Host "Installer complete: $(Join-Path $projectRoot 'dist\installer')"
