using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace DuneMaterials
{
    // UI-thread only. Captures artwork, never a visual ancestor of a material surface.
    public static class BackdropRenderer
    {
        static readonly Color BaseColor = Color.FromRgb(40, 45, 53);

        public static BitmapSource Capture(FrameworkElement source, int width, int height, Color baseColor)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(baseColor), null, new Rect(0, 0, width, height));
                var brush = new VisualBrush(source) {
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(0, 0, source.ActualWidth, source.ActualHeight),
                    Stretch = Stretch.Fill
                };
                dc.DrawRectangle(brush, null, new Rect(0, 0, width, height));
            }
            return Render(visual, width, height);
        }

        public static BitmapSource Render(Visual source, int width, int height)
        {
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(source);
            bitmap.Freeze();
            return bitmap;
        }

        public static ulong Signature(BitmapSource image)
        {
            byte[] pixels = new byte[image.PixelWidth * image.PixelHeight * 4];
            image.CopyPixels(pixels, image.PixelWidth * 4, 0);
            ulong hash = 14695981039346656037UL;
            foreach (byte value in pixels) { hash ^= value; hash = unchecked(hash * 1099511628211UL); }
            return hash;
        }

        public static BitmapSource Frost(BitmapSource source, double radius)
        {
            int width = source.PixelWidth, height = source.PixelHeight, stride = width * 4;
            byte[] pixels = new byte[stride * height];
            source.CopyPixels(pixels, stride, 0);
            for (int i = 0; i < pixels.Length; i += 4)
            {
                double luminance = .0722 * pixels[i] + .7152 * pixels[i + 1] + .2126 * pixels[i + 2];
                for (int c = 0; c < 3; c++)
                    pixels[i + c] = (byte)Math.Max(0, Math.Min(255, (luminance + (pixels[i + c] - luminance) * .78) * .86));
            }
            var toned = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
            toned.Freeze();
            int pad = Math.Max(2, (int)Math.Ceiling(radius * 1.5));
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(toned, new Rect(pad, pad, width, height));
                dc.DrawImage(new CroppedBitmap(toned, new Int32Rect(0, 0, width, 1)), new Rect(pad, 0, width, pad));
                dc.DrawImage(new CroppedBitmap(toned, new Int32Rect(0, height - 1, width, 1)), new Rect(pad, height + pad, width, pad));
                dc.DrawImage(new CroppedBitmap(toned, new Int32Rect(0, 0, 1, height)), new Rect(0, pad, pad, height));
                dc.DrawImage(new CroppedBitmap(toned, new Int32Rect(width - 1, 0, 1, height)), new Rect(width + pad, pad, pad, height));
                for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
                    dc.DrawImage(new CroppedBitmap(toned, new Int32Rect(x * (width - 1), y * (height - 1), 1, 1)),
                        new Rect(x * (width + pad), y * (height + pad), pad, pad));
            }
            visual.Effect = new BlurEffect { Radius = radius, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality };
            var blurred = Render(visual, width + pad * 2, height + pad * 2);
            var result = new CroppedBitmap(blurred, new Int32Rect(pad, pad, width, height));
            result.Freeze();
            return result;
        }

        public static Brush Material(BitmapSource image, Rect sample, bool popup, bool hover, bool pressed)
        {
            var drawing = new DrawingGroup();
            using (var dc = drawing.Open())
            {
                var unit = new Rect(0, 0, 1, 1);
                // Opaque backing also covers out-of-bounds crops near popup/window edges.
                dc.DrawRectangle(new SolidColorBrush(BaseColor), null, unit);
                dc.DrawRectangle(new ImageBrush(image) {
                    ViewboxUnits = BrushMappingMode.RelativeToBoundingBox, Viewbox = sample,
                    Stretch = Stretch.Fill, TileMode = TileMode.None
                }, null, unit);
                byte alpha = popup ? (byte)180 : (byte)104;
                Color top = Color.FromArgb(alpha, 64, 70, 78);
                Color bottom = Color.FromArgb(alpha, 33, 37, 44);
                dc.DrawRectangle(new LinearGradientBrush(top, bottom, 90), null, unit);
                if (hover) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(10, 255, 255, 255)), null, unit);
                if (pressed) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)), null, unit);
            }
            drawing.Freeze();
            var result = new DrawingBrush(drawing) { Stretch = Stretch.Fill };
            result.Freeze();
            return result;
        }
    }
}
