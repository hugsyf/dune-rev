[CmdletBinding()]
param(
    [string]$ToolboxPath = "$env:LOCALAPPDATA\Playnite\toolbox.exe",
    [switch]$SkipChecks
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Run with Windows PowerShell 5.1.' }
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Release.psd1')
foreach ($entry in @(@('Source/theme.yaml', $config.ThemeVersion), @('MaterialExtension/extension.yaml', $config.ExtensionVersion))) {
    $actual = (Select-String -LiteralPath (Join-Path $project $entry[0]) -Pattern '^Version: (.+)$').Matches[0].Groups[1].Value.Trim()
    if ($actual -ne $entry[1]) { throw "Version mismatch: $($entry[0])" }
}
$assemblyText = [IO.File]::ReadAllText((Join-Path $project 'MaterialExtension/AssemblyInfo.cs'))
if (-not $assemblyText.Contains('AssemblyVersion("' + $config.ExtensionVersion + '.0")')) { throw 'Extension assembly version mismatch.' }
& (Join-Path $PSScriptRoot 'Update-Theme.ps1') -Check
& (Join-Path $PSScriptRoot 'Build-Materials.ps1')
if (-not $SkipChecks) {
    & (Join-Path $PSScriptRoot 'Test-Materials.ps1')
    & (Join-Path $PSScriptRoot 'Test-MaterialSettings.ps1')
}
& (Join-Path $project 'package.ps1') -ToolboxPath $ToolboxPath
$output = Join-Path $project ('dist/releases/' + $config.Tag)
[IO.Directory]::CreateDirectory($output) | Out-Null
$themeName = 'DuneRev-' + $config.Tag.TrimStart('v') + '.pthm'
$extensionName = 'DuneMaterials-' + $config.ExtensionVersion + '.pext'
$themeInput = Join-Path $project ('release/DuneRev_aa8df0f9-9406-4ea6-a31a-3bd13853fd40_' + $config.ThemeVersion.Replace('.', '_') + '.pthm')
Copy-Item -LiteralPath $themeInput -Destination (Join-Path $output $themeName)
Copy-Item -LiteralPath (Join-Path $project ('dist/' + $extensionName)) -Destination (Join-Path $output $extensionName)
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Assert-ZipFile($archive, $entryName, $sourcePath) {
    $entry = $archive.GetEntry($entryName)
    if ($null -eq $entry) { $entry = $archive.GetEntry($entryName.Replace('/', '\')) }
    if ($null -eq $entry) { throw "Missing packaged file: $entryName" }
    $stream = $entry.Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $actualHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $sha.Dispose() }
    if ($actualHash -ne (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash) { throw "Stale packaged file: $entryName" }
}
$theme = [IO.Compression.ZipFile]::OpenRead((Join-Path $output $themeName))
try {
    foreach ($file in @('theme.yaml', 'Common.xaml', 'Views/GridViewGameOverview.xaml', 'Views/DetailsViewGameOverview.xaml', 'Views/TopPanel.xaml', 'Views/FilterPanelView.xaml')) {
        Assert-ZipFile $theme $file (Join-Path $project ('Source/' + $file))
    }
} finally { $theme.Dispose() }
$extension = [IO.Compression.ZipFile]::OpenRead((Join-Path $output $extensionName))
try {
    Assert-ZipFile $extension 'extension.yaml' (Join-Path $project 'MaterialExtension/extension.yaml')
    Assert-ZipFile $extension 'DuneMaterials.dll' (Join-Path $project 'dist/DuneMaterials/DuneMaterials.dll')
} finally { $extension.Dispose() }
$checksums = foreach ($name in @($themeName, $extensionName)) {
    (Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $name
}
[IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'), [string[]]$checksums, [Text.UTF8Encoding]::new($false))
$buildInfo = @{ Tag = $config.Tag; Commit = (& git -C $project rev-parse HEAD).Trim(); ThemeVersion = $config.ThemeVersion; ExtensionVersion = $config.ExtensionVersion; ChecksSkipped = [bool]$SkipChecks } | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $output 'build-info.json'), $buildInfo, [Text.UTF8Encoding]::new($false))
Write-Output "Verified preview packages: $output"
