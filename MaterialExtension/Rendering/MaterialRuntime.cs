using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace DuneMaterials.Rendering
{
    internal sealed class MaterialRuntime : IDisposable
    {
        readonly Dictionary<FrameworkElement, MaterialScope> scopes = new Dictionary<FrameworkElement, MaterialScope>();
        readonly HashSet<MaterialSession> sessions = new HashSet<MaterialSession>();
        readonly HashSet<MaterialSession> pending = new HashSet<MaterialSession>();
        readonly Dictionary<MaterialScope, HashSet<MaterialSession>> leases = new Dictionary<MaterialScope, HashSet<MaterialSession>>();
        readonly Queue<double> samples = new Queue<double>();
        readonly Queue<double> remaps = new Queue<double>();
        long remapCount;
        readonly Dispatcher dispatcher;
        readonly Action<string> info;
        RealtimeBackend realtime = new RealtimeBackend();
        bool disposed, queued;
        string lastReason;
        public MaterialOptions Options { get; private set; }
        public MaterialRuntime(MaterialOptions options, Action<string> diagnostics)
        {
            dispatcher = Application.Current.Dispatcher; info = diagnostics;
            Options = options.Copy();
            RenderCapability.TierChanged += TierChanged;
        }
        public void Configure(MaterialOptions options)
        {
            foreach (var session in sessions.ToArray()) { session.ClearFailure(); session.Fallback(); }
            RestoreNative(); realtime.Dispose(); realtime = new RealtimeBackend();
            foreach (var scope in scopes.Values) scope.Backdrop.Clear();
            Options = options.Copy(); lastReason = null; samples.Clear(); remaps.Clear(); remapCount = 0;
            QueueAll();
        }
        public void Register(MaterialSession session) { if (!disposed) { sessions.Add(session); Queue(session); } }
        public void Unregister(MaterialSession session) { ReleaseBackground(session); sessions.Remove(session); pending.Remove(session); }
        public MaterialScope RegisterSource(FrameworkElement anchor, FrameworkElement source, bool live)
        {
            if (disposed || anchor == null || source == null) return null;
            MaterialScope scope;
            if (!scopes.TryGetValue(anchor, out scope)) { scope = new MaterialScope(anchor); scopes.Add(anchor, scope); }
            if (live) scope.LiveVisual = source;
            else
            {
                leases.Remove(scope); realtime.Release(scope); scope.Backdrop.Clear();
                if (scope.Source != null) scope.Source.Dispose();
                scope.Source = null;
                try
                {
                    scope.Source = new NativeSourceAdapter(source, delegate { scope.Notify(); QueueAll(); });
                }
                catch (Exception exception)
                {
                    if (scope.LiveVisual == null) scopes.Remove(anchor);
                    info("Dune material source initialization failed; using theme backgrounds: " + exception);
                    QueueAll();
                    return null;
                }
            }
            scope.Notify(); QueueAll(); return scope;
        }
        public void UnregisterSource(MaterialScope scope, FrameworkElement source, bool live)
        {
            if (scope == null) return;
            if (live) { if (scope.LiveVisual == source) scope.LiveVisual = null; }
            else if (scope.Source != null && scope.Source.Root == source)
            { scope.Source.Dispose(); scope.Source = null; leases.Remove(scope); realtime.Release(scope); scope.Backdrop.Clear(); }
            if (scope.Source == null && scope.LiveVisual == null) { scopes.Remove(scope.Anchor); leases.Remove(scope); realtime.Release(scope); }
            QueueAll();
        }
        static IEnumerable<DependencyObject> Parents(DependencyObject node)
        {
            if (node is Visual) { var p = VisualTreeHelper.GetParent(node); if (p != null) yield return p; }
            var logical = LogicalTreeHelper.GetParent(node); if (logical != null) yield return logical;
            var fe = node as FrameworkElement; if (fe != null && fe.TemplatedParent != null) yield return fe.TemplatedParent;
            var menu = node as ContextMenu; if (menu != null && menu.PlacementTarget != null) yield return menu.PlacementTarget;
            var tip = node as ToolTip; if (tip != null && tip.PlacementTarget != null) yield return tip.PlacementTarget;
            var popup = node as Popup; if (popup != null && popup.PlacementTarget != null) yield return popup.PlacementTarget;
        }
        public MaterialScope FindScope(FrameworkElement target)
        {
            var seen = new HashSet<DependencyObject>(); var queue = new Queue<DependencyObject>(); queue.Enqueue(target);
            MaterialScope windowScope = null;
            while (queue.Count != 0 && seen.Count < 256)
            {
                var node = queue.Dequeue(); if (!seen.Add(node)) continue;
                var element = node as FrameworkElement; MaterialScope scope;
                if (element != null && scopes.TryGetValue(element, out scope) && scope.Frame != null &&
                    (scope.Source.Root.IsVisible || (scope.LiveVisual != null && scope.LiveVisual.IsVisible)))
                { if (element is Window) windowScope = scope; }
                foreach (var parent in Parents(node)) queue.Enqueue(parent);
            }
            return windowScope;
        }
        public void Queue(MaterialSession session)
        {
            if (disposed) return; pending.Add(session);
            if (queued) return; queued = true;
            dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(Update));
        }
        internal void QueueAll() { foreach (var session in sessions.ToArray()) { session.Dirty = true; Queue(session); } }
        void Update()
        {
            queued = false; if (disposed) return;
            var batch = pending.ToArray(); pending.Clear();
            var watch = Stopwatch.StartNew();
            foreach (var session in batch)
            {
                try { session.Update(); }
                catch (Exception ex) { SurfaceFailed(session, ex); }
            }
            double elapsed = watch.Elapsed.TotalMilliseconds;
            samples.Enqueue(elapsed); while (samples.Count > 256) samples.Dequeue();

        }
        internal void SurfaceFailed(MaterialSession session, Exception exception)
        { session.Fail(); info("Dune v2 surface fallback until source/settings change: " + exception.Message); }
        internal void SourceMissing()
        { Reason("No ready registered window backdrop; using theme surfaces"); }
        internal void RecordRemap(double milliseconds)
        { remapCount++; remaps.Enqueue(milliseconds); while (remaps.Count > 256) remaps.Dequeue(); }
        public Brush Acquire(MaterialScope scope, MaterialRole role, out FrameworkElement coordinates, out bool live)
        {
            live = false; coordinates = scope.Frame == null ? null : scope.Frame.Coordinates;
            if (!Options.UseNewRenderer) return null;
            if (role == MaterialRole.Background && !scope.Source.BackgroundBlurEnabled) return null;
            var window = Window.GetWindow(scope.Anchor) ?? scope.Anchor as Window;
            if (window != null && window.WindowState == WindowState.Minimized) { realtime.Dispose(); return null; }
            var source = scope.LiveSource;
            bool recursive = source != null && sessions.Any(s => s.Active && (ReferenceEquals(s.Target, source) || source.IsAncestorOf(s.Target)));
            if (recursive) { Reason("live scene contains a material target; using theme surfaces"); return null; }
            var paint = realtime.Acquire(scope);
            if (paint == null) { Reason("live source unavailable/unsupported; using theme surfaces"); return null; }
            live = true;
            coordinates = ReferenceEquals(source, scope.Source.RawScene) ? scope.Frame.Coordinates : source;
            return scope.Backdrop.Compose(scope, paint, coordinates.RenderSize);
        }
        public void BackgroundReady(MaterialSession session, MaterialScope scope)
        {
            HashSet<MaterialSession> existing;
            if (leases.TryGetValue(scope, out existing) && existing.Contains(session)) return;
            ReleaseBackground(session);
            HashSet<MaterialSession> holders;
            if (!leases.TryGetValue(scope, out holders)) { holders = new HashSet<MaterialSession>(); leases.Add(scope, holders); }
            holders.Add(session); if (scope.Source != null) scope.Source.Suppress(true);
        }
        public void ReleaseBackground(MaterialSession session)
        {
            foreach (var entry in leases)
                if (entry.Value.Remove(session) && entry.Value.Count == 0 && entry.Key.Source != null) entry.Key.Source.Suppress(false);
        }
        void RestoreNative() { foreach (var scope in scopes.Values) if (scope.Source != null) scope.Source.Suppress(false); leases.Clear(); }
        void Reason(string value) { if (value != lastReason) { lastReason = value; info("Dune v2: " + value); } }
        void TierChanged(object sender, EventArgs args) { realtime.Dispose(); realtime = new RealtimeBackend(); foreach (var scope in scopes.Values) scope.Backdrop.Clear(); QueueAll(); }
        internal void WindowStateChanged() { realtime.Dispose(); realtime = new RealtimeBackend(); foreach (var scope in scopes.Values) scope.Backdrop.Clear(); QueueAll(); }
        public string Describe()
        {
            var ordered = samples.OrderBy(v => v).ToArray();
            double p95 = ordered.Length == 0 ? 0 : ordered[(int)Math.Floor((ordered.Length - 1) * .95)];
            int live = sessions.Count(s => s.Active && s.Target.IsVisible && s.HasMaterial && s.IsLive);
            int fallback = sessions.Count(s => s.Active && s.Target.IsVisible && !s.HasMaterial);
            var mappings = remaps.OrderBy(v => v).ToArray();
            double mappingP95 = mappings.Length == 0 ? 0 : mappings[(int)Math.Floor((mappings.Length - 1) * .95)];
            return string.Format("Material API 1\nRealtime materials enabled: {0}\nLayers: Background / Content / Flyout\nScopes: {1}; sessions: {2}\nPainted visible surfaces: {3}\nShared blur nodes: {4}; builds since reset: {5}\nUI rebuild batch P95: {6:F2} ms ({7} samples)\nScroll/layout remap P95: {8:F3} ms; changes: {9}\nLast fallback: {10}\nVisible surfaces using ordinary brushes: {11}\nGPU/present frame performance is not measured by this report.",
                Options.UseNewRenderer, scopes.Count, sessions.Count, live, realtime.NodeCount, realtime.BuildCount,
                p95, ordered.Length, mappingP95, remapCount, lastReason ?? "none", fallback);
        }
        public void Dispose()
        {
            if (disposed) return; RestoreNative(); disposed = true; RenderCapability.TierChanged -= TierChanged;
            foreach (var scope in scopes.Values) { scope.Backdrop.Clear(); if (scope.Source != null) scope.Source.Dispose(); }
            scopes.Clear(); pending.Clear(); realtime.Dispose();
        }
    }
}
