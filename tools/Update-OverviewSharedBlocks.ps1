# Keep plugin/native control names in each overview's existing template namescope.
# These fragments are copied at development time; no extra runtime control ships.
[CmdletBinding()]
param([switch]$Check)
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '..\Source\Views'
$fragments = [Xml.XmlDocument]::new()
$fragments.Load((Join-Path $PSScriptRoot 'SummaryCards.xml'))
function GetGridBlock($text, $name) {
    $opening = [regex]::Match($text, '<Grid(?=[\s>])[^>]*\bx:Name="' + [regex]::Escape($name) + '"[^>]*>')
    if (-not $opening.Success) { throw "Missing shared block: $name" }
    $depth = 0
    foreach ($token in [regex]::Matches($text.Substring($opening.Index), '<Grid(?=[\s>])[^>]*>|</Grid\s*>')) {
        if ($token.Value.StartsWith('</')) { $depth-- }
        elseif (-not $token.Value.EndsWith('/>')) { $depth++ }
        if ($depth -eq 0) { return @{ Start=$opening.Index; Length=$token.Index+$token.Length } }
    }
    throw "Unclosed shared block: $name"
}
function FormatNode($node) {
    $builder = [Text.StringBuilder]::new()
    $settings = [Xml.XmlWriterSettings]::new()
    $settings.OmitXmlDeclaration = $true
    $settings.Indent = $true
    $settings.IndentChars = '    '
    $settings.NewLineChars = "`n"
    $writer = [Xml.XmlWriter]::Create($builder, $settings)
    try { $node.WriteTo($writer) } finally { $writer.Dispose() }
    # The destination overview already declares these namespaces.
    return $builder.ToString().Replace(' xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"','').Replace(' xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"','')
}
foreach ($file in @('DetailsViewGameOverview.xaml','GridViewGameOverview.xaml')) {
    $path = Join-Path $root $file
    $text = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
    $result = $text
    foreach ($node in $fragments.DocumentElement.ChildNodes) {
        if ($node -isnot [Xml.XmlElement]) { continue }
        $name = $node.GetAttribute('Name','http://schemas.microsoft.com/winfx/2006/xaml')
        $block = GetGridBlock $result $name
        $lineStart = $result.LastIndexOf("`n", $block.Start) + 1
        $indent = $result.Substring($lineStart, $block.Start-$lineStart)
        $markup = (FormatNode $node).Replace("`n", "`n" + $indent)
        $result = $result.Substring(0,$block.Start) + $markup + $result.Substring($block.Start+$block.Length)
    }
    if ($Check) {
        if ($text -ne $result) { throw "Shared summary cards are stale: $file" }
    } elseif ($text -ne $result) {
        [IO.File]::WriteAllText($path, $result, [Text.UTF8Encoding]::new($false))
    }
}
Write-Output 'Shared summary cards are up to date.'
