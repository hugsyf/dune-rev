using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Windows.Markup;
using System.Windows.Data;
using System.Globalization;
using System.IO;
using DuneMaterials.Rendering;

// Windowless algorithm/lifetime checks. No Window, rendering, screenshots or Playnite automation.
internal static class MaterialChecks
{
    sealed class NativeHost : Border
    {
        public static readonly DependencyProperty IsBlurEnabledProperty = DependencyProperty.Register("IsBlurEnabled", typeof(bool), typeof(NativeHost), new PropertyMetadata(true));
        public static readonly DependencyProperty BlurAmountProperty = DependencyProperty.Register("BlurAmount", typeof(double), typeof(NativeHost), new PropertyMetadata(78.0));
        public bool IsBlurEnabled { get { return (bool)GetValue(IsBlurEnabledProperty); } set { SetValue(IsBlurEnabledProperty, value); } }
        public double BlurAmount { get { return (double)GetValue(BlurAmountProperty); } set { SetValue(BlurAmountProperty, value); } }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void CheckScrollSampling()
    {
        var root = new Canvas { Width = 800, Height = 600 };
        var artwork = Bitmap(16, 50);
        var source = new Image { Source = artwork, Width = 800, Height = 600, Stretch = Stretch.Fill };
        var content = new Canvas { Width = 800, Height = 1800 };
        var movement = new TranslateTransform(); content.RenderTransform = movement;
        var localSource = new Image { Source = artwork, Width = 800, Height = 600, Stretch = Stretch.Fill };
        var target = new Border { Width = 300, Height = 100 }; Canvas.SetLeft(target, 40); Canvas.SetTop(target, 240);
        content.Children.Add(localSource); content.Children.Add(target); root.Children.Add(source); root.Children.Add(content);
        root.Measure(new Size(800, 600)); root.Arrange(new Rect(0, 0, 800, 600));
        var scene = new GeometryDrawing(Brushes.Blue, null, new RectangleGeometry(new Rect(0, 0, 800, 600)));
        var globalSample = new DrawingBrush(scene) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = MaterialSession.Map(target, source) };
        var localSample = new DrawingBrush(scene) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = MaterialSession.Map(target, localSource) };
        movement.Y = -120;
        bool globalChanged = MaterialSession.Remap(globalSample, target, source);
        Require(globalChanged && globalSample.Viewbox.Y == 120, "Scrolled surface retained the previous global crop: " + globalChanged + "; " + globalSample.Viewbox);
        Require(!MaterialSession.Remap(localSample, target, localSource) && localSample.Viewbox.Y == 240, "Hero-local artwork drifted when both visuals scrolled together");
        Require(!MaterialSession.Remap(globalSample, target, source), "Unchanged geometry rewrote the crop");
        movement.Y = -250;
        Require(MaterialSession.Remap(globalSample, target, source) && globalSample.Viewbox.Y == -10, "Crossing the viewport edge lost sampling continuity");
        Console.WriteLine("PASS: scroll crop changes synchronously, global artwork stays aligned, Hero-local artwork stays attached and unchanged geometry is reused. No rendering performed.");
    }
    static void CheckOverviewFade()
    {
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../.."));
        string markup = File.ReadAllText(Path.Combine(root, "Source/DerivedStyles/DetailsScrollViewer.xaml"))
            .Replace("<ResourceDictionary ", "<ResourceDictionary xmlns:checks='clr-namespace:MaterialTests;assembly=MaterialChecks' ")
            .Replace("{PluginConverter Plugin=DuneMaterials, Converter=ScrollFadeConverter}", "{checks:ScrollFade}");
        Application.Current.Resources["BaseStyle"] = new Style(typeof(FrameworkElement));
        var dictionary = (ResourceDictionary)XamlReader.Parse(markup);
        var viewer = new ScrollViewer { Style = (Style)dictionary["DetailsScrollViewer"], DataContext = new object(),
            Content = new Border { Height = 1800 }, Width = 400, Height = 600, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        viewer.ApplyTemplate(); viewer.Measure(new Size(400, 600)); viewer.Arrange(new Rect(0, 0, 400, 600)); viewer.UpdateLayout();
        var presenter = (ScrollContentPresenter)viewer.Template.FindName("PART_ScrollContentPresenter", viewer);
        Require(presenter.OpacityMask == null, "Initial Hero edge must remain unmasked");
        viewer.ScrollToVerticalOffset(1);
        Until(delegate { viewer.UpdateLayout(); return viewer.VerticalOffset == 1; });
        Until(delegate { var mask = presenter.OpacityMask as LinearGradientBrush; return mask != null && mask.EndPoint.Y == 1; });
        viewer.ScrollToVerticalOffset(100);
        Until(delegate { viewer.UpdateLayout(); return viewer.VerticalOffset == 100; });
        Until(delegate { return ((LinearGradientBrush)presenter.OpacityMask).EndPoint.Y == 28; });
        var scrollbar = (FrameworkElement)viewer.Template.FindName("PART_VerticalScrollBar", viewer);
        Require(scrollbar.OpacityMask == null, "Scroll edge mask affected the scrollbar");
        viewer.ScrollToTop(); Until(delegate { viewer.UpdateLayout(); return viewer.VerticalOffset == 0; });
        Require(presenter.OpacityMask == null, "Scrolling back to the top did not restore the unmasked Hero");
        Application.Current.Resources.Remove("BaseStyle");
        Console.WriteLine("PASS: actual overview template parses; fade grows from 1 to 28 DIPs and restores at the top, leaving the scrollbar untouched. No rendering performed.");
    }
    static void CheckDeferredFallback()
    {
        const string key = "MaterialLateFallbackRegression";
        string markup = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
            xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
            xmlns:checks='clr-namespace:MaterialTests;assembly=MaterialChecks'>
            <Style x:Key='LateStyle' TargetType='Border'><Setter Property='Background'><Setter.Value>
                <PriorityBinding>
                    <Binding RelativeSource='{RelativeSource Self}' Converter='{checks:MissingPluginConverter}' />
                    <Binding Path='Resources[MaterialLateFallbackRegression]' Source='{x:Static Application.Current}' />
                </PriorityBinding>
            </Setter.Value></Setter></Style></ResourceDictionary>";
        var dictionary = (ResourceDictionary)XamlReader.Parse(markup);
        var style = (Style)dictionary["LateStyle"]; // Parse before the brush exists, matching Playnite's order.
        var lateResources = new ResourceDictionary(); var fallback = new SolidColorBrush(Colors.CadetBlue);
        lateResources[key] = fallback; Application.Current.Resources.MergedDictionaries.Add(lateResources);
        var border = new Border { Style = style };
        Until(delegate { return ReferenceEquals(border.Background, fallback); });
        Application.Current.Resources.MergedDictionaries.Remove(lateResources);
        Console.WriteLine("PASS: style parses before fallback brush registration; missing-plugin priority binding resolves the later merged resource. No rendering performed.");
    }
    static BitmapSource Bitmap(int size, byte colour)
    {
        var pixels = new byte[size * size * 4];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = colour; pixels[i + 1] = 80; pixels[i + 2] = 180; pixels[i + 3] = 255; }
        var image = BitmapSource.Create(size, size, 96, 96, PixelFormats.Pbgra32, null, pixels, size * 4); image.Freeze(); return image;
    }
    static void CheckRealtimeSharing(MaterialScope scope)
    {
        string reason = RealtimeBackend.IneligibleReason(scope.Source.RawScene, true);
        if (reason != null) { Console.WriteLine("SKIP: retained realtime graph check: " + reason); return; }
        using (var backend = new RealtimeBackend())
        {
            var surface = backend.Acquire(scope, BlurLevel.Surface);
            for (int i = 0; i < 100; i++) Require(ReferenceEquals(surface, backend.Acquire(scope, BlurLevel.Surface)), "Realtime consumers rebuilt the same shared node");
            Require(backend.NodeCount == 1 && backend.BuildCount == 1, "Realtime node count grew with consumer count");
            var content = backend.Acquire(scope, BlurLevel.Content);
            var surfaceNode = (Border)((VisualBrush)surface).Visual;
            var contentNode = (Border)((VisualBrush)content).Visual;
            var padded = (VisualBrush)surfaceNode.Background;
            var crop = ((VisualBrush)surface).Viewbox;
            Require(padded.TileMode == TileMode.FlipXY && padded.ViewportUnits == BrushMappingMode.Absolute && crop.X >= ((BlurEffect)surfaceNode.Effect).Radius && crop.Y == crop.X, "Realtime blur edges are not mirrored/padded before cropping");
            Require(surfaceNode.Width <= 1024 && surfaceNode.Height <= 1024 && crop.Right < surfaceNode.Width && crop.Bottom < surfaceNode.Height, "Blur padding exceeded the node budget or leaked into output bounds");
            Require(((BlurEffect)contentNode.Effect).Radius > ((BlurEffect)surfaceNode.Effect).Radius, "Dense content did not get stronger shared blur");
            Require(backend.NodeCount == 2 && backend.BuildCount == 2, "Content level must add exactly one node");
            var image = (Image)scope.Frame.Coordinates;
            image.Width = 900; image.Measure(new Size(900, 600)); image.Arrange(new Rect(0, 0, 900, 600));
            Require(!ReferenceEquals(surface, backend.Acquire(scope, BlurLevel.Surface)) && backend.BuildCount == 3, "Source resizing must rebuild the scoped node once");
            backend.Release(scope); Require(backend.NodeCount == 0, "Detached scope retained realtime nodes");
            image.Width = 800; image.Measure(new Size(800, 600)); image.Arrange(new Rect(0, 0, 800, 600));
            Console.WriteLine("PASS: realtime retained graph shares nodes across 100 consumers, adds one dense-content level, rebuilds on resize and releases on detach. No rendering performed.");
        }
    }
    static void Until(Func<bool> ready)
    {
        var watch = Stopwatch.StartNew();
        while (!ready())
        {
            if (watch.ElapsedMilliseconds > 15000) throw new Exception("Dispatcher/worker timeout");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate { frame.Continue = false; }));
            Dispatcher.PushFrame(frame); Thread.Sleep(1);
        }
    }
    [STAThread] static int Main()
    {
        try
        {
            var application = new Application();
            CheckDeferredFallback();
            CheckScrollSampling(); CheckOverviewFade();
            HeroScreenshotChecks.Run();
            HeroMediaChecks.Run();
            BackdropSceneChecks.Run();
            WindowEdgeChecks.Run();
            var options = new MaterialOptions();
            Require(options.UseNewRenderer && options.Mode == MaterialMode.Realtime && options.AllowExperimentalRealtime && !options.HeroAutoPlay, "New installs must use recommended realtime materials with automatic playback off");
            int notifications = 0, builds = 0;
            var native = new Image { Source = Bitmap(1024, 50), Width = 800, Height = 600, Stretch = Stretch.UniformToFill };
            var nativeRoot = new NativeHost { Child = native, Opacity = .72 };
            nativeRoot.Measure(new Size(800, 600)); nativeRoot.Arrange(new Rect(0, 0, 800, 600));
            var scope = new MaterialScope(new Grid());
            scope.Source = new NativeSourceAdapter(nativeRoot, delegate { notifications++; });
            Until(delegate { return scope.Frame != null; });
            Require(scope.Frame.Bitmap.IsFrozen && ReferenceEquals(scope.Frame.Bitmap, native.Source), "Native adapter must retain the raw frozen artwork, not rendered pixels");
            Require(ReferenceEquals(((Image)scope.Source.RawScene).Source, native.Source), "Realtime scene must use the same raw artwork");
            CheckRealtimeSharing(scope);
            using (var cache = new CachedBackend(Dispatcher.CurrentDispatcher, delegate { }, delegate(string message) { if (message.Contains("texture")) builds++; }))
            {
                for (int i = 0; i < 100; i++) cache.Acquire(scope, BlurLevel.Surface);
                ImageBrush surface = null;
                Until(delegate { surface = cache.Acquire(scope, BlurLevel.Surface) as ImageBrush; return surface != null; });
                var bitmap = (BitmapSource)surface.ImageSource;
                Require(builds == 1 && bitmap.IsFrozen && bitmap.PixelWidth <= 640 && bitmap.PixelHeight <= 640, "100 consumers must share one bounded frozen texture");
                Until(delegate { return cache.Acquire(scope, BlurLevel.Ambient) != null; });
                Require(builds == 2, "Ambient and Surface need separate shared levels");
                long version = scope.Frame.Version;
                native.Source = Bitmap(512, 150);
                Until(delegate { return scope.Frame.Version > version; });
                Until(delegate { return cache.Acquire(scope, BlurLevel.Surface) != null; });
                Require(builds == 3, "A new artwork must not reuse the previous artwork's result");
                Until(delegate { return cache.Acquire(scope, BlurLevel.Ambient) != null; });
                Require(builds == 4, "New artwork needs its own Ambient result");
                version = scope.Frame.Version; nativeRoot.BlurAmount = 39; nativeRoot.IsBlurEnabled = false;
                Require(scope.Frame.Version > version && scope.Source.PageBlurScale == .5 && !scope.Source.BackgroundBlurEnabled,
                    "Native blur preferences must invalidate the frame without changing preferences");
                Until(delegate { return cache.Acquire(scope, BlurLevel.Ambient) != null; });
                Require(builds == 5, "Native blur strength must distinguish Ambient cache entries");
                for (int i = 0; i < 100; i++) cache.Acquire(scope, BlurLevel.Content);
                Until(delegate { return cache.Acquire(scope, BlurLevel.Content) != null; });
                Require(builds == 6, "Dense content fallback must share one additional cache job");
                for (int i = 0; i < 25; i++)
                {
                    native.Source = Bitmap(640, (byte)i);
                    Until(delegate { return ReferenceEquals(scope.Frame.Bitmap, native.Source); });
                    Until(delegate { return cache.Acquire(scope, BlurLevel.Surface) != null; });
                    Require(cache.Bytes <= 32 * 1024 * 1024, "Cache output budget exceeded");
                }
                // Supersede queued work without allowing a stale result to become the new frame.
                for (int i = 0; i < 12; i++) { native.Source = Bitmap(256, (byte)i); cache.Acquire(scope, BlurLevel.Surface); }
                Until(delegate { return cache.Acquire(scope, BlurLevel.Surface) != null; });
                Require(ReferenceEquals(scope.Frame.Bitmap, native.Source), "Stale work replaced the active frame");
                scope.Source.Suppress(true); Require(nativeRoot.Opacity == 0, "Native source suppression failed");
                scope.Source.Suppress(false); Require(nativeRoot.Opacity == .72 && nativeRoot.BlurAmount == 39 && !nativeRoot.IsBlurEnabled, "Native source/preferences were not restored");
                native.Source = null;
                Require(scope.Frame == null && cache.Acquire(scope, BlurLevel.Surface) == null, "Cleared artwork retained stale pixels");
                cache.Release(scope);
            }
            scope.Source.Dispose();
            Require(notifications > 1, "Native source notifications missing");
            Console.WriteLine("PASS: raw source lifecycle, shared levels, 100 consumers/one build, 640px output, 32MiB budget, superseded work, source clear and native restore. No rendering performed.");
            application.Shutdown(); return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

namespace MaterialTests
{
    public sealed class ScrollFadeExtension : MarkupExtension
    {
        public override object ProvideValue(IServiceProvider provider) { return new ScrollFadeConverter(); }
    }
    // Model an installed converter wrapper whose plugin is not yet available.
    public sealed class MissingPluginConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider provider) { return this; }
        public object Convert(object value, Type type, object parameter, CultureInfo culture) { return DependencyProperty.UnsetValue; }
        public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) { return Binding.DoNothing; }
    }
}
