# Exercise the actual summary XAML with fixture data, without installing extensions.
# Run: powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File tools/Test-SummaryCards.ps1
# Add -PreviewDirectory "release\summary-previews" to regenerate sample-data PNGs.
# Add -PreviewOnly to render snapshots without running the layout matrix.
[CmdletBinding()]
param([string]$PreviewDirectory, [ValidateSet('en_US','zh_CN')][string]$Language='en_US', [switch]$PreviewOnly, [switch]$CatalogOnly)
$ErrorActionPreference = 'Stop'
if($PreviewOnly -and -not $PreviewDirectory) { throw '-PreviewOnly requires -PreviewDirectory.' }
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
Add-Type -ReferencedAssemblies PresentationFramework,PresentationCore,WindowsBase @'
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
public class SummaryInvertedVisibility : IValueConverter {
    public object Convert(object value, Type type, object parameter, CultureInfo culture) { return value is bool && (bool)value ? Visibility.Collapsed : Visibility.Visible; }
    public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) { throw new NotSupportedException(); }
}
public class SummaryObjectToString : IValueConverter {
    public object Convert(object value, Type type, object parameter, CultureInfo culture) { return value == null ? "" : value.ToString(); }
    public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) { throw new NotSupportedException(); }
}
public class SummaryFixture {
    public string ConfigurationPath { get; set; }
    public SummaryHltb HowLongToBeat { get; set; }
    public SummaryActivity GameActivity { get; set; }
    public SummaryRequirements SystemChecker { get; set; }
    public SummaryAchievements PlayniteAchievements { get; set; }
    public SummaryLocalizations CheckLocalizations { get; set; }
    public SummaryDlcSettings CheckDlc { get; set; }
}
public class SummaryHltb { public bool HasData { get; set; } public string MainStoryFormat { get; set; } public long MainExtra { get; set; } public string MainExtraFormat { get; set; } public long Completionist { get; set; } public string CompletionistFormat { get; set; } }
public class SummaryActivity { public bool HasData { get; set; } public string LastPlaytimeSession { get; set; } public string LastDateSession { get; set; } public string RecentActivity { get; set; } }
public class SummaryRequirements { public bool EnableIntegrationButton { get; set; } public bool HasData { get; set; } public bool IsMinimumOK { get; set; } public bool IsRecommendedOK { get; set; } }
public class SummaryAchievements { public object ModernTheme { get; set; } }
public class SummaryModernAchievements { public bool HasAchievements { get; set; } public int UnlockedCount { get; set; } public int AchievementCount { get; set; } public bool HasLatestAchievementData { get; set; } public SummaryLatestAchievement LatestAchievementData { get; set; } }
public class SummaryLatestAchievement { public string DisplayName { get; set; } public DateTime DateUnlocked { get; set; } }
public class SummaryAvailability { public bool HasData { get; set; } }
public class SummaryLocalizations : SummaryAvailability { public bool HasNativeSupport { get; set; } public System.Collections.ObjectModel.ObservableCollection<string> ListNativeSupport { get; set; } public SummaryLocalizations() { ListNativeSupport = new System.Collections.ObjectModel.ObservableCollection<string>(); } }
public class SummaryDlcSettings : SummaryAvailability { public System.Collections.ObjectModel.ObservableCollection<SummaryDlc> ListDlcs { get; set; } public SummaryDlcSettings() { ListDlcs = new System.Collections.ObjectModel.ObservableCollection<SummaryDlc>(); } }
public class SummaryDlc { public bool IsOwned { get; set; } }
'@
$source = Join-Path $PSScriptRoot '..\Source'
$app = [Windows.Application]::new()
$testWindow=[Windows.Window]::new()
$testWindow.Left=-32000; $testWindow.Top=-32000
$testWindow.Width=1800; $testWindow.Height=1200
$testWindow.ShowInTaskbar=$false; $testWindow.ShowActivated=$false
$testWindow.WindowStyle='None'; $testWindow.ResizeMode='NoResize'; $testWindow.SizeToContent='Height'
function Pump($view, $width) {
    if($view.Parent -eq $testWindow) { $testWindow.Width=$width }
    for ($pass=0; $pass -lt 5; $pass++) {
        [void]$view.Dispatcher.Invoke([Windows.Threading.DispatcherPriority]::DataBind,[Action]{})
        $view.Measure([Windows.Size]::new($width,[double]::PositiveInfinity))
        $view.Arrange([Windows.Rect]::new(0,0,$width,$view.DesiredSize.Height))
        $view.UpdateLayout()
        [void]$view.Dispatcher.Invoke([Windows.Threading.DispatcherPriority]::ApplicationIdle,[Action]{})
    }
}
function Fixture($mask) {
    $languages=[SummaryLocalizations]@{HasData=$mask -ne 0; HasNativeSupport=$true}
    $languages.ListNativeSupport.Add('English'); $languages.ListNativeSupport.Add('Chinese Simplified')
    $dlc=[SummaryDlcSettings]@{HasData=$mask -ne 0}
    foreach($index in 0..7) { $dlc.ListDlcs.Add([SummaryDlc]@{IsOwned=$index -lt 3}) }
    return [SummaryFixture]@{
        ConfigurationPath=Join-Path $source '..\release\missing-extension-fixture'
        HowLongToBeat=[SummaryHltb]@{ HasData=($mask -band 1) -ne 0; MainStoryFormat=$(if($Language -eq 'zh_CN'){'12 小时 30 分钟'}else{'12 h 30 min'}); MainExtra=72000L; MainExtraFormat=$(if($Language -eq 'zh_CN'){'20 小时'}else{'20 h'}); Completionist=144000L; CompletionistFormat=$(if($Language -eq 'zh_CN'){'40 小时'}else{'40 h'}) }
        GameActivity=[SummaryActivity]@{ HasData=($mask -band 2) -ne 0; LastPlaytimeSession=$(if($Language -eq 'zh_CN'){'45 分钟'}else{'45 min'}); LastDateSession='2026/10/02'; RecentActivity=$(if($Language -eq 'zh_CN'){'过去两周游玩 3.5 小时'}else{'3.5 hours in the past 2 weeks'}) }
        SystemChecker=[SummaryRequirements]@{ EnableIntegrationButton=($mask -band 4) -ne 0; HasData=$true; IsMinimumOK=$true; IsRecommendedOK=$true }
        PlayniteAchievements=[SummaryAchievements]@{ ModernTheme=[SummaryModernAchievements]@{ HasAchievements=($mask -band 8) -ne 0; UnlockedCount=12; AchievementCount=40; HasLatestAchievementData=$true; LatestAchievementData=[SummaryLatestAchievement]@{ DisplayName='A very long achievement title to verify truncation'; DateUnlocked=[datetime]'2026-10-02T10:30:00' } } }
        CheckLocalizations=$languages
        CheckDlc=$dlc
    }
}
function ParseFixtureXaml($node) {
    $xaml=$node.OuterXml -replace '\{PluginSettings Plugin=(\w+), Path=', '{Binding Path=$1.'
    $xaml=$xaml.Replace('{Api Paths.ConfigurationPath,','{Binding Path=ConfigurationPath,')
    $xaml=$xaml -replace 'Tag="\{PluginStatus[^"}]+\}"','Tag="False"' -replace 'Visibility="\{PluginStatus[^"}]+\}"','Visibility="Collapsed"'
    $xaml=[regex]::Replace($xaml,"\{ThemeFile '([^']+)'\}", { param($themeMatch) ([Uri]::new((Join-Path $source $themeMatch.Groups[1].Value))).AbsoluteUri })
    return [Windows.Markup.XamlReader]::Parse('<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">'+$xaml+'</Grid>')
}
function AddNativeFixture($view,$name,$text) {
    $control=$view.FindName($name)
    if ($null -eq $control) { return }
    $label=[Windows.Controls.TextBlock]::new(); $label.Text=$text
    $label.Foreground=$app.FindResource('TextBrush')
    $control.Content=$label
}
function Descendants($element) {
    foreach($child in [Windows.LogicalTreeHelper]::GetChildren($element)) {
        if($child -is [Windows.DependencyObject]) { $child; Descendants $child }
    }
}
function AssertRowAlignment($view,$keys,$context) {
    $summary=$view.FindName('DuneSummary')
    $visible=@($keys | ForEach-Object { $view.FindName('Dune'+$_+'Card') } | Where-Object Visibility -eq 'Visible')
    foreach($row in @($visible | Group-Object { [Math]::Round($_.TranslatePoint([Windows.Point]::new(0,0),$summary).Y,1) })) {
        $headingY=$null; $valueY=$null; $bottomY=$null
        $naturalHeight=($row.Group | ForEach-Object { $_.DesiredSize.Height-$_.Margin.Top-$_.Margin.Bottom } | Measure-Object -Maximum).Maximum
        $catalog=$view.FindName('DuneCatalogSummary')
        if($catalog -and [Math]::Abs($catalog.TranslatePoint([Windows.Point]::new(0,0),$summary).Y-[double]$row.Name) -lt 0.1) { $naturalHeight=[Math]::Max($naturalHeight,$catalog.DesiredSize.Height-12) }

        foreach($card in $row.Group) {
            $key=$card.Name -replace '^Dune','' -replace 'Card$',''
            $currentHeadingY=$view.FindName('Dune'+$key+'Heading').TranslatePoint([Windows.Point]::new(0,0),$summary).Y
            $currentValueY=$view.FindName('Dune'+$key+'Value').TranslatePoint([Windows.Point]::new(0,0),$summary).Y
            $currentBottomY=$card.TranslatePoint([Windows.Point]::new(0,0),$summary).Y+$card.ActualHeight
            if($null -ne $headingY -and ([Math]::Abs($currentHeadingY-$headingY) -gt 0.1 -or [Math]::Abs($currentValueY-$valueY) -gt 0.1 -or [Math]::Abs($currentBottomY-$bottomY) -gt 0.1)) { throw "Misaligned heading, value or bottom: $context / $($card.Name)" }
            if([Math]::Abs($card.ActualHeight-$naturalHeight) -gt 0.1) { throw "Row does not fit its content: $context / $($card.Name) / actual=$($card.ActualHeight) / natural=$naturalHeight" }
            $headingY=$currentHeadingY; $valueY=$currentValueY; $bottomY=$currentBottomY
        }
    }
    AssertSourceFooters $view $context
}
function AssertProviderSymbol($control,$context) {
    [void]$control.ApplyTemplate()
    $image=$control.Template.FindName('PluginImage',$control)
    $glyph=$control.Template.FindName('FallbackGlyph',$control)
    if($null -eq $image -or $null -eq $glyph) { throw "Missing local-icon template: $context" }
    if($null -eq $image.Source) {
        if($glyph.Visibility -ne 'Visible' -or $image.Visibility -ne 'Collapsed') { throw "Missing Fluent fallback: $context" }
    } elseif($image.Source.PixelWidth -le 0 -or $glyph.Visibility -ne 'Collapsed') { throw "Invalid local icon: $context" }
}
function AssertSourceFooters($view,$context) {
    $summary=$view.FindName('DuneSummary')
    $achievement=$view.FindName('DuneAchievementsCard')
    $latest=$view.FindName('DuneLatestAchievementRow')
    foreach($key in @('HowLongToBeat','Activity')) {
        $card=$view.FindName('Dune'+$key+'Card')
        $footer=$view.FindName('Dune'+$key+'SourceFooter')
        $expected=$card.Visibility -eq 'Visible' -and $achievement.Visibility -eq 'Visible' -and $latest.Visibility -eq 'Visible' -and [Windows.Controls.Grid]::GetRow($card) -eq [Windows.Controls.Grid]::GetRow($achievement)
        if(($footer.Visibility -eq 'Visible') -ne $expected) { throw "Wrong source visibility: $context / $key" }
        $headingIcon=$view.FindName('Dune'+$key+'ProviderIcon')
        if(($headingIcon.Visibility -eq 'Visible') -eq $expected) { throw "Provider identity must appear exactly once: $context / $key" }
        if($expected) {
            $footerBottom=$footer.TranslatePoint([Windows.Point]::new(0,0),$summary).Y+$footer.ActualHeight
            $cardBottom=$card.TranslatePoint([Windows.Point]::new(0,0),$summary).Y+$card.ActualHeight
            if([Math]::Abs($cardBottom-$footerBottom-16) -gt 0.1) { throw "Source must anchor to the card bottom: $context / $key" }
            $footerTop=$footerBottom-$footer.ActualHeight
            $body=$view.FindName('Dune'+$key+'Value').Parent
            foreach($details in @($body.Children | Where-Object { $_ -is [Windows.Controls.StackPanel] -and $_.Visibility -eq 'Visible' -and [Windows.Controls.Grid]::GetRow($_) -eq 3 })) {
                $detailsBottom=$details.TranslatePoint([Windows.Point]::new(0,0),$summary).Y+$details.ActualHeight
                if($detailsBottom -gt $footerTop-8+0.1) { throw "Source overlaps supplementary details: $context / $key" }
            }
            $icon=@($footer.Children | Where-Object { $_ -is [Windows.Controls.ContentControl] })[0]
            AssertProviderSymbol $icon "$context / $key"
            if([Math]::Abs($icon.ActualWidth-16) -gt 0.1) { throw "Plugin source icon must remain 16 px: $context / $key" }
        }
    }
    foreach($key in @('HowLongToBeat','Activity','Requirements','Achievements')) {
        $card=$view.FindName('Dune'+$key+'Card')
        if($card.Visibility -ne 'Visible') { continue }
        $icon=$view.FindName('Dune'+$key+'ProviderIcon')
        $heading=$view.FindName('Dune'+$key+'Heading')
        $arrow=$view.FindName('Dune'+$key+'OpenChevron')
        if($icon -is [Windows.Controls.ContentControl]) { AssertProviderSymbol $icon "$context / $key" }
        if($arrow.Visibility -ne 'Visible') { throw "Opener must retain its chevron: $context / $key" }
        if($icon.Visibility -eq 'Visible') {
            if([Math]::Abs($icon.ActualWidth-16) -gt 0.1 -or [Math]::Abs($icon.ActualHeight-16) -gt 0.1) { throw "Provider icon must remain 16 px: $context / $key" }
            $right=$arrow.TranslatePoint([Windows.Point]::new(0,0),$heading).X+$arrow.ActualWidth
            if($right -gt $heading.ActualWidth+0.1) { throw "Opener overflows its heading: $context / $key" }
        }
        if($card.Cursor -ne [Windows.Input.Cursors]::Hand) { throw "Plugin opener must use the hand cursor: $context / $key" }
    }
}
function AssertCombinedSummary($view,$width,$context) {
    $summary=$view.FindName('DuneSummary')
    $cards=@(@('PlayTime','LastPlayed','Completion','Requirements','HowLongToBeat','Activity','Achievements') | ForEach-Object { $view.FindName('Dune'+$_+'Card') } | Where-Object Visibility -eq 'Visible')
    $cards+=@(@('Languages','Dlc') | ForEach-Object { $view.FindName('Dune'+$_+'Summary') } | Where-Object Visibility -eq 'Visible')
    $rects=@($cards | ForEach-Object {
        $origin=$_.TranslatePoint([Windows.Point]::new(0,0),$summary)
        [pscustomobject]@{ Name=$_.Name; Left=$origin.X; Top=$origin.Y; Right=$origin.X+$_.ActualWidth; Bottom=$origin.Y+$_.ActualHeight }
    })
    foreach($rect in $rects) {
        if($rect.Left -lt -0.1 -or $rect.Top -lt -0.1 -or $rect.Right -gt $summary.ActualWidth+0.1 -or $rect.Bottom -gt $summary.ActualHeight+0.1 -or $rect.Right -le $rect.Left) { throw "Summary card overflow: $context / $($rect.Name)" }
    }
    for($a=0;$a -lt $rects.Count;$a++) {
        for($b=$a+1;$b -lt $rects.Count;$b++) {
            if([Math]::Min($rects[$a].Right,$rects[$b].Right)-[Math]::Max($rects[$a].Left,$rects[$b].Left) -gt 0.1 -and [Math]::Min($rects[$a].Bottom,$rects[$b].Bottom)-[Math]::Max($rects[$a].Top,$rects[$b].Top) -gt 0.1) { throw "Summary overlap: $context / $($rects[$a].Name) / $($rects[$b].Name)" }
        }
    }
    $rows=@($rects | Group-Object { [Math]::Round($_.Top,1) } | Sort-Object { [double]$_.Name })
    foreach($row in $rows) {
        $bottoms=$row.Group | Measure-Object Bottom -Minimum -Maximum
        if($bottoms.Maximum-$bottoms.Minimum -gt 0.1) { throw "Combined card bottoms must align: $context" }
        $columns=@($row.Group | Sort-Object Left)
        if([Math]::Abs($columns[0].Left) -gt 0.1 -or [Math]::Abs($columns[-1].Right-($summary.ActualWidth-12)) -gt 0.1) { throw "Summary row must fill its available width: $context" }
        for($i=1;$i -lt $columns.Count;$i++) {
            if([Math]::Abs($columns[$i].Left-$columns[$i-1].Right-12) -gt 0.1) { throw "Uneven card spacing: $context" }
        }
    }
    for($i=1;$i -lt $rows.Count;$i++) {
        if([Math]::Abs([double]$rows[$i].Name-($rows[$i-1].Group | Measure-Object Bottom -Maximum).Maximum-12) -gt 0.1) { throw "Uneven row spacing: $context" }
    }
    if($summary.ActualWidth -ge 1320 -and $rows.Count -gt 2) { throw "Wide summary must fit in two rows: $context" }
    foreach($kind in @('Languages','Dlc')) {
        $card=$view.FindName('Dune'+$kind+'Summary')
        if($card.Visibility -ne 'Visible') { continue }
        $footer=$view.FindName('Dune'+$kind+'SourceFooter')
        $achievement=$view.FindName('DuneAchievementsCard')
        $expected=$summary.ActualWidth -ge 1320 -and $achievement.Visibility -eq 'Visible' -and $view.FindName('DuneLatestAchievementRow').Visibility -eq 'Visible'
        if(($footer.Visibility -eq 'Visible') -ne $expected) { throw "Wrong catalogue source visibility: $context / $kind" }
        if(($view.FindName('Dune'+$kind+'CatalogProviderIcon').Visibility -eq 'Visible') -eq $expected) { throw "Duplicate catalogue identity: $context / $kind" }
        if($expected -and [Math]::Abs($card.ActualHeight-($footer.TranslatePoint([Windows.Point]::new(0,0),$card).Y+$footer.ActualHeight)-16) -gt 0.1) { throw "Catalogue source must anchor to bottom: $context / $kind" }
    }
}
function VisualDescendants($element) {
    for($index=0; $index -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($element); $index++) {
        $child=[Windows.Media.VisualTreeHelper]::GetChild($element,$index)
        $child; VisualDescendants $child
    }
}
function AddCompletionFixture($view) {
    $hostControl=$view.FindName('ThemeExtras_SettableCompletionStatus')
    $control=[Windows.Controls.UserControl]::new()
    $hostControl.Content=$control
    $control.Style=$hostControl.FindResource([Windows.Controls.UserControl])
    $options=@([pscustomobject]@{Name='Playing'},[pscustomobject]@{Name='Completed'})
    $control.DataContext=[pscustomobject]@{CompletionStatusOptions=$options; Value=$options[0]}
    $hostControl.Visibility='Visible'
    @($view.FindName('DuneCompletionValue').Children[0].Children | Where-Object { $_ -is [Windows.Controls.Border] })[0].Visibility='Collapsed'
    return $control
}
function AssertCompletionTypography($view,$width) {
    $control=AddCompletionFixture $view
    Pump $view $width
    $combo=@(VisualDescendants $control | Where-Object { $_ -is [Windows.Controls.ComboBox] })[0]
    $selected=@(VisualDescendants $combo | Where-Object { $_ -is [Windows.Controls.TextBlock] -and $_.Text -eq 'Playing' })[0]
    $primary=[Windows.Controls.TextBlock]::new(); $primary.Style=$app.FindResource('DuneSummaryPrimaryText')
    if($null -eq $selected -or $selected.FontSize -ne $primary.FontSize -or $selected.FontWeight -ne $primary.FontWeight -or $selected.FontFamily.Source -ne $primary.FontFamily.Source) { throw 'Completion selection must match primary typography.' }
    if($selected.DesiredSize.Height -gt $combo.ActualHeight+0.1) { throw 'Completion selection must not clip its text.' }
    $combo.IsDropDownOpen=$true
    Pump $view $width
    $item=$combo.ItemContainerGenerator.ContainerFromIndex(0)
    $item.ApplyTemplate() | Out-Null
    $option=@(VisualDescendants $item | Where-Object { $_ -is [Windows.Controls.TextBlock] -and $_.Text -eq 'Playing' })[0]
    if($null -eq $option -or $item.ActualHeight -lt $option.DesiredSize.Height) { throw 'Completion popup options must remain visible without clipping.' }
    $combo.IsDropDownOpen=$false
    Pump $view $width
}
function AssertCatalog($node,$context) {
    $catalog=ParseFixtureXaml $node
    $testWindow.Content=$catalog
    foreach($mask in 0..3) {
        $data=Fixture 15
        $data.CheckLocalizations.HasData=($mask -band 1) -ne 0
        $data.CheckDlc.HasData=($mask -band 2) -ne 0
        $catalog.DataContext=$data
        foreach($width in @(400,600,900)) {
            Pump $catalog $width
            $languageCard=$catalog.FindName('DuneLanguagesSummary'); $dlcCard=$catalog.FindName('DuneDlcSummary')
            foreach($entry in @(@('Languages',1),@('Dlc',2))) {
                $card=$catalog.FindName('Dune'+$entry[0]+'Summary')
                if(($card.Visibility -eq 'Visible') -ne (($mask -band $entry[1]) -ne 0)) { throw "Catalog availability: $context / $mask / $width" }
                if($card.Visibility -eq 'Visible' -and $card.ActualWidth -gt $width+0.1) { throw "Catalog card overflow: $context / $width" }
            }
            if($mask -eq 0 -and $catalog.DesiredSize.Height -ne 0) { throw 'Missing catalog data must reserve no height.' }
            if($mask -in @(1,2)) {
                $only=if($mask -eq 1){$languageCard}else{$dlcCard}
                if([Windows.Controls.Grid]::GetRow($only) -ne 0 -or [Windows.Controls.Grid]::GetColumnSpan($only) -ne 2) { throw 'A lone catalog card must fill the row.' }
            }
            if($mask -eq 3 -and $width -ge 600) {
                if([Windows.Controls.Grid]::GetRow($dlcCard) -ne 0 -or [Windows.Controls.Grid]::GetColumn($dlcCard) -ne 1 -or [Windows.Controls.Grid]::GetColumnSpan($languageCard) -ne 1) { throw 'Wide catalog cards must share the row.' }
                $languageValue=$catalog.FindName('DuneLanguagesCatalogValue').TranslatePoint([Windows.Point]::new(0,0),$catalog).Y
                $dlcValue=$catalog.FindName('DuneDlcCatalogValue').TranslatePoint([Windows.Point]::new(0,0),$catalog).Y
                if([Math]::Abs($languageValue-$dlcValue) -gt 0.1 -or [Math]::Abs($languageCard.ActualHeight-$dlcCard.ActualHeight) -gt 0.1) { throw 'Catalog primary origins and bottoms must align.' }
            }
            if($mask -eq 3 -and $width -lt 600 -and [Windows.Controls.Grid]::GetRow($dlcCard) -ne 1) { throw 'Narrow catalog cards must stack.' }
        }
    }
    $data=Fixture 15; $catalog.DataContext=$data
    Pump $catalog 900
    $counts=$catalog.FindName('DuneDlcOwnershipCounts')
    $groups=@($counts.Items)
    if($groups.Count -ne 2 -or @($groups | Where-Object Name -eq $true)[0].ItemCount -ne 3 -or @($groups | Where-Object Name -eq $false)[0].ItemCount -ne 5) { throw 'DLC ownership groups must use the plugin flags.' }
    if($groups[0].Name -ne $true) { throw 'Owned counts must have a stable first position.' }
    $data.CheckDlc.ListDlcs.Add([SummaryDlc]@{IsOwned=$true}); Pump $catalog 900
    if($catalog.FindName('DuneDlcCount').Text -ne '9' -or @($counts.Items | Where-Object Name -eq $true)[0].ItemCount -ne 4) { throw 'DLC counts must follow live collection changes.' }
    if($catalog.FindName('DunePreferredLanguageStatus').Text -ne $app.FindResource('LOCDunePreferredLanguageListed')) { throw 'Preferred language must use the plugin result.' }
    $data=Fixture 15; $data.CheckLocalizations.HasNativeSupport=$false; $catalog.DataContext=$data; Pump $catalog 900
    if($catalog.FindName('DunePreferredLanguageStatus').Text -ne $app.FindResource('LOCDunePreferredLanguageMissing')) { throw 'Missing preferred language must have neutral wording.' }
    $catalog.DataContext=[pscustomobject]@{CheckDlc=$data.CheckDlc; CheckLocalizations=[pscustomobject]@{HasData=$true; ListNativeSupport=$data.CheckLocalizations.ListNativeSupport}}
    Pump $catalog 900
    if($catalog.FindName('DunePreferredLanguageStatus').Text -ne $app.FindResource('LOCDuneLanguageDetails')) { throw 'Unavailable preferred-language results must not imply lack of support.' }
    foreach($owned in @($true,$false)) {
        $data=Fixture 15; foreach($entry in $data.CheckDlc.ListDlcs) { $entry.IsOwned=$owned }
        $catalog.DataContext=$data; Pump $catalog 900
        if($counts.Items.Count -ne 1 -or $counts.Items[0].Name -ne $owned -or $counts.Items[0].ItemCount -ne 8) { throw 'All-owned and none-owned catalogs must retain accurate counts.' }
    }
    $catalog.DataContext=Fixture 15; Pump $catalog 900
    foreach($entry in @(@('Languages','CheckLocalizations','DuneShowLanguagesSummary'),@('Dlc','CheckDlc','DuneShowDlcSummary'))) {
        $opener=$catalog.FindName($entry[1]+'_PluginButton')
        if($opener.Visibility -ne 'Collapsed') { throw 'Empty catalog opener must collapse.' }
        AddNativeFixture $catalog ($entry[1]+'_PluginButton') 'Open'; Pump $catalog 900
        if($opener.Visibility -ne 'Visible' -or $catalog.FindName('Dune'+$entry[0]+'Summary').Cursor -ne [Windows.Input.Cursors]::Hand) { throw 'Native catalog opener must appear with its click hint.' }
        $app.Resources.MergedDictionaries[0][$entry[2]]=$false; Pump $catalog 900
        if($catalog.FindName('Dune'+$entry[0]+'Summary').Visibility -ne 'Collapsed') { throw 'Catalog preference failed.' }
        $app.Resources.MergedDictionaries[0][$entry[2]]=$true; Pump $catalog 900
        $opener.Content.Visibility='Collapsed'; Pump $catalog 900
        if($opener.Visibility -ne 'Collapsed' -or $catalog.FindName('Dune'+$entry[0]+'Summary').Visibility -ne 'Visible') { throw 'Disabling a native opener must preserve informational data.' }
        $icon=$catalog.FindName('Dune'+$entry[0]+'CatalogProviderIcon')
        if($icon -is [Windows.Controls.ContentControl]) { AssertProviderSymbol $icon "$context / catalog" }
    }
    $data=Fixture 15; $data.CheckLocalizations.ListNativeSupport.Clear(); $data.CheckDlc.ListDlcs.Clear(); $catalog.DataContext=$data; Pump $catalog 900
    if($catalog.DesiredSize.Height -ne 0) { throw 'Empty/older catalog APIs must not leave blank cards.' }
    $catalog.DataContext=[pscustomobject]@{}; Pump $catalog 900
    if($catalog.DesiredSize.Height -ne 0) { throw 'Absent catalog plugins must not leave blank cards.' }
    $testWindow.Content=$null
}
function AssertDlcLists($node,$context) {
    foreach($mask in 0..7) {
        $tabsXaml='<TabControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">'+($node.OuterXml -replace '\{PluginSettings Plugin=(\w+), Path=', '{Binding Path=$1.')+'</TabControl>'
        $tabs=[Windows.Markup.XamlReader]::Parse($tabsXaml); $tabs.DataContext=Fixture 15
        $testWindow.Content=$tabs
        foreach($index in 0..2) {
            $kind=@('All','Owned','NotOwned')[$index]
            if($mask -band (1 -shl $index)) {
                AddNativeFixture $tabs ('CheckDlc_PluginListDlc'+$kind) 'Native DLC list'
                $hostControl=$tabs.FindName('CheckDlc_PluginListDlc'+$kind)
                $hostControl.Content.DataContext=[pscustomobject]@{ItemsSource=[Collections.ObjectModel.ObservableCollection[string]]::new()}
            }
        }
        Pump $tabs 400
        if(($tabs.Items[0].Visibility -eq 'Visible') -ne ($mask -ne 0)) { throw "DLC outer tab availability: $context / $mask" }
        if($mask -ne 0) {
            $lists=$tabs.FindName('DuneDlcLists')
            if($null -eq $lists.SelectedItem -or $lists.SelectedItem.Visibility -ne 'Visible') { throw "DLC must select an enabled list: $context / $mask / selected=$($lists.SelectedIndex)" }
            $selectedKind=@('All','Owned','NotOwned')[$lists.SelectedIndex]
            $hostControl=$tabs.FindName('CheckDlc_PluginListDlc'+$selectedKind)
            if($hostControl.Height -ne $app.FindResource('DuneDlcPanelHeight') -or $hostControl.ActualHeight -gt $app.FindResource('DuneDlcPanelHeight')+0.1) { throw 'Native DLC list must have a finite viewport.' }
            $emptyText=@(VisualDescendants $lists | Where-Object { $_ -is [Windows.Controls.TextBlock] -and $_.Text -eq $app.FindResource('LOCDuneEmptyDlcCategory') })
            if(@($emptyText | Where-Object Visibility -eq 'Visible').Count -eq 0) { throw 'Empty DLC category must display its empty state.' }
            $hostControl.Content.DataContext.ItemsSource.Add('DLC entry'); Pump $tabs 400
            if(@($emptyText | Where-Object Visibility -eq 'Visible').Count -ne 0) { throw 'DLC empty state must disappear when data arrives.' }
            $hostControl.Content.Visibility='Collapsed'; Pump $tabs 400
            if($mask -in @(1,2,4) -and $tabs.Items[0].Visibility -ne 'Collapsed') { throw 'Disabling the last native list must hide the tab.' }
            if($mask -notin @(1,2,4) -and ($null -eq $lists.SelectedItem -or $lists.SelectedItem.Visibility -ne 'Visible')) { throw "DLC selection must recover when a native list is disabled: $context / $mask / selected=$($lists.SelectedIndex)" }
        }
    }
    $testWindow.Content=$null
}
function SavePreview($node,$width,$file,$mask=15) {
    $view=ParseFixtureXaml $node
    $view.DataContext=Fixture $mask
    $view.FindName('PART_TextPlayTime').Text='18 h 25 min'
    $view.FindName('PART_TextLastActivity').Text='2026/10/02'
    $view.FindName('PART_ButtonCompletionStatus').Content='Playing'
    [void](AddCompletionFixture $view)
    AddNativeFixture $view 'PlayniteAchievements_AchievementCompactLatest' ([char]0x2605)
    AddNativeFixture $view 'CheckDlc_PluginButton' 'Open'; AddNativeFixture $view 'CheckLocalizations_PluginButton' 'Open'
    $frame=[Windows.Controls.Border]::new()
    $frame.Background=$app.FindResource('WindowBackgroundBrush'); $frame.Padding=[Windows.Thickness]::new(24)
    $stack=[Windows.Controls.StackPanel]::new(); $stack.Children.Add($view) | Out-Null
    $frame.Child=$stack
    $testWindow.Content=$frame
    Pump $frame ($width+48)
    $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new([int][Math]::Ceiling($frame.ActualWidth),[int][Math]::Ceiling($frame.ActualHeight),96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($frame)
    $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream=[IO.File]::Create($file)
    try { $encoder.Save($stream) } finally { $stream.Dispose(); $testWindow.Content=$null; $frame.Child=$null }
}
try {
    # Host-provided resources used by the real theme templates.
    $app.Resources['ObjectToStringConverter']=[SummaryObjectToString]::new()
    $app.Resources['AccentIdleColor']=[Windows.Media.ColorConverter]::ConvertFromString('#799CE8')
    foreach ($relative in @('Constants.xaml','Common.xaml','DefaultControls/Border.xaml','DefaultControls/TabControl.xaml','DefaultControls/TextBlock.xaml','DefaultControls/ProgressBar.xaml','DefaultControls/ToolTip.xaml')) {
        $app.Resources.MergedDictionaries.Add([Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $source $relative))))
    }
    $app.Resources['BooleanToVisibilityConverter']=[Windows.Controls.BooleanToVisibilityConverter]::new()
    $app.Resources['InvertedBooleanToVisibilityConverter']=[SummaryInvertedVisibility]::new()
    # Only the host's CompletionStatus CLR type is substituted; keep the real selector templates.
    $comboXaml=[IO.File]::ReadAllText((Join-Path $source 'DefaultControls/ComboBox.xaml')).Replace(' DataType="{x:Type CompletionStatus}"','')
    $app.Resources.MergedDictionaries.Add([Windows.Markup.XamlReader]::Parse($comboXaml))
    $app.Resources.MergedDictionaries.Add([Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $source 'DerivedStyles/PropertyItemButton.xaml'))))
    $app.Resources.MergedDictionaries.Add([Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $source ('Localization/'+$Language+'.xaml')))))
    foreach($key in @('LOCTimePlayed','LOCLastPlayed','LOCCompletionStatus','LOCGameActivityTitle','LOCOpen')) {
        $app.Resources[$key]=$(if($Language -eq 'zh_CN'){@{ LOCTimePlayed='游玩时间'; LOCLastPlayed='最近游玩'; LOCCompletionStatus='完成状态'; LOCGameActivityTitle='活动记录'; LOCOpen='打开' }[$key]}else{@{ LOCTimePlayed='Play time'; LOCLastPlayed='Last played'; LOCCompletionStatus='Completion'; LOCGameActivityTitle='Activity'; LOCOpen='Open' }[$key]})
    }
    $optional=@('HowLongToBeat','Activity','Requirements','Achievements')
    $keys=@('PlayTime','HowLongToBeat','LastPlayed','Completion','Activity','Requirements','Achievements')
    # Render before repeated binding changes, which can leave stale CJK/Latin glyph
    # runs in offscreen WPF bitmaps even when layout measurements remain correct.
    if($PreviewDirectory) {
        [void][IO.Directory]::CreateDirectory($PreviewDirectory)
        $testWindow.Show()
        foreach($previewFile in @('DetailsViewGameOverview.xaml','GridViewGameOverview.xaml')) {
            $previewDoc=[Xml.XmlDocument]::new(); $previewDoc.Load((Join-Path $source ('Views/'+$previewFile)))
            $previewNs=[Xml.XmlNamespaceManager]::new($previewDoc.NameTable); $previewNs.AddNamespace('p','http://schemas.microsoft.com/winfx/2006/xaml/presentation'); $previewNs.AddNamespace('x','http://schemas.microsoft.com/winfx/2006/xaml')
            $previewNode=$previewDoc.SelectSingleNode('//p:Grid[@x:Name="DuneSummary"]',$previewNs)
            if($previewFile.StartsWith('Details')) {
                SavePreview $previewNode 1320 (Join-Path $PreviewDirectory "Details Summary Wide.png") 15
                SavePreview $previewNode 1320 (Join-Path $PreviewDirectory "Details Summary Plugins.png") 7
                SavePreview $previewNode 1320 (Join-Path $PreviewDirectory "Details Summary Basic.png") 0
            } else {
                SavePreview $previewNode 640 (Join-Path $PreviewDirectory "Grid Summary Compact.png") 15
            }
        }
        if($PreviewOnly) { Write-Output "Rendered fresh summary previews ($Language)."; return }
    }
    foreach($catalogFile in @('DetailsViewGameOverview.xaml','GridViewGameOverview.xaml')) {
        $catalogDoc=[Xml.XmlDocument]::new(); $catalogDoc.Load((Join-Path $source ('Views/'+$catalogFile)))
        $catalogNs=[Xml.XmlNamespaceManager]::new($catalogDoc.NameTable); $catalogNs.AddNamespace('p','http://schemas.microsoft.com/winfx/2006/xaml/presentation'); $catalogNs.AddNamespace('x','http://schemas.microsoft.com/winfx/2006/xaml')
        AssertCatalog ($catalogDoc.SelectSingleNode('//p:Grid[@x:Name="DuneCatalogSummary"]',$catalogNs)) $catalogFile
        AssertDlcLists ($catalogDoc.SelectSingleNode('//p:TabItem[@Tag="{DynamicResource DuneShowDlcTab}"]',$catalogNs)) $catalogFile
    }
    if($CatalogOnly) { Write-Output "PASS ($Language): catalog rows, plugin counts, preferred-language results, native openers, preferences, absent/older APIs."; return }
    $cases=0
    foreach($file in @('DetailsViewGameOverview.xaml','GridViewGameOverview.xaml')) {
        $doc=[Xml.XmlDocument]::new(); $doc.Load((Join-Path $source ('Views/'+$file)))
        $ns=[Xml.XmlNamespaceManager]::new($doc.NameTable); $ns.AddNamespace('p','http://schemas.microsoft.com/winfx/2006/xaml/presentation'); $ns.AddNamespace('x','http://schemas.microsoft.com/winfx/2006/xaml')
        $names=@($doc.SelectNodes('//*[@x:Name or @Name]',$ns) | ForEach-Object { if($_.HasAttribute('Name')) {$_.GetAttribute('Name')} else {$_.GetAttribute('Name','http://schemas.microsoft.com/winfx/2006/xaml')} })
        $duplicates=@($names | Group-Object | Where-Object Count -gt 1)
        if($duplicates.Count) { throw "Duplicate control names in ${file}: $($duplicates.Name -join ', ')" }
        $node=$doc.SelectSingleNode('//p:Grid[@x:Name="DuneSummary"]',$ns)
        $view=ParseFixtureXaml $node
        $testWindow.Content=$view
        if(-not $testWindow.IsVisible) { $testWindow.Show() }
        foreach($entry in @{PART_TextPlayTime='18 h 25 min'; PART_TextLastActivity='2026/10/02'}.GetEnumerator()) { $view.FindName($entry.Key).Text=$entry.Value }
        $view.FindName('PART_ButtonCompletionStatus').Content='Playing'
        AssertCompletionTypography $view 1320
        AddNativeFixture $view 'PlayniteAchievements_AchievementCompactLatest' ([char]0x2605)
        AddNativeFixture $view 'CheckDlc_PluginButton' 'Open'; AddNativeFixture $view 'CheckLocalizations_PluginButton' 'Open'
        # One matrix owns geometry and alignment checks for all six optional cards.
        foreach($combinedMask in 0..63) {
            $data=Fixture ($combinedMask -band 15)
            $data.CheckLocalizations.HasData=($combinedMask -band 16) -ne 0
            $data.CheckDlc.HasData=($combinedMask -band 32) -ne 0
            $view.DataContext=$data
            foreach($width in @(400,600,640,900,1320,1760)) {
                Pump $view $width
                $context="$file / combined=$combinedMask / $width"
                AssertCombinedSummary $view $width $context
                AssertRowAlignment $view $keys $context
                for($bit=0; $bit -lt 4; $bit++) {
                    $card=$view.FindName('Dune'+$optional[$bit]+'Card')
                    if(($card.Visibility -eq 'Visible') -ne (($combinedMask -band (1 -shl $bit)) -ne 0)) { throw "Wrong plugin visibility: $context / $($optional[$bit])" }
                }
                $summary=$view.FindName('DuneSummary')
                $achievement=$view.FindName('DuneAchievementsCard')
                if($width -lt 1320 -and $achievement.Visibility -eq 'Visible') {
                    $top=$achievement.TranslatePoint([Windows.Point]::new(0,0),$summary).Y
                    $neighbors=@($keys | ForEach-Object { $view.FindName('Dune'+$_+'Card') } | Where-Object { $_.Visibility -eq 'Visible' -and [Math]::Abs($_.TranslatePoint([Windows.Point]::new(0,0),$summary).Y-$top) -lt 0.1 })
                    if($neighbors.Count -ne 1) { throw "Achievements must occupy their own row in regular panes: $context" }
                }
                $cases++
            }
        }
        # Native detail settings may hide any baseline field; restore the same view.
        $baseline=@('PlayTime','LastPlayed','Completion')
        $baselineLabels=@('PART_ElemPlayTime','PART_ElemLastPlayed','PART_ElemCompletionStatus')
        foreach($pluginMask in @(0,4,63)) {
            $data=Fixture ($pluginMask -band 15)
            $data.CheckLocalizations.HasData=($pluginMask -band 16) -ne 0
            $data.CheckDlc.HasData=($pluginMask -band 32) -ne 0
            $view.DataContext=$data
            foreach($basicMask in @(0..7)+@(0,7)) {
                for($bit=0; $bit -lt $baseline.Count; $bit++) {
                    $view.FindName($baselineLabels[$bit]).Visibility=$(if($basicMask -band (1 -shl $bit)){'Visible'}else{'Collapsed'})
                }
                foreach($width in @(400,900,1320)) {
                    Pump $view $width
                    $context="$file / basic=$basicMask / plugins=$pluginMask / $width"
                    AssertCombinedSummary $view $width $context
                    AssertRowAlignment $view $keys $context
                    for($bit=0; $bit -lt $baseline.Count; $bit++) {
                        if(($view.FindName('Dune'+$baseline[$bit]+'Card').Visibility -eq 'Visible') -ne [bool]($basicMask -band (1 -shl $bit))) { throw "Wrong native field visibility: $context" }
                    }
                    if($basicMask -eq 0 -and $pluginMask -eq 0 -and $view.FindName('DuneSummary').DesiredSize.Height -ne 0) { throw 'An entirely hidden summary must reserve no height.' }
                }
            }
        }
        # Missing estimates, date, recent activity and latest unlock affect the whole row.
        foreach($footerMask in 0..31) {
            $data=Fixture 15
            if(-not ($footerMask -band 1)) { $data.HowLongToBeat.MainExtra=0L }
            if(-not ($footerMask -band 2)) { $data.HowLongToBeat.Completionist=0L }
            if(-not ($footerMask -band 4)) { $data.GameActivity.LastDateSession='' }
            if(-not ($footerMask -band 8)) { $data.GameActivity.RecentActivity='' }
            $data.PlayniteAchievements.ModernTheme.HasLatestAchievementData=($footerMask -band 16) -ne 0
            $view.DataContext=$data
            foreach($width in @(640,900,1320)) {
                Pump $view $width
                AssertRowAlignment $view $keys "$file / footer=$footerMask / $width"
                $cases++
            }
        }
        # Resource changes must collapse cards and repack without recreating the view.
        $view.DataContext=Fixture 15
        Pump $view 640
        $progress=@(Descendants $view | Where-Object { $_ -is [Windows.Controls.ProgressBar] })[0]
        $indicator=$progress.Template.FindName('PART_Indicator',$progress)
        if([Math]::Abs($indicator.ActualWidth/$progress.ActualWidth-0.3) -gt 0.01) { throw "Achievement progress does not reflect the unlocked fraction (value=$($progress.Value), max=$($progress.Maximum), indicator=$($indicator.ActualWidth), width=$($progress.ActualWidth), track=$($progress.Template.FindName('PART_Track',$progress).ActualWidth))." }
        foreach($key in $optional) {
            $resource='DuneShow'+$key+'Card'
            $app.Resources.MergedDictionaries[0][$resource]=$false; Pump $view 640
            if($view.FindName('Dune'+$key+'Card').Visibility -ne 'Collapsed') { throw "Preference failed: $resource (resource=$($app.Resources[$resource]), tag=$($view.FindName($resource+'Preference').Tag))" }
            AssertRowAlignment $view $keys "$file / hidden $key"
            $app.Resources.MergedDictionaries[0][$resource]=$true; Pump $view 640
            if($view.FindName('Dune'+$key+'Card').Visibility -ne 'Visible') { throw "Preference failed to restore: $resource" }
        }
        # Resolve the live latest row through its compact control, which is a direct child.
        $latest=$view.FindName('PlayniteAchievements_AchievementCompactLatest').Parent
        $expandedAchievementHeight=$view.FindName('DuneAchievementsCard').ActualHeight
        $app.Resources.MergedDictionaries[0]['DuneShowLatestAchievement']=$false; Pump $view 640
        if($latest.Visibility -ne 'Collapsed') { throw 'Latest achievement option failed.' }
        if($view.FindName('DuneAchievementsCard').ActualHeight -ge $expandedAchievementHeight-1) { throw 'Hiding the latest unlock must shrink its card.' }
        Pump $view 1320
        AssertRowAlignment $view $keys "$file / latest unlock hidden"
        $app.Resources.MergedDictionaries[0]['DuneShowLatestAchievement']=$true
        Pump $view 1320
        AssertRowAlignment $view $keys "$file / latest unlock restored"
        $app.Resources.MergedDictionaries[0]['DuneSummaryDetailed']=$false; Pump $view 1320
        if($latest.Visibility -ne 'Collapsed') { throw 'Compact mode must hide latest achievement details.' }
        AssertRowAlignment $view $keys "$file / compact density"
        $app.Resources.MergedDictionaries[0]['DuneSummaryDetailed']=$true
        $extras=@(Descendants $view | Where-Object { $_ -is [Windows.FrameworkElement] -and $_.Style -eq $app.FindResource('DuneWideSummaryDetails') })[0]
        Pump $view 640
        if($extras.Visibility -ne 'Collapsed') { throw "Narrow panes must keep HLTB secondary times compact (view=$($view.ActualWidth), breakpoint=$($view.FindName('DuneSummaryDetailBreakpoint').ActualWidth), extras=$($extras.Visibility))." }
        $recent=@(Descendants $view | Where-Object { $_ -is [Windows.FrameworkElement] -and $_.Style -eq $app.FindResource('DuneRecentActivityText') })[0]
        if($recent.Visibility -ne 'Collapsed') { throw 'Narrow panes must keep recent activity in the detailed content.' }
        Pump $view 900
        if($extras.Visibility -ne 'Visible') { throw 'Wide panes must show HLTB secondary times.' }
        if($recent.Visibility -ne 'Visible') { throw 'Wide panes must retain recent activity in detailed mode.' }
        $zeroData=Fixture 15; $zeroData.HowLongToBeat.MainExtra=0L; $zeroData.HowLongToBeat.Completionist=0L
        $view.DataContext=$zeroData; Pump $view 900
        foreach($row in $extras.Children) { if($row.Visibility -ne 'Collapsed') { throw 'Missing HLTB estimates must not display zero placeholders.' } }
        if($extras.Visibility -ne 'Collapsed') { throw 'Missing extra estimates must remove the footer and its spacing.' }
        AssertRowAlignment $view $keys "$file / missing HLTB details"
        $emptyActivity=Fixture 15; $emptyActivity.HowLongToBeat.MainExtra=0L; $emptyActivity.HowLongToBeat.Completionist=0L; $emptyActivity.GameActivity.LastDateSession=''; $emptyActivity.GameActivity.RecentActivity=''
        $view.DataContext=$emptyActivity; Pump $view 900
        if([Math]::Abs($view.FindName('DuneActivityCard').ActualHeight-96) -gt 0.1 -or [Math]::Abs($view.FindName('DuneHowLongToBeatCard').ActualHeight-96) -gt 0.1) { throw 'Removing both footers must shrink their entire row.' }
        AssertRowAlignment $view $keys "$file / empty rich row"
        $view.DataContext=Fixture 15
        $view.FindName('PART_TextPlayTime').Text='123456 hours of play time with a longer localized value'
        Pump $view 600
        if($view.FindName('DunePlayTimeCard').ActualHeight -le 96 -or [Math]::Abs($view.FindName('DuneCompletionCard').ActualHeight-96) -gt 0.1) { throw 'A wrapped value must grow only its own row.' }
        AssertRowAlignment $view $keys "$file / wrapped basic value"
        $view.FindName('PART_TextPlayTime').Text='18 h 25 min'
        $unknown=Fixture 15; $unknown.SystemChecker.IsMinimumOK=$false; $unknown.SystemChecker.IsRecommendedOK=$false
        $view.DataContext=$unknown; Pump $view 640
        $requirementsText=@(Descendants $view.FindName('DuneRequirementsCard') | Where-Object { $_ -is [Windows.Controls.TextBlock] } | ForEach-Object Text)
        if($requirementsText -notcontains $app.FindResource('LOCDuneRequirementsReady')) { throw 'Unknown requirements must remain a lookup entry.' }
        foreach($status in @(
            @{ HasData=$false; Minimum=$true; Recommended=$true; State='Lookup'; Glyph=0xE721; Label='LOCOpen' },
            @{ HasData=$true; Minimum=$false; Recommended=$false; State='Ready'; Glyph=0xE946; Label='LOCDuneRequirementsReady' },
            @{ HasData=$true; Minimum=$true; Recommended=$false; State='Minimum'; Glyph=0xE73E; Label='LOCDuneMinimumMet' },
            @{ HasData=$true; Minimum=$false; Recommended=$true; State='Recommended'; Glyph=0xE930; Label='LOCDuneRecommendedMet' },
            @{ HasData=$true; Minimum=$true; Recommended=$true; State='Recommended'; Glyph=0xE930; Label='LOCDuneRecommendedMet' }
        )) {
            $data=Fixture 15; $data.SystemChecker.HasData=$status.HasData; $data.SystemChecker.IsMinimumOK=$status.Minimum; $data.SystemChecker.IsRecommendedOK=$status.Recommended
            $view.DataContext=$data; Pump $view 640
            if($view.FindName('DuneRequirementsValue').Tag -ne $status.State -or $view.FindName('DuneRequirementsStatusGlyph').Text -ne [string][char]$status.Glyph -or $view.FindName('DuneRequirementsStatusLabel').Text -ne $app.FindResource($status.Label)) { throw "Requirements icon/label mismatch: $file / $($status.State)" }
            AssertRowAlignment $view $keys "$file / requirements $($status.State)"
        }
        $locked=Fixture 15; $locked.PlayniteAchievements.ModernTheme.UnlockedCount=0; $locked.PlayniteAchievements.ModernTheme.HasLatestAchievementData=$false
        $view.DataContext=$locked; Pump $view 640
        if($view.FindName('DuneAchievementsCard').Visibility -ne 'Visible' -or $progress.Value -ne 0 -or $latest.Visibility -ne 'Collapsed') { throw 'A game with zero unlocked achievements must keep its summary and hide latest details.' }
        $oldData=Fixture 15; $oldData.PlayniteAchievements.ModernTheme=[pscustomobject]@{ HasAchievements=$true; UnlockedCount=12; AchievementCount=40 }
        $view.DataContext=$oldData; Pump $view 640
        if($latest.Visibility -ne 'Collapsed') { throw 'Older achievement API must hide the latest row.' }
        # An old/absent extension exposes no object. All optional summaries must collapse.
        $view.DataContext=[pscustomobject]@{}; Pump $view 640
        foreach($key in $optional) { if($view.FindName('Dune'+$key+'Card').Visibility -ne 'Collapsed') { throw 'Missing extension did not collapse.' } }
        $view.DataContext=Fixture 15
        # New tabs must have no empty entry when their extension is absent or disabled.
        foreach($plugin in @('CheckLocalizations','CheckDlc')) {
            $tabNode=$doc.SelectSingleNode('//p:TabItem[@Tag="{DynamicResource DuneShow'+$(if($plugin -eq 'CheckDlc'){'Dlc'}else{'Languages'})+'Tab}"]',$ns)
            $tabsXaml='<TabControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">'+($tabNode.OuterXml -replace '\{PluginSettings Plugin=(\w+), Path=', '{Binding Path=$1.')+'</TabControl>'
            $tabs=[Windows.Markup.XamlReader]::Parse($tabsXaml); $tabs.DataContext=Fixture 15
            $testWindow.Content=$tabs; Pump $tabs 640
            $tab=$tabs.Items[0]
            if($tab.Visibility -ne 'Collapsed') { throw "Empty $plugin tab must collapse." }
            $hostName=if($plugin -eq 'CheckDlc'){'CheckDlc_PluginListDlcAll'}else{'CheckLocalizations_PluginListLanguages'}
            AddNativeFixture $tabs $hostName 'Native list fixture'; Pump $tabs 640
            if($tab.Visibility -ne 'Visible') { throw "$plugin tab failed to appear with native content." }
            if($plugin -eq 'CheckLocalizations') {
                Pump $tabs 400
                $languageScroll=@(VisualDescendants $tabs | Where-Object { $_ -is [Windows.Controls.ScrollViewer] -and $_.HorizontalScrollBarVisibility -eq 'Auto' })[0]
                if($null -eq $languageScroll -or $languageScroll.ScrollableWidth -le 0 -or $languageScroll.ExtentWidth -lt 600) { throw 'Language columns must remain reachable through horizontal scrolling.' }
                Pump $tabs 640
            }
            $preference=if($plugin -eq 'CheckDlc'){'DuneShowDlcTab'}else{'DuneShowLanguagesTab'}
            $app.Resources.MergedDictionaries[0][$preference]=$false; Pump $tabs 640
            if($tab.Visibility -ne 'Collapsed') { throw "$plugin tab preference failed." }
            $app.Resources.MergedDictionaries[0][$preference]=$true; Pump $tabs 640
            if($tab.Visibility -ne 'Visible') { throw "$plugin tab preference failed to restore." }
            $tabs.FindName($hostName).Content.Visibility='Collapsed'; Pump $tabs 640
            if($tab.Visibility -ne 'Collapsed') { throw "$plugin tab remained after native control was disabled." }
            $tabs.DataContext=[pscustomobject]@{}; $tabs.FindName($hostName).Content.Visibility='Visible'; Pump $tabs 640
            if($tab.Visibility -ne 'Collapsed') { throw "$plugin tab must hide with missing game data." }
        }
    }
    Write-Output "PASS ($Language): $cases summary combinations, native field hiding/restoration, nine-card packing/spacing, adaptive row heights, heading/value/bottom alignment, conditional provider/source icons, completion selection and popup typography, requirements glyphs, bounds, overlap, density, live preferences, missing extensions, and native tab visibility."
} finally { $app.Shutdown() }
