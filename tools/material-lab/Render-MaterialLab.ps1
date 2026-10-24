param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../../.local-archive/material-lab'),
    [string]$BackgroundImage,
    [switch]$Interactive
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase, System.Xaml
$references = @(
    [System.Windows.Media.Visual].Assembly.Location,
    [System.Windows.Window].Assembly.Location,
    [System.Windows.Point].Assembly.Location,
    [System.Xaml.XamlReader].Assembly.Location
)
Add-Type -Path (Join-Path $PSScriptRoot 'MaterialLab.cs') -ReferencedAssemblies $references
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($outputPath) | Out-Null
$imagePath = if ($BackgroundImage) { (Resolve-Path -LiteralPath $BackgroundImage).Path } else { $null }
[Dune.MaterialLab]::Export($outputPath, $imagePath)
Write-Output "WPF material previews: $outputPath"
if ($Interactive) { [Dune.MaterialLab]::Show($imagePath) }
