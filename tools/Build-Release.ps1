[CmdletBinding()]
param([string]$ToolboxPath = "$env:LOCALAPPDATA\Playnite\toolbox.exe")
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Run with Windows PowerShell 5.1.' }
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Release.psd1')
$includeExtension = [bool]$config.IncludeExtension
function Assert-Version($relative, $expected) {
    $actual = (Select-String -LiteralPath (Join-Path $project $relative) -Pattern '^Version: (.+)$').Matches[0].Groups[1].Value.Trim()
    if ($actual -ne $expected) { throw "Version mismatch: $relative" }
}
Assert-Version 'Source/theme.yaml' $config.ThemeVersion
if ($includeExtension) {
    Assert-Version 'MaterialExtension/extension.yaml' $config.ExtensionVersion
    $assembly = [IO.File]::ReadAllText((Join-Path $project 'MaterialExtension/AssemblyInfo.cs'))
    if (-not $assembly.Contains('AssemblyVersion("' + $config.ExtensionVersion + '.0")')) { throw 'Extension assembly version mismatch.' }
}
& (Join-Path $PSScriptRoot 'Update-Theme.ps1')
if ($includeExtension) { & (Join-Path $PSScriptRoot 'Build-Materials.ps1') }
& (Join-Path $project 'package.ps1') -ToolboxPath $ToolboxPath
$output = Join-Path $project ('dist/releases/' + $config.Tag)
[IO.Directory]::CreateDirectory($output) | Out-Null
$id = (Select-String -LiteralPath (Join-Path $project 'Source/theme.yaml') -Pattern '^Id: (.+)$').Matches[0].Groups[1].Value.Trim()
$packedName = $id + '_' + $config.ThemeVersion.Replace('.', '_') + '.pthm'
$themeName = if ($config.Tag -like '*-preview.*') { 'DuneRev-' + $config.Tag.TrimStart('v') + '.pthm' } else { $packedName }
Copy-Item -LiteralPath (Join-Path $project ('release/' + $packedName)) -Destination (Join-Path $output $themeName)
$names = @($themeName)
if ($includeExtension) {
    $extensionName = 'DuneMaterials-' + $config.ExtensionVersion + '.pext'
    Copy-Item -LiteralPath (Join-Path $project ('dist/' + $extensionName)) -Destination (Join-Path $output $extensionName)
    $names += $extensionName
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Assert-ZipFile($archive, $entryName, $sourcePath) {
    $entry = $archive.GetEntry($entryName)
    if ($null -eq $entry) { $entry = $archive.GetEntry($entryName.Replace('/', '\')) }
    if ($null -eq $entry) { throw "Missing packaged file: $entryName" }
    $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
    try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $sha.Dispose() }
    if ($actual -ne (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash) { throw "Stale packaged file: $entryName" }
}
$source = Join-Path $project 'Source'
$theme = [IO.Compression.ZipFile]::OpenRead((Join-Path $output $themeName))
try {
    # Toolbox omits design files and files identical to Playnite's defaults.
    # Compare the shipped markup with source and require the release's core files.
    foreach ($entry in $theme.Entries) {
        $relative = $entry.FullName.Replace([char]92, [char]47)
        if ([IO.Path]::GetExtension($relative) -in @('.xaml', '.yaml')) {
            Assert-ZipFile $theme $relative (Join-Path $source $relative)
        }
    }
    foreach ($relative in @('theme.yaml', 'Common.xaml', 'Constants.xaml', 'thememodifier.yaml', 'Views/DetailsViewGameOverview.xaml', 'Views/GridViewGameOverview.xaml')) {
        Assert-ZipFile $theme $relative (Join-Path $source $relative)
    }
} finally { $theme.Dispose() }
if ($includeExtension) {
    $extension = [IO.Compression.ZipFile]::OpenRead((Join-Path $output $extensionName))
    try {
        Assert-ZipFile $extension 'extension.yaml' (Join-Path $project 'MaterialExtension/extension.yaml')
        Assert-ZipFile $extension 'DuneMaterials.dll' (Join-Path $project 'dist/DuneMaterials/DuneMaterials.dll')
    } finally { $extension.Dispose() }
}
$sums = @($names | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $output $_) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_ })
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'), (($sums -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
$info = @{
    Tag = $config.Tag; Commit = (& git -C $project rev-parse HEAD).Trim()
    ThemeVersion = $config.ThemeVersion; ExtensionVersion = $config.ExtensionVersion
    IncludeExtension = $includeExtension; ThemePackage = $themeName
    ThemeHash = (Get-FileHash -LiteralPath (Join-Path $output $themeName) -Algorithm SHA256).Hash.ToLowerInvariant()
} | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $output 'build-info.json'), $info, [Text.UTF8Encoding]::new($false))
Write-Output "Verified release packages: $output"
