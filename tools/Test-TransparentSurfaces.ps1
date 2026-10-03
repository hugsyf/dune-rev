# Render cover masks through Playnite's theme-before-MainModel startup order.
# Run: powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File tools/Test-TransparentSurfaces.ps1
[CmdletBinding()]
param([string]$SourceDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
Add-Type -ReferencedAssemblies PresentationFramework,PresentationCore,WindowsBase,System.Xaml @'
using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
namespace DuneSurfaceTests {
    public class AppState {
        public static readonly AppState Instance = new AppState();
        public CoverSettings AppSettings { get; set; }
        public AppState() { AppSettings = CoverSettings.Instance; }
        // Playnite loads theme resources before assigning MainModel; that setter
        // does not raise PropertyChanged. Preserve that startup order here.
        public ModelState MainModel { get; set; }
    }
    public class ModelState { public CoverSettings AppSettings { get; set; } }
    public class CoverSettings : INotifyPropertyChanged {
        public static readonly CoverSettings Instance = new CoverSettings();
        private double width = 200, height = 300;
        public double GridItemWidth { get { return width; } set { width = value; Changed("GridItemWidth"); } }
        public double GridItemHeight { get { return height; } set { height = value; Changed("GridItemHeight"); } }
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed(string name) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name)); }
    }
    // Match Playnite's Settings markup extension, including shared-template deferral.
    public class SettingsExtension : MarkupExtension {
        private readonly string path;
        public SettingsExtension(string path) { this.path = path; }
        public override object ProvideValue(IServiceProvider services) {
            IProvideValueTarget target = (IProvideValueTarget)services.GetService(typeof(IProvideValueTarget));
            if (target.TargetObject.GetType().FullName == "System.Windows.SharedDp") return this;
            Binding binding = new Binding("AppSettings." + path) { Source = AppState.Instance, Mode = BindingMode.OneWay };
            DependencyProperty property = target.TargetProperty as DependencyProperty;
            if (property != null) return BindingOperations.SetBinding((DependencyObject)target.TargetObject, property, binding);
            PropertyInfo member = target.TargetProperty as PropertyInfo;
            if (member != null && member.PropertyType == typeof(BindingBase)) return binding;
            return this;
        }
    }
}
'@
$source=if($SourceDirectory) { (Resolve-Path -LiteralPath $SourceDirectory).Path } else { Join-Path $PSScriptRoot '..\Source' }
$app=[Windows.Application]::new()
function Pump($view,$width,$height) {
    foreach($pass in 0..4) {
        [void]$view.Dispatcher.Invoke([Windows.Threading.DispatcherPriority]::DataBind,[Action]{})
        $view.Measure([Windows.Size]::new($width,$height))
        $view.Arrange([Windows.Rect]::new(0,0,$width,$height)); $view.UpdateLayout()
    }
}
function Render($view,$width,$height) {
    Pump $view $width $height
    $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new($width,$height,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($view)
    $pixels=New-Object byte[] ($width*$height*4); $bitmap.CopyPixels($pixels,$width*4,0)
    return ,$pixels
}
function Pixel($pixels,$width,$x,$y) { $index=($y*$width+$x)*4; return ,$pixels[$index..($index+3)] }
function ReadXml($path) {
    $doc=[Xml.XmlDocument]::new(); $doc.Load($path)
    return $doc
}
try {
    foreach($file in @('Constants.xaml','Common.xaml','DefaultControls/ProgressBar.xaml')) {
        $app.Resources.MergedDictionaries.Add([Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $source $file))))
    }
    $app.Resources['BooleanToVisibilityConverter']=[Windows.Controls.BooleanToVisibilityConverter]::new()
    $doc=ReadXml (Join-Path $source 'DerivedStyles/GridViewItemTemplate.xaml')
    $ns=[Xml.XmlNamespaceManager]::new($doc.NameTable)
    $ns.AddNamespace('p','http://schemas.microsoft.com/winfx/2006/xaml/presentation'); $ns.AddNamespace('x','http://schemas.microsoft.com/winfx/2006/xaml')
    $maskStyle=$doc.SelectSingleNode('//p:Style[@x:Key="VisualBrushBorderMask"]',$ns)
    $coverStyle=$doc.SelectSingleNode('//p:StackPanel.Style',$ns)
    $selectionStyle=$doc.SelectSingleNode('//p:Border[@x:Name="SelectedGameBorder"]/p:Grid/p:Grid.Style',$ns)
    $assembly=([DuneSurfaceTests.SettingsExtension]).Assembly.GetName().Name
    $templates=@{}
    foreach($layer in @('cover','selection')) {
        $panel=if($layer -eq 'cover') {'StackPanel'} else {'Grid'}
        $style=if($layer -eq 'cover') {$coverStyle.OuterXml} else {$selectionStyle.OuterXml}
        $markup='<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:fixture="clr-namespace:DuneSurfaceTests;assembly='+$assembly+'" TargetType="Control"><ControlTemplate.Resources>'+$maskStyle.OuterXml+'</ControlTemplate.Resources><Grid Width="{Settings GridItemWidth}" Height="{Settings GridItemHeight}"><Border Name="Mask" Style="{StaticResource VisualBrushBorderMask}" Tag="{DynamicResource GridViewGameCoverUseRoundedCorners}"/><'+$panel+'>'+$style+'<Image Name="FixtureCover" Height="{Settings GridItemHeight}" Stretch="Uniform"/></'+$panel+'></Grid></ControlTemplate>'
        $markup=$markup.Replace('{Settings ','{fixture:Settings ').Replace(' xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"','')
        $markup=$markup.Replace('Source={x:Static p:PlayniteApplication.Current}','Source={x:Static fixture:AppState.Instance}')
        $markup=$markup.Replace('<ControlTemplate xmlns:x=', '<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x=')
        $templates[$layer]=[Windows.Markup.XamlReader]::Parse($markup)
    }
    [DuneSurfaceTests.AppState]::Instance.MainModel=[DuneSurfaceTests.ModelState]@{AppSettings=[DuneSurfaceTests.CoverSettings]::Instance}
    $count=0
    foreach($color in @('#FF303640','#00303640','#33303640')) {
        foreach($opacity in @(1.0,0.0)) {
            $brush=[Windows.Media.BrushConverter]::new().ConvertFromString($color)
            $brush.Opacity=$opacity; $app.Resources['ControlBackgroundBrush']=$brush
            foreach($rounded in @($true,$false)) {
                $app.Resources['GridViewGameCoverUseRoundedCorners']=$rounded
                foreach($layer in @('cover','selection')) {
                    # Two controls share one template to catch visual/resource sharing problems.
                    $controls=@([Windows.Controls.Control]::new(),[Windows.Controls.Control]::new())
                    foreach($view in $controls) { $view.Template=$templates[$layer]; [void]$view.ApplyTemplate() }
                    foreach($dimensions in @(@(200,300),@(120,180),@(320,200),@(200,300))) {
                        $width=$dimensions[0]; $height=$dimensions[1]
                        [DuneSurfaceTests.CoverSettings]::Instance.GridItemWidth=$width
                        [DuneSurfaceTests.CoverSettings]::Instance.GridItemHeight=$height
                        foreach($view in $controls) {
                            $image=$view.Template.FindName('FixtureCover',$view)
                            $image.Source=[Windows.Media.DrawingImage]::new([Windows.Media.GeometryDrawing]::new([Windows.Media.Brushes]::Red,$null,[Windows.Media.RectangleGeometry]::new([Windows.Rect]::new(0,0,$width,$height))))
                            $pixels=Render $view $width $height
                            $center=Pixel $pixels $width ([int]($width/2)) ([int]($height/2))
                            if(($center -join ',') -ne '0,0,255,255') { throw "Image lost opacity: $layer / $color / $opacity / $rounded / ${width}x$height" }
                            $corner=Pixel $pixels $width 0 0
                            if($rounded -and $corner[3] -ne 0) { throw 'Rounded clipping lost its transparent corner.' }
                            if(-not $rounded -and $corner[3] -ne 255) { throw 'Square covers must keep their corners.' }
                            # Preserve the configured backdrop in letterboxed image margins.
                            $image.Source=[Windows.Media.DrawingImage]::new([Windows.Media.GeometryDrawing]::new([Windows.Media.Brushes]::Red,$null,[Windows.Media.RectangleGeometry]::new([Windows.Rect]::new(0,0,1,1))))
                            $letterbox=Render $view $width $height
                            $edge=if($width -lt $height) {Pixel $letterbox $width ([int]($width/2)) 2} else {Pixel $letterbox $width 2 ([int]($height/2))}
                            $expectedAlpha=if($rounded) {[int][Math]::Round($brush.Color.A*$opacity)} else {0}
                            if($edge[3] -ne $expectedAlpha) { throw "Unexpected image margin: $layer / $color / $opacity / $rounded / ${width}x$height / alpha=$($edge[3]), expected=$expectedAlpha" }
                            $count++
                        }
                    }
                }
            }
        }
    }
    # Transparent backgrounds must not turn indeterminate progress into a full bar.
    foreach($key in @('ControlBackgroundBrush','ControlBorderBrush','ControlTrackBrush')) { $app.Resources[$key]=[Windows.Media.Brushes]::Transparent }
    foreach($width in @(100,300)) {
        $bar=[Windows.Controls.ProgressBar]::new(); $bar.Style=$app.FindResource([Windows.Controls.ProgressBar]); $bar.Foreground=[Windows.Media.Brushes]::Red; $bar.Background=[Windows.Media.Brushes]::Transparent
        $bar.Value=50
        $pixels=Render $bar $width 10
        $left=Pixel $pixels $width ([int]($width*0.25)) 5; $right=Pixel $pixels $width ([int]($width*0.75)) 5
        if($left[2] -ne 255 -or $right[3] -ne 0) { throw "Determinate progress mismatch: left=$($left -join ','), right=$($right -join ','), track=$($bar.Template.FindName('PART_Track',$bar).ActualWidth), indicator=$($bar.Template.FindName('PART_Indicator',$bar).ActualWidth)" }
        $bar.IsIndeterminate=$true; Pump $bar $width 10
        $pixels=Render $bar $width 10
        $filled=0
        foreach($column in 0..($width-1)) { $pixel=Pixel $pixels $width $column 5; if($pixel[2] -gt 0) { $filled++ } }
        if($filled -gt [Math]::Ceiling($width*0.31)) { throw 'Transparent surface colors exposed a stationary, fully filled indeterminate bar.' }
        $animation=$bar.Template.FindName('Animation',$bar)
        $translation=$bar.Template.FindName('IndicatorTranslation',$bar)
        # Freeze at two animation positions for deterministic pixel assertions.
        $translation.BeginAnimation([Windows.Media.TranslateTransform]::XProperty,$null)
        foreach($x in @(5,65)) {
            $translation.X=$x; $pixels=Render $bar $width 10
            $inside=Pixel $pixels $width ([int]($width*($x+15)/100)) 5
            $outside=Pixel $pixels $width ([int]($width*($(if($x -eq 5) {80} else {20}))/100)) 5
            if($inside[2] -ne 255 -or $outside[3] -ne 0) { throw 'Transparent surface colors broke the moving indicator.' }
        }
        $bar.IsIndeterminate=$false; $bar.Value=25
        $pixels=Render $bar $width 10
        $left=Pixel $pixels $width ([int]($width*0.1)) 5; $right=Pixel $pixels $width ([int]($width*0.6)) 5
        if($left[2] -ne 255 -or $right[3] -ne 0 -or $animation.Visibility -eq 'Visible') { throw 'Progress must restore its value after leaving indeterminate mode.' }
    }
    Write-Output "PASS: theme-before-MainModel startup; $count cover/selection renders; transparent and translucent colors, brush opacity, corner toggles, shared templates, zoom, letterboxing; determinate and indeterminate progress."
} finally { $app.Shutdown() }
