# Exercise the real responsive hero shell and tab templates without plugin DLLs.
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$source=Join-Path $PSScriptRoot '..\Source'
$app=[Windows.Application]::new()
$app.ShutdownMode='OnExplicitShutdown'
function Pump($view,$width) {
    foreach($pass in 0..4) {
        [void]$view.Dispatcher.Invoke([Windows.Threading.DispatcherPriority]::DataBind,[Action]{})
        $view.Measure([Windows.Size]::new($width,300))
        $view.Arrange([Windows.Rect]::new(0,0,$width,300)); $view.UpdateLayout()
    }
}
function AssertMediaOptions {
    $window=[Windows.Window]::new()
    $window.Left=-32000; $window.Top=-32000; $window.Width=800; $window.Height=400
    $window.ShowInTaskbar=$false; $window.ShowActivated=$false
    try {
        foreach($file in @('DetailsViewGameOverview.xaml','GridViewGameOverview.xaml')) {
            $doc=[Xml.XmlDocument]::new(); $doc.Load((Join-Path $source ('Views/'+$file)))
            $ns=[Xml.XmlNamespaceManager]::new($doc.NameTable)
            $ns.AddNamespace('p','http://schemas.microsoft.com/winfx/2006/xaml/presentation'); $ns.AddNamespace('x','http://schemas.microsoft.com/winfx/2006/xaml')
            $gridView=$file -like 'Grid*'
            $preference=if($gridView){'GridViewAllowUseOfLogos'}else{'DetailsViewAllowUseOfLogos'}
            $logo=$doc.SelectSingleNode('//p:Border[@x:Name="GameIcon"]',$ns).ParentNode.OuterXml
            $logo=$logo -replace '\{PluginSettings Plugin=(\w+), Path=', '{Binding Path=$1.'
            $template=[Windows.Markup.XamlReader]::Parse('<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Control">'+$logo+'</ControlTemplate>')
            $view=[Windows.Controls.Control]::new(); $view.Template=$template; $window.Content=$view
            if(-not $window.IsVisible){$window.Show()}; [void]$view.ApplyTemplate()
            foreach($mask in 0..3) {
                $view.DataContext=[pscustomobject]@{ExtraMetadataLoader=[pscustomobject]@{EnableLogos=[bool]($mask -band 1);IsLogoAvailable=[bool]($mask -band 2)}}
                foreach($allowed in @($true,$false,$true)) {
                    $app.Resources.MergedDictionaries[0][$preference]=$allowed; Pump $view 640
                    $expected=$allowed -and $mask -eq 3
                    if(($template.FindName('ExtraMetadataLoader_LogoLoaderControlGrid',$view).Visibility -eq 'Visible') -ne $expected -or ($template.FindName('GameIcon',$view).Visibility -eq 'Visible') -eq $expected) { throw "Logo and native icon must be mutually exclusive: $file / $mask / $allowed" }
                }
            }
            $view.DataContext=[pscustomobject]@{}; Pump $view 640
            if($template.FindName('GameIcon',$view).Visibility -ne 'Visible') { throw "Missing logo plugin must keep the native icon: $file" }
            $style=$doc.SelectSingleNode('//p:StackPanel.Style/p:Style[p:Style.Triggers/p:MultiDataTrigger/p:MultiDataTrigger.Conditions/p:Condition[contains(@Binding,"EnableVideoPlayer")]]',$ns)
            $markup=$style.OuterXml -replace '\{PluginSettings Plugin=(\w+), Path=', '{Binding Path=$1.'
            $template=[Windows.Markup.XamlReader]::Parse('<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Control"><Grid><ContentControl Name="ExtraMetadataLoader_VideoLoaderControl" Content="{Binding VideoContent}" Visibility="Collapsed"/><StackPanel Name="FixtureMedia"><StackPanel.Style>'+ $markup+'</StackPanel.Style></StackPanel></Grid></ControlTemplate>')
            $view.Template=$template; [void]$view.ApplyTemplate()
            foreach($mask in @(0..7)+@(0,3)) {
                $settings=[pscustomobject]@{EnableVideoPlayer=[bool]($mask -band 1);IsAnyVideoAvailable=[bool]($mask -band 2);EnableAlternativeDetailsVideoPlayer=[bool]($mask -band 4);EnableAlternativeGridVideoPlayer=[bool]($mask -band 4)}
                $view.DataContext=[pscustomobject]@{ExtraMetadataLoader=$settings;VideoContent=[pscustomobject]@{VideoSource='fixture.mp4'}}
                Pump $view 640
                if(($template.FindName('FixtureMedia',$view).Visibility -eq 'Visible') -ne ($mask -eq 3)) { throw "Video options must respect the player switch and availability: $file / $mask" }
            }
            if($gridView) {
                $view.DataContext=[pscustomobject]@{ExtraMetadataLoader=$settings;VideoContent=[pscustomobject]@{VideoSource=$null}}; Pump $view 640
                if($template.FindName('FixtureMedia',$view).Visibility -ne 'Collapsed') { throw 'A grid video without a source must hide its selector.' }
            }
            $view.DataContext=[pscustomobject]@{}; Pump $view 640
            if($template.FindName('FixtureMedia',$view).Visibility -ne 'Collapsed') { throw "Missing video plugin must hide its selector: $file" }
        }
    } finally { $window.Close() }
}
try {
    foreach($file in @('Constants.xaml','Common.xaml','DefaultControls/Border.xaml','DefaultControls/TabControl.xaml')) {
        $app.Resources.MergedDictionaries.Add([Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $source $file))))
    }
    AssertMediaOptions
    $doc=[Xml.XmlDocument]::new();$doc.Load((Join-Path $source 'Views/GridViewGameOverview.xaml'))
    $ns=[Xml.XmlNamespaceManager]::new($doc.NameTable)
    $ns.AddNamespace('p','http://schemas.microsoft.com/winfx/2006/xaml/presentation');$ns.AddNamespace('x','http://schemas.microsoft.com/winfx/2006/xaml')
    $footer=$doc.SelectSingleNode('//p:Grid[@x:Name="DuneGridHeroFooter"]',$ns).CloneNode($true)
    $media=$footer.SelectSingleNode('./p:StackPanel',$ns)
    # Replace plugin-owned contents, keeping the actual responsive style.
    foreach($child in @($media.ChildNodes)) { if($child.LocalName -ne 'StackPanel.Style') { [void]$media.RemoveChild($child) } }
    $style=$media.SelectSingleNode('./p:StackPanel.Style/p:Style',$ns)
    [void]$style.RemoveChild($style.SelectSingleNode('./p:Setter[@Property="Visibility"]',$ns))
    $triggers=$style.SelectSingleNode('./p:Style.Triggers',$ns)
    foreach($child in @($triggers.ChildNodes | Select-Object -Skip 1)) { [void]$triggers.RemoveChild($child) }
    $media.SetAttribute('Name','FixtureMedia')
    $dummy=$doc.CreateElement('Border',$ns.LookupNamespace('p'));$dummy.SetAttribute('Width','250');$dummy.SetAttribute('Height','44');[void]$media.AppendChild($dummy)
    $actions=$footer.SelectSingleNode('./p:Grid[@x:Name="DuneGameActions"]',$ns)
    foreach($child in @($actions.ChildNodes)) { if($child.LocalName -notin @('Grid.Style','Grid.ColumnDefinitions')) { [void]$actions.RemoveChild($child) } }
    $dummy=$doc.CreateElement('Border',$ns.LookupNamespace('p'));$dummy.SetAttribute('Grid.ColumnSpan','3');$dummy.SetAttribute('Height','40');[void]$actions.AppendChild($dummy)
    $metadata=$footer.SelectSingleNode('./p:WrapPanel',$ns)
    $metadata.SetAttribute('Name','FixtureMetadata')
    foreach($child in @($metadata.ChildNodes)) { if($child.LocalName -ne 'WrapPanel.Style') { [void]$metadata.RemoveChild($child) } }
    foreach($index in 1..8) {
        $dummy=$doc.CreateElement('Border',$ns.LookupNamespace('p'));$dummy.SetAttribute('Width','60');$dummy.SetAttribute('Height','20');[void]$metadata.AppendChild($dummy)
    }
    $root=[Windows.Markup.XamlReader]::Parse('<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">'+$footer.OuterXml+'</Grid>')
    foreach($width in @(640,948,1100,1500,640)) {
        foreach($visible in @('Visible','Collapsed')) {
            $root.FindName('FixtureMedia').Visibility=$visible; Pump $root $width
            $shell=$root.FindName('DuneGridHeroFooter');$mediaControl=$root.FindName('FixtureMedia');$metadataControl=$root.FindName('FixtureMetadata')
            $metadataBottom=$metadataControl.TranslatePoint([Windows.Point]::new(0,0),$shell).Y+$metadataControl.ActualHeight
            if($shell.ActualWidth -ge 900 -or $visible -eq 'Collapsed') {
                if([Math]::Abs($shell.ActualHeight-$metadataBottom) -gt 0.1) { throw "Hero metadata must reach the footer bottom: $width / $visible" }
            }
            if($shell.ActualWidth -ge 900 -and $visible -eq 'Visible') {
                if([Windows.Controls.Grid]::GetRow($mediaControl) -ne 0 -or [Windows.Controls.Grid]::GetRowSpan($mediaControl) -ne 2 -or [Windows.Controls.Grid]::GetColumnSpan($root.FindName('DuneGameActions')) -ne 1) { throw 'Wide hero must share its bottom between commands and media.' }
                $mediaBottom=$mediaControl.TranslatePoint([Windows.Point]::new(0,0),$shell).Y+$mediaControl.ActualHeight
                if([Math]::Abs($mediaBottom-$metadataBottom) -gt 0.1) { throw 'Hero media and metadata bottoms must align.' }
            }
        }
    }
    foreach($file in @('DetailsViewGameOverview.xaml','GridViewGameOverview.xaml')) {
        $doc.Load((Join-Path $source ('Views/'+$file)))
        foreach($key in @('Languages','Dlc')) {
            $node=$doc.SelectSingleNode('//p:TabItem[@Tag="{DynamicResource DuneShow'+$key+'Tab}"]',$ns)
            $tab=$node.CloneNode($false);$tab.RemoveAttribute('Header');$tab.RemoveAttribute('Tag');$tab.SetAttribute('Header',$key)
            $tab.AppendChild($node.SelectSingleNode('./p:TabItem.Style',$ns).CloneNode($true)) | Out-Null
            $markup=$tab.OuterXml -replace '\{PluginSettings Plugin=(\w+), Path=', '{Binding Path=$1.'
            $tabs=[Windows.Markup.XamlReader]::Parse('<TabControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">'+$markup+'</TabControl>')
            $item=$tabs.Items[0];$item.Visibility='Visible';Pump $tabs 600;$item.ApplyTemplate() | Out-Null
            if($null -eq $item.Template.FindName('SelectionIndicator',$item)) { throw "Section tab must show its selection indicator: $file / $key" }
        }
    }
    Write-Output 'PASS: logo/icon preferences, video option gates, missing media plugins, hero bottom anchoring, media wrapping/absence and plugin tab selection indicators.'
} finally { $app.Shutdown() }
