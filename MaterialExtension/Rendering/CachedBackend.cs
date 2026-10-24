using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DuneMaterials.Rendering
{
    internal sealed class CachedBackend : IMaterialBackend
    {
        sealed class Identity { public long Value; }
        sealed class Entry { public BitmapSource Result; public Task Job; public long Bytes, Stamp, Identity; public CancellationTokenSource Cancel; public bool Obsolete; }
        readonly ConditionalWeakTable<BitmapSource, Identity> identities = new ConditionalWeakTable<BitmapSource, Identity>();
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        readonly Dictionary<MaterialScope, long> activeSources = new Dictionary<MaterialScope, long>();
        readonly SemaphoreSlim worker = new SemaphoreSlim(1, 1);
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly Dispatcher dispatcher;
        readonly Action changed;
        readonly Action<string> info;
        long nextIdentity, clock, bytes;
        bool disposed;
        const long Budget = 32 * 1024 * 1024;
        public string Name { get { return "Cached"; } }
        public long Bytes { get { return bytes; } }
        public CachedBackend(Dispatcher ui, Action update, Action<string> diagnostics) { dispatcher = ui; changed = update; info = diagnostics; }
        public Brush Acquire(MaterialScope scope, BlurLevel level)
        {
            var frame = scope.Frame;
            if (frame == null || disposed) return null;
            Identity id;
            if (!identities.TryGetValue(frame.Bitmap, out id)) { id = new Identity { Value = ++nextIdentity }; identities.Add(frame.Bitmap, id); }
            activeSources[scope] = id.Value; CancelUnused();
            double fraction = MaterialProfiles.BlurFraction(level, scope.Source.PageBlurScale);
            string key = id.Value + ":" + level + ":" + fraction.ToString("R", CultureInfo.InvariantCulture);
            Entry entry;
            if (!entries.TryGetValue(key, out entry))
            {
                // Bound pending work too; a cycling/unsupported source must not queue indefinitely.
                if (entries.Count >= 32) Evict(true);
                if (entries.Count >= 32) return null;
                entry = new Entry { Identity = id.Value, Cancel = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token) }; entries.Add(key, entry);
                var token = entry.Cancel.Token;
                var bitmap = frame.Bitmap;
                var target = entry;
                entry.Job = Task.Run(delegate {
                    try
                    {
                        worker.Wait(token);
                        BitmapSource result;
                        var watch = Stopwatch.StartNew();
                        try { result = PilotTexture.Build(bitmap, token, fraction); }
                        finally { worker.Release(); }
                        watch.Stop();
                        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate {
                            if (disposed) return;
                            if (token.IsCancellationRequested) { Remove(key, target); changed(); return; }
                            target.Result = result; target.Bytes = result.PixelWidth * (long)result.PixelHeight * 4;
                            bytes += target.Bytes; target.Stamp = ++clock; Evict(false);
                            info("Dune v2: " + level + " texture " + result.PixelWidth + "x" + result.PixelHeight + ", worker=" + watch.ElapsedMilliseconds + "ms");
                            changed();
                        }));
                    }
                    catch (OperationCanceledException)
                    { dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate { if (!disposed) { Remove(key, target); changed(); } })); }
                    catch (Exception ex)
                    {
                        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate {
                            if (disposed) return;
                            info("Dune v2: cached source failed: " + ex.Message);
                            // Keep the failed entry until source/version changes; no retry loop.
                        }));
                    }
                });
            }
            entry.Stamp = ++clock;
            if (entry.Result == null) return null;
            var brush = new ImageBrush(entry.Result) { Stretch = frame.Stretch, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
            brush.Freeze(); return brush;
        }
        void CancelUnused()
        {
            var used = new HashSet<long>(activeSources.Values);
            foreach (var entry in entries.Values)
                if (entry.Result == null && !entry.Obsolete && !used.Contains(entry.Identity))
                { entry.Obsolete = true; entry.Cancel.Cancel(); }
        }
        internal void Release(MaterialScope scope) { activeSources.Remove(scope); CancelUnused(); }
        internal void Suspend() { activeSources.Clear(); CancelUnused(); }
        void Remove(string key, Entry expected)
        {
            Entry current;
            if (!entries.TryGetValue(key, out current) || current != expected) return;
            bytes -= current.Bytes; entries.Remove(key);
            current.Job.ContinueWith(delegate { current.Cancel.Dispose(); });
        }
        void Evict(bool force)
        {
            while (bytes > Budget || (force && entries.Count >= 32))
            {
                string oldest = null; long age = long.MaxValue;
                foreach (var item in entries)
                    if ((item.Value.Result != null || (item.Value.Job != null && item.Value.Job.IsCompleted)) && item.Value.Stamp < age) { oldest = item.Key; age = item.Value.Stamp; }
                if (oldest == null) break;
                Remove(oldest, entries[oldest]);
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; lifetime.Cancel();
            // Cancellation source must outlive worker tokens/Wait registrations.
            var tasks = new List<Task>(); foreach (var entry in entries.Values) if (entry.Job != null) tasks.Add(entry.Job);
            var oldEntries = new List<Entry>(entries.Values);
            Task.WhenAll(tasks).ContinueWith(delegate(Task completed) { var ignored = completed.Exception; foreach (var entry in oldEntries) entry.Cancel.Dispose(); lifetime.Dispose(); worker.Dispose(); });
            activeSources.Clear();
            entries.Clear(); bytes = 0;
        }
    }
}
