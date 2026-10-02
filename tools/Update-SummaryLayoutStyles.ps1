# Generates native WPF layout states. No code or extra assembly ships with the theme.
[CmdletBinding()]
param([switch]$Check)
$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot '..\Source\DefaultControls\Border.xaml'
$keys = @('PlayTime','LastPlayed','Completion','Requirements','HowLongToBeat','Activity','Achievements','Catalog')
$optional = @('HowLongToBeat','Activity','Requirements','Achievements','Languages','Dlc')
$sourceKeys = @('HowLongToBeat','Activity')
$states = @{}
foreach ($key in $keys) { $states[$key] = [Collections.Generic.List[string]]::new() }
$sourceStates = @{}
foreach ($key in $sourceKeys) { $sourceStates[$key] = [Collections.Generic.List[string]]::new() }
function Placement($row, $column, $span) {
    return @{ 'Grid.Row'=$row; 'Grid.Column'=$column; 'Grid.ColumnSpan'=$span }
}
for ($mask = 0; $mask -lt 64; $mask++) {
    $present = @{}
    $flags = for ($bit = 0; $bit -lt 6; $bit++) {
        $present[$optional[$bit]] = ($mask -band (1 -shl $bit)) -ne 0
        if ($present[$optional[$bit]]) { 'Visible' } else { 'Collapsed' }
    }
    foreach ($mode in 'Regular','Wide') {
        $positions = @{}
        # Keep simple values together; each Auto row grows only to its own content.
        $simple = @('PlayTime','LastPlayed','Completion')
        if ($present.Requirements) { $simple += 'Requirements' }
        $nextRow = 1
        if ($mode -eq 'Regular' -and $simple.Count -eq 4) {
            for ($index = 0; $index -lt $simple.Count; $index++) {
                $positions[$simple[$index]] = Placement ([int][Math]::Floor($index / 2)) (($index % 2) * 30) 30
            }
            $nextRow = 2
        } else {
            $track = 60 / $simple.Count
            for ($index = 0; $index -lt $simple.Count; $index++) {
                $positions[$simple[$index]] = Placement 0 ($index * $track) $track
            }
        }
        $items = @('HowLongToBeat','Activity' | Where-Object { $present[$_] })
        if ($mode -eq 'Wide' -and $present.Achievements) { $items += 'Achievements' }
        if ($mode -eq 'Wide') { $items += @('Languages','Dlc' | Where-Object { $present[$_] }) }
        if ($items.Count) {
            $track = 60 / $items.Count
            for ($index = 0; $index -lt $items.Count; $index++) {
                if ($items[$index] -in @('Languages','Dlc')) {
                    if (-not $positions.ContainsKey('Catalog')) {
                        $catalogCount = [int]$present.Languages + [int]$present.Dlc
                        $positions.Catalog = Placement $nextRow ($index * $track) ($catalogCount * $track)
                    }
                } else {
                    $positions[$items[$index]] = Placement $nextRow ($index * $track) $track
                }
            }
            $nextRow++
        }
        if ($mode -eq 'Regular') {
            if ($present.Achievements) { $positions.Achievements = Placement $nextRow 0 60; $nextRow++ }
            if ($present.Languages -or $present.Dlc) { $positions.Catalog = Placement $nextRow 0 60 }
        }
        $state = $mode + '|' + ($flags -join '|')
        foreach ($key in $keys) {
            if (-not $positions.ContainsKey($key)) { continue }
            $lines = [Collections.Generic.List[string]]::new()
            $lines.Add('            <DataTrigger Binding="{Binding Tag, ElementName=DuneSummary}" Value="' + $state + '">')
            $defaults = Placement ([Array]::IndexOf($keys,$key)) 0 60
            foreach ($property in @('Grid.Row','Grid.Column','Grid.ColumnSpan')) {
                if ($positions[$key][$property] -eq $defaults[$property]) { continue }
                $lines.Add('                <Setter Property="' + $property + '" Value="' + $positions[$key][$property] + '" />')
            }
            $lines.Add('            </DataTrigger>')
            $states[$key].Add(($lines -join "`n"))
        }
        foreach ($key in $sourceKeys) {
            # Derive source visibility from placement, not a separate width assumption.
            if (-not $positions.ContainsKey($key) -or -not $positions.ContainsKey('Achievements')) { continue }
            if ($positions[$key]['Grid.Row'] -ne $positions.Achievements['Grid.Row']) { continue }
            $sourceStates[$key].Add(@"
            <MultiDataTrigger>
                <MultiDataTrigger.Conditions>
                    <Condition Binding="{Binding Tag, ElementName=DuneSummary}" Value="$state" />
                    <Condition Binding="{Binding Visibility, ElementName=DuneLatestAchievementRow}" Value="Visible" />
                </MultiDataTrigger.Conditions>
                <Setter Property="Visibility" Value="Visible" />
            </MultiDataTrigger>
"@)
        }
    }
}
$styles = foreach ($key in $keys) {
    $basedOn = if ($optional -contains $key) { ' BasedOn="{StaticResource ClickableDetailCard}"' } else { '' }
    $margin = if ($key -eq 'Catalog') { '0' } else { '0,0,12,12' }
    $minHeight = if ($key -eq 'Catalog') { '0' } else { '96' }
    $index = [Array]::IndexOf($keys, $key)
    @"
    <Style x:Key="Dune${key}Layout" TargetType="Grid"$basedOn>
        <Setter Property="Margin" Value="$margin" />
        <Setter Property="MinHeight" Value="$minHeight" />
        <Setter Property="VerticalAlignment" Value="Stretch" />
        <Setter Property="Grid.Row" Value="$index" />
        <Setter Property="Grid.Column" Value="0" />
        <Setter Property="Grid.ColumnSpan" Value="60" />
        <Setter Property="Grid.RowSpan" Value="1" />
        <Style.Triggers>
$($states[$key] -join "`n")
        </Style.Triggers>
    </Style>
"@
}
$styles += foreach ($key in $sourceKeys) {
    @"
    <Style x:Key="Dune${key}SourceLayout" TargetType="Grid" BasedOn="{StaticResource DuneSummarySourceFooter}">
        <Style.Triggers>
$($sourceStates[$key] -join "`n")
        </Style.Triggers>
    </Style>
"@
}
$start = '    <!-- BEGIN GENERATED SUMMARY LAYOUTS -->'
$end = '    <!-- END GENERATED SUMMARY LAYOUTS -->'
$generated = $start + "`n    <!-- Regenerate with tools/Update-SummaryLayoutStyles.ps1. Equal-height cards use content-sized Auto rows and 60 responsive tracks. -->`n" + ($styles -join "`n") + "`n" + $end
$generated = $generated.Replace("`r`n", "`n")
$text = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
if ($text.Contains($start)) {
    $first = $text.IndexOf($start)
    $last = $text.IndexOf($end, $first) + $end.Length
    $result = $text.Substring(0,$first) + $generated + $text.Substring($last)
} else {
    $result = $text.Replace('</ResourceDictionary>', $generated + "`n</ResourceDictionary>")
}
if ($Check) {
    if ($text -ne $result) { throw 'Generated summary styles are stale.' }
    Write-Output 'Summary layout styles are up to date.'
} else {
    [IO.File]::WriteAllText($path, $result, (New-Object Text.UTF8Encoding($false)))
}
