using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DuneMaterials.Rendering
{
    // Shared composition above the blur backend and below surface-specific tints.
    // Both backends use identical page masking/darkening; no UI is captured.
    internal sealed class BackdropScene
    {
        sealed class Entry
        {
            internal object Paint;
            internal Brush Base;
            internal long Version;
            internal Size Size;
            internal DrawingBrush Result;
        }
        readonly Dictionary<BlurLevel, Entry> entries = new Dictionary<BlurLevel, Entry>();
        static readonly Brush neutral = Neutral();
        static Brush Neutral() { var brush = new SolidColorBrush(Color.FromRgb(40, 45, 53)); brush.Freeze(); return brush; }
        internal int Count { get { return entries.Count; } }
        internal Brush Compose(MaterialScope scope, BlurLevel level, Brush paint, Size size)
        {
            if (!scope.IsWindowBackdrop) return paint;
            var background = scope.Anchor.TryFindResource("WindowBackgroundBrush") as Brush ?? neutral;
            object identity = paint;
            var image = paint as ImageBrush; if (image != null) identity = image.ImageSource;
            var visual = paint as VisualBrush; if (visual != null) identity = visual.Visual;
            Entry old;
            if (entries.TryGetValue(level, out old) && ReferenceEquals(old.Paint, identity) &&
                ReferenceEquals(old.Base, background) && old.Version == scope.Frame.Version && old.Size == size) return old.Result;
            var artwork = new DrawingGroup();
            using (var dc = artwork.Open()) {
                dc.DrawRectangle(paint, null, new Rect(size));
                if (scope.Source.PageDarkening != null) dc.DrawRectangle(scope.Source.PageDarkening, null, new Rect(size));
            }
            artwork.OpacityMask = scope.Source.PageMask;
            var composite = new DrawingGroup();
            using (var dc = composite.Open()) {
                // Filling the base here makes cards and uncovered page regions
                // resolve fading artwork against exactly the same neutral color.
                dc.DrawRectangle(background, null, new Rect(size)); dc.DrawDrawing(artwork);
            }
            var result = new DrawingBrush(composite) { Stretch = Stretch.Fill };
            entries[level] = new Entry { Paint = identity, Base = background, Version = scope.Frame.Version, Size = size, Result = result };
            return result;
        }
        internal void Clear() { entries.Clear(); }
    }
}
