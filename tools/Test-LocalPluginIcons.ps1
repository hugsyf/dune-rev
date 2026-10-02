# Test installed-icon lookup and Fluent fallbacks with synthetic plugin folders.
# Run: powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File tools/Test-LocalPluginIcons.ps1
# Optional -PlayniteDirectory uses the installed Api extension with the same fake data.
# No installed plugins or user configuration are read by this test.
[CmdletBinding()]
param([switch]$IconWorker, [string]$FixtureDirectory, [string]$PlayniteDirectory)
$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot '..\Source'
$tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
if(-not $IconWorker) {
    $fixtureRoot=[IO.Path]::GetFullPath((Join-Path $tempRoot ('DuneIconFixture-'+[Guid]::NewGuid().ToString('N'))))
    New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
    try {
        foreach($case in @('all icons','partial icons','invalid icons')) {
            foreach($folderName in @('playnite-howlongtobeat-plugin','playnite-gameactivity-plugin','PlayniteAchievements','playnite-checklocalizations-plugin')) {
                if($case -eq 'partial icons' -and $folderName -ne 'playnite-howlongtobeat-plugin') { continue }
                $folder=Join-Path $fixtureRoot ($case+'/Extensions/'+$folderName)
                New-Item -ItemType Directory -Path $folder -Force | Out-Null
                if($case -eq 'invalid icons') { [IO.File]::WriteAllText((Join-Path $folder 'icon.png'),'Not an image') }
                else { Copy-Item -LiteralPath (Join-Path $source 'Images/applogo.png') -Destination (Join-Path $folder 'icon.png') }
            }
        }
        # WPF URI caches can keep files open until its process exits.
        $engine=Join-Path $PSHOME 'powershell.exe'
        if($PlayniteDirectory) { $engine=Join-Path $env:WINDIR 'SysWOW64\WindowsPowerShell\v1.0\powershell.exe' }
        $workerArguments=@('-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',$PSCommandPath,'-IconWorker','-FixtureDirectory',$fixtureRoot)
        if($PlayniteDirectory) { $workerArguments+=@('-PlayniteDirectory',$PlayniteDirectory) }
        & $engine @workerArguments
        if($LASTEXITCODE -ne 0) { throw 'Local plugin icon checks failed.' }
    } finally {
        $resolvedFixture=[IO.Path]::GetFullPath($fixtureRoot)
        if(-not $resolvedFixture.StartsWith($tempRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $resolvedFixture) -notmatch '^DuneIconFixture-[a-f0-9]{32}$') { throw 'Fixture cleanup escaped its temporary directory.' }
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
    return
}
$fixtureRoot=(Resolve-Path -LiteralPath $FixtureDirectory).Path
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
if($PlayniteDirectory) {
    foreach($assemblyFile in @('Playnite.SDK.dll','Playnite.dll')) {
        [void][Reflection.Assembly]::LoadFrom((Join-Path $PlayniteDirectory $assemblyFile))
    }
}
Add-Type -ReferencedAssemblies PresentationFramework,PresentationCore,WindowsBase,System.Xaml @'
using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
namespace DuneIconTests {
    public class Paths : INotifyPropertyChanged {
        private string configurationPath;
        public string ConfigurationPath {
            get { return configurationPath; }
            set { configurationPath = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("ConfigurationPath")); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
    public class ApiData {
        public static readonly ApiData Instance = new ApiData();
        public ApiData PlayniteApiGlobal { get { return this; } }
        public Paths Paths { get; private set; }
        private ApiData() { Paths = new Paths(); }
    }
    // Match Playnite's binding deferral when one template has multiple instances.
    public class ApiExtension : MarkupExtension {
        public string Path { get; set; }
        public string PathRoot { get; private set; }
        public string StringFormat { get; set; }
        public object FallbackValue { get; set; }
        public ApiExtension() : this(null) { }
        public ApiExtension(string path) {
            Path = path;
            PathRoot = "PlayniteApiGlobal";
            // Playnite adds the separator in its constructor, before named properties.
            if (!String.IsNullOrEmpty(path)) PathRoot += ".";
        }
        public override object ProvideValue(IServiceProvider services) {
            IProvideValueTarget target = (IProvideValueTarget)services.GetService(typeof(IProvideValueTarget));
            if (target.TargetObject.GetType().FullName == "System.Windows.SharedDp") return this;
            Binding binding = new Binding(PathRoot + Path) { Source = ApiData.Instance, Mode = BindingMode.OneWay, StringFormat = StringFormat };
            if (FallbackValue != null) binding.FallbackValue = FallbackValue;
            if (target.TargetProperty == null) return binding;
            DependencyProperty property = target.TargetProperty as DependencyProperty;
            if (property != null) return BindingOperations.SetBinding((DependencyObject)target.TargetObject, property, binding);
            PropertyInfo member = target.TargetProperty as PropertyInfo;
            if (member != null && member.PropertyType == typeof(BindingBase)) return binding;
            return this;
        }
    }
}
'@
$providers=@(
    @('HowLongToBeat','playnite-howlongtobeat-plugin','DuneHowLongToBeatProviderIcon'),
    @('Activity','playnite-gameactivity-plugin','DuneActivityProviderIcon'),
    @('Achievements','PlayniteAchievements','DuneAchievementsProviderIcon'),
    @('Languages','playnite-checklocalizations-plugin','DuneLanguagesCatalogProviderIcon')
)
$app=[Windows.Application]::new()
$window=[Windows.Window]::new()
$window.Left=-32000;$window.Top=-32000;$window.Width=1000;$window.Height=400
$window.ShowInTaskbar=$false;$window.ShowActivated=$false
function Pump($panel) {
    foreach($pass in 0..4) {
        [void]$panel.Dispatcher.Invoke([Windows.Threading.DispatcherPriority]::DataBind,[Action]{})
        $panel.Measure([Windows.Size]::new(900,300));$panel.Arrange([Windows.Rect]::new(0,0,900,300));$panel.UpdateLayout()
    }
}
try {
    foreach($relative in @('Constants.xaml','Common.xaml')) {
        $app.Resources.MergedDictionaries.Add([Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $source $relative))))
    }
    $assemblyName=([DuneIconTests.ApiExtension]).Assembly.GetName().Name
    foreach($file in @('DetailsViewGameOverview.xaml','GridViewGameOverview.xaml')) {
        $doc=[Xml.XmlDocument]::new();$doc.Load((Join-Path $source ('Views/'+$file)))
        $ns=[Xml.XmlNamespaceManager]::new($doc.NameTable)
        $ns.AddNamespace('p','http://schemas.microsoft.com/winfx/2006/xaml/presentation');$ns.AddNamespace('x','http://schemas.microsoft.com/winfx/2006/xaml')
        $paths='';$icons=''
        foreach($provider in $providers) {
            $paths+=$doc.SelectSingleNode('//p:TextBlock[@x:Name="Dune'+$provider[0]+'IconPath"]',$ns).OuterXml
            $icons+=$doc.SelectSingleNode('//p:ContentControl[@x:Name="'+$provider[2]+'"]',$ns).OuterXml
        }
        $markup=('<Grid>'+ $paths+'<StackPanel Orientation="Horizontal">'+$icons+'</StackPanel></Grid>').Replace('{Api ','{fixture:Api ')
        if($PlayniteDirectory) {
            $markup=$markup.Replace('{fixture:Api ','{native:Api ').Replace("FallbackValue=''}","Source={x:Static fixture:ApiData.Instance}, FallbackValue=''}")
        }
        $template=[Windows.Markup.XamlReader]::Parse('<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:fixture="clr-namespace:DuneIconTests;assembly='+$assemblyName+'" xmlns:native="clr-namespace:Playnite.Extensions.Markup;assembly=Playnite" TargetType="Control">'+$markup+'</ControlTemplate>')
        $panel=[Windows.Controls.StackPanel]::new();$window.Content=$panel
        $hosts=@()
        foreach($index in 0..1) {
            $hostControl=[Windows.Controls.Control]::new();$hostControl.Template=$template
            [void]$panel.Children.Add($hostControl);[void]$hostControl.ApplyTemplate();$hosts+=$hostControl
        }
        if(-not $window.IsVisible) { $window.Show() }
        foreach($case in @('all icons','missing icons','partial icons','invalid icons','all icons')) {
            [DuneIconTests.ApiData]::Instance.Paths.ConfigurationPath=Join-Path $fixtureRoot $case
            Pump $panel
            foreach($hostControl in $hosts) {
                foreach($provider in $providers) {
                    $icon=$template.FindName($provider[2],$hostControl);[void]$icon.ApplyTemplate()
                    $image=$icon.Template.FindName('PluginImage',$icon);$glyph=$icon.Template.FindName('FallbackGlyph',$icon)
                    $expected=$case -eq 'all icons' -or ($case -eq 'partial icons' -and $provider[0] -eq 'HowLongToBeat')
                    if(($null -ne $image.Source) -ne $expected) { throw "Local icon availability: $file / $case / $($provider[0])" }
                    if($expected) {
                        if($image.Source.PixelWidth -le 0 -or $glyph.Visibility -ne 'Collapsed') { throw 'Loaded icons must hide the Fluent fallback.' }
                    } elseif($glyph.Visibility -ne 'Visible' -or $image.Visibility -ne 'Collapsed') { throw 'Missing or invalid icons must show the Fluent fallback.' }
                    if([Math]::Abs($icon.ActualWidth-16) -gt 0.1 -or [Math]::Abs($icon.ActualHeight-16) -gt 0.1) { throw 'Icon or fallback changed layout size.' }
                }
            }
        }
        foreach($hostControl in $hosts) {
            foreach($provider in $providers) {
                $icon=$template.FindName($provider[2],$hostControl)
                $icon.Template.FindName('PluginImage',$icon).Source=$null
            }
            $hostControl.Template=$null
        }
        $window.Content=$null
    }
    Write-Output 'PASS: local icons, absent/partial/invalid files, path changes, spaces, two native-style template instances and 16px Fluent fallbacks.'
} finally {
    $window.Close();$app.Shutdown()
}
