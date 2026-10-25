using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DuneMaterials.Rendering
{
    public enum MaterialRole { Background, Content, Flyout }

    public sealed class MaterialOptions
    {
        public bool UseNewRenderer { get; set; }
        public bool ReduceMotion { get; set; }
        public bool Noise { get; set; }
        public bool HeroAutoPlay { get; set; }
        // Keep the serialized enable key so existing on/off preferences survive upgrades.
        // Removed Mode/AllowExperimentalRealtime values are ignored when old settings load.
        public MaterialOptions() { Noise = true; UseNewRenderer = true; }
        public MaterialOptions Copy() { return (MaterialOptions)MemberwiseClone(); }
    }

    public sealed class MaterialFrame
    {
        public readonly long Version;
        public readonly BitmapSource Bitmap;
        public readonly FrameworkElement Coordinates;
        public readonly Stretch Stretch;
        public MaterialFrame(long version, BitmapSource bitmap, FrameworkElement coordinates, Stretch stretch)
        { Version = version; Bitmap = bitmap; Coordinates = coordinates; Stretch = stretch; }
    }

    public sealed class MaterialScope
    {
        public readonly FrameworkElement Anchor;
        internal readonly bool IsWindowBackdrop;
        internal readonly BackdropScene Backdrop = new BackdropScene();
        internal NativeSourceAdapter Source;
        internal FrameworkElement LiveVisual;
        internal event EventHandler Changed;
        public MaterialFrame Frame { get { return Source == null ? null : Source.Frame; } }
        public FrameworkElement LiveSource { get { return LiveVisual != null && LiveVisual.IsVisible && LiveVisual.Opacity > .001 ? LiveVisual : Source == null ? null : Source.RawScene; } }
        internal MaterialScope(FrameworkElement anchor) : this(anchor, anchor is Window) { }
        internal MaterialScope(FrameworkElement anchor, bool windowBackdrop) { Anchor = anchor; IsWindowBackdrop = windowBackdrop; }
        internal void Notify() { var handler = Changed; if (handler != null) handler(this, EventArgs.Empty); }
    }

    internal static class MaterialProfiles
    {
        public static MaterialRole RoleFor(string uid)
        {
            return uid.EndsWith("Popup", StringComparison.Ordinal) ? MaterialRole.Flyout : MaterialRole.Content;
        }
        static Brush NeutralTint(byte opacity)
        {
            var brush = new SolidColorBrush(Color.FromArgb(opacity, 30, 34, 41));
            brush.Freeze(); return brush;
        }
        static readonly Brush contentTint = NeutralTint(140);
        static readonly Brush flyoutTint = NeutralTint(202);
        public static Brush Tint(MaterialRole role)
        { return role == MaterialRole.Flyout ? flyoutTint : contentTint; }
        public static readonly Brush Sheen = CreateSheen();
        static Brush CreateSheen()
        {
            var brush = new LinearGradientBrush();
            // Keep the sheen at the edge, independent of the card's height.
            // A full-height low-alpha ramp quantizes into horizontal bands.
            brush.MappingMode = BrushMappingMode.Absolute;
            brush.StartPoint = new Point(0, 0); brush.EndPoint = new Point(0, 12);
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(4, 255, 255, 255), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
            brush.Freeze(); return brush;
        }
        public static readonly Brush Grain = CreateGrain();
        static Brush CreateGrain()
        {
            const int side = 64;
            var pixels = new byte[side * side * 4];
            var random = new Random(4147);
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte alpha = (byte)random.Next(2, 5);
                byte shade = random.Next(2) == 0 ? (byte)0 : alpha;
                pixels[i] = pixels[i + 1] = pixels[i + 2] = shade; pixels[i + 3] = alpha;
            }
            var bitmap = BitmapSource.Create(side, side, 96, 96, PixelFormats.Pbgra32, null, pixels, side * 4); bitmap.Freeze();
            var brush = new ImageBrush(bitmap) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, side, side), Stretch = Stretch.None };
            brush.Freeze(); return brush;
        }
    }
}
