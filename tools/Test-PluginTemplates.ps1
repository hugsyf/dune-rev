# Exercise Playnite's markup-extension contract inside reusable WPF templates.
# Run: powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File tools/Test-PluginTemplates.ps1
# Unlike plain Binding fixtures, Playnite defers SharedDp targets and returns the
# expression from BindingOperations.SetBinding for concrete dependency properties:
# https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Extensions/Markup/BindingExtension.cs
[CmdletBinding()]
param([string]$SourceDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
Add-Type -ReferencedAssemblies PresentationFramework,PresentationCore,WindowsBase,System.Xaml @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
namespace DuneTemplateTests {
    public class SettingsProxy : INotifyPropertyChanged {
        public static readonly SettingsProxy Instance = new SettingsProxy();
        private GameSettings game;
        public GameSettings Game {
            get { return game; }
            set { game = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Game")); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
    public class GameSettings {
        public DlcSettings CheckDlc { get; set; }
        public LanguageSettings CheckLocalizations { get; set; }
    }
    public class DlcSettings { public bool HasData { get; set; } public List<Dlc> ListDlcs { get; set; } }
    public class Dlc { public bool IsOwned { get; set; } }
    public class LanguageSettings { public bool HasData { get; set; } public bool HasNativeSupport { get; set; } public List<string> ListNativeSupport { get; set; } }
    public class PluginSettingsExtension : MarkupExtension {
        public string Plugin { get; set; }
        public string Path { get; set; }
        public object FallbackValue { get; set; }
        public override object ProvideValue(IServiceProvider services) {
            IProvideValueTarget target = (IProvideValueTarget)services.GetService(typeof(IProvideValueTarget));
            if (target.TargetObject.GetType().FullName == "System.Windows.SharedDp") return this;
            Binding binding = new Binding("Game." + Plugin + "." + Path) {
                Source = SettingsProxy.Instance,
                Mode = BindingMode.OneWay
            };
            if (FallbackValue != null) binding.FallbackValue = FallbackValue;
            if (target.TargetProperty == null) return binding;
            DependencyProperty property = target.TargetProperty as DependencyProperty;
            if (property != null) {
                if (property.PropertyType == typeof(Visibility)) binding.Converter = new BooleanToVisibilityConverter();
                return BindingOperations.SetBinding((DependencyObject)target.TargetObject, property, binding);
            }
            PropertyInfo member = target.TargetProperty as PropertyInfo;
            if (member != null && member.PropertyType == typeof(BindingBase)) return binding;
            return this;
        }
    }
}
'@
$source=if($SourceDirectory) { (Resolve-Path -LiteralPath $SourceDirectory).Path } else { Join-Path $PSScriptRoot '..\Source' }
$app=[Windows.Application]::new()
$window=[Windows.Window]::new()
$window.Left=-32000; $window.Top=-32000; $window.Width=1000; $window.Height=600
$window.ShowInTaskbar=$false; $window.ShowActivated=$false
$window.WindowStyle='None'; $window.ResizeMode='NoResize'
function Pump($control) {
    for($pass=0;$pass -lt 5;$pass++) {
        [void]$control.Dispatcher.Invoke([Windows.Threading.DispatcherPriority]::DataBind,[Action]{})
        $control.Measure([Windows.Size]::new(900,500)); $control.Arrange([Windows.Rect]::new(0,0,900,500)); $control.UpdateLayout()
        [void]$control.Dispatcher.Invoke([Windows.Threading.DispatcherPriority]::ApplicationIdle,[Action]{})
    }
}
function Fixture($owned,$missing,$preferred=$true) {
    $dlcs=[Collections.Generic.List[DuneTemplateTests.Dlc]]::new()
    for($index=0;$index -lt $owned+$missing;$index++) { $dlcs.Add([DuneTemplateTests.Dlc]@{IsOwned=$index -lt $owned}) }
    return [DuneTemplateTests.GameSettings]@{
        CheckDlc=[DuneTemplateTests.DlcSettings]@{HasData=$dlcs.Count -gt 0;ListDlcs=$dlcs}
        CheckLocalizations=[DuneTemplateTests.LanguageSettings]@{HasData=$true;HasNativeSupport=$preferred;ListNativeSupport=[Collections.Generic.List[string]]@('English','Chinese Simplified')}
    }
}
try {
    foreach($relative in @('Constants.xaml','Common.xaml','DefaultControls/Border.xaml')) {
        $app.Resources.MergedDictionaries.Add([Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $source $relative))))
    }
    [DuneTemplateTests.SettingsProxy]::Instance.Game=Fixture 3 5
    $assemblyName=([DuneTemplateTests.PluginSettingsExtension]).Assembly.GetName().Name
    foreach($file in @('DetailsViewGameOverview.xaml','GridViewGameOverview.xaml')) {
        $doc=[Xml.XmlDocument]::new(); $doc.Load((Join-Path $source ('Views/'+$file)))
        $ns=[Xml.XmlNamespaceManager]::new($doc.NameTable); $ns.AddNamespace('p','http://schemas.microsoft.com/winfx/2006/xaml/presentation'); $ns.AddNamespace('x','http://schemas.microsoft.com/winfx/2006/xaml')
        # Direct extension expressions in non-visual template resources bypass
        # Playnite's SharedDp deferral. Keep them on visual properties instead.
        $node=$doc.SelectSingleNode('//p:Grid[@x:Name="DuneCatalogSummary"]',$ns)
        $markup=$node.OuterXml.Replace('{PluginSettings ','{fixture:PluginSettings ')
        $markup=[regex]::Replace($markup,"\{ThemeFile '([^']+)'\}", { param($match) ([Uri]::new((Join-Path $source $match.Groups[1].Value))).AbsoluteUri })
        $template=[Windows.Markup.XamlReader]::Parse('<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:fixture="clr-namespace:DuneTemplateTests;assembly='+$assemblyName+'" TargetType="Control">'+$markup+'</ControlTemplate>')
        $panel=[Windows.Controls.StackPanel]::new(); $window.Content=$panel
        $hosts=@()
        foreach($index in 0..1) {
            $control=[Windows.Controls.Control]::new(); $control.Template=$template
            $panel.Children.Add($control) | Out-Null; $hosts+=$control
            $control.ApplyTemplate() | Out-Null
        }
        if(-not $window.IsVisible) { $window.Show() }
        foreach($state in @(@(3,5),@(0,8),@(8,0),@(0,0))) {
            [DuneTemplateTests.SettingsProxy]::Instance.Game=Fixture $state[0] $state[1]
            Pump $panel
            foreach($control in $hosts) {
                $count=$template.FindName('DuneDlcCount',$control)
                $groups=$template.FindName('DuneDlcOwnershipCounts',$control)
                if($count.Text -ne [string]($state[0]+$state[1])) { throw "Stale template count: $file / $($state -join ',')" }
                if([int]($groups.Items | Measure-Object ItemCount -Sum).Sum -ne $state[0]+$state[1]) { throw "Stale template groups: $file / $($state -join ',')" }
            }
        }
        # Reuse the same sealed template after disposing its first visual instance.
        $hosts[0].Template=$null; $hosts[0].Template=$template
        $hosts[0].ApplyTemplate() | Out-Null
        [DuneTemplateTests.SettingsProxy]::Instance.Game=Fixture 2 1 $false
        Pump $panel
        foreach($control in $hosts) {
            if($template.FindName('DuneDlcCount',$control).Text -ne '3' -or $template.FindName('DunePreferredLanguageStatus',$control).Text -ne $app.FindResource('LOCDunePreferredLanguageMissing')) { throw "Template recreation failed: $file" }
        }
        foreach($attribute in $doc.SelectNodes('//p:CollectionViewSource/@*',$ns)) {
            if($attribute.Value -match '\{PluginSettings\b') { throw "Unsafe plugin expression on a template resource: $file / $($attribute.Name)" }
        }
        $window.Content=$null
    }
    Write-Output 'PASS: native-style plugin binding, two template instances, game changes, empty data, template recreation.'
} finally { $window.Close(); $app.Shutdown() }
