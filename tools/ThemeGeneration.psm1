# Development-time generation only; plugin controls stay in their original namescopes.
function Read-ThemeText($path) {
    [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
}

function Format-XamlNodes($nodes) {
    $builder = [Text.StringBuilder]::new()
    $settings = [Xml.XmlWriterSettings]::new()
    $settings.OmitXmlDeclaration = $true
    $settings.ConformanceLevel = [Xml.ConformanceLevel]::Fragment
    $settings.Indent = $true
    $settings.IndentChars = '    '
    $settings.NewLineChars = "`n"
    $writer = [Xml.XmlWriter]::Create($builder, $settings)
    try { foreach ($node in $nodes) { $node.WriteTo($writer) } } finally { $writer.Dispose() }
    # Both destination views already declare these namespaces.
    $builder.ToString().Replace(' xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"','').Replace(' xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"','')
}

function Get-NamedElementBlock($text, $element, $name) {
    if (-not $name) { throw 'A shared fragment is missing x:Name.' }
    # Skip comments so commented examples cannot become replacement targets.
    $pattern = '(?s)<!--.*?-->|<' + $element + '(?=[\s>])[^>]*>|</' + $element + '\s*>'
    $tokens = @([regex]::Matches($text, $pattern) | Where-Object { -not $_.Value.StartsWith('<!--') })
    $namePattern = '\bx:Name="' + [regex]::Escape($name) + '"'
    $openings = @($tokens | Where-Object { $_.Value -match $namePattern })
    if ($openings.Count -ne 1) { throw "Expected one $element named $name; found $($openings.Count)." }
    $opening = $openings[0]
    $depth = 0
    foreach ($token in $tokens) {
        if ($token.Index -lt $opening.Index) { continue }
        if ($token.Value.StartsWith('</')) { $depth-- }
        elseif (-not $token.Value.EndsWith('/>')) { $depth++ }
        if ($depth -eq 0) {
            return @{ Start = $opening.Index; Length = $token.Index + $token.Length - $opening.Index }
        }
    }
    throw "Unclosed shared block: $name"
}

function Replace-NamedElement($text, $element, $name, $markup) {
    $block = Get-NamedElementBlock $text $element $name
    $lineStart = $text.LastIndexOf("`n", $block.Start) + 1
    $indent = $text.Substring($lineStart, $block.Start - $lineStart)
    if ($indent -notmatch '^[ \t]*$') { throw "Shared block must start on its own line: $name" }
    $formatted = $markup.Replace("`n", "`n" + $indent)
    $text.Remove($block.Start, $block.Length).Insert($block.Start, $formatted)
}

function Replace-GeneratedRegion($text, $start, $end, $content) {
    $starts = [regex]::Matches($text, [regex]::Escape($start))
    $ends = [regex]::Matches($text, [regex]::Escape($end))
    if ($starts.Count -ne 1 -or $ends.Count -ne 1) {
        throw "Expected exactly one start and end marker for $start"
    }
    if ($ends[0].Index -le $starts[0].Index) { throw "Reversed generated region: $start" }
    $length = $ends[0].Index + $end.Length - $starts[0].Index
    $text.Remove($starts[0].Index, $length).Insert($starts[0].Index, $start + "`n" + $content + "`n" + $end)
}

function Update-HeroMetadataText($text, $content) {
    $regions = [regex]::Matches($text, '(?s)(<!--GameInfo-->\s*)(<WrapPanel\b[^>]*>)(.*?)(</WrapPanel>\s*)(?=<!-- Primary command)')
    if ($regions.Count -ne 1) { throw "Expected one Hero metadata region; found $($regions.Count)." }
    $region = $regions[0]
    # Retain view-specific row/column triggers on the outer WrapPanel.
    $layout = [regex]::Match($region.Groups[3].Value, '(?s)^\s*<WrapPanel.Style>.*?</WrapPanel.Style>').Value
    $indent = [regex]::Match($region.Groups[1].Value, '[ \t]*$').Value
    $formatted = ($indent + '    ') + $content.Replace("`n", "`n" + $indent + '    ')
    $replacement = $region.Groups[1].Value + $region.Groups[2].Value + $layout + "`n" + $formatted + "`n" + $indent + $region.Groups[4].Value
    $text.Remove($region.Index, $region.Length).Insert($region.Index, $replacement)
}

function New-StateBinding($name, $cards, $span, $visibility) {
    $format = '{}' + ((0..$cards.Count | ForEach-Object { '{' + $_ + '}' }) -join '|')
    $bindings = @('            <Binding ElementName="DuneLayoutMode" Path="Tag" />')
    $bindings += @($cards | ForEach-Object { '            <Binding ElementName="' + $_.Element + '" Path="Visibility" />' })
    @"
<TextBlock x:Name="$name" Height="0" Visibility="$visibility" Grid.ColumnSpan="$span" IsHitTestVisible="False">
    <TextBlock.Text>
        <MultiBinding StringFormat="$format">
$($bindings -join "`n")
        </MultiBinding>
    </TextBlock.Text>
</TextBlock>
"@
}

function Get-ThemeUpdates($SourceRoot) {
    $layout = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'SummaryLayout.psd1')
    $fragments = foreach ($file in 'SummaryCards.xml','OverviewDetails.xml','UserRating.xml','HeroScreenshots.xml') {
        $document = [Xml.XmlDocument]::new()
        $document.Load((Join-Path $PSScriptRoot $file))
        foreach ($node in $document.DocumentElement.ChildNodes) {
            if ($node -is [Xml.XmlElement]) {
                @{
                    Name = $node.GetAttribute('Name','http://schemas.microsoft.com/winfx/2006/xaml')
                    Element = $node.LocalName
                    Markup = Format-XamlNodes @($node)
                }
            }
        }
    }
    $hero = [Xml.XmlDocument]::new()
    $hero.Load((Join-Path $PSScriptRoot 'HeroMetadata.xml'))
    $heroMarkup = Format-XamlNodes $hero.DocumentElement.ChildNodes
    $basicState = New-StateBinding 'DuneBasicState' $layout.Basic 12 'Collapsed'
    $optionalState = New-StateBinding 'DuneSummaryState' $layout.Optional 60 'Hidden'

    $path = Join-Path $SourceRoot 'DefaultControls/Border.xaml'
    $text = Read-ThemeText $path
    $styles = Get-SummaryLayoutStyles $layout
    $content = '    <!-- Regenerate with tools/Update-Theme.ps1. Basic cards use 12 tracks; plugin cards use 60. Each Auto row fits its content. -->' + "`n" + $styles
    $result = Replace-GeneratedRegion $text '    <!-- BEGIN GENERATED SUMMARY LAYOUTS -->' '    <!-- END GENERATED SUMMARY LAYOUTS -->' $content
    @{ Path = $path; Original = $text; Generated = $result }

    foreach ($file in 'DetailsViewGameOverview.xaml','GridViewGameOverview.xaml') {
        $path = Join-Path $SourceRoot "Views/$file"
        $text = Read-ThemeText $path
        $result = $text
        foreach ($fragment in $fragments) {
            $result = Replace-NamedElement $result $fragment.Element $fragment.Name $fragment.Markup
        }
        $result = Update-HeroMetadataText $result $heroMarkup
        $result = Replace-NamedElement $result 'TextBlock' 'DuneBasicState' $basicState
        $result = Replace-NamedElement $result 'TextBlock' 'DuneSummaryState' $optionalState
        @{ Path = $path; Original = $text; Generated = $result }
    }
}

