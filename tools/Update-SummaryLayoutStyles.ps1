# Generates native WPF layout states. No code or extra assembly ships with the theme.
[CmdletBinding()]
param([switch]$Check)
$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot '..\Source\DefaultControls\Border.xaml'
$keys = @('PlayTime','LastPlayed','Completion','Requirements','HowLongToBeat','Activity','Achievements','Catalog')
$basic = @('PlayTime','LastPlayed','Completion','Requirements')
$optional = @('HowLongToBeat','Activity','Achievements','Languages','Dlc')
$clickable = @('Requirements','HowLongToBeat','Activity','Achievements')
$sourceKeys = @('HowLongToBeat','Activity')
$states = @{}
foreach ($key in $keys) { $states[$key] = [Collections.Generic.List[string]]::new() }
$sourceStates = @{}
foreach ($key in $sourceKeys) { $sourceStates[$key] = [Collections.Generic.List[string]]::new() }
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
for ($mask = 0; $mask -lt 16; $mask++) {
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
for ($mask = 0; $mask -lt 32; $mask++) {
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
    $basedOn = if ($clickable -contains $key) { ' BasedOn="{StaticResource ClickableDetailCard}"' } else { '' }
    $margin = if ($key -eq 'Catalog') { '0' } else { '0,0,12,12' }
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
$generated = $start + "`n    <!-- Regenerate with tools/Update-SummaryLayoutStyles.ps1. Basic cards use 12 tracks; plugin cards use 60. Each Auto row fits its content. -->`n" + ($styles -join "`n") + "`n" + $end
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
& (Join-Path $PSScriptRoot 'Update-OverviewSharedBlocks.ps1') -Check:$Check
