using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace DuneMaterials
{
    // UI-thread dictionary; only frozen bitmaps and cancellation tokens enter workers.
    internal sealed class MaterialTextureCache : IDisposable
    {
        readonly Dictionary<BitmapSource, Task<BitmapSource>> jobs = new Dictionary<BitmapSource, Task<BitmapSource>>();
        readonly Action<string> info;
        readonly SemaphoreSlim worker = new SemaphoreSlim(1, 1);
        CancellationTokenSource lifetime = new CancellationTokenSource();
        public MaterialTextureCache(Action<string> diagnostics) { info = diagnostics; }

        public Task<BitmapSource> Get(BitmapSource bitmap)
        {
            Task<BitmapSource> job;
            if (jobs.TryGetValue(bitmap, out job)) return job;
            if (jobs.Count >= 8)
            {
                BitmapSource expired = null;
                foreach (var entry in jobs) if (entry.Value.IsCompleted) { expired = entry.Key; break; }
                if (expired == null) throw new InvalidOperationException("Material worker queue is full; retaining XAML fallback.");
                jobs.Remove(expired);
            }
            var token = lifetime.Token;
            job = Task.Run(delegate {
                worker.Wait(token);
                try
                {
                    var watch = Stopwatch.StartNew();
                    var result = PilotTexture.Build(bitmap, token);
                    info(string.Format("Dune Materials: shared texture {0}x{1}, worker={2}ms", result.PixelWidth, result.PixelHeight, watch.ElapsedMilliseconds));
                    return result;
                }
                finally { worker.Release(); }
            }, token);
            jobs.Add(bitmap, job);
            return job;
        }

        public void Reset()
        {
            var previous = lifetime;
            previous.Cancel();
            var pending = new List<Task>(jobs.Values);
            jobs.Clear();
            lifetime = new CancellationTokenSource();
            Task.WhenAll(pending).ContinueWith(delegate(Task finished) {
                // Observe faults from jobs whose surfaces have already detached.
                var ignored = finished.Exception;
                previous.Dispose();
            });
        }

        public void Dispose() { Reset(); lifetime.Dispose(); }
    }
}
