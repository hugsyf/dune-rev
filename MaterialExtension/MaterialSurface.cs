using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DuneMaterials
{
    // One surface, using a shared frozen bitmap cache; no capture or render-loop hook.
    internal sealed class MaterialSurface : IDisposable
    {
        readonly Border border;
        readonly MaterialTextureCache cache;
        readonly Func<FrameworkElement> nativeBackground;
        readonly Action<string> info;
        readonly Action<Exception> error;
        readonly bool popup, hero, panel;
        readonly Dispatcher dispatcher;
        readonly List<Image> watched = new List<Image>();
        readonly DependencyPropertyDescriptor sourceProperty = DependencyPropertyDescriptor.FromProperty(Image.SourceProperty, typeof(Image));
        readonly DependencyPropertyDescriptor tagProperty = DependencyPropertyDescriptor.FromProperty(FrameworkElement.TagProperty, typeof(Border));
        readonly DependencyPropertyDescriptor backgroundProperty = DependencyPropertyDescriptor.FromProperty(Border.BackgroundProperty, typeof(Border));
        readonly DependencyPropertyDescriptor pressedProperty = DependencyPropertyDescriptor.FromProperty(ButtonBase.IsPressedProperty, typeof(ButtonBase));
        readonly DependencyPropertyDescriptor heroSourceProperty = DependencyPropertyDescriptor.FromProperty(FrameworkElement.TagProperty, typeof(ContentControl));
        readonly ButtonBase button;
        FrameworkElement background;
        ContentControl heroSourceSelector;
        Image image;
        BitmapSource original, texture;
        Brush fallback, applied;
        bool disposed, queued, applying, failed, lastHover, lastPressed;
        int generation, attempts;
        Rect lastCrop;
        Size lastSize;
        string lastState;

        public MaterialSurface(Border target, MaterialTextureCache textures, Func<FrameworkElement> source,
            Action<string> diagnostics, Action<Exception> errors)
        {
            border = target; cache = textures; nativeBackground = source; info = diagnostics; error = errors;
            dispatcher = border.Dispatcher; popup = border.Uid == "Dune.Material.Popup"; hero = border.Uid == "Dune.Material.Hero";
            panel = border.Uid == "Dune.Material.Panel";
            fallback = border.Background;
            tagProperty.AddValueChanged(border, SettingChanged);
            backgroundProperty.AddValueChanged(border, BackgroundChanged);
            border.IsVisibleChanged += VisibilityChanged;
            border.LayoutUpdated += Layout;
            button = border.TemplatedParent as ButtonBase;
            if (button != null)
            {
                button.MouseEnter += Feedback; button.MouseLeave += Feedback;
                pressedProperty.AddValueChanged(button, BackgroundChanged);
            }
            Queue();
        }

        bool Enabled { get { return Equals(border.Tag, true); } }
        static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            if (root == null) yield break;
            yield return root;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
        }

        public void Reset()
        {
            generation++; Restore();
            foreach (var item in watched) sourceProperty.RemoveValueChanged(item, SourceChanged);
            watched.Clear(); background = null; image = null; original = texture = null;
            attempts = 0; failed = false; Queue();
        }
        void SettingChanged(object sender, EventArgs args) { Reset(); }
        void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) { Reset(); }
        void BackgroundChanged(object sender, EventArgs args) { if (!applying) Queue(); }
        void Feedback(object sender, System.Windows.Input.MouseEventArgs args) { Queue(); }

        void Layout(object sender, EventArgs args)
        {
            if (disposed || failed || !border.IsVisible || !Enabled) return;
            if (texture != null) Apply();
            else if (background == null && attempts < 3) Queue();
        }
        void Queue()
        {
            if (disposed || queued) return;
            queued = true;
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate {
                queued = false;
                if (!disposed && !failed && border.IsVisible && Enabled) Resolve();
            }));
        }

        FrameworkElement FindBackground()
        {
            if (hero)
            {
                for (DependencyObject node = border; node != null; node = VisualTreeHelper.GetParent(node))
                {
                    var overview = node as Control;
                    if (overview == null || !(overview.GetType().Name == "DetailsViewGameOverview" || overview.GetType().Name == "GridViewGameOverview")) continue;
                    var selector = overview.Template.FindName("DuneHeroMaterialSourceRegistration", overview) as ContentControl;
                    if (heroSourceSelector != selector)
                    {
                        if (heroSourceSelector != null) heroSourceProperty.RemoveValueChanged(heroSourceSelector, HeroSourceChanged);
                        heroSourceSelector = selector;
                        if (heroSourceSelector != null) heroSourceProperty.AddValueChanged(heroSourceSelector, HeroSourceChanged);
                    }
                    var selected = selector == null ? null : selector.Tag as FrameworkElement;
                    if (selected != null && selected.IsVisible) return selected;
                    var local = overview.Template.FindName("PART_ImageBackground", overview) as FrameworkElement;
                    if (local != null && local.IsVisible) return local;
                    break;
                }
            }
            return nativeBackground();
        }

        void HeroSourceChanged(object sender, EventArgs args) { if (!disposed) Reset(); }

        void Resolve()
        {
            try
            {
                if (background == null)
                {
                    attempts++;
                    background = FindBackground();
                    if (background == null) { State("fallback: native background unavailable"); return; }
                    foreach (var candidate in Descendants(background).OfType<Image>())
                    { watched.Add(candidate); sourceProperty.AddValueChanged(candidate, SourceChanged); }
                    image = watched.Where(i => i.Source is BitmapSource).OrderByDescending(i => i.Opacity).FirstOrDefault();
                }
                var bitmap = image == null ? null : image.Source as BitmapSource;
                if (bitmap == null || !bitmap.IsFrozen || bitmap.IsDownloading)
                { State("fallback: no ready frozen bitmap"); return; }
                if (ReferenceEquals(original, bitmap)) { if (texture != null) Apply(); return; }
                generation++; Restore(); original = bitmap; texture = null;
                int request = generation;
                cache.Get(bitmap).ContinueWith(delegate(Task<BitmapSource> completed) {
                    var fault = completed.Exception;
                    dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate {
                        if (disposed || request != generation) return;
                        if (completed.IsCanceled) return;
                        if (fault != null) { Fail(fault.GetBaseException()); return; }
                        texture = completed.Result;
                        Apply();
                    }));
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
            if (disposed || failed || !Enabled || !border.IsVisible || texture == null || image == null) return;
            try
            {
                if (image.ActualWidth < 1 || image.ActualHeight < 1 || border.ActualWidth < 1 || border.ActualHeight < 1) return;
                if (PresentationSource.FromVisual(border) == null || PresentationSource.FromVisual(image) == null) return;
                var watch = Stopwatch.StartNew();
                Point a, b;
                if (PresentationSource.FromVisual(border) != PresentationSource.FromVisual(image))
                {
                    a = image.PointFromScreen(border.PointToScreen(new Point()));
                    b = image.PointFromScreen(border.PointToScreen(new Point(border.ActualWidth, border.ActualHeight)));
                }
                else
                {
                    var transform = border.TransformToVisual(image);
                    a = transform.Transform(new Point()); b = transform.Transform(new Point(border.ActualWidth, border.ActualHeight));
                }
                Rect crop = new Rect(a, b);
                Size size = image.RenderSize;
                bool hover = button != null && button.IsMouseOver, pressed = button != null && button.IsPressed;
                if (ReferenceEquals(applied, border.Background) && crop == lastCrop && size == lastSize && hover == lastHover && pressed == lastPressed) return;
                // Partly offscreen cards may extend beyond the wallpaper; their visible
                // intersection still samples correctly. Outside-only popups keep fallback.
                if (crop.Width <= 0 || crop.Height <= 0 || !crop.IntersectsWith(new Rect(size)))
                { Restore(); State("fallback: outside artwork viewport"); return; }
                var baseColor = border.TryFindResource("WindowSurfaceColor");
                var backing = new SolidColorBrush(baseColor is Color ? (Color)baseColor : Color.FromRgb(40, 45, 53));
                var scene = new DrawingGroup();
                using (var dc = scene.Open())
                {
                    dc.DrawRectangle(backing, null, new Rect(size));
                    dc.PushOpacity(background.Opacity);
                    dc.DrawRectangle(new ImageBrush(texture) { Stretch = image.Stretch, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center }, null, new Rect(size));
                    dc.Pop();
                }
                var sampled = new DrawingBrush(scene) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = crop, Stretch = Stretch.Fill };
                var surface = new DrawingGroup();
                using (var dc = surface.Open())
                {
                    var unit = new Rect(0, 0, 1, 1);
                    dc.DrawRectangle(backing, null, unit);
                    dc.DrawRectangle(sampled, null, unit);
                    byte top = popup ? (byte)205 : panel ? (byte)180 : (byte)140;
                    byte bottom = popup ? (byte)220 : panel ? (byte)195 : (byte)155;
                    dc.DrawRectangle(new LinearGradientBrush(Color.FromArgb(top, 40, 46, 54), Color.FromArgb(bottom, 25, 29, 35), 90), null, unit);
                    if (hover) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(8, 255, 255, 255)), null, unit);
                    if (pressed) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(18, 0, 0, 0)), null, unit);
                }
                var brush = new DrawingBrush(surface) { Stretch = Stretch.Fill };
                brush.Freeze();
                if (!ReferenceEquals(applied, border.Background)) fallback = border.Background;
                applying = true;
                try { border.SetCurrentValue(Border.BackgroundProperty, brush); }
                finally { applying = false; }
                applied = brush; lastCrop = crop; lastSize = size; lastHover = hover; lastPressed = pressed;
                if (watch.ElapsedMilliseconds > 12) { Fail(new InvalidOperationException("Material mapping exceeded 12ms; surface stopped until reset.")); return; }
                State("applied; shared texture; " + (popup ? "dark popup" : panel ? "navigation/panel" : hero ? "Hero action" : "card/input"));
            }
            catch (Exception ex) { Fail(ex); }
        }
        void Restore()
        {
            if (applied != null && ReferenceEquals(border.Background, applied))
            {
                applying = true;
                try { border.SetCurrentValue(Border.BackgroundProperty, fallback); }
                finally { applying = false; }
            }
            applied = null;
        }
        void State(string value) { if (value != lastState) { lastState = value; info("Dune Materials [" + border.Uid + "]: " + value); } }
        void Fail(Exception ex) { failed = true; generation++; Restore(); error(ex); }
        public void Dispose()
        {
            disposed = true; generation++; Restore();
            foreach (var item in watched) sourceProperty.RemoveValueChanged(item, SourceChanged);
            watched.Clear();
            tagProperty.RemoveValueChanged(border, SettingChanged); backgroundProperty.RemoveValueChanged(border, BackgroundChanged);
            if (heroSourceSelector != null) heroSourceProperty.RemoveValueChanged(heroSourceSelector, HeroSourceChanged);
            heroSourceSelector = null;
            border.IsVisibleChanged -= VisibilityChanged; border.LayoutUpdated -= Layout;
            if (button != null)
            {
                button.MouseEnter -= Feedback; button.MouseLeave -= Feedback;
                pressedProperty.RemoveValueChanged(button, BackgroundChanged);
            }
            original = texture = null;
        }
    }
}
