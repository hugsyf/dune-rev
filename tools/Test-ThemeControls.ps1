# Small offscreen sanity check. Real plugin behavior is checked in Playnite.
# Run: powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File tools/Test-ThemeControls.ps1
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$source=Join-Path $PSScriptRoot '..\Source'
foreach($file in Get-ChildItem $source -Recurse -File -Filter '*.xaml') {
    $document=[Xml.XmlDocument]::new()
    $document.Load($file.FullName)
}
$app=[Windows.Application]::new()
try {
    # Playnite preflights each dictionary before merging the theme resources.
    # Border must resolve its own custom BasedOn styles without themed Common.xaml.
    [void][Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $source 'DefaultControls/Border.xaml')))
    foreach($relative in @('Constants.xaml','Common.xaml','DefaultControls/Border.xaml','DefaultControls/TabControl.xaml','DefaultControls/ScrollViewer.xaml','DefaultControls/ListBox.xaml')) {
        $dictionary=[Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $source $relative)))
        $app.Resources.MergedDictionaries.Add($dictionary)
    }
    $plugin=[Windows.Controls.ContentControl]::new()
    $plugin.Style=$app.FindResource('DunePluginContent')
    if($plugin.Visibility -ne 'Collapsed') { throw 'An empty plugin host must not reserve space.' }
    $content=[Windows.Controls.Border]::new()
    $content.Height=80
    $plugin.Content=$content
    [void]$plugin.Dispatcher.Invoke([Windows.Threading.DispatcherPriority]::DataBind,[Action]{})
    if($plugin.Visibility -ne 'Visible' -or $plugin.FontWeight -ne 'Normal') { throw 'Plugin content must appear with normal text weight.' }
    $content.Visibility='Collapsed'
    [void]$plugin.Dispatcher.Invoke([Windows.Threading.DispatcherPriority]::DataBind,[Action]{})
    $plugin.Measure([Windows.Size]::new(320,[double]::PositiveInfinity))
    if($plugin.DesiredSize.Height -ne 0) { throw 'A plugin-hidden control must not leave height or margin behind.' }
    $content.Visibility='Visible'
    [void]$plugin.Dispatcher.Invoke([Windows.Threading.DispatcherPriority]::DataBind,[Action]{})
    if($plugin.Visibility -ne 'Visible') { throw 'Plugin content must reappear after a game change.' }
    # One overflowing viewport checks both scroll axes, without a plugin mock.
    $scroll=[Windows.Controls.ScrollViewer]::new()
    $scroll.Style=$app.FindResource([Windows.Controls.ScrollViewer])
    $scroll.HorizontalScrollBarVisibility='Auto'; $scroll.VerticalScrollBarVisibility='Auto'
    $scroll.Content=[Windows.Controls.Border]::new()
    $scroll.Content.Width=800; $scroll.Content.Height=1200
    $scroll.Measure([Windows.Size]::new(320,200)); $scroll.Arrange([Windows.Rect]::new(0,0,320,200)); $scroll.UpdateLayout()
    foreach($axis in @('Vertical','Horizontal')) {
        $bar=$scroll.Template.FindName("PART_${axis}ScrollBar",$scroll)
        $bar.ApplyTemplate() | Out-Null
        $track=$bar.Template.FindName('PART_Track',$bar)
        if($bar.Visibility -ne 'Visible' -or $track.Orientation.ToString() -ne $axis) { throw "$axis scrollbar is hidden or misoriented." }
        if(($axis -eq 'Vertical' -and $bar.ActualWidth -lt 12) -or ($axis -eq 'Horizontal' -and $bar.ActualHeight -lt 12)) { throw 'Scroll hit area is too small.' }
    }
    $scroll.ScrollToVerticalOffset(200); $scroll.ScrollToHorizontalOffset(100); $scroll.UpdateLayout()
    if($scroll.VerticalOffset -le 0 -or $scroll.HorizontalOffset -le 0 -or $scroll.ViewportWidth -gt 308 -or $scroll.ViewportHeight -gt 188) { throw 'Scrolling or reserved scrollbar space failed.' }
    Write-Output 'PASS: XAML, isolated dictionary preflight, shared resources, plugin visibility, scrolling.'
} finally { $app.Shutdown() }
