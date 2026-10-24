using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;

// Exercise the real theme fragments with a fake screenshot provider, without any
// Playnite/media/material extension, network requests, windows or rendering.
internal static class HeroScreenshotChecks
{
    sealed class Provider : ContentControl, INotifyPropertyChanged
    {
        BitmapSource current;
        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<object> Screenshots { get; private set; }
        public BitmapSource CurrentImageBitmap
        {
            get { return current; }
            set { current = value; var handler = PropertyChanged; if (handler != null) handler(this, new PropertyChangedEventArgs("CurrentImageBitmap")); }
        }
        public ICommand SelectPreviousScreenshotCommand { get; private set; }
        public ICommand SelectNextScreenshotCommand { get; private set; }
        public int Switches;
        public Provider()
        {
            Screenshots = new ObservableCollection<object>();
            SelectPreviousScreenshotCommand = new Command(delegate { Switches--; });
            SelectNextScreenshotCommand = new Command(delegate { Switches++; });
        }
    }
    sealed class Command : ICommand
    {
        readonly Action action;
        public Command(Action callback) { action = callback; }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter) { action(); }
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate { frame.Continue = false; }));
        Dispatcher.PushFrame(frame);
    }
    static BitmapSource Picture(int width, int height)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, new byte[width * height * 4], width * 4);
        bitmap.Freeze(); return bitmap;
    }
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../.."));
        var common = new XmlDocument(); common.Load(Path.Combine(root, "Source/Common.xaml"));
        var screenshots = new XmlDocument(); screenshots.Load(Path.Combine(root, "tools/HeroScreenshots.xml"));
        string styles = string.Join("\n", common.DocumentElement.ChildNodes.OfType<XmlElement>()
            .Where(n => n.GetAttribute("Key", "http://schemas.microsoft.com/winfx/2006/xaml") == "DuneHeroStoreScreenshotLayer" ||
                        n.GetAttribute("Key", "http://schemas.microsoft.com/winfx/2006/xaml") == "DuneStoreScreenshotsContent")
            .Select(n => n.OuterXml));
        string fragments = string.Join("\n", screenshots.DocumentElement.ChildNodes.OfType<XmlElement>().Select(n => n.OuterXml));
        string markup = "<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
            "xmlns:checks='clr-namespace:MaterialTests;assembly=MaterialChecks'><Grid.Resources>" + styles +
            "</Grid.Resources><Grid x:Name='HeroBanner'><Image x:Name='PART_ImageBackground'/><Grid x:Name='GameVideo' Visibility='Collapsed'/>" + fragments +
            "<ContentControl x:Name='Gallery' Tag='{Binding Content, ElementName=SteamScreenshots_SteamScreenshotsViewControl}' Style='{StaticResource DuneStoreScreenshotsContent}'/>" +
            "</Grid></Grid>";
        markup = markup.Replace("{PluginSettings Plugin=DuneMaterials, Path=Options.ReduceMotion, FallbackValue=False}", "{checks:ReducedMotion}")
            .Replace("{PluginConverter Plugin=DuneMaterials, Converter=MaterialElementConverter}", "{checks:MissingPluginConverter}");
        var view = (Grid)XamlReader.Parse(markup);
        var host = (ContentControl)view.FindName("SteamScreenshots_SteamScreenshotsViewControl");
        var preference = (Border)view.FindName("DuneHeroMediaSelection");
        var layer = (Grid)view.FindName("DuneHeroScreenshotLayer");
        var image = (Image)view.FindName("DuneHeroScreenshotImage");
        var source = (ContentControl)view.FindName("DuneHeroMaterialSourceRegistration");
        var native = (Image)view.FindName("PART_ImageBackground");
        var gallery = (ContentControl)view.FindName("Gallery");
        preference.Tag = "Screenshots"; Drain();
        Require(layer.Visibility == Visibility.Collapsed && ReferenceEquals(source.Tag, native), "Missing screenshot plugin did not retain native Hero");
        var provider = new Provider { Visibility = Visibility.Collapsed }; host.Content = provider; Drain();
        Require(layer.Visibility == Visibility.Collapsed, "Unready screenshot provider became visible");
        provider.Screenshots.Add(new object()); provider.Screenshots.Add(new object());
        provider.CurrentImageBitmap = Picture(640, 360); provider.Visibility = Visibility.Visible; Drain();
        Require(layer.Visibility == Visibility.Visible && ReferenceEquals(image.Source, provider.CurrentImageBitmap) && ReferenceEquals(source.Tag, image), "Ready screenshot did not select Hero image/material source");
        using (var adapter = new DuneMaterials.Rendering.NativeSourceAdapter(image, delegate { }))
        {
            Require(adapter.Frame != null && ReferenceEquals(adapter.Frame.Bitmap, provider.CurrentImageBitmap), "Explicit Hero screenshot source was deferred to a later dispatcher frame");
        }
        provider.Screenshots.RemoveAt(1); Drain();
        Require(layer.Visibility == Visibility.Visible, "Single screenshot should keep Hero visible");
        provider.Screenshots.Add(new object()); Drain();
        gallery.ApplyTemplate(); Drain();
        Require(ReferenceEquals(LogicalTreeHelper.GetParent(provider), host) && gallery.Content == null, "Gallery reparented the native provider");
        var galleryRoot = (FrameworkElement)VisualTreeHelper.GetChild(gallery, 0);
        Require(ReferenceEquals(galleryRoot.DataContext, provider), "Gallery did not share the provider's data");
        preference.Tag = "Background"; Drain();
        Require(layer.Visibility == Visibility.Collapsed && ReferenceEquals(source.Tag, native), "Preference did not restore original Hero source");
        preference.Tag = "Screenshots"; provider.CurrentImageBitmap = Picture(160, 90); Drain();
        Require(layer.Visibility == Visibility.Collapsed, "Transparent download-failure placeholder replaced native Hero");
        provider.CurrentImageBitmap = Picture(640, 360); Drain();
        ((Grid)view.FindName("GameVideo")).Visibility = Visibility.Visible; Drain();
        provider.CurrentImageBitmap = null; provider.Visibility = Visibility.Collapsed; provider.Screenshots.Clear(); Drain();
        Require(layer.Visibility == Visibility.Collapsed && image.Source == null && ReferenceEquals(source.Tag, native), "Cleared game context retained previous screenshot");
        host.Content = null; Drain();
        Require(layer.Visibility == Visibility.Collapsed, "Removing the plugin provider broke fallback");
        Console.WriteLine("PASS: actual Hero/gallery fragments without extension DLLs: absent/unready/ready media, shared provider, native commands, preference, placeholder, video and context-clear fallbacks. No rendering performed.");
    }
}

namespace MaterialTests
{
    public sealed class ReducedMotionExtension : MarkupExtension
    {
        public override object ProvideValue(IServiceProvider provider) { return new Binding { Source = true }; }
    }
}
