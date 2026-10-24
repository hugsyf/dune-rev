using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;
using System.Diagnostics;
using System.Threading;
using DuneMaterials.Rendering;

internal static class BackdropSceneChecks
{
    public sealed class SourceHost : Border
    {
        public static readonly DependencyProperty ImageOpacityMaskProperty = DependencyProperty.Register("ImageOpacityMask", typeof(Brush), typeof(SourceHost));
        public static readonly DependencyProperty ImageDarkeningBrushProperty = DependencyProperty.Register("ImageDarkeningBrush", typeof(Brush), typeof(SourceHost));
        public Brush ImageOpacityMask { get { return (Brush)GetValue(ImageOpacityMaskProperty); } set { SetValue(ImageOpacityMaskProperty, value); } }
        public Brush ImageDarkeningBrush { get { return (Brush)GetValue(ImageDarkeningBrushProperty); } set { SetValue(ImageDarkeningBrushProperty, value); } }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate { frame.Continue = false; }));
        Dispatcher.PushFrame(frame);
    }
    public static void Run()
    {
        var bitmap = BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, new byte[16 * 16 * 4], 16 * 4); bitmap.Freeze();
        var image = new Image { Source = bitmap, Width = 800, Height = 600 };
        var mask = new LinearGradientBrush(Color.FromArgb(204, 0, 0, 0), Color.FromArgb(153, 0, 0, 0), 90);
        var native = new SourceHost { Child = image, ImageOpacityMask = mask, ImageDarkeningBrush = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)) };
        native.Measure(new Size(800, 600)); native.Arrange(new Rect(0, 0, 800, 600));
        var anchor = new Grid(); var baseColor = new SolidColorBrush(Colors.DarkSlateGray);
        anchor.Resources["WindowBackgroundBrush"] = baseColor;
        var global = new MaterialScope(anchor, true);
        global.Source = new NativeSourceAdapter(native, delegate { }); Drain();
        Require(global.Frame != null, "Backdrop source did not initialize");
        var paint = new ImageBrush(bitmap); var size = image.RenderSize;
        var page = (DrawingBrush)global.Backdrop.Compose(global, BlurLevel.Ambient, paint, size);
        foreach (MaterialRole role in new[] { MaterialRole.Background, MaterialRole.Card, MaterialRole.Control, MaterialRole.Navigation }) {
            var level = MaterialProfiles.LevelFor(role, true);
            Require(level == BlurLevel.Ambient && ReferenceEquals(page, global.Backdrop.Compose(global, level, new ImageBrush(bitmap), size)), "Page and content do not share the same cached backdrop composition");
        }
        var scene = (DrawingGroup)page.Drawing;
        Require(scene.Children.Count == 2 && ReferenceEquals(((GeometryDrawing)scene.Children[0]).Brush, baseColor), "Backdrop does not resolve against the window's neutral color");
        var artwork = (DrawingGroup)scene.Children[1];
        Require(ReferenceEquals(artwork.OpacityMask, mask) && artwork.Children.Count == 2 && ReferenceEquals(((GeometryDrawing)artwork.Children[1]).Brush, native.ImageDarkeningBrush), "Page mask/darkening were dropped or applied twice");
        var denseLevel = MaterialProfiles.LevelFor(MaterialRole.Content, true);
        Require(denseLevel == BlurLevel.Content && MaterialProfiles.LevelFor(MaterialRole.Flyout, true) == denseLevel, "Dense surfaces/flyouts lost their stronger blur level");
        global.Backdrop.Compose(global, denseLevel, paint, size);
        Require(global.Backdrop.Count == 2, "Backdrop cache grew with the number of consumers");
        using (var runtime = new MaterialRuntime(new MaterialOptions { UseNewRenderer = true, Mode = MaterialMode.Cached }, delegate { })) {
            Brush card = null; FrameworkElement coordinates; bool live;
            var watch = Stopwatch.StartNew();
            while (card == null && watch.ElapsedMilliseconds < 5000) {
                card = runtime.Acquire(global, MaterialRole.Card, out coordinates, out live); Drain(); Thread.Sleep(1);
            }
            var background = runtime.Acquire(global, MaterialRole.Background, out coordinates, out live);
            Require(card != null && ReferenceEquals(card, background) && ReferenceEquals(coordinates, global.Frame.Coordinates) && !live, "Cached runtime did not return the same composed background to page and cards");
        }
        var previous = page;
        native.ImageDarkeningBrush = Brushes.Black;
        page = (DrawingBrush)global.Backdrop.Compose(global, BlurLevel.Ambient, paint, size);
        Require(!ReferenceEquals(previous, page), "Native darkening changes reused stale composition");
        previous = page; anchor.Resources["WindowBackgroundBrush"] = Brushes.Navy;
        page = (DrawingBrush)global.Backdrop.Compose(global, BlurLevel.Ambient, paint, size);
        Require(!ReferenceEquals(previous, page), "Window base-color replacement reused stale composition");
        var hero = new MaterialScope(new Grid());
        Require(ReferenceEquals(hero.Backdrop.Compose(hero, BlurLevel.Surface, paint, size), paint) && hero.Backdrop.Count == 0 && MaterialProfiles.LevelFor(MaterialRole.Control, false) == BlurLevel.Surface, "Hero media inherited the global backdrop policy");
        var lightTint = (LinearGradientBrush)MaterialProfiles.Tint(MaterialRole.Card, true);
        var denseTint = (LinearGradientBrush)MaterialProfiles.Tint(MaterialRole.Content, true);
        var popupTint = (LinearGradientBrush)MaterialProfiles.Tint(MaterialRole.Flyout, true);
        Require(lightTint.GradientStops[0].Color.A < denseTint.GradientStops[0].Color.A && denseTint.GradientStops[0].Color.A < popupTint.GradientStops[0].Color.A, "Surface tint hierarchy is inconsistent");
        Require(ReferenceEquals(MaterialProfiles.Tint(MaterialRole.Control, false), MaterialProfiles.Tint(MaterialRole.Control)), "Hero's accepted tint profile changed");
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../.."));
        var xml = new XmlDocument(); xml.Load(Path.Combine(root, "Source/Views/Library.xaml"));
        var ns = new XmlNamespaceManager(xml.NameTable); ns.AddNamespace("p", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
        foreach (XmlElement stop in xml.SelectNodes("//p:FadeImage.ImageOpacityMask//p:GradientStop", ns))
            Require(((Color)ColorConverter.ConvertFromString(stop.GetAttribute("Color"))).A >= 153, "Library artwork still fades completely into a flat color");
        global.Backdrop.Clear(); global.Source.Dispose();
        Require(global.Backdrop.Count == 0, "Backdrop cache did not release source references");
        Console.WriteLine("PASS: shared page/card scene, one mask/darkening composition, two shared levels, invalidation, bounded fade, tint hierarchy and independent Hero. No windows or rendering.");
    }
}
