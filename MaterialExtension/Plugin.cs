using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Collections.Generic;
using DuneMaterials.Rendering;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;

namespace DuneMaterials
{
    public sealed class MaterialPlugin : GenericPlugin
    {
        readonly MaterialRuntime runtime;
        public MaterialSettings MaterialSettings { get; private set; }
        public override Guid Id { get { return Guid.Parse("821c6070-ef6e-4dd6-a597-ab9aad905a0a"); } }
        public MaterialPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = true };
            var options = LoadPluginSettings<MaterialOptions>() ?? new MaterialOptions();
            runtime = new MaterialRuntime(options, delegate(string message) { LogManager.GetLogger().Info(message); });
            MaterialSettings = new MaterialSettings(options, delegate(MaterialOptions next) {
                SavePluginSettings(next);
                Application.Current.Dispatcher.Invoke(new Action(delegate { Apply(next); }));
            }, runtime.Describe);
            AddConvertersSupport(new AddConvertersSupportArgs { SourceName = "DuneMaterials", Converters = new List<IValueConverter> {
                new MaterialBrushConverter(runtime), new MaterialElementConverter(runtime), new ScrollFadeConverter(),
                new ExpansionLayoutTemplateConverter(),
                new HeroMediaConverter(delegate { return MaterialSettings.Options.HeroAutoPlay; }, delegate(bool enabled) {
                    MaterialSettings.Options.HeroAutoPlay = enabled;
                    SavePluginSettings(MaterialSettings.Options);
                }) } });
            AddSettingsSupport(new AddSettingsSupportArgs { SourceName = "DuneMaterials", SettingsRoot = "MaterialSettings" });
        }
        public override ISettings GetSettings(bool firstRunSettings) { return MaterialSettings; }
        public override UserControl GetSettingsView(bool firstRunSettings) { return MaterialSettings.View(); }
        void Apply(MaterialOptions options)
        {
            runtime.Configure(options);
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            Application.Current.Dispatcher.Invoke(new Action(delegate {
                Apply(runtime.Options);
            }));
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        { Application.Current.Dispatcher.Invoke(new Action(delegate { runtime.Dispose(); })); }
    }
}
