using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading;
using System.IO;
using System.Xml;
using DuneMaterials.Rendering;

internal static class HeroMediaChecks
{
    public sealed class Screenshot
    {
        readonly int owner = Thread.CurrentThread.ManagedThreadId;
        public BitmapSource Bitmap;
        public bool FullImageLoadedOffUi;
        public BitmapSource ThumbnailImage { get { return Bitmap; } }
        public BitmapSource FullImage { get { FullImageLoadedOffUi = owner != Thread.CurrentThread.ManagedThreadId; return Bitmap; } }
    }
    public sealed class Command : ICommand
    {
        readonly Action action;
        public Command(Action callback) { action = callback; }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter) { action(); }
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
    public sealed class Model : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void Changed(string property) { var handler = PropertyChanged; if (handler != null) handler(this, new PropertyChangedEventArgs(property)); }
        public Model SettingsModel { get { return this; } }
        public Model Settings { get { return this; } }
        public Model Game { get { return this; } }
        public object Id { get; set; }
        public BitmapSource CurrentImageBitmap { get; set; }
        public Visibility Visibility { get; set; }
        public ObservableCollection<object> Screenshots { get; private set; }
        public object SelectedScreenshot { get; set; }
        public object VideoSource { get; set; }
        public bool IsVideoPlaying { get; set; }
        public bool IsPlayerMuted { get; set; }
        public ICommand VideoPauseCommand { get; private set; }
        public ICommand VideoPlayCommand { get; private set; }
        public ICommand SelectNextScreenshotCommand { get; private set; }
        public ICommand SelectPreviousScreenshotCommand { get; private set; }
        public int Pauses, Plays, Steps;
        public void MediaPause() { Pauses++; IsVideoPlaying = false; Changed("IsVideoPlaying"); }
        public Model()
        {
            Screenshots = new ObservableCollection<object>();
            VideoPauseCommand = new Command(delegate { if (IsVideoPlaying) MediaPause(); });
            VideoPlayCommand = new Command(delegate { Plays++; IsVideoPlaying = true; Changed("IsVideoPlaying"); });
            SelectNextScreenshotCommand = new Command(delegate { Step(1); });
            SelectPreviousScreenshotCommand = new Command(delegate { Step(Screenshots.Count - 1); });
        }
        void Step(int offset) {
            Steps++; SelectedScreenshot = Screenshots[(Screenshots.IndexOf(SelectedScreenshot) + offset) % Screenshots.Count];
            Changed("SelectedScreenshot");
        }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate { frame.Continue = false; }));
        Dispatcher.PushFrame(frame);
    }
    static double LayoutWidth(HeroMediaBar bar)
    {
        bar.Measure(new Size(1000, 100)); bar.Arrange(new Rect(bar.DesiredSize));
        return bar.DesiredSize.Width;
    }
    public static void Run()
    {
        var template = (ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Control'>
            <Grid><Grid x:Name='HeroBanner'><Image x:Name='PART_ImageBackground'/></Grid>
            <ContentControl x:Name='SteamScreenshots_SteamScreenshotsViewControl'/>
            <ContentControl x:Name='ExtraMetadataLoader_VideoLoaderControl'/>
            <ContentControl x:Name='DuneHeroMedia'/></Grid></ControlTemplate>");
        var context = new Model { Id = Guid.NewGuid() };
        var view = new Control { Template = template, DataContext = context }; view.ApplyTemplate();
        var host = (ContentControl)template.FindName("DuneHeroMedia", view);
        bool persisted = false;
        var bar = new HeroMediaBar(host, delegate { return persisted; }, delegate(bool enabled) { persisted = enabled; }); host.Content = bar;
        bar.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Drain();
        var controls = (StackPanel)bar.Children[0];
        var tiles = (StackPanel)bar.Children[1];
        var toggle = (ToggleButton)controls.Children[3];
        Require(bar.Selected == "Background" && toggle.Visibility == Visibility.Collapsed && !persisted, "Background-only game exposed automatic controls");
        var provider = new Model { Visibility = Visibility.Visible };
        var bitmap = BitmapSource.Create(320, 180, 96, 96, PixelFormats.Bgra32, null, new byte[320 * 180 * 4], 320 * 4); bitmap.Freeze();
        var nativeArtwork = (Image)template.FindName("PART_ImageBackground", view);
        nativeArtwork.Opacity = 0; nativeArtwork.OpacityMask = Brushes.Transparent; nativeArtwork.Source = bitmap; Drain();
        var thumbnail = (Image)((Button)tiles.Children[0]).Content;
        Require(ReferenceEquals(thumbnail.Source, bitmap) && thumbnail.Width > 0 && thumbnail.Height > 0, "Background thumbnail copied masked pixels or measured to zero");
        Require(((Button)tiles.Children[0]).Height == 56 && ((Button)((Grid)tiles.Children[1]).Children[2]).Height == 56 && ((Button)tiles.Children[2]).Height == 56, "Media tiles have inconsistent heights");
        toggle.Visibility = Visibility.Visible; toggle.Measure(new Size(500, 100));
        Require((string)toggle.Content == "\uE8EE" && toggle.Width == 32 && toggle.Height == 32 && toggle.FontSize == 16 && toggle.FontFamily.Source.Contains("Segoe Fluent Icons"), "Automatic playback does not use the shared Fluent icon control size");
        toggle.Visibility = Visibility.Collapsed;
        var hero = (Grid)template.FindName("HeroBanner", view);
        Require(hero.Children.Count == 1, "Playback button was injected into the Hero instead of the controls row");
        var first = new Screenshot { Bitmap = bitmap }; var second = new Screenshot { Bitmap = bitmap };
        provider.CurrentImageBitmap = bitmap; provider.Screenshots.Add(first); provider.SelectedScreenshot = first;
        ((ContentControl)template.FindName("SteamScreenshots_SteamScreenshotsViewControl", view)).Content = provider; Drain();
        Require(bar.Selected == "Screenshots" && toggle.Visibility == Visibility.Visible && toggle.IsChecked == false, "Ready screenshots did not become default with automatic mode off");
        var frame = (Grid)tiles.Children[1];
        Require(frame.Width == 128 && ((Button)frame.Children[0]).Width == 44 && (frame.Width - ((Button)frame.Children[2]).Width) / 2 == 16, "Screenshot neighbors still extend too far beyond the center tile");
        Require(frame.Children[0].Visibility == Visibility.Collapsed && frame.Children[1].Visibility == Visibility.Collapsed, "Single screenshot exposed side previews");
        provider.Screenshots.Add(second); Drain();
        Require(frame.Children[0].Visibility == Visibility.Collapsed && frame.Children[1].Visibility == Visibility.Visible, "Two screenshots duplicated the same side preview");
        provider.Screenshots.Add(new Screenshot { Bitmap = bitmap }); Drain();
        Require(frame.Children[0].Visibility == Visibility.Visible && frame.Children[1].Visibility == Visibility.Visible, "Three screenshots did not expose both side previews");
        ((Button)frame.Children[1]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (provider.Steps == 0 && DateTime.UtcNow < deadline) { Drain(); Thread.Sleep(5); }
        Drain();
        Require(provider.Steps == 1 && ReferenceEquals(provider.SelectedScreenshot, second) && second.FullImageLoadedOffUi, "Requested image was not prepared off UI before the native next command");
        context.Id = Guid.NewGuid(); context.Changed("Id"); Drain();
        var movie = new Model { VideoSource = new Uri("file:///trailer.mp4"), IsVideoPlaying = true };
        ((ContentControl)template.FindName("ExtraMetadataLoader_VideoLoaderControl", view)).Content = movie; Drain();
        Require(bar.Selected == "Video" && !movie.IsVideoPlaying && movie.Plays == 0 && movie.Pauses > 0, "Video priority unexpectedly started playback");
        var videoThumbnail = ((Button)tiles.Children[2]).Content as VideoThumbnail;
        Require(videoThumbnail != null && videoThumbnail.Frame == null, "Unavailable video preview borrowed the screenshot instead of using its own fallback");
        provider.Changed("CurrentImageBitmap"); Drain();
        Require(videoThumbnail.Frame == null, "Screenshot updates replaced the independent video preview");
        bar.VideoFailed(null, null); Drain();
        Require(bar.Selected == "Screenshots" && !bar.HasVideo, "Decoder failure did not fall back to screenshots");
        movie.VideoSource = new Uri("file:///valid-trailer.mp4"); movie.Changed("VideoSource"); Drain();
        Require(bar.Selected == "Video", "A replacement video source retained the old failure state");
        double videoWidth = LayoutWidth(bar);
        movie.IsPlayerMuted = true; movie.Changed("IsPlayerMuted"); Drain();
        Require((string)((Button)controls.Children[2]).Content == "\uE74F", "Muted video lost its volume icon");
        movie.IsPlayerMuted = false; movie.Changed("IsPlayerMuted"); Drain();
        Require((string)((Button)controls.Children[2]).Content == "\uE767", "Unmuted video lost its volume icon");
        var tileOrigin = tiles.TranslatePoint(new Point(), bar);
        Require(controls.Visibility == Visibility.Visible && controls.Children[0].Visibility == Visibility.Visible && controls.Children[1].Visibility == Visibility.Collapsed && controls.Children[2].Visibility == Visibility.Visible, "Paused video did not expose play and mute above its tiles");
        Require(tileOrigin.Y >= controls.RenderSize.Height + 8, "Media controls overlap the thumbnail row");
        bar.Select("Background"); Drain();
        Require(LayoutWidth(bar) == videoWidth && tiles.TranslatePoint(new Point(), bar) == tileOrigin && controls.Children[0].Visibility == Visibility.Collapsed && controls.Children[1].Visibility == Visibility.Collapsed && controls.Children[2].Visibility == Visibility.Collapsed, "Switching to background shifted thumbnails or retained video control slots");
        Require(bar.Selected == "Background" && !toggle.IsEnabled && toggle.Visibility == Visibility.Visible, "Background selection did not disable Auto");
        provider.Changed("CurrentImageBitmap"); Drain();
        Require(bar.Selected == "Background", "Late provider update overrode manual background choice");
        bar.Select("Screenshots"); toggle.IsChecked = true; Drain();
        Require(LayoutWidth(bar) == videoWidth && tiles.TranslatePoint(new Point(), bar) == tileOrigin && ((string)toggle.ToolTip).Contains("On"), "Screenshot selection or Auto state changed toolbar width, or did not report its state");
        Require(persisted && movie.Plays == 0, "Automatic preference did not persist or played an inactive video");
        toggle.IsChecked = false; Drain(); Require(!persisted, "Automatic preference did not switch off");
        movie.VideoSource = null; movie.Changed("VideoSource"); Drain();
        provider.CurrentImageBitmap = null; provider.Changed("CurrentImageBitmap"); Drain();
        Require(bar.Selected == "Background" && toggle.Visibility == Visibility.Collapsed, "Unavailable media did not fall back to native artwork");
        context.Id = Guid.NewGuid(); context.Changed("Id");
        provider.CurrentImageBitmap = bitmap; provider.Changed("CurrentImageBitmap"); Drain();
        Require(bar.Selected == "Screenshots", "Game change did not reset manual media selection");
        bar.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Require(hero.Children.Count == 1, "Unloading altered the native Hero children");
        movie.VideoSource = new Uri("file:///other.mp4"); movie.Changed("VideoSource"); Drain();
        Require(bar.Selected == "Screenshots" && movie.Plays == 0, "Unloaded Hero retained provider observers");
        var native = (UserControl)XamlReader.Parse("<UserControl xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Grid Visibility='Hidden'><MediaElement x:Name='player' LoadedBehavior='Manual'/></Grid></UserControl>");
        ((ContentControl)template.FindName("ExtraMetadataLoader_VideoLoaderControl", view)).Content = native;
        var preview = (Grid)native.Content;
        bar.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Drain();
        Require(preview.Visibility == Visibility.Visible, "Hero inherited EML's hidden paused-preview preference");
        bar.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Require(preview.Visibility == Visibility.Hidden, "Native preview visibility was not restored on unload");
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../.."));
        foreach (string file in new[] { "DetailsViewGameOverview.xaml", "GridViewGameOverview.xaml" }) {
            var xml = new XmlDocument(); xml.Load(Path.Combine(root, "Source/Views", file));
            var ns = new XmlNamespaceManager(xml.NameTable); ns.AddNamespace("p", "http://schemas.microsoft.com/winfx/2006/xaml/presentation"); ns.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            var player = (XmlElement)xml.SelectSingleNode("//p:ContentControl[@x:Name='ExtraMetadataLoader_VideoLoaderControl']", ns);
            Require(((XmlElement)player.ParentNode).GetAttribute("Name", "http://schemas.microsoft.com/winfx/2006/xaml") == "GameVideo" && !player.HasAttribute("Width"), "Native video still renders through a hidden thumbnail host");
            Require(xml.SelectSingleNode("//p:VisualBrush[contains(@Visual,'ExtraMetadataLoader_VideoLoaderControl')]", ns) == null, "Hero still copies native video via VisualBrush");
        }
        Console.WriteLine("PASS: Two-row media controls, stable thumbnail positions, Hero media priority, manual choice, native video preview pause, default-off/persisted Auto, missing-media fallback and unloaded bindings. No windows or rendering.");
    }
}
