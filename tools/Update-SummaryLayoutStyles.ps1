# Generates native WPF layout states. No code or extra assembly ships with the theme.
[CmdletBinding()]
param([switch]$Check)
$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot '..\Source\DefaultControls\Border.xaml'
$keys = @('PlayTime','HowLongToBeat','LastPlayed','Completion','Activity','Requirements','Achievements')
$optional = @('HowLongToBeat','Activity','Requirements','Achievements')
$states = @{}
foreach ($key in $keys) { $states[$key] = [Collections.Generic.List[string]]::new() }
function Placement($row, $column, $span, $rowSpan = 1, $height = 96, $expanded = $false) {
    return @{ 'Grid.Row'=$row; 'Grid.Column'=$column; 'Grid.ColumnSpan'=$span; 'Grid.RowSpan'=$rowSpan; 'MinHeight'=$height; 'Tag'=$(if ($expanded) { 'Expanded' } else { 'Compact' }) }
}
for ($mask = 0; $mask -lt 16; $mask++) {
    $present = @{}
    $flags = for ($bit = 0; $bit -lt 4; $bit++) {
        $present[$optional[$bit]] = ($mask -band (1 -shl $bit)) -ne 0
        if ($present[$optional[$bit]]) { 'Visible' } else { 'Collapsed' }
    }
    foreach ($mode in 'Regular','Wide') {
        $positions = @{}
        if ($mask -eq 0) {
            # A single compact baseline strip, rather than three tall empty tiles.
            $positions.PlayTime = Placement 0 0 20
            $positions.LastPlayed = Placement 0 20 20
            $positions.Completion = Placement 0 40 20
        } elseif ($mode -eq 'Regular') {
            $items = @($keys | Where-Object { $_ -ne 'Achievements' -and (-not $present.ContainsKey($_) -or $present[$_]) })
            for ($index = 0; $index -lt $items.Count; $index++) {
                $span = if ($index -eq $items.Count - 1 -and $index % 2 -eq 0) { 60 } else { 30 }
                $positions[$items[$index]] = Placement ([int][Math]::Floor($index / 2)) (($index % 2) * 30) $span
            }
            if ($present.Achievements) { $positions.Achievements = Placement ([int][Math]::Ceiling($items.Count / 2)) 0 60 1 112 }
        } else {
            $small = @('LastPlayed','Completion')
            if ($present.Activity) { $small += 'Activity' }
            if ($present.Requirements) { $small += 'Requirements' }
            $achievementUnits = if ($mask -eq 15) { 2 } else { 1 }
            $units = 1 + [int]$present.HowLongToBeat + [int][Math]::Ceiling($small.Count / 2) + ([int]$present.Achievements * $achievementUnits)
            $track = 60 / $units
            $column = 0
            $positions.PlayTime = Placement 0 $column $track 2 204 $true
            $column += $track
            if ($present.HowLongToBeat) {
                $positions.HowLongToBeat = Placement 0 $column $track 2 204 $true
                $column += $track
            }
            for ($index = 0; $index -lt $small.Count; $index += 2) {
                if ($index + 1 -lt $small.Count) {
                    $positions[$small[$index]] = Placement 0 $column $track
                    $positions[$small[$index + 1]] = Placement 1 $column $track
                } else {
                    $positions[$small[$index]] = Placement 0 $column $track 2 204
                }
                $column += $track
            }
            if ($present.Achievements) { $positions.Achievements = Placement 0 $column ($track * $achievementUnits) 2 204 $true }
        }
        $state = $mode + '|' + ($flags -join '|')
        foreach ($key in $keys) {
            if (-not $positions.ContainsKey($key)) { continue }
            $lines = [Collections.Generic.List[string]]::new()
            $lines.Add('            <DataTrigger Binding="{Binding Tag, ElementName=DuneSummary}" Value="' + $state + '">')
            $defaults = Placement ([Array]::IndexOf($keys,$key)) 0 60 1 $(if ($key -eq 'Achievements') { 112 } else { 96 })
            foreach ($property in @('Grid.Row','Grid.Column','Grid.ColumnSpan','Grid.RowSpan','MinHeight','Tag')) {
                if ($positions[$key][$property] -eq $defaults[$property]) { continue }
                $lines.Add('                <Setter Property="' + $property + '" Value="' + $positions[$key][$property] + '" />')
            }
            $lines.Add('            </DataTrigger>')
            $states[$key].Add(($lines -join "`n"))
        }
    }
}
$styles = foreach ($key in $keys) {
    $basedOn = if ($optional -contains $key) { ' BasedOn="{StaticResource ClickableDetailCard}"' } else { '' }
    $index = [Array]::IndexOf($keys, $key)
    $height = if ($key -eq 'Achievements') { 112 } else { 96 }
    @"
    <Style x:Key="Dune${key}Layout" TargetType="Grid"$basedOn>
        <Setter Property="Margin" Value="0,0,12,12" />
        <Setter Property="MinHeight" Value="$height" />
        <Setter Property="Grid.Row" Value="$index" />
        <Setter Property="Grid.Column" Value="0" />
        <Setter Property="Grid.ColumnSpan" Value="60" />
        <Setter Property="Grid.RowSpan" Value="1" />
        <Setter Property="Tag" Value="Compact" />
        <Style.Triggers>
$($states[$key] -join "`n")
        </Style.Triggers>
    </Style>
"@
}
$start = '    <!-- BEGIN GENERATED SUMMARY LAYOUTS -->'
$end = '    <!-- END GENERATED SUMMARY LAYOUTS -->'
$generated = $start + "`n    <!-- Regenerate with tools/Update-SummaryLayoutStyles.ps1. 60 tracks divide exactly into 2, 3, 4, 5 and 6 columns. -->`n" + ($styles -join "`n") + "`n" + $end
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
