using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DuneMaterials.Rendering
{
    public enum MaterialMode { Cached, Realtime }
    public enum MaterialRole { Background, Card, Control, Navigation, Flyout, Content }
    public enum BlurLevel { Ambient, Surface, Content }

    public sealed class MaterialOptions
    {
        public bool UseNewRenderer { get; set; }
        public MaterialMode Mode { get; set; }
        public bool AllowExperimentalRealtime { get; set; }
        public bool ReduceMotion { get; set; }
        public bool Noise { get; set; }
        public bool HeroAutoPlay { get; set; }
        public MaterialOptions() { Noise = true; UseNewRenderer = true; Mode = MaterialMode.Realtime; AllowExperimentalRealtime = true; }
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

    public interface IMaterialBackend : IDisposable
    {
        string Name { get; }
        Brush Acquire(MaterialScope scope, BlurLevel level);
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
            if (uid.EndsWith("Popup", StringComparison.Ordinal)) return MaterialRole.Flyout;
            if (uid.EndsWith("Content", StringComparison.Ordinal)) return MaterialRole.Content;
            if (uid.EndsWith("Panel", StringComparison.Ordinal)) return MaterialRole.Navigation;
            if (uid.EndsWith("Hero", StringComparison.Ordinal) || uid.EndsWith("Input", StringComparison.Ordinal)) return MaterialRole.Control;
            return MaterialRole.Card;
        }
        public static BlurLevel LevelFor(MaterialRole role)
        { return role == MaterialRole.Background ? BlurLevel.Ambient : role == MaterialRole.Content ? BlurLevel.Content : BlurLevel.Surface; }
        public static BlurLevel LevelFor(MaterialRole role, bool windowBackdrop)
        { return !windowBackdrop ? LevelFor(role) : role == MaterialRole.Content || role == MaterialRole.Flyout ? BlurLevel.Content : BlurLevel.Ambient; }
        public static double BlurFraction(BlurLevel level, double pageScale)
        { return level == BlurLevel.Ambient ? .045 * pageScale : level == BlurLevel.Content ? Math.Max(.06, .045 * pageScale) : .018; }
        static readonly Brush[] tints = CreateTints();
        static readonly Brush[] backdropTints = CreateBackdropTints();
        public static Brush Tint(MaterialRole role)
        { return tints[(int)role]; }
        public static Brush Tint(MaterialRole role, bool windowBackdrop)
        { return windowBackdrop ? backdropTints[(int)role] : Tint(role); }
        static Brush[] CreateBackdropTints()
        {
            var brushes = new Brush[Enum.GetValues(typeof(MaterialRole)).Length];
            foreach (MaterialRole role in Enum.GetValues(typeof(MaterialRole))) {
                byte a = role == MaterialRole.Flyout ? (byte)190 : role == MaterialRole.Content ? (byte)88 : role == MaterialRole.Navigation ? (byte)84 : role == MaterialRole.Control ? (byte)56 : (byte)36;
                byte b = role == MaterialRole.Flyout ? (byte)210 : role == MaterialRole.Content ? (byte)108 : role == MaterialRole.Navigation ? (byte)104 : role == MaterialRole.Control ? (byte)72 : (byte)52;
                var brush = new LinearGradientBrush(Color.FromArgb(a, 40, 46, 54), Color.FromArgb(b, 25, 29, 35), 90);
                brush.Freeze(); brushes[(int)role] = brush;
            }
            return brushes;
        }
        static Brush[] CreateTints()
        {
            var brushes = new Brush[Enum.GetValues(typeof(MaterialRole)).Length];
            foreach (MaterialRole role in Enum.GetValues(typeof(MaterialRole))) brushes[(int)role] = CreateTint(role);
            return brushes;
        }
        static Brush CreateTint(MaterialRole role)
        {
            byte a = role == MaterialRole.Flyout ? (byte)205 : role == MaterialRole.Navigation ? (byte)180 : role == MaterialRole.Content ? (byte)156 : (byte)140;
            byte b = role == MaterialRole.Flyout ? (byte)220 : role == MaterialRole.Navigation ? (byte)195 : role == MaterialRole.Content ? (byte)172 : (byte)155;
            var result = new LinearGradientBrush(Color.FromArgb(a, 40, 46, 54), Color.FromArgb(b, 25, 29, 35), 90);
            result.Freeze(); return result;
        }
        public static readonly Brush Grain = CreateGrain();
        static Brush CreateGrain()
        {
            const int side = 64;
            var pixels = new byte[side * side * 4];
            var random = new Random(4147);
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte alpha = random.Next(4) == 0 ? (byte)2 : (byte)1;
                pixels[i] = pixels[i + 1] = pixels[i + 2] = alpha; pixels[i + 3] = alpha;
            }
            var bitmap = BitmapSource.Create(side, side, 96, 96, PixelFormats.Pbgra32, null, pixels, side * 4); bitmap.Freeze();
            var brush = new ImageBrush(bitmap) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, side, side), Stretch = Stretch.None };
            brush.Freeze(); return brush;
        }
    }
}
