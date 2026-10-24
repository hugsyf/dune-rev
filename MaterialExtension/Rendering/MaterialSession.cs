using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DuneMaterials.Rendering
{
    // Owns a brush returned by an explicit theme binding; never changes a target property.
    internal sealed class MaterialSession
    {
        readonly MaterialRuntime runtime;
        readonly DrawingGroup output = new DrawingGroup();
        readonly DrawingBrush brush;
        Brush fallback;
        MaterialRole role;
        bool enabled, showingMaterial;
        MaterialScope lastScope;
        Rect lastCrop;
        Size lastSize;
        Size lastArtworkSize;
        DrawingBrush samplingBrush;
        FrameworkElement samplingCoordinates;
        Size samplingSize;
        long lastVersion;
        int revision;
        Brush paintedFallback;
        Size fallbackSize;
        bool isFallback;
        bool failed;
        MaterialScope attemptedScope, failedScope;
        long failedVersion;
        public readonly FrameworkElement Target;
        public bool Active { get; private set; }
        public bool HasMaterial { get { return showingMaterial; } }
        public bool IsLive { get; private set; }
        public bool Dirty = true;
        public Brush Brush { get { return brush; } }
        public MaterialSession(MaterialRuntime service, FrameworkElement target)
        {
            runtime = service; Target = target;
            brush = new DrawingBrush(output) { Stretch = Stretch.Fill };
            Target.Loaded += Loaded; Target.Unloaded += Unloaded;
            Target.IsVisibleChanged += VisibilityChanged;
            Target.LayoutUpdated += Layout;
            if (Target.IsLoaded) Activate();
        }
        public void Configure(Brush original, bool master, MaterialRole profile)
        {
            ClearFailure();
            fallback = original; enabled = master; role = profile; Dirty = true;
            if (Active) runtime.Queue(this); else Fallback();
        }
        void Activate() { Active = true; Dirty = true; runtime.Register(this); }
        void Loaded(object sender, RoutedEventArgs args) { if (!Active) Activate(); }
        void Unloaded(object sender, RoutedEventArgs args) { Active = false; runtime.Unregister(this); Fallback(); lastScope = null; }
        void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) { Dirty = true; if (Active) runtime.Queue(this); }
        void Layout(object sender, EventArgs args)
        {
            if (!Active || !enabled || !runtime.Options.UseNewRenderer || !Target.IsVisible) return;
            try
            {
                // LayoutUpdated runs before the layout is committed for presentation. Moving
                // this crop to a Background-priority job leaves one frame with the old offset.
                if (!TryRemap()) runtime.Queue(this);
            }
            catch (Exception exception) { runtime.SurfaceFailed(this, exception); }
        }
        bool TryRemap()
        {
            var frame = lastScope == null ? null : lastScope.Frame;
            if (Dirty || !showingMaterial || samplingBrush == null || samplingCoordinates == null || frame == null ||
                frame.Version != lastVersion || Target.RenderSize != lastSize || frame.Coordinates.RenderSize != lastArtworkSize ||
                samplingCoordinates.RenderSize != samplingSize) return false;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            bool changed = Remap(samplingBrush, Target, samplingCoordinates);
            lastCrop = ReferenceEquals(samplingCoordinates, frame.Coordinates) ? samplingBrush.Viewbox : Map(Target, frame.Coordinates);
            if (changed) runtime.RecordRemap(watch.Elapsed.TotalMilliseconds);
            return true;
        }
        internal static bool Remap(DrawingBrush sample, FrameworkElement target, FrameworkElement coordinates)
        {
            var crop = Map(target, coordinates);
            if (sample.Viewbox == crop) return false;
            sample.Viewbox = crop; return true;
        }
        public void Fallback()
        {
            runtime.ReleaseBackground(this); showingMaterial = false; IsLive = false;
            samplingBrush = null; samplingCoordinates = null;
            var size = new Size(Math.Max(1, Target.ActualWidth), Math.Max(1, Target.ActualHeight));
            if (isFallback && ReferenceEquals(paintedFallback, fallback) && size == fallbackSize) return;
            revision++; isFallback = true; paintedFallback = fallback; fallbackSize = size;
            using (var dc = output.Open()) dc.DrawRectangle(fallback, null, new Rect(size));
        }
        public void ClearFailure() { failed = false; }
        public void Fail()
        {
            Fallback(); failed = true; Dirty = false; failedScope = attemptedScope;
            failedVersion = attemptedScope == null || attemptedScope.Frame == null ? -1 : attemptedScope.Frame.Version;
        }
        internal static Rect Map(FrameworkElement target, FrameworkElement source)
        {
            if (PresentationSource.FromVisual(target) == PresentationSource.FromVisual(source))
                return target.TransformToVisual(source).TransformBounds(new Rect(target.RenderSize));
            // Popups have their own presentation source. Convert through screen pixels (DPI aware).
            var start = source.PointFromScreen(target.PointToScreen(new Point()));
            var end = source.PointFromScreen(target.PointToScreen(new Point(target.ActualWidth, target.ActualHeight)));
            return new Rect(start, end);
        }
        public void Update()
        {
            if (!Active || !enabled || !runtime.Options.UseNewRenderer || !Target.IsVisible || Target.ActualWidth < 1 || Target.ActualHeight < 1)
            { if (showingMaterial || Dirty) Fallback(); Dirty = false; return; }
            var scope = !Dirty && lastScope != null ? lastScope : runtime.FindScope(Target);
            attemptedScope = scope;
            if (scope == null || scope.Frame == null) { if (showingMaterial || Dirty) Fallback(); Dirty = false; return; }
            var frame = scope.Frame;
            if (failed && failedScope == scope && failedVersion == frame.Version) { Dirty = false; return; }
            failed = false;
            var crop = Map(Target, frame.Coordinates);
            var frameCrop = crop;
            var size = Target.RenderSize;
            if (!Dirty && showingMaterial && scope == lastScope && frame.Version == lastVersion && crop == lastCrop && size == lastSize && frame.Coordinates.RenderSize == lastArtworkSize) return;
            Dirty = false;
            FrameworkElement coordinates; bool live;
            var paint = runtime.Acquire(scope, role, out coordinates, out live);
            if (paint == null || coordinates == null) { Fallback(); return; }
            crop = Map(Target, coordinates);
            var sceneSize = coordinates.RenderSize;
            if (crop.IsEmpty || crop.Width <= 0 || crop.Height <= 0 || sceneSize.Width < 1 || sceneSize.Height < 1) { Fallback(); return; }
            var scene = new DrawingGroup();
            using (var dc = scene.Open())
            {
                dc.DrawRectangle(paint, null, new Rect(sceneSize));
                if (!scope.IsWindowBackdrop && role == MaterialRole.Background && scope.Source.PageDarkening != null)
                    dc.DrawRectangle(scope.Source.PageDarkening, null, new Rect(sceneSize));
            }
            if (!scope.IsWindowBackdrop && role == MaterialRole.Background) scene.OpacityMask = scope.Source.PageMask;
            var cropped = new DrawingBrush(scene) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = crop, Stretch = Stretch.Fill };
            samplingBrush = cropped; samplingCoordinates = coordinates; samplingSize = sceneSize;
            var layer = new DrawingGroup();
            var bounds = new Rect(size);
            using (var dc = layer.Open())
            {
                dc.DrawRectangle(cropped, null, bounds);
                if (role != MaterialRole.Background)
                {
                    dc.DrawRectangle(MaterialProfiles.Tint(role, scope.IsWindowBackdrop), null, bounds);
                    if (Target.IsMouseOver && role != MaterialRole.Content) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)), null, bounds);
                    if (runtime.Options.Noise) dc.DrawRectangle(MaterialProfiles.Grain, null, bounds);
                }
            }
            bool transition = showingMaterial && lastScope == scope && lastVersion != frame.Version && !live &&
                !runtime.Options.ReduceMotion && SystemParameters.ClientAreaAnimation;
            int current = ++revision;
            // Keep only the last completed layer, never chains of animated drawings.
            Drawing previous = output.Children.Count == 1 ? output.Children[0] : null;
            output.Children.Clear();
            if (transition && previous != null)
            {
                output.Children.Add(previous); output.Children.Add(layer);
                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop };
                fade.Completed += delegate {
                    if (revision != current) return;
                    layer.BeginAnimation(DrawingGroup.OpacityProperty, null);
                    output.Children.Clear(); output.Children.Add(layer);
                };
                layer.BeginAnimation(DrawingGroup.OpacityProperty, fade);
            }
            else output.Children.Add(layer);
            showingMaterial = true; IsLive = live; isFallback = false; lastScope = scope; lastVersion = frame.Version; lastCrop = frameCrop; lastSize = size; lastArtworkSize = frame.Coordinates.RenderSize;
            if (role == MaterialRole.Background) runtime.BackgroundReady(this, scope);
        }
    }
}
