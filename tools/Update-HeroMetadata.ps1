# Copy shared Hero metadata into the original overview template namescopes.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$fragment = [Xml.XmlDocument]::new()
$fragment.Load((Join-Path $PSScriptRoot 'HeroMetadata.xml'))
$builder = [Text.StringBuilder]::new()
$settings = [Xml.XmlWriterSettings]::new()
$settings.OmitXmlDeclaration = $true
$settings.ConformanceLevel = [Xml.ConformanceLevel]::Fragment
$settings.Indent = $true
$settings.IndentChars = '    '
$settings.NewLineChars = "`n"
$writer = [Xml.XmlWriter]::Create($builder,$settings)
try { foreach ($node in $fragment.DocumentElement.ChildNodes) { $node.WriteTo($writer) } } finally { $writer.Dispose() }
$content = $builder.ToString().Replace(' xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"','').Replace(' xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"','')
foreach ($view in @('DetailsViewGameOverview.xaml','GridViewGameOverview.xaml')) {
    $path = Join-Path $PSScriptRoot "../Source/Views/$view"
    $text = [IO.File]::ReadAllText($path)
    $region = [regex]::Match($text, '(?s)(<!--GameInfo-->\s*)(<WrapPanel\b[^>]*>)(.*?)(</WrapPanel>\s*)(?=<!-- Primary command)')
    if (-not $region.Success) { throw "Missing Hero metadata region: $view" }
    # Retain view-specific grid row/column triggers on the outer WrapPanel.
    $layout = [regex]::Match($region.Groups[3].Value,'(?s)^\s*<WrapPanel.Style>.*?</WrapPanel.Style>').Value
    $indent = [regex]::Match($region.Groups[1].Value,'[ \t]*$').Value
    $formatted = ($indent + '    ') + $content.Replace("`n", "`n" + $indent + '    ')
    $replacement = $region.Groups[1].Value + $region.Groups[2].Value + $layout + "`n" + $formatted + "`n" + $indent + $region.Groups[4].Value
    [IO.File]::WriteAllText($path,$text.Remove($region.Index,$region.Length).Insert($region.Index,$replacement),[Text.UTF8Encoding]::new($false))
}
Write-Output 'Shared Hero metadata regenerated.'
