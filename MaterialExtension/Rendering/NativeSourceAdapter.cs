using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DuneMaterials.Rendering
{
    // The sole compatibility boundary for native FadeImage internals. Never scans a window.
    internal sealed class NativeSourceAdapter : IDisposable
    {
        readonly FrameworkElement root;
        readonly Action changed;
        readonly List<Image> images = new List<Image>();
        readonly List<DependencyPropertyDescriptor> preferences = new List<DependencyPropertyDescriptor>();
        readonly DependencyPropertyDescriptor sourceProperty = DependencyPropertyDescriptor.FromProperty(Image.SourceProperty, typeof(Image));
        bool disposed, queued, suppressing;
        double? savedOpacity;
        long version;
        Image rawScene;
        public FrameworkElement RawScene
        {
            get
            {
                if (Frame == null) return null;
                if (rawScene == null) rawScene = new Image();
                if (!ReferenceEquals(rawScene.Source, Frame.Bitmap)) rawScene.Source = Frame.Bitmap;
                if (rawScene.Stretch != Frame.Stretch) rawScene.Stretch = Frame.Stretch;
                var size = Frame.Coordinates.RenderSize;
                if (size.Width > 0 && size.Height > 0 && (rawScene.RenderSize != size || !rawScene.IsMeasureValid || !rawScene.IsArrangeValid))
                { rawScene.Width = size.Width; rawScene.Height = size.Height; rawScene.Measure(size); rawScene.Arrange(new Rect(size)); }
                return rawScene;
            }
        }
        public MaterialFrame Frame { get; private set; }
        public FrameworkElement Root { get { return root; } }
        public NativeSourceAdapter(FrameworkElement source, Action update)
        {
            root = source; changed = update;
            root.Loaded += Loaded; root.IsVisibleChanged += VisibilityChanged;
            foreach (string name in new[] { "ImageOpacityMask", "ImageDarkeningBrush", "IsBlurEnabled", "BlurAmount" })
            {
                var descriptor = DependencyPropertyDescriptor.FromName(name, root.GetType(), root.GetType());
                if (descriptor != null) { preferences.Add(descriptor); descriptor.AddValueChanged(root, PreferenceChanged); }
            }
            var direct = root as Image;
            if (direct != null)
            {
                images.Add(direct); sourceProperty.AddValueChanged(direct, SourceChanged);
                SetFrame(direct); // Explicit screenshot Images need no delayed visual-tree discovery.
            }
            else Queue();
        }
        void PreferenceChanged(object sender, EventArgs args)
        {
            if (Frame != null) Frame = new MaterialFrame(++version, Frame.Bitmap, Frame.Coordinates, Frame.Stretch);
            changed();
        }
        public bool BackgroundBlurEnabled
        {
            get { var property = root.GetType().GetProperty("IsBlurEnabled"); return property == null || !object.Equals(property.GetValue(root, null), false); }
        }
        public double PageBlurScale
        {
            get
            {
                var property = root.GetType().GetProperty("BlurAmount");
                double amount = property == null ? 78 : Convert.ToDouble(property.GetValue(root, null));
                return Math.Round(Math.Max(0, Math.Min(3, amount / 78)), 2);
            }
        }
        internal static IEnumerable<DependencyObject> Descendants(DependencyObject node)
        {
            if (node == null) yield break;
            yield return node;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                foreach (var child in Descendants(VisualTreeHelper.GetChild(node, i))) yield return child;
        }
        void Loaded(object sender, RoutedEventArgs args) { Queue(); }
        void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) { changed(); }
        void Queue()
        {
            if (queued || disposed) return; queued = true;
            root.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate {
                queued = false; if (disposed) return;
                foreach (var image in Descendants(root).OfType<Image>())
                    if (!images.Contains(image)) { images.Add(image); sourceProperty.AddValueChanged(image, SourceChanged); }
                SetFrame(images.Where(i => i.Source is BitmapSource).OrderByDescending(i => i.Opacity).FirstOrDefault());
            }));
        }
        void SourceChanged(object sender, EventArgs args) { SetFrame(sender as Image); }
        void SetFrame(Image image)
        {
            if (disposed) return;
            var bitmap = image == null ? null : image.Source as BitmapSource;
            if (bitmap == null)
            {
                if (Frame != null && Frame.Coordinates == image)
                {
                    Frame = null; if (rawScene != null) rawScene.Source = null;
                    var other = images.Where(i => i != image && i.Source is BitmapSource).OrderByDescending(i => i.Opacity).FirstOrDefault();
                    if (other != null) SetFrame(other); else changed();
                }
                return;
            }
            if (!bitmap.IsFrozen || bitmap.IsDownloading) return;
            if (Frame != null && ReferenceEquals(Frame.Bitmap, bitmap) && ReferenceEquals(Frame.Coordinates, image)) return;
            Frame = new MaterialFrame(++version, bitmap, image, image.Stretch); changed();
        }
        public Brush PageMask { get { return ReadBrush("ImageOpacityMask"); } }
        public Brush PageDarkening { get { return ReadBrush("ImageDarkeningBrush"); } }
        Brush ReadBrush(string name)
        {
            var property = root.GetType().GetProperty(name);
            return property == null ? null : property.GetValue(root, null) as Brush;
        }
        public void Suppress(bool value)
        {
            if (suppressing || disposed) return;
            suppressing = true;
            try
            {
                if (value && savedOpacity == null) { savedOpacity = root.Opacity; root.SetCurrentValue(UIElement.OpacityProperty, 0.0); }
                else if (!value && savedOpacity != null)
                { if (root.Opacity == 0) root.SetCurrentValue(UIElement.OpacityProperty, savedOpacity.Value); savedOpacity = null; }
            }
            finally { suppressing = false; }
        }
        public void Dispose()
        {
            Suppress(false); disposed = true;
            root.Loaded -= Loaded; root.IsVisibleChanged -= VisibilityChanged;
            foreach (var image in images) sourceProperty.RemoveValueChanged(image, SourceChanged);
            foreach (var descriptor in preferences) descriptor.RemoveValueChanged(root, PreferenceChanged); preferences.Clear();
            images.Clear(); Frame = null; if (rawScene != null) rawScene.Source = null; rawScene = null;
        }
    }
}
