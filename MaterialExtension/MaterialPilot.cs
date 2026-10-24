using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DuneMaterials
{
    // One native-background card only. No visual captures or rendering callbacks.
    internal sealed class MaterialPilot : IDisposable
    {
        const string Marker = "Dune.Material.Pilot";
        readonly Action<string> info;
        readonly Action<Exception> error;
        readonly Dispatcher dispatcher = Application.Current.Dispatcher;
        readonly DependencyPropertyDescriptor sourceProperty = DependencyPropertyDescriptor.FromProperty(Image.SourceProperty, typeof(Image));
        readonly DependencyPropertyDescriptor settingProperty = DependencyPropertyDescriptor.FromProperty(FrameworkElement.TagProperty, typeof(Border));
        readonly List<Image> watchedImages = new List<Image>();
        readonly HashSet<Border> candidates = new HashSet<Border>();
        Border card;
        FrameworkElement background;
        Image image;
        Brush fallback, applied;
        BitmapSource original, texture;
        CancellationTokenSource work;
        bool disposed, queued, failed, enabled;
        int attempts, generation;
        Rect lastCrop;
        Size lastSize;
        string lastState;

        public MaterialPilot(Action<string> diagnostics, Action<Exception> errors) { info = diagnostics; error = errors; }

        public void Start()
        {
            EventManager.RegisterClassHandler(typeof(Border), FrameworkElement.LoadedEvent, new RoutedEventHandler(Loaded), true);
            DiscoverCandidates();
            State("ready v0.2.3; candidates=" + candidates.Count + "; only the last-played card is eligible");
        }

        void Loaded(object sender, RoutedEventArgs args)
        {
            var border = sender as Border;
            if (!disposed && border != null && IsPilot(border)) Register(border);
        }

        static bool IsPilot(Border border)
        { return border.Uid == Marker || border.Name == "DuneLastPlayedMaterial"; }

        bool IsEnabled() { return card != null && Equals(card.Tag, true); }

        void DiscoverCandidates()
        {
            var window = Application.Current.MainWindow;
            if (window == null) return;
            // Event-driven recovery for templates created after plugin startup.
            // No timer: this executes only at startup or an SDK selection event.
            foreach (var border in Descendants(window).OfType<Border>().Where(IsPilot).Take(2).ToArray()) Register(border);
            info("Dune pilot: discovery candidates=" + candidates.Count + ", active=" + (card == null ? "none" : card.Name));
        }

        void Register(Border border)
        {
            if (candidates.Add(border))
            {
                border.IsVisibleChanged += VisibilityChanged;
                border.Unloaded += CandidateUnloaded;
                info("Dune pilot: candidate registered name=" + border.Name + ", visible=" + border.IsVisible + ", switch=" + (border.Tag ?? "<null>"));
            }
            if (border.IsVisible) Attach(border);
        }

        void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            var border = (Border)sender;
            if (disposed) return;
            if (border.IsVisible) Attach(border);
            else if (ReferenceEquals(card, border)) Detach();
        }

        void CandidateUnloaded(object sender, RoutedEventArgs args)
        {
            var border = (Border)sender;
            border.IsVisibleChanged -= VisibilityChanged;
            border.Unloaded -= CandidateUnloaded;
            candidates.Remove(border);
            if (ReferenceEquals(card, border)) Detach();
        }

        static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            if (root == null) yield break;
            yield return root;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
        }

        void Attach(Border border)
        {
            if (ReferenceEquals(card, border)) return;
            Detach();
            card = border;
            failed = false;
            fallback = card.Background;
            settingProperty.AddValueChanged(card, SettingChanged);
            card.LayoutUpdated += Layout;
            attempts = 0;
            SettingChanged(card, EventArgs.Empty);
            Queue();
        }

        void SettingChanged(object sender, EventArgs args)
        {
            enabled = IsEnabled();
            State("switch=" + (card.Tag ?? "<null>") + ", type=" + (card.Tag == null ? "null" : card.Tag.GetType().Name) + ", visible=" + card.IsVisible);
            if (enabled) { attempts = 0; failed = false; Queue(); }
            else { Cancel(); Restore(); original = texture = null; }
        }

        public void SelectionChanged()
        {
            if (disposed) return;
            DiscoverCandidates();
            attempts = 0;
            failed = false;
            Cancel();
            Restore();
            Unwatch();
            background = null;
            original = texture = null;
            Queue();
        }

        void Layout(object sender, EventArgs args)
        {
            // At most one tiny geometry check per layout; no scene traversal here.
            if (card == null || disposed || failed) return;
            bool requested = IsEnabled();
            if (requested != enabled)
            {
                enabled = requested;
                if (enabled) Queue();
                else { Cancel(); Restore(); original = texture = null; }
            }
            if (!enabled) return;
            if (background == null) { if (attempts < 3) Queue(); return; }
            if (texture != null) Apply();
        }

        void Queue()
        {
            if (disposed || queued || card == null) return;
            queued = true;
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate {
                queued = false;
                if (!disposed && card != null) Resolve();
            }));
        }

        void Resolve()
        {
            if (failed || !card.IsVisible || !IsEnabled()) return;
            try
            {
                if (background == null)
                {
                    attempts++;
                    for (DependencyObject node = card; node != null; node = VisualTreeHelper.GetParent(node))
                    {
                        var control = node as Control;
                        if (control == null || control.GetType().Name != "Library") continue;
                        background = control.Template.FindName("PART_ImageBackground", control) as FrameworkElement;
                        break;
                    }
                    if (background == null) { State("fallback: native Library background unavailable"); return; }
                    // Inspect only the named background's small subtree, not the window.
                    foreach (var candidate in Descendants(background).OfType<Image>())
                    {
                        watchedImages.Add(candidate);
                        sourceProperty.AddValueChanged(candidate, SourceChanged);
                    }
                    // FadeImage alternates two Image controls; observe both bitmap slots.
                    image = watchedImages.Where(i => i.Source is BitmapSource).OrderByDescending(i => i.Opacity).FirstOrDefault();
                }
                if (image == null) { State("fallback: native background has no static Image bitmap"); return; }
                var bitmap = image.Source as BitmapSource;
                if (bitmap == null || !bitmap.IsFrozen || bitmap.IsDownloading)
                { State("fallback: bitmap is not frozen/ready; UI-thread decode is prohibited"); return; }
                if (ReferenceEquals(original, bitmap)) { if (texture != null) Apply(); return; }
                Cancel();
                Restore();
                original = bitmap;
                texture = null;
                State("source: " + background.GetType().Name + "/" + image.Name + " frozen bitmap " + bitmap.PixelWidth + "x" + bitmap.PixelHeight);
                var cancellation = work = new CancellationTokenSource();
                int request = generation;
                // Only the frozen bitmap crosses threads. No WPF visual leaves the UI thread.
                Task.Run(delegate {
                    var watch = Stopwatch.StartNew();
                    try
                    {
                        var built = PilotTexture.Build(bitmap, cancellation.Token);
                        long elapsed = watch.ElapsedMilliseconds;
                        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate {
                            if (disposed || request != generation || cancellation.IsCancellationRequested || card == null) return;
                            texture = built;
                            info(string.Format("Dune pilot: texture built {0}x{1}, worker={2}ms", built.PixelWidth, built.PixelHeight, elapsed));
                            Apply();
                        }));
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { dispatcher.BeginInvoke(new Action(delegate { if (!disposed && request == generation) Fail(ex); })); }
                    finally
                    {
                        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate {
                            if (ReferenceEquals(work, cancellation)) work = null;
                            cancellation.Dispose();
                        }));
                    }
                });
            }
            catch (Exception ex) { Fail(ex); }
        }

        void SourceChanged(object sender, EventArgs args)
        {
            var changed = sender as Image;
            if (changed != null && changed.Source is BitmapSource) image = changed;
            Queue();
        }

        void Apply()
        {
            if (card == null || image == null || texture == null || !card.IsVisible) return;
            try
            {
                if (!IsEnabled()) { Restore(); return; }
                if (image.ActualWidth < 1 || image.ActualHeight < 1 || card.ActualWidth < 1 || card.ActualHeight < 1) return;
                var watch = Stopwatch.StartNew();
                var transform = card.TransformToVisual(image);
                Point a = transform.Transform(new Point()), b = transform.Transform(new Point(card.ActualWidth, card.ActualHeight));
                Rect crop = new Rect(a, b);
                Size size = image.RenderSize;
                if (ReferenceEquals(card.Background, applied) && crop == lastCrop && size == lastSize) return;
                Rect overlap = Rect.Intersect(crop, new Rect(size));
                double coverage = crop.Width > 0 && crop.Height > 0 && !overlap.IsEmpty
                    ? overlap.Width * overlap.Height / (crop.Width * crop.Height) : 0;
                if (coverage < .95)
                {
                    Restore();
                    State(string.Format("fallback: crop outside artwork viewport; image={0}, crop={1}, coverage={2:P0}", size, crop, coverage));
                    return;
                }
                // Paint in the Image's coordinate system, then crop; preserve Image.Stretch.
                var artwork = new DrawingGroup();
                using (var dc = artwork.Open())
                    dc.DrawRectangle(new ImageBrush(texture) { Stretch = image.Stretch, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center }, null, new Rect(size));
                // The page's radial fade is a layout effect, not a material mask.
                // Applying it here erases artwork at lower card positions. Sample
                // the unfaded bitmap and provide an independent readability tint.
                // The two Image opacities animate during cross-fade; sampling 0 here
                // would bake an invisible texture. The pilot displays the new static image.
                artwork.Opacity = background.Opacity;
                var scene = new DrawingGroup();
                using (var dc = scene.Open())
                {
                    var baseColor = card.TryFindResource("WindowSurfaceColor");
                    dc.DrawRectangle(new SolidColorBrush(baseColor is Color ? (Color)baseColor : Color.FromRgb(40, 45, 53)), null, new Rect(size));
                    dc.DrawDrawing(artwork);
                    // The material supplies its own readability tint below. Copying
                    // the page darkening here would attenuate artwork a second time.
                }
                var sampled = new DrawingBrush(scene) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = crop, Stretch = Stretch.Fill };
                var surface = new DrawingGroup();
                using (var dc = surface.Open())
                {
                    dc.DrawRectangle(sampled, null, new Rect(0, 0, 1, 1));
                    dc.DrawRectangle(new LinearGradientBrush(Color.FromArgb(140, 40, 46, 54), Color.FromArgb(155, 25, 29, 35), 90), null, new Rect(0, 0, 1, 1));
                }
                var brush = new DrawingBrush(surface) { Stretch = Stretch.Fill };
                brush.Freeze();
                if (!ReferenceEquals(applied, card.Background)) fallback = card.Background;
                card.SetCurrentValue(Border.BackgroundProperty, brush);
                applied = brush;
                lastCrop = crop;
                lastSize = size;
                if (watch.ElapsedMilliseconds > 12) { Fail(new InvalidOperationException("UI mapping exceeded 12ms; pilot stopped for this selection.")); return; }
                if (lastState == null || !lastState.StartsWith("applied", StringComparison.Ordinal))
                    info(string.Format("Dune pilot: UI map={0:F2}ms, image={1}, crop={2}, coverage={3:P0}; page fade/darkening excluded", watch.Elapsed.TotalMilliseconds, size, crop, coverage));
                State("applied raw-artwork material to last-played card; blur updates only on bitmap changes");
            }
            catch (Exception ex) { Fail(ex); }
        }

        void State(string state) { if (state != lastState) { lastState = state; info("Dune pilot: " + state); } }
        void Fail(Exception ex) { failed = true; Cancel(); Restore(); Unwatch(); texture = original = null; error(ex); }
        void Cancel() { generation++; if (work != null) { work.Cancel(); work = null; } }
        void Restore() { if (card != null && applied != null && ReferenceEquals(card.Background, applied)) card.SetCurrentValue(Border.BackgroundProperty, fallback); applied = null; }
        void Unwatch()
        {
            foreach (var watched in watchedImages) sourceProperty.RemoveValueChanged(watched, SourceChanged);
            watchedImages.Clear();
            image = null;
        }
        void Detach()
        {
            Cancel(); Restore(); Unwatch(); background = null; original = texture = null;
            if (card != null) { card.LayoutUpdated -= Layout; settingProperty.RemoveValueChanged(card, SettingChanged); }
            card = null;
        }
        public void Dispose()
        {
            disposed = true; Detach();
            foreach (var candidate in candidates)
            { candidate.IsVisibleChanged -= VisibilityChanged; candidate.Unloaded -= CandidateUnloaded; }
            candidates.Clear();
        }
    }
}
