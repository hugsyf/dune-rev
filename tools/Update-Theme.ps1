# Generate all theme fragments before writing any output.
[CmdletBinding()]
param([switch]$Check)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ThemeGeneration.psm1') -Force

$sourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Source'))
$updates = @(Get-ThemeUpdates -SourceRoot $sourceRoot)
$changed = @($updates | Where-Object { $_.Original -cne $_.Generated })
if ($Check) {
    if ($changed.Count) {
        $paths = ($changed | ForEach-Object { $_.Path }) -join "`n"
        throw "Generated theme files are stale. Run tools/Update-Theme.ps1:`n$paths"
    }
    Write-Output 'All generated theme files are up to date.'
    return
}

foreach ($update in $changed) {
    [IO.File]::WriteAllText($update.Path, $update.Generated, [Text.UTF8Encoding]::new($false))
    Write-Output "Updated: $($update.Path)"
}
if (-not $changed.Count) { Write-Output 'All generated theme files are up to date.' }
