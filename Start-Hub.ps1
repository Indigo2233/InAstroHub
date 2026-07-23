$ErrorActionPreference = 'Stop'
$published = Join-Path $PSScriptRoot 'dist\app\AstroDeviceHub.App.exe'
if (Test-Path -LiteralPath $published) {
    & $published
    exit $LASTEXITCODE
}

dotnet run --project (Join-Path $PSScriptRoot 'desktop\AstroDeviceHub.Desktop.csproj')
