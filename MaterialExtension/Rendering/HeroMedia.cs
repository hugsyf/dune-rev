using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DuneMaterials.Rendering
{
    // Media ownership stays with the native plugins. This controller owns only
    // selection, thumbnail presentation and one slideshow timer per loaded Hero.
    public sealed class HeroMediaConverter : IValueConverter
    {
        readonly Func<bool> readAuto;
        readonly Action<bool> writeAuto;
        public HeroMediaConverter(Func<bool> read, Action<bool> write) { readAuto = read; writeAuto = write; }
        public object Convert(object value, Type type, object parameter, CultureInfo culture)
        { var host = value as ContentControl; return host == null ? null : new HeroMediaBar(host, readAuto, writeAuto); }
        public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) { return Binding.DoNothing; }
    }

    public sealed class HeroMediaBar : StackPanel, INotifyPropertyChanged
    {
        readonly ContentControl host;
        readonly Func<bool> readAuto;
        readonly Action<bool> writeAuto;
        readonly DispatcherTimer slideshow;
        readonly Button backgroundButton, screenshotButton, videoButton, previousButton, nextButton, playButton, pauseButton, muteButton;
        readonly Image current = new Image(), previous = new Image(), next = new Image(), background = new Image();
        readonly VideoThumbnail videoThumbnail = new VideoThumbnail();
        readonly ToggleButton autoButton;
        readonly Grid screenshotFrame;
        readonly StackPanel mediaTiles, mediaControls;
        ContentControl screenshots, video;
        FrameworkElement hero;
        FrameworkElement nativePreview;
        MediaElement nativePlayer;
        NativeSourceAdapter backgroundSource;
        ImageSource backgroundImage;
        bool videoReady;
        object previewOwner, previousVisibility;
        ScrollViewer scroll;
        Window window;
        bool attached, pending, manualSelection, manualPlayback, auto, updating, wasEligible;
        object gameId, lastVideoSource, failedVideoSource;
        object previousItem, nextItem;
        bool loadingThumbnails, stepping;
        int generation;
        string selected = "Background";
        public event PropertyChangedEventHandler PropertyChanged;
        public string Selected { get { return selected; } }
        public ImageSource PosterImage { get { return HasScreenshots ? Read(ShotImageProperty) as ImageSource : backgroundImage; } }
        public bool VideoReady { get { return videoReady; } }
        internal bool HasScreenshots { get { return Read(ShotImageProperty) is BitmapSource && (int)Read(ShotCountProperty) > 0 && (Visibility)Read(ShotVisibilityProperty) == Visibility.Visible && !Placeholder(Read(ShotImageProperty) as BitmapSource); } }
        internal bool HasVideo { get { return Read(VideoSourceProperty) != null && !Equals(Read(VideoSourceProperty), failedVideoSource); } }
        static bool Placeholder(BitmapSource bitmap) { return bitmap != null && bitmap.PixelWidth == 160 && bitmap.PixelHeight == 90; }
        object Read(DependencyProperty property) { return GetValue(property); }
        static DependencyProperty Observe(string name, Type type, object initial)
        { return DependencyProperty.Register(name, type, typeof(HeroMediaBar), new PropertyMetadata(initial, delegate(DependencyObject target, DependencyPropertyChangedEventArgs args) { ((HeroMediaBar)target).Queue(); })); }
        internal static readonly DependencyProperty ShotImageProperty = Observe("ShotImage", typeof(BitmapSource), null);
        internal static readonly DependencyProperty ShotCountProperty = Observe("ShotCount", typeof(int), 0);
        internal static readonly DependencyProperty ShotSelectionProperty = Observe("ShotSelection", typeof(object), null);
        internal static readonly DependencyProperty ShotVisibilityProperty = Observe("ShotVisibility", typeof(Visibility), Visibility.Collapsed);
        internal static readonly DependencyProperty VideoSourceProperty = Observe("VideoSource", typeof(object), null);
        internal static readonly DependencyProperty VideoPlayingProperty = Observe("VideoPlaying", typeof(bool), false);
        internal static readonly DependencyProperty GameIdProperty = Observe("GameId", typeof(object), null);
        internal static readonly DependencyProperty MutedProperty = Observe("Muted", typeof(bool), true);

        public HeroMediaBar(ContentControl owner, Func<bool> read, Action<bool> write)
        {
            host = owner; readAuto = read; writeAuto = write;
            Orientation = Orientation.Vertical; VerticalAlignment = VerticalAlignment.Bottom;
            mediaTiles = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            mediaControls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Height = 32, Margin = new Thickness(0, 0, 0, 8) };
            Children.Add(mediaControls); Children.Add(mediaTiles);
            foreach (var image in new[] { current, previous, next, background }) {
                image.Stretch = Stretch.UniformToFill;
                image.ClipToBounds = true;
                image.SizeChanged += delegate(object sender, SizeChangedEventArgs args) {
                    var target = (Image)sender; target.Clip = new RectangleGeometry(new Rect(target.RenderSize), 3, 3);
                };
            }
            backgroundButton = Tile("背景 / Background", 96, delegate { Select("Background"); });
            background.Width = 90; background.Height = 50;
            backgroundButton.Content = background;
            mediaTiles.Children.Add(backgroundButton);
            screenshotFrame = new Grid { Width = 128, Height = 56, Margin = new Thickness(8, 0, 0, 0) };
            previousButton = Preview(previous, true, delegate { Step(false); });
            nextButton = Preview(next, false, delegate { Step(true); });
            screenshotFrame.Children.Add(previousButton); screenshotFrame.Children.Add(nextButton);
            screenshotButton = Tile("商店截图 / Store screenshots", 96, delegate { Select("Screenshots"); });
            current.Width = 90; current.Height = 50;
            screenshotButton.HorizontalAlignment = HorizontalAlignment.Center;
            screenshotButton.Content = current; current.Stretch = Stretch.UniformToFill;
            screenshotFrame.Children.Add(screenshotButton); mediaTiles.Children.Add(screenshotFrame);
            videoButton = Tile("视频 / Video", 96, delegate { Select("Video"); });
            videoButton.Content = videoThumbnail;
            videoButton.Margin = new Thickness(8, 0, 0, 0); mediaTiles.Children.Add(videoButton);
            playButton = Tile("播放视频 / Play video", 32, delegate {
                manualPlayback = !(bool)Read(VideoPlayingProperty);
                if (manualPlayback) { if (Eligible()) Execute(video, "VideoPlayCommand"); }
                else { SetAuto(false); Execute(video, "VideoPauseCommand"); }
            });
            playButton.Content = "\uE768"; playButton.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"); playButton.Height = 32; playButton.FontSize = 16;
            playButton.HorizontalAlignment = HorizontalAlignment.Center; playButton.VerticalAlignment = VerticalAlignment.Center;
            playButton.HorizontalContentAlignment = HorizontalAlignment.Center; playButton.VerticalContentAlignment = VerticalAlignment.Center;
            pauseButton = Tile("暂停视频 / Pause video", 32, delegate { SetAuto(false); Execute(video, "VideoPauseCommand"); });
            pauseButton.Content = "\uE769"; pauseButton.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"); pauseButton.FontSize = 16; pauseButton.Height = 32;
            pauseButton.HorizontalContentAlignment = HorizontalAlignment.Center; pauseButton.VerticalContentAlignment = VerticalAlignment.Center;
            mediaControls.Children.Add(playButton); mediaControls.Children.Add(pauseButton);
            muteButton = Tile("静音 / 取消静音 · Mute / Unmute", 32, delegate { Set(video == null ? null : video.Content, "IsPlayerMuted", !(bool)Read(MutedProperty)); });
            muteButton.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            muteButton.Height = 32; muteButton.FontSize = 16; muteButton.HorizontalAlignment = HorizontalAlignment.Right; muteButton.VerticalAlignment = VerticalAlignment.Center;
            muteButton.Margin = new Thickness(8, 0, 0, 0); mediaControls.Children.Add(muteButton);
            muteButton.HorizontalContentAlignment = HorizontalAlignment.Center; muteButton.VerticalContentAlignment = VerticalAlignment.Center;
            autoButton = new ToggleButton { Content = "\uE8EE", Width = 32, FontSize = 16, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), Padding = new Thickness(0), Height = 32, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "自动轮播截图 / 自动播放视频 · Auto slideshow / video" };
            autoButton.HorizontalContentAlignment = HorizontalAlignment.Center; autoButton.VerticalContentAlignment = VerticalAlignment.Center;
            autoButton.Uid = "Dune.Material.Hero";
            autoButton.SetResourceReference(FrameworkElement.TagProperty, "DuneMaterialsEnabled");
            AutomationProperties.SetName(autoButton, "自动轮播与播放 / Automatic slideshow and playback");
            autoButton.Checked += delegate { if (!updating) SetAuto(true); };
            autoButton.Unchecked += delegate { if (!updating) SetAuto(false); };
            mediaControls.Children.Add(autoButton);
            slideshow = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(6) };
            slideshow.Tick += delegate { Step(true); };
            MouseEnter += delegate { UpdatePlayback(); }; MouseLeave += delegate { UpdatePlayback(); };
            Loaded += Attach; Unloaded += Detach;
        }
        static Button Tile(string title, double width, Action action)
        {
            var button = new Button { Width = width, Height = 56, Padding = new Thickness(2), ToolTip = title, Background = new SolidColorBrush(Color.FromArgb(100, 18, 24, 32)), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), VerticalAlignment = VerticalAlignment.Center };
            button.SetResourceReference(FrameworkElement.StyleProperty, "DuneIconActionButton");
            button.FontFamily = new FontFamily("Segoe UI");
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.VerticalContentAlignment = VerticalAlignment.Stretch;
            AutomationProperties.SetName(button, title); button.Click += delegate { action(); }; return button;
        }
        static Button Preview(Image image, bool left, Action action)
        {
            image.Stretch = Stretch.UniformToFill;
            image.OpacityMask = new LinearGradientBrush(left ? Colors.Transparent : Colors.White, left ? Colors.White : Colors.Transparent, new Point(0, .5), new Point(1, .5));
            var button = Tile(left ? "上一张截图 / Previous screenshot" : "下一张截图 / Next screenshot", 44, action);
            image.Width = 38; image.Height = 36;
            button.Height = 42; button.Content = image; button.Opacity = .9;
            button.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            button.VerticalAlignment = VerticalAlignment.Center;
            button.RenderTransformOrigin = new Point(.5, .5); button.RenderTransform = new SkewTransform(0, left ? -3 : 3);
            return button;
        }
        void Watch(DependencyProperty property, object source, string path)
        { BindingOperations.SetBinding(this, property, new Binding(path) { Source = source, Mode = BindingMode.OneWay }); }
        FrameworkElement Find(string name)
        {
            var control = host.TemplatedParent as Control;
            return control == null || control.Template == null ? null : control.Template.FindName(name, control) as FrameworkElement;
        }
        void Attach(object sender, RoutedEventArgs args)
        {
            if (attached) return; attached = true;
            screenshots = Find("SteamScreenshots_SteamScreenshotsViewControl") as ContentControl;
            video = Find("ExtraMetadataLoader_VideoLoaderControl") as ContentControl;
            hero = Find("HeroBanner"); scroll = Find("PART_ScrollViewHost") as ScrollViewer; window = Window.GetWindow(host);
            var artwork = Find("PART_ImageBackground");
            if (artwork != null) { backgroundSource = new NativeSourceAdapter(artwork, UpdateBackground); UpdateBackground(); }
            auto = readAuto != null && readAuto();
            Watch(GameIdProperty, host, "DataContext.Game.Id");
            if (screenshots != null) {
                Watch(ShotImageProperty, screenshots, "Content.CurrentImageBitmap"); Watch(ShotCountProperty, screenshots, "Content.Screenshots.Count");
                Watch(ShotSelectionProperty, screenshots, "Content.SelectedScreenshot"); Watch(ShotVisibilityProperty, screenshots, "Content.Visibility");
            }
            if (video != null) {
                Watch(VideoSourceProperty, video, "Content.VideoSource"); Watch(VideoPlayingProperty, video, "Content.SettingsModel.Settings.IsVideoPlaying"); Watch(MutedProperty, video, "Content.IsPlayerMuted");
                video.AddHandler(MediaElement.MediaFailedEvent, new EventHandler<ExceptionRoutedEventArgs>(VideoFailed), true);
            }
            if (scroll != null) scroll.ScrollChanged += Scrolled;
            if (window != null) { window.Activated += Activity; window.Deactivated += Activity; window.StateChanged += Activity; }
            if (hero != null) { hero.IsVisibleChanged += VisibilityChanged; hero.SizeChanged += Resized; }
            Refresh();
        }
        void Detach(object sender, RoutedEventArgs args)
        {
            attached = false; generation++; slideshow.Stop(); Execute(video, "VideoPauseCommand");
            if (video != null) video.RemoveHandler(MediaElement.MediaFailedEvent, new EventHandler<ExceptionRoutedEventArgs>(VideoFailed));
            if (scroll != null) scroll.ScrollChanged -= Scrolled;
            if (window != null) { window.Activated -= Activity; window.Deactivated -= Activity; window.StateChanged -= Activity; }
            if (hero != null) { hero.IsVisibleChanged -= VisibilityChanged; hero.SizeChanged -= Resized; }
            RestorePreview();
            if (backgroundSource != null) backgroundSource.Dispose(); backgroundSource = null; backgroundImage = null;
            BindingOperations.ClearAllBindings(this);
            background.Source = null; videoThumbnail.Dispose();
            current.Source = previous.Source = next.Source = null;
            screenshots = video = null; hero = null; scroll = null; window = null;
            gameId = lastVideoSource = failedVideoSource = null; manualSelection = manualPlayback = wasEligible = false;
            previousItem = nextItem = null;
        }
        void Activity(object sender, EventArgs args) { UpdatePlayback(); }
        void Scrolled(object sender, ScrollChangedEventArgs args) { UpdatePlayback(); }
        void Resized(object sender, SizeChangedEventArgs args) { UpdatePlayback(); }
        void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) { UpdatePlayback(); }
        internal void VideoFailed(object sender, ExceptionRoutedEventArgs args)
        { failedVideoSource = Read(VideoSourceProperty); videoReady = false; Notify("VideoReady"); manualPlayback = false; Refresh(); }
        void UpdateBackground()
        {
            backgroundImage = backgroundSource == null || backgroundSource.Frame == null ? null : backgroundSource.Frame.Bitmap;
            background.Source = backgroundImage; Notify("PosterImage");
        }
        void Notify(string property) { var handler = PropertyChanged; if (handler != null) handler(this, new PropertyChangedEventArgs(property)); }
        void Queue()
        {
            if (!attached || pending) return; pending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate { pending = false; if (attached) Refresh(); }));
        }
        internal void Refresh()
        {
            PreparePreview();
            var id = Read(GameIdProperty);
            if (!Equals(gameId, id)) { gameId = id; generation++; failedVideoSource = null; manualSelection = manualPlayback = wasEligible = false; }
            bool changedSource = !Equals(lastVideoSource, Read(VideoSourceProperty));
            if (changedSource) {
                lastVideoSource = Read(VideoSourceProperty); manualPlayback = wasEligible = false;
                videoThumbnail.Update(lastVideoSource as Uri);
                failedVideoSource = null;
                videoReady = false; Notify("VideoReady");
                // Pause also initializes the first frame of EML's manual MediaElement.
                // Do not toggle EML's global AutoPlayVideos preference.
                PausePreview(video);
                if (auto) Set(video == null ? null : video.Content, "IsPlayerMuted", true);
            }
            if (!manualSelection || (selected == "Video" && !HasVideo) || (selected == "Screenshots" && !HasScreenshots))
                SetSelected(HasVideo ? "Video" : HasScreenshots ? "Screenshots" : "Background");
            screenshotFrame.Visibility = HasScreenshots ? Visibility.Visible : Visibility.Collapsed;
            videoButton.Visibility = HasVideo ? Visibility.Visible : Visibility.Collapsed;
            playButton.Visibility = selected == "Video" && HasVideo && !(bool)Read(VideoPlayingProperty) ? Visibility.Visible : Visibility.Collapsed;
            // Controls occupy a separate row so selection never shifts the media tiles.
            mediaControls.Visibility = HasScreenshots || HasVideo ? Visibility.Visible : Visibility.Collapsed;
            muteButton.Visibility = selected == "Video" && HasVideo ? Visibility.Visible : Visibility.Collapsed;
            muteButton.Content = (bool)Read(MutedProperty) ? "\uE74F" : "\uE767";
            pauseButton.Visibility = selected == "Video" && HasVideo && (bool)Read(VideoPlayingProperty) ? Visibility.Visible : Visibility.Collapsed;
            autoButton.Visibility = HasScreenshots || HasVideo ? Visibility.Visible : Visibility.Collapsed;
            autoButton.IsEnabled = selected != "Background";
            updating = true; autoButton.IsChecked = auto; updating = false;
            autoButton.SetResourceReference(Control.ForegroundProperty, auto ? "AccentBrush" : "TextBrush");
            string status = auto ? "已开启 / On" : "已关闭 / Off";
            autoButton.ToolTip = selected == "Background" ? "背景图片无需轮播 / Background is static" :
                (selected == "Video" ? "自动播放视频 / Automatic video playback · " : "自动轮播截图 / Automatic screenshot slideshow · ") + status;
            AutomationProperties.SetHelpText(autoButton, (string)autoButton.ToolTip);
            var edge = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
            backgroundButton.BorderBrush = selected == "Background" ? Brushes.White : edge;
            screenshotButton.BorderBrush = selected == "Screenshots" ? Brushes.White : edge;
            videoButton.BorderBrush = selected == "Video" ? Brushes.White : edge;
            Notify("PosterImage");
            UpdateThumbnails(); UpdatePlayback();
        }
        internal void Select(string value)
        {
            if (value == "Video" && !HasVideo || value == "Screenshots" && !HasScreenshots) return;
            manualSelection = true; manualPlayback = false; wasEligible = false; SetSelected(value); Refresh();
        }
        void SetSelected(string value)
        {
            if (selected == value) return;
            Execute(video, "VideoPauseCommand"); selected = value; wasEligible = false;
            var handler = PropertyChanged; if (handler != null) handler(this, new PropertyChangedEventArgs("Selected"));
        }
        void SetAuto(bool enabled)
        {
            auto = enabled; manualPlayback = false; wasEligible = false;
            if (enabled) Set(video == null ? null : video.Content, "IsPlayerMuted", true);
            if (writeAuto != null) writeAuto(enabled); Refresh();
        }
        void Step(bool forward)
        {
            if (!HasScreenshots || (int)Read(ShotCountProperty) < 2 || stepping || !attached) return;
            Select("Screenshots");
            var provider = screenshots == null ? null : screenshots.Content;
            var items = Get(provider, "Screenshots") as IList;
            var selection = Read(ShotSelectionProperty); int index = items == null ? -1 : items.IndexOf(selection);
            if (index < 0) return;
            var target = items[(index + (forward ? 1 : items.Count - 1)) % items.Count];
            int version = generation; stepping = true; slideshow.Stop();
            // SteamScreenshots' lazy FullImage getter
            // can download synchronously; warm exactly the requested image off UI.
            Task.Run(delegate { Get(target, "FullImage"); }).ContinueWith(delegate(Task task) {
                Dispatcher.BeginInvoke(new Action(delegate {
                    stepping = false;
                    if (attached && generation == version && selected == "Screenshots" && screenshots.Content == provider && ReferenceEquals(Read(ShotSelectionProperty), selection))
                        Execute(screenshots, forward ? "SelectNextScreenshotCommand" : "SelectPreviousScreenshotCommand");
                    UpdatePlayback();
                }));
            });
        }
        void UpdateThumbnails()
        {
            var items = Get(screenshots == null ? null : screenshots.Content, "Screenshots") as IList;
            var item = Read(ShotSelectionProperty); int count = items == null ? 0 : items.Count;
            current.Source = HasScreenshots ? Read(ShotImageProperty) as ImageSource : null;
            int index = items == null ? -1 : items.IndexOf(item);
            previousButton.Visibility = index >= 0 && count > 2 ? Visibility.Visible : Visibility.Collapsed;
            nextButton.Visibility = index >= 0 && count > 1 ? Visibility.Visible : Visibility.Collapsed;
            object left = index >= 0 && count > 2 ? items[(index + count - 1) % count] : null;
            object right = index >= 0 && count > 1 ? items[(index + 1) % count] : null;
            if (ReferenceEquals(previousItem, left) && ReferenceEquals(nextItem, right)) return;
            if (loadingThumbnails) return;
            previousItem = left; nextItem = right; previous.Source = next.Source = null;
            loadingThumbnails = true; int version = generation;
            Task.Run(delegate {
                var a = Get(left, "ThumbnailImage") as BitmapSource;
                var b = Get(right, "ThumbnailImage") as BitmapSource;
                // Native plugin images are frozen. Never transfer mutable WPF objects.
                return new BitmapSource[] { a != null && a.IsFrozen ? a : null, b != null && b.IsFrozen ? b : null };
            }).ContinueWith(delegate(Task<BitmapSource[]> task) {
                Dispatcher.BeginInvoke(new Action(delegate {
                    loadingThumbnails = false;
                    if (!attached || generation != version) { previousItem = nextItem = null; if (attached) UpdateThumbnails(); return; }
                    if (task.Status == TaskStatus.RanToCompletion) { previous.Source = task.Result[0]; next.Source = task.Result[1]; }
                    UpdateThumbnails();
                }));
            });
        }
        bool Eligible()
        {
            if (!attached || hero == null || !hero.IsVisible || window == null || !window.IsActive || window.WindowState == WindowState.Minimized) return false;
            if (scroll == null) return true;
            try {
                var bounds = hero.TransformToAncestor(scroll).TransformBounds(new Rect(hero.RenderSize));
                return bounds.IntersectsWith(new Rect(0, 0, scroll.ViewportWidth, scroll.ViewportHeight));
            } catch (InvalidOperationException) { return false; }
        }
        void UpdatePlayback()
        {
            bool eligible = Eligible();
            bool run = eligible && HasVideo && selected == "Video" && (auto || manualPlayback);
            if (!run) Execute(video, "VideoPauseCommand");
            else if (!wasEligible || auto) Execute(video, "VideoPlayCommand");
            wasEligible = run;
            bool cycle = eligible && auto && selected == "Screenshots" && HasScreenshots && (int)Read(ShotCountProperty) > 1 && !IsMouseOver && !stepping;
            if (cycle) { if (!slideshow.IsEnabled) slideshow.Start(); } else slideshow.Stop();
        }
        static object Get(object target, string property)
        {
            if (target == null) return null;
            var info = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
            if (info == null) return null;
            try { return info.GetValue(target, null); } catch (TargetInvocationException) { return null; }
        }
        static void Execute(ContentControl target, string property)
        {
            var command = Get(target == null ? null : target.Content, property) as ICommand;
            if (command != null && command.CanExecute(null)) command.Execute(null);
        }
        static void Set(object target, string property, bool value)
        {
            if (target == null) return;
            var info = target.GetType().GetProperty(property);
            if (info != null && info.CanWrite && info.PropertyType == typeof(bool)) info.SetValue(target, value, null);
        }
        static void PausePreview(ContentControl target)
        {
            object content = target == null ? null : target.Content;
            var method = content == null ? null : content.GetType().GetMethod("MediaPause", Type.EmptyTypes);
            try { if (method != null) method.Invoke(content, null); else Execute(target, "VideoPauseCommand"); }
            catch (TargetInvocationException) { /* A failed decoder must not prevent the theme from loading. */ }
        }
        void PreparePreview()
        {
            object content = video == null ? null : video.Content;
            if (ReferenceEquals(previewOwner, content)) return;
            RestorePreview();
            var control = content as FrameworkElement;
            var player = control == null ? null : control.FindName("player") as MediaElement;
            nativePreview = player == null ? null : player.Parent as FrameworkElement;
            if (nativePreview == null) return;
            previewOwner = content;
            nativePlayer = player; nativePlayer.MediaOpened += MediaOpened;
            // EML can hide its paused-preview Grid from a global preference.
            // This explicitly owned Hero needs a preview independent of that setting.
            previousVisibility = nativePreview.ReadLocalValue(UIElement.VisibilityProperty);
            nativePreview.SetValue(UIElement.VisibilityProperty, Visibility.Visible);
        }
        void RestorePreview()
        {
            if (nativePlayer != null) nativePlayer.MediaOpened -= MediaOpened; nativePlayer = null;
            if (nativePreview != null) {
                var expression = previousVisibility as BindingExpressionBase;
                if (expression != null) BindingOperations.SetBinding(nativePreview, UIElement.VisibilityProperty, expression.ParentBindingBase);
                else if (previousVisibility == DependencyProperty.UnsetValue) nativePreview.ClearValue(UIElement.VisibilityProperty);
                else nativePreview.SetValue(UIElement.VisibilityProperty, previousVisibility);
            }
            nativePreview = null; previewOwner = previousVisibility = null;
        }
        void MediaOpened(object sender, RoutedEventArgs args)
        { videoReady = true; Notify("VideoReady"); UpdatePlayback(); }
    }
}
