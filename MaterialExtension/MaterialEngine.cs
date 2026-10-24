using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DuneMaterials
{
    public sealed class MaterialEngine : IDisposable
    {
        const string Prefix = "Dune.Material.";
        readonly Dictionary<Border, Surface> surfaces = new Dictionary<Border, Surface>();
        readonly Dictionary<FrameworkElement, Backdrop> backdrops = new Dictionary<FrameworkElement, Backdrop>();
        readonly Dictionary<Popup, FrameworkElement> popups = new Dictionary<Popup, FrameworkElement>();
        readonly Action<Exception> log;
        readonly Action<string> trace;
        readonly DispatcherTimer discovery = new DispatcherTimer(DispatcherPriority.Background);
        string lastStatus;
        bool disposed, ticking;
        DateTime lastFrame;
        DateTime lastError;

        sealed class Surface
        {
            public Border Border;
            public FrameworkElement Owner;
            public Brush Fallback, Applied;
            public Rect Sample;
            public Backdrop Backdrop;
            public int Version;
            public bool Hover, Pressed;
        }

        sealed class Backdrop
        {
            public FrameworkElement Source;
            public BitmapSource Image;
            public DateTime NextProbe;
            public ulong Hash;
            public Size Size;
            public Color Base;
            public double Radius;
            public int Version;
        }

        public MaterialEngine(Action<Exception> logger, Action<string> diagnostics)
        { log = logger; trace = diagnostics; }

        public void Start()
        {
            EventManager.RegisterClassHandler(typeof(Border), FrameworkElement.LoadedEvent, new RoutedEventHandler(BorderLoaded), true);
            EventManager.RegisterClassHandler(typeof(Popup), FrameworkElement.LoadedEvent, new RoutedEventHandler(PopupLoaded), true);
            // A theme/resource reload can set Uid after Loaded has already fired.
            discovery.Interval = TimeSpan.FromSeconds(2);
            discovery.Tick += Rediscover;
            discovery.Start();
            Rediscover(null, EventArgs.Empty);
        }

        void Rediscover(object sender, EventArgs args)
        {
            if (disposed) return;
            try
            {
                foreach (Window window in Application.Current.Windows) Discover(window, null);
                int visible = 0, enabled = 0, sources = 0, applied = 0;
                foreach (var surface in surfaces.Values)
                {
                    if (!surface.Border.IsVisible) continue;
                    visible++;
                    if (surface.Owner != null && Equals(surface.Owner.TryFindResource("DuneAdvancedMaterialsEnabled"), true)) enabled++;
                    if (Source(surface) != null) sources++;
                    if (surface.Applied != null && ReferenceEquals(surface.Applied, surface.Border.Background)) applied++;
                }
                string status = string.Format("Dune Materials: surfaces={0}, visible={1}, enabled={2}, sources={3}, applied={4}",
                    surfaces.Count, visible, enabled, sources, applied);
                if (status != lastStatus) { lastStatus = status; if (trace != null) trace(status); }
            }
            catch (Exception error) { if (log != null) log(error); }
        }

        static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            if (root == null) yield break;
            yield return root;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
        }

        static DependencyObject Parent(DependencyObject value)
        {
            var visualParent = value is Visual ? VisualTreeHelper.GetParent(value) : null;
            return visualParent ?? LogicalTreeHelper.GetParent(value);
        }

        static FrameworkElement Anchor(FrameworkElement element)
        {
            for (DependencyObject node = element; node != null; node = Parent(node))
            {
                var menu = node as ContextMenu;
                if (menu != null) return menu.PlacementTarget as FrameworkElement;
                var tip = node as ToolTip;
                if (tip != null) return tip.PlacementTarget as FrameworkElement;
                var popup = node as Popup;
                if (popup != null) return popup.PlacementTarget as FrameworkElement ?? popup.TemplatedParent as FrameworkElement;
            }
            return element;
        }

        void BorderLoaded(object sender, RoutedEventArgs args)
        {
            var border = sender as Border;
            if (!disposed && border != null && border.Uid.StartsWith(Prefix, StringComparison.Ordinal)) Attach(border, null);
        }

        void PopupLoaded(object sender, RoutedEventArgs args)
        {
            var popup = sender as Popup;
            if (disposed || popup == null || popups.ContainsKey(popup)) return;
            // Watch only popups owned by a Dune view; no modification of other themes.
            var owner = popup.PlacementTarget as FrameworkElement ?? popup.TemplatedParent as FrameworkElement;
            if (owner == null || owner.TryFindResource("DuneAdvancedMaterialsEnabled") == null) return;
            popups.Add(popup, owner);
            popup.Opened += PopupOpened;
            popup.Unloaded += PopupUnloaded;
            if (popup.IsOpen) Discover(popup.Child, owner);
        }

        void PopupOpened(object sender, EventArgs args)
        {
            var popup = (Popup)sender;
            FrameworkElement owner;
            if (popups.TryGetValue(popup, out owner)) Discover(popup.Child, owner);
        }

        void PopupUnloaded(object sender, RoutedEventArgs args)
        {
            var popup = (Popup)sender;
            popup.Opened -= PopupOpened;
            popup.Unloaded -= PopupUnloaded;
            popups.Remove(popup);
        }

        void Discover(DependencyObject root, FrameworkElement owner)
        {
            foreach (var node in Descendants(root))
            {
                var border = node as Border;
                if (border != null && border.Uid.StartsWith(Prefix, StringComparison.Ordinal)) Attach(border, owner);
                var popup = node as Popup;
                if (popup != null) PopupLoaded(popup, null);
            }
        }

        void Attach(Border border, FrameworkElement owner)
        {
            Surface existing;
            if (surfaces.TryGetValue(border, out existing)) { if (owner != null) existing.Owner = owner; return; }
            surfaces.Add(border, new Surface { Border = border, Owner = owner ?? Anchor(border) ?? border, Fallback = border.Background });
            border.Unloaded += BorderUnloaded;
            if (!ticking) { CompositionTarget.Rendering += Frame; ticking = true; }
        }

        static void Restore(Surface surface)
        {
            if (surface.Applied != null && ReferenceEquals(surface.Border.Background, surface.Applied))
                surface.Border.SetCurrentValue(Border.BackgroundProperty, surface.Fallback);
            surface.Applied = null;
            surface.Backdrop = null;
        }

        void BorderUnloaded(object sender, RoutedEventArgs args)
        {
            var border = (Border)sender;
            Surface surface;
            if (surfaces.TryGetValue(border, out surface)) { Restore(surface); surfaces.Remove(border); }
            border.Unloaded -= BorderUnloaded;
            if (surfaces.Count == 0)
            {
                CompositionTarget.Rendering -= Frame;
                ticking = false;
                backdrops.Clear();
            }
        }

        static Control FindControl(FrameworkElement owner, string type)
        {
            for (DependencyObject node = owner; node != null; node = Parent(node))
                if (node.GetType().Name == type) return node as Control;
            return null;
        }

        static FrameworkElement Part(Control control, string name)
        {
            return control == null || control.Template == null ? null : control.Template.FindName(name, control) as FrameworkElement;
        }

        static FrameworkElement Source(Surface surface)
        {
            FrameworkElement owner = surface.Owner;
            if (surface.Border.Uid == Prefix + "Hero")
            {
                var overview = FindControl(owner, "DetailsViewGameOverview") ?? FindControl(owner, "GridViewGameOverview");
                var custom = Part(overview, "BackgroundChanger_PluginBackgroundImage") as ContentControl;
                var native = Part(overview, "NativeBackgroundLayer");
                if (native != null && !native.IsVisible && custom != null && custom.Content != null && custom.IsVisible) return custom;
                if (native != null && native.IsVisible) return native;
            }
            var library = FindControl(owner, "Library");
            // Popup placement targets may be detached from the overview but still belong to its window.
            if (library == null)
            {
                Window window = Window.GetWindow(owner);
                if (window != null) library = Descendants(window).OfType<Control>().FirstOrDefault(c => c.GetType().Name == "Library");
            }
            return Part(library, "PART_ImageBackground");
        }

        void Probe(Backdrop backdrop, DateTime now, FrameworkElement owner)
        {
            var source = backdrop.Source;
            if (source.ActualWidth < 1 || source.ActualHeight < 1 || !source.IsVisible) { backdrop.Image = null; return; }
            double radius = 32;
            object setting = owner.TryFindResource("DuneMaterialBlurRadius");
            if (setting is double) radius = Math.Max(8, Math.Min(64, (double)setting));
            var background = owner.TryFindResource("WindowSurfaceColor");
            Color baseColor = background is Color ? (Color)background : Color.FromRgb(40, 45, 53);
            var size = source.RenderSize;
            if (now < backdrop.NextProbe && size == backdrop.Size && radius == backdrop.Radius && baseColor == backdrop.Base) return;
            backdrop.NextProbe = now.AddMilliseconds(500);
            ulong hash = BackdropRenderer.Signature(BackdropRenderer.Capture(source, 24, 24, baseColor));
            if (backdrop.Image != null && hash == backdrop.Hash && size == backdrop.Size && radius == backdrop.Radius && baseColor == backdrop.Base) return;
            double scale = Math.Min(.5, 1024 / Math.Max(size.Width, size.Height));
            int width = Math.Max(1, (int)Math.Ceiling(size.Width * scale)), height = Math.Max(1, (int)Math.Ceiling(size.Height * scale));
            backdrop.Image = BackdropRenderer.Frost(BackdropRenderer.Capture(source, width, height, baseColor), Math.Max(1, radius * scale));
            backdrop.Size = size;
            backdrop.Radius = radius;
            backdrop.Base = baseColor;
            backdrop.Hash = hash;
            backdrop.Version++;
        }

        void Frame(object sender, EventArgs args)
        {
            DateTime now = DateTime.UtcNow;
            if (disposed || (now - lastFrame).TotalMilliseconds < 33) return;
            lastFrame = now;
            var used = new HashSet<FrameworkElement>();
            foreach (var surface in surfaces.Values.ToArray())
            {
                try
                {
                    Border border = surface.Border;
                    if (!border.IsVisible || border.ActualWidth < 1 || border.ActualHeight < 1) { Restore(surface); continue; }
                    var owner = surface.Owner;
                    if (!Equals(owner.TryFindResource("DuneAdvancedMaterialsEnabled"), true)) { Restore(surface); continue; }
                    var source = Source(surface);
                    if (source == null || PresentationSource.FromVisual(source) == null || PresentationSource.FromVisual(border) == null)
                    { Restore(surface); continue; }
                    // Never allow recursive feedback, even if a theme accidentally marks an ancestor.
                    if (source.IsAncestorOf(border) || ReferenceEquals(source, border)) { Restore(surface); continue; }
                    Backdrop backdrop;
                    if (!backdrops.TryGetValue(source, out backdrop))
                    { backdrop = new Backdrop { Source = source }; backdrops.Add(source, backdrop); }
                    used.Add(source);
                    Probe(backdrop, now, owner);
                    if (backdrop.Image == null) { Restore(surface); continue; }
                    Point a = source.PointFromScreen(border.PointToScreen(new Point(0, 0)));
                    Point b = source.PointFromScreen(border.PointToScreen(new Point(border.ActualWidth, border.ActualHeight)));
                    Rect crop = new Rect(a.X / source.ActualWidth, a.Y / source.ActualHeight,
                        Math.Max(.00001, (b.X - a.X) / source.ActualWidth), Math.Max(.00001, (b.Y - a.Y) / source.ActualHeight));
                    bool hover = border.IsMouseOver || (border.Parent is FrameworkElement && ((FrameworkElement)border.Parent).IsMouseOver);
                    var button = border.TemplatedParent as ButtonBase;
                    bool pressed = button != null && button.IsPressed;
                    if (ReferenceEquals(surface.Applied, border.Background) && surface.Backdrop == backdrop && surface.Version == backdrop.Version &&
                        surface.Sample == crop && surface.Hover == hover && surface.Pressed == pressed) continue;
                    if (!ReferenceEquals(surface.Applied, border.Background)) surface.Fallback = border.Background;
                    surface.Applied = BackdropRenderer.Material(backdrop.Image, crop, border.Uid == Prefix + "Popup", hover, pressed);
                    border.SetCurrentValue(Border.BackgroundProperty, surface.Applied);
                    surface.Backdrop = backdrop;
                    surface.Version = backdrop.Version;
                    surface.Sample = crop;
                    surface.Hover = hover;
                    surface.Pressed = pressed;
                }
                catch (Exception error)
                {
                    Restore(surface);
                    if ((now - lastError).TotalSeconds > 30) { lastError = now; if (log != null) log(error); }
                }
            }
            foreach (var source in backdrops.Keys.Where(s => !used.Contains(s)).ToArray()) backdrops.Remove(source);
        }

        public void Dispose()
        {
            disposed = true;
            discovery.Stop();
            discovery.Tick -= Rediscover;
            CompositionTarget.Rendering -= Frame;
            foreach (var surface in surfaces.Values) { Restore(surface); surface.Border.Unloaded -= BorderUnloaded; }
            foreach (var popup in popups.Keys) { popup.Opened -= PopupOpened; popup.Unloaded -= PopupUnloaded; }
            surfaces.Clear(); backdrops.Clear(); popups.Clear();
        }
    }
}
