$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$distRoot = Join-Path $projectRoot 'dist'
$serverOutput = Join-Path $distRoot 'server'
$desktopOutput = Join-Path $distRoot 'app'
$ascomOutput = Join-Path $distRoot 'ascom'

if (Test-Path -LiteralPath $distRoot) {
    $resolvedProject = (Resolve-Path -LiteralPath $projectRoot).Path.TrimEnd('\') + '\'
    $resolvedDist = (Resolve-Path -LiteralPath $distRoot).Path
    if (-not $resolvedDist.StartsWith($resolvedProject, [StringComparison]::OrdinalIgnoreCase)) {
        throw "拒绝清理项目目录之外的发布路径：$resolvedDist"
    }
    Remove-Item -LiteralPath $resolvedDist -Recurse -Force
}

dotnet test (Join-Path $projectRoot 'tests\AstroDeviceHub.Tests.csproj') -c Release
dotnet publish (Join-Path $projectRoot 'AstroDeviceHub.csproj') -c Release -r win-x64 --self-contained true -o $serverOutput
dotnet publish (Join-Path $projectRoot 'desktop\AstroDeviceHub.Desktop.csproj') -c Release -r win-x64 --self-contained true -o $desktopOutput
dotnet publish (Join-Path $projectRoot 'ascom\AstroDeviceHub.Ascom.csproj') -c Release -o $ascomOutput

$nestedServer = Join-Path $ascomOutput 'server'
New-Item -ItemType Directory -Force -Path $nestedServer | Out-Null
Copy-Item -Path (Join-Path $serverOutput '*') -Destination $nestedServer -Recurse -Force

Write-Host "Build complete: $distRoot"
