[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $project '.local-archive/material-sdk/Playnite.SDK.dll'))
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $project 'dist/DuneMaterials/DuneMaterials.dll'))
$optionsType = $assembly.GetType('DuneMaterials.Rendering.MaterialOptions')
$settingsType = $assembly.GetType('DuneMaterials.MaterialSettings')
function Require($condition, $message) { if (-not $condition) { throw $message } }
$options = [Activator]::CreateInstance($optionsType)
Require ($options.UseNewRenderer -and $options.Mode.ToString() -eq 'Realtime' -and $options.AllowExperimentalRealtime -and -not $options.HeroAutoPlay) 'New-install defaults are incorrect.'
# Existing values must remain intact when merely opening settings.
$options.UseNewRenderer = $false
$options.Mode = [Enum]::Parse($options.Mode.GetType(), 'Cached')
$saveType = [Action``1].MakeGenericType($optionsType)
$persist = [Management.Automation.LanguagePrimitives]::ConvertTo({ param($value) $script:savedOptions = $value }, $saveType)
$describe = [Func[string]]{ 'Settings check; no windows or rendering.' }
$constructor = $settingsType.GetConstructors([Reflection.BindingFlags]'Instance,NonPublic')[0]
$settings = $constructor.Invoke(@($options, $persist, $describe))
$viewMethod = $settingsType.GetMethod('View', [Reflection.BindingFlags]'Instance,NonPublic')
foreach ($language in @('en-US', 'zh-CN')) {
    $oldCulture = [Threading.Thread]::CurrentThread.CurrentUICulture
    try {
        [Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::GetCultureInfo($language)
        $view = $viewMethod.Invoke($settings, @())
        $panel = $view.Content.Content
        $rendering = $panel.Children | Where-Object { $_ -is [Windows.Controls.StackPanel] } | Select-Object -First 1
        $mode = $rendering.Children | Where-Object { $_ -is [Windows.Controls.ComboBox] } | Select-Object -First 1
        $expander = $panel.Children | Where-Object { $_ -is [Windows.Controls.Expander] } | Select-Object -First 1
        Require ($mode.Items.Count -eq 2 -and $mode.SelectedValue.ToString() -eq 'Cached') 'Opening settings changed the saved mode.'
        Require (-not $settings.Options.UseNewRenderer -and -not $expander.IsExpanded) 'Opening settings changed preferences or expanded diagnostics.'
        $settings.BeginEdit()
        $settings.Options.HeroAutoPlay = $true
        $restore = $panel.Children | Where-Object { $_ -is [Windows.Controls.Button] } | Select-Object -First 1
        $restore.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent))
        Require ($settings.Options.UseNewRenderer -and $settings.Options.Mode.ToString() -eq 'Realtime' -and $settings.Options.HeroAutoPlay) 'Recommended settings changed Hero playback preference.'
        $settings.CancelEdit()
        Require (-not $settings.Options.UseNewRenderer -and $settings.Options.Mode.ToString() -eq 'Cached' -and -not $settings.Options.HeroAutoPlay) 'Cancel did not restore preferences.'
    } finally { [Threading.Thread]::CurrentThread.CurrentUICulture = $oldCulture }
}
$settings.BeginEdit()
$settings.Options.Mode = [Enum]::Parse($options.Mode.GetType(), 'Realtime')
$settings.Options.AllowExperimentalRealtime = $false
$settings.EndEdit()
Require ($script:savedOptions.AllowExperimentalRealtime -and $script:savedOptions.Mode.ToString() -eq 'Realtime') 'Saving realtime mode left the old experimental gate disabled.'
Write-Output 'PASS: English/Chinese settings, preserved preferences, recommended defaults, cancel/save and collapsed diagnostics. No windows or rendering.'
