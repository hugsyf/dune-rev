param(
    [string]$SdkPath = (Join-Path $PSScriptRoot '../.local-archive/material-sdk/Playnite.SDK.dll'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../dist/DuneMaterials')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase, System.Xaml
$sdk = (Resolve-Path -LiteralPath $SdkPath).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($output) | Out-Null
$references = @(
    [System.Windows.Media.Visual].Assembly.Location,
    [System.Windows.Window].Assembly.Location,
    [System.Windows.Point].Assembly.Location,
    [System.Xaml.XamlReader].Assembly.Location,
    $sdk
)
$compiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
# The withdrawn visual-capture engine is deliberately excluded.
$sources = @('Plugin.cs', 'MaterialSettings.cs', 'AssemblyInfo.cs', 'MaterialCoordinator.cs', 'MaterialSurface.cs', 'MaterialTextureCache.cs', 'PilotTexture.cs') | ForEach-Object { Join-Path $PSScriptRoot "../MaterialExtension/$_" }
$sources += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../MaterialExtension/Rendering') -Filter '*.cs' | ForEach-Object { $_.FullName }
$arguments = @('/nologo', '/target:library', '/optimize+', '/warnaserror+', "/out:$output/DuneMaterials.dll")
$arguments += $references | ForEach-Object { '/reference:' + $_ }
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Material extension compilation failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../MaterialExtension/extension.yaml') -Destination $output
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../LICENSE') -Destination $output
Add-Type -AssemblyName System.IO.Compression.FileSystem
$versionLine = Select-String -LiteralPath (Join-Path $output 'extension.yaml') -Pattern '^Version: (.+)$'
$version = $versionLine.Matches[0].Groups[1].Value.Trim()
$package = Join-Path (Split-Path $output) "DuneMaterials-$version.pext"
if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package }
[IO.Compression.ZipFile]::CreateFromDirectory($output, $package)
Write-Output "Built extension: $package"
