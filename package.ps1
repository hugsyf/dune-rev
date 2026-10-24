[CmdletBinding()]
param(
    [string]$ToolboxPath = "$env:LOCALAPPDATA\Playnite\toolbox.exe"
)

$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$sourcePath = Join-Path $projectRoot 'Source'
$manifestPath = Join-Path $sourcePath 'theme.yaml'
$releasePath = Join-Path $projectRoot 'release'

if (-not (Test-Path -LiteralPath $ToolboxPath -PathType Leaf)) {
    throw "Playnite toolbox was not found: $ToolboxPath"
}

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Theme manifest was not found: $manifestPath"
}

New-Item -ItemType Directory -Path $releasePath -Force | Out-Null

$existingPackages = @(
    Get-ChildItem -LiteralPath $releasePath -Filter '*.pthm' -File |
        Select-Object -ExpandProperty FullName
)

Write-Host "Packing the theme from: $sourcePath"
Write-Host "Release directory:      $releasePath"

& $ToolboxPath pack $sourcePath $releasePath
if ($LASTEXITCODE -ne 0) {
    throw "Playnite toolbox failed with exit code $LASTEXITCODE."
}

$packages = @(
    Get-ChildItem -LiteralPath $releasePath -Filter '*.pthm' -File |
        Sort-Object LastWriteTime -Descending
)

if ($packages.Count -eq 0) {
    throw 'Playnite toolbox completed without producing a .pthm package.'
}

$newPackages = @($packages | Where-Object { $_.FullName -notin $existingPackages })
if ($newPackages.Count -eq 0) {
    $newPackages = @($packages | Select-Object -First 1)
}

Write-Host ''
Write-Host 'Package created:'
$newPackages | ForEach-Object {
    Write-Host "  $($_.FullName)"
}
