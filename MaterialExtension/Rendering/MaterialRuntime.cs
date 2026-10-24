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
        CachedBackend cached;
        RealtimeBackend realtime = new RealtimeBackend();
        bool disposed, queued, blocked;
        int slowBatches;
        string lastReason;
        public MaterialOptions Options { get; private set; }
        public MaterialRuntime(MaterialOptions options, Action<string> diagnostics)
        {
            dispatcher = Application.Current.Dispatcher; info = diagnostics;
            Options = options.Copy(); cached = new CachedBackend(dispatcher, QueueAll, info);
            RenderCapability.TierChanged += TierChanged;
        }
        public void Configure(MaterialOptions options)
        {
            foreach (var session in sessions.ToArray()) { session.ClearFailure(); session.Fallback(); }
            RestoreNative(); realtime.Dispose(); realtime = new RealtimeBackend();
            foreach (var scope in scopes.Values) scope.Backdrop.Clear();
            Options = options.Copy(); blocked = false; slowBatches = 0; lastReason = null; samples.Clear(); remaps.Clear(); remapCount = 0;
            if (!Options.UseNewRenderer) cached.Suspend();
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
                leases.Remove(scope); cached.Release(scope); realtime.Release(scope); scope.Backdrop.Clear();
                if (scope.Source != null) scope.Source.Dispose();
                scope.Source = new NativeSourceAdapter(source, delegate { scope.Notify(); QueueAll(); });
            }
            scope.Notify(); QueueAll(); return scope;
        }
        public void UnregisterSource(MaterialScope scope, FrameworkElement source, bool live)
        {
            if (scope == null) return;
            if (live) { if (scope.LiveVisual == source) scope.LiveVisual = null; }
            else if (scope.Source != null && scope.Source.Root == source)
            { scope.Source.Dispose(); scope.Source = null; leases.Remove(scope); cached.Release(scope); realtime.Release(scope); scope.Backdrop.Clear(); }
            if (scope.Source == null && scope.LiveVisual == null) { scopes.Remove(scope.Anchor); leases.Remove(scope); realtime.Release(scope); cached.Release(scope); }
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
                { if (element is Window) windowScope = scope; else return scope; }
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
            if (Options.Mode == MaterialMode.Realtime && elapsed > 8) slowBatches++; else slowBatches = 0;
            if (slowBatches >= 3 && !blocked)
            {
                blocked = true; realtime.Dispose(); realtime = new RealtimeBackend();
                foreach (var scope in scopes.Values) scope.Backdrop.Clear();
                Reason("realtime UI batch guard exceeded; using cached backend"); QueueAll();
            }
        }
        internal void SurfaceFailed(MaterialSession session, Exception exception)
        { session.Fail(); info("Dune v2 surface fallback until source/settings change: " + exception.Message); }
        internal void RecordRemap(double milliseconds)
        { remapCount++; remaps.Enqueue(milliseconds); while (remaps.Count > 256) remaps.Dequeue(); }
        public Brush Acquire(MaterialScope scope, MaterialRole role, out FrameworkElement coordinates, out bool live)
        {
            live = false; coordinates = scope.Frame == null ? null : scope.Frame.Coordinates;
            if (!Options.UseNewRenderer) return null;
            if (role == MaterialRole.Background && (!scope.Source.BackgroundBlurEnabled || scope.Source.PageBlurScale == 0)) return null;
            var window = Window.GetWindow(scope.Anchor) ?? scope.Anchor as Window;
            if (window != null && window.WindowState == WindowState.Minimized) { realtime.Dispose(); return null; }
            BlurLevel level = MaterialProfiles.LevelFor(role, scope.IsWindowBackdrop);
            if (Options.Mode == MaterialMode.Realtime && Options.AllowExperimentalRealtime && !blocked)
            {
                var source = scope.LiveSource;
                bool recursive = source != null && sessions.Any(s => s.Active && (ReferenceEquals(s.Target, source) || source.IsAncestorOf(s.Target)));
                if (!recursive)
                {
                    var paint = realtime.Acquire(scope, level);
                    if (paint != null) { live = true; coordinates = scope.Source != null && ReferenceEquals(source, scope.Source.RawScene) ? scope.Frame.Coordinates : source;
                        return scope.Backdrop.Compose(scope, level, paint, coordinates.RenderSize); }
                }
                Reason(recursive ? "live scene contains a material target" : "live source unavailable/unsupported; using cached backend");
            }
            else if (Options.Mode == MaterialMode.Realtime) Reason("realtime preview requires explicit approval and hardware/source support; using cached backend");
            var cachedPaint = cached.Acquire(scope, level);
            return cachedPaint == null ? null : scope.Backdrop.Compose(scope, level, cachedPaint, coordinates.RenderSize);
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
            int cachedSurfaces = sessions.Count(s => s.Active && s.Target.IsVisible && s.HasMaterial && !s.IsLive);
            var mappings = remaps.OrderBy(v => v).ToArray();
            double mappingP95 = mappings.Length == 0 ? 0 : mappings[(int)Math.Floor((mappings.Length - 1) * .95)];
            return string.Format("Material API 1\nNew renderer: {0}\nRequested mode: {1}\nScopes: {2}; sessions: {3}\nPainted visible surfaces: Realtime={4}; Cached={5}\nRealtime shared blur nodes: {6}; builds since reset: {7}\nCached output: {8:F2} MiB\nUI rebuild batch P95: {9:F2} ms ({10} samples)\nScroll/layout remap P95: {12:F3} ms per changed surface; changes: {13}\nLast fallback: {11}\nGPU/present frame performance is NOT measured by this report.",
                Options.UseNewRenderer, Options.Mode, scopes.Count, sessions.Count, live, cachedSurfaces, realtime.NodeCount, realtime.BuildCount,
                cached.Bytes / 1048576.0, p95, ordered.Length, lastReason ?? "none", mappingP95, remapCount);
        }
        public void Dispose()
        {
            if (disposed) return; RestoreNative(); disposed = true; RenderCapability.TierChanged -= TierChanged;
            foreach (var scope in scopes.Values) { scope.Backdrop.Clear(); if (scope.Source != null) scope.Source.Dispose(); }
            scopes.Clear(); pending.Clear(); cached.Dispose(); realtime.Dispose();
        }
    }
}
