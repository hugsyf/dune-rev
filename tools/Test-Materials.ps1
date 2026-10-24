[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase, System.Xaml
$output = Join-Path $PSScriptRoot '../dist/material-checks'
[IO.Directory]::CreateDirectory($output) | Out-Null
$compiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
$references = @([Windows.Media.Visual].Assembly.Location, [Windows.Window].Assembly.Location, [Windows.Point].Assembly.Location, [System.Xaml.XamlReader].Assembly.Location)
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../MaterialExtension/Rendering') -Filter '*.cs' | ForEach-Object { $_.FullName })
$sources += Join-Path $PSScriptRoot '../MaterialExtension/PilotTexture.cs'
$sources += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'material-tests') -Filter '*.cs' | ForEach-Object { $_.FullName }
$executable = Join-Path $output 'MaterialChecks.exe'
$arguments = @('/nologo', '/target:exe', '/optimize+', '/warnaserror+', "/out:$executable")
$arguments += $references | ForEach-Object { '/reference:' + $_ }
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Material checks compilation failed.' }
& $executable
if ($LASTEXITCODE -ne 0) { throw 'Material algorithm/lifetime checks failed.' }
