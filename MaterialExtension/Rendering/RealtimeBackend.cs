using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DuneMaterials.Rendering
{
    internal sealed class RealtimeBackend : IDisposable
    {
        sealed class Node { public FrameworkElement Source; public Size Size; public double Strength; public FrameworkElement Blur; public Brush Brush; }
        readonly Dictionary<MaterialScope, Node> scopes = new Dictionary<MaterialScope, Node>();
        public long BuildCount { get; private set; }
        public int NodeCount
        { get { return scopes.Count; } }
        public static string IneligibleReason(FrameworkElement source, bool owned)
        {
            if ((RenderCapability.Tier >> 16) < 2 || RenderOptions.ProcessRenderMode == RenderMode.SoftwareOnly) return "WPF hardware tier 2 is required";
            if (source == null || (!owned && !source.IsVisible) || source.ActualWidth < 1 || source.ActualHeight < 1) return "no visible explicit live scene";
            var seen = new HashSet<DependencyObject>(); var queue = new Queue<DependencyObject>(); queue.Enqueue(source);
            while (queue.Count != 0)
            {
                var node = queue.Dequeue(); if (!seen.Add(node)) continue;
                if (seen.Count > 2048) return "live scene graph exceeds safety limit";
                var element = node as FrameworkElement;
                if (node is HwndHost || node is MaterialPresenter || (element != null && element.Uid.StartsWith("Dune.Material.", StringComparison.Ordinal)))
                    return "scene contains an HWND or material output (unsupported/recursive)";
                // A video is often referenced by a Rectangle's VisualBrush rather than parented
                // inside the scene. Follow that edge too, with cycle detection.
                var shape = node as Shape;
                if (shape != null) { AddVisual(shape.Fill, queue); AddVisual(shape.Stroke, queue); }
                var border = node as Border; if (border != null) AddVisual(border.Background, queue);
                var panel = node as Panel; if (panel != null) AddVisual(panel.Background, queue);
                var control = node as Control; if (control != null) AddVisual(control.Background, queue);
                var ui = node as UIElement; if (ui != null) AddVisual(ui.OpacityMask, queue);
                if (node is Visual)
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) queue.Enqueue(VisualTreeHelper.GetChild(node, i));
            }
            return null;
        }
        static void AddVisual(Brush brush, Queue<DependencyObject> queue)
        { var reference = brush as VisualBrush; if (reference != null && reference.Visual != null) queue.Enqueue(reference.Visual); }
        public Brush Acquire(MaterialScope scope)
        {
            var source = scope.LiveSource;
            if (IneligibleReason(source, scope.Source != null && ReferenceEquals(source, scope.Source.RawScene)) != null) return null;
            Node node;
            var size = source.RenderSize;
            double strength = scope.Source.BlurRadius;
            if (!scope.Source.BackgroundBlurEnabled) strength = 0;
            if (scopes.TryGetValue(scope, out node) && node.Source == source && node.Size == size && node.Strength == strength) return node.Brush;
            // Mirror the edge before blur instead of blending with transparent
            // pixels outside the scene (which otherwise produces dark window bands).
            // Leave room for at most 66 DIPs of padding on each edge.
            double scale = Math.Min(1, 892 / Math.Max(size.Width, size.Height));
            double radius = Math.Min(64, strength * scale);
            double padding = Math.Ceiling(radius) + 2;
            var content = new Rect(padding, padding, size.Width * scale, size.Height * scale);
            var graded = new Border {
                Width = content.Width + padding * 2, Height = content.Height + padding * 2,
                Background = new VisualBrush(source) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(size),
                    ViewportUnits = BrushMappingMode.Absolute, Viewport = content, TileMode = TileMode.FlipXY, Stretch = Stretch.Fill },
                Effect = new MaterialColorEffect()
            };
            var blurred = new Border {
                Width = graded.Width, Height = graded.Height, Child = graded,
                Effect = new BlurEffect { Radius = radius, RenderingBias = RenderingBias.Quality },
                CacheMode = new BitmapCache(1) { EnableClearType = false, SnapsToDevicePixels = true }
            };
            blurred.Measure(new Size(blurred.Width, blurred.Height)); blurred.Arrange(new Rect(0, 0, blurred.Width, blurred.Height));
            var brush = new VisualBrush(blurred) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = content, Stretch = Stretch.Fill };
            // A live VisualBrush cannot be frozen or sent to workers. WPF owns updates.
            scopes[scope] = new Node { Source = source, Size = size, Strength = strength, Blur = blurred, Brush = brush };
            BuildCount++;
            return brush;
        }
        public void Dispose() { scopes.Clear(); }
        internal void Release(MaterialScope scope) { scopes.Remove(scope); }
    }
}