function Get-SummaryLayoutStyles($layout) {
    $cards = @($layout.Basic) + @($layout.Optional)
    $keys = @($cards | ForEach-Object { if ($_.Layout) { $_.Layout } else { $_.Key } } | Select-Object -Unique)
    $basic = @($layout.Basic | ForEach-Object { $_.Key })
    $optional = @($layout.Optional | ForEach-Object { $_.Key })
    $clickable = @($cards | Where-Object { $_.Clickable } | ForEach-Object { $_.Key })
    $states = @{}
    foreach ($key in $keys) { $states[$key] = [Collections.Generic.List[string]]::new() }
    function Placement($row, $column, $span) {
        return @{ 'Grid.Row'=$row; 'Grid.Column'=$column; 'Grid.ColumnSpan'=$span }
    }
    function AddLayoutState($key, $group, $state, $position, $span) {
        $lines = [Collections.Generic.List[string]]::new()
        foreach ($property in @('Grid.Row','Grid.Column','Grid.ColumnSpan')) {
            $default = if ($property -eq 'Grid.ColumnSpan') { $span } else { 0 }
            if ($position[$property] -eq $default) { continue }
            $lines.Add('                <Setter Property="' + $property + '" Value="' + $position[$property] + '" />')
        }
        if (-not $lines.Count) { return }
        $states[$key].Add('            <DataTrigger Binding="{Binding Tag, ElementName=' + $group + '}" Value="' + $state + '">' + "`n" + ($lines -join "`n") + "`n            </DataTrigger>")
    }
    # Basic statistics pack independently. Twelve tracks support one to four cards;
    # hidden native fields do not multiply the plugin layout state space.
    for ($mask = 0; $mask -lt (1 -shl $basic.Count); $mask++) {
        $present = @{}
        $flags = for ($bit = 0; $bit -lt $basic.Count; $bit++) {
            $present[$basic[$bit]] = ($mask -band (1 -shl $bit)) -ne 0
            if ($present[$basic[$bit]]) { 'Visible' } else { 'Collapsed' }
        }
        $simple = @($basic | Where-Object { $present[$_] })
        if (-not $simple.Count) { continue }
        foreach ($mode in 'Narrow','Regular','Wide') {
            $state = $mode + '|' + ($flags -join '|')
            for ($index = 0; $index -lt $simple.Count; $index++) {
                $position = if ($mode -eq 'Narrow') {
                    Placement $index 0 12
                } elseif ($mode -eq 'Regular' -and $simple.Count -eq 4) {
                    Placement ([int][Math]::Floor($index / 2)) (($index % 2) * 6) 6
                } else {
                    $track = 12 / $simple.Count
                    Placement 0 ($index * $track) $track
                }
                AddLayoutState $simple[$index] 'DuneBasicCards' $state $position 12
            }
        }
    }
    for ($mask = 0; $mask -lt (1 -shl $optional.Count); $mask++) {
        $present = @{}
        $flags = for ($bit = 0; $bit -lt $optional.Count; $bit++) {
            $present[$optional[$bit]] = ($mask -band (1 -shl $bit)) -ne 0
            if ($present[$optional[$bit]]) { 'Visible' } else { 'Collapsed' }
        }
        foreach ($mode in 'Narrow','Regular','Wide') {
            $positions = @{}
            # Row zero contains the independent basic statistics grid.
            $nextRow = 1
            $items = @('HowLongToBeat','Activity' | Where-Object { $present[$_] })
            if ($mode -eq 'Wide' -and $present.Achievements) { $items += 'Achievements' }
            if ($mode -eq 'Wide') { $items += @('Languages','Dlc' | Where-Object { $present[$_] }) }
            if ($items.Count) {
                $track = 60 / $items.Count
                for ($index = 0; $index -lt $items.Count; $index++) {
                    if ($mode -eq 'Narrow') {
                        $positions[$items[$index]] = Placement $nextRow 0 60
                        $nextRow++
                    } elseif ($items[$index] -in @('Languages','Dlc')) {
                        if (-not $positions.ContainsKey('Catalog')) {
                            $catalogCount = [int]$present.Languages + [int]$present.Dlc
                            $positions.Catalog = Placement $nextRow ($index * $track) ($catalogCount * $track)
                        }
                    } else {
                        $positions[$items[$index]] = Placement $nextRow ($index * $track) $track
                    }
                }
                if ($mode -ne 'Narrow') { $nextRow++ }
            }
            if ($mode -ne 'Wide') {
                if ($present.Achievements) { $positions.Achievements = Placement $nextRow 0 60; $nextRow++ }
                if ($present.Languages -or $present.Dlc) { $positions.Catalog = Placement $nextRow 0 60 }
            }
            $state = $mode + '|' + ($flags -join '|')
            foreach ($key in $keys) {
                if (-not $positions.ContainsKey($key)) { continue }
                AddLayoutState $key 'DuneSummary' $state $positions[$key] 60
            }
        }
    }
    $styles = foreach ($key in $keys) {
        $basedOn = if ($clickable -contains $key) { ' BasedOn="{StaticResource ClickableDetailCard}"' } else { '' }
        $margin = if ($key -eq 'Catalog') { '0' } else { '{DynamicResource DuneSummaryCardMargin}' }
        $minHeight = if ($key -eq 'Catalog') { '0' } else { '96' }
        $span = if ($basic -contains $key) { 12 } else { 60 }
        @"
    <Style x:Key="Dune${key}Layout" TargetType="Grid"$basedOn>
        <Setter Property="Margin" Value="$margin" />
        <Setter Property="MinHeight" Value="$minHeight" />
        <Setter Property="VerticalAlignment" Value="Stretch" />
        <Setter Property="Grid.Row" Value="0" />
        <Setter Property="Grid.Column" Value="0" />
        <Setter Property="Grid.ColumnSpan" Value="$span" />
        <Setter Property="Grid.RowSpan" Value="1" />
        <Style.Triggers>
$($states[$key] -join "`n")
        </Style.Triggers>
    </Style>
"@
    }
    ($styles -join "`n").Replace("`r`n", "`n")
}

Export-ModuleMember -Function Get-ThemeUpdates
