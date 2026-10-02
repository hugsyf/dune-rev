# Exercise the real responsive hero shell and tab templates without plugin DLLs.
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$source=Join-Path $PSScriptRoot '..\Source'
$app=[Windows.Application]::new()
function Pump($view,$width) {
    foreach($pass in 0..4) {
        [void]$view.Dispatcher.Invoke([Windows.Threading.DispatcherPriority]::DataBind,[Action]{})
        $view.Measure([Windows.Size]::new($width,300))
        $view.Arrange([Windows.Rect]::new(0,0,$width,300)); $view.UpdateLayout()
    }
}
try {
    foreach($file in @('Constants.xaml','Common.xaml','DefaultControls/Border.xaml','DefaultControls/TabControl.xaml')) {
        $app.Resources.MergedDictionaries.Add([Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $source $file))))
    }
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
            if($item.FontSize -ne 16 -or $null -eq $item.Template.FindName('SelectionIndicator',$item)) { throw "Section tab must use the Fluent template and 16pt text: $file / $key" }
        }
    }
    Write-Output 'PASS: hero bottom anchoring, media wrapping/absence, Fluent plugin tabs and consistent heading sizes.'
} finally { $app.Shutdown() }
