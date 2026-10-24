// Standalone WPF experiment, deliberately not compiled into the Playnite theme.
// The cached texture contains artwork only: never sample the foreground/card itself.
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Dune
{
    public sealed class MaterialLab : FrameworkElement
    {
        const int SceneWidth = 420, SceneHeight = 480;
        readonly BitmapSource artwork;
        readonly BitmapSource frosted;
        Rect card = new Rect(30, 190, 360, 190);
        Point lastPointer;
        bool dragging;
        int material = 1;

        public MaterialLab(string imagePath)
        {
            Width = SceneWidth;
            Height = SceneHeight;
            Focusable = true;
            artwork = MakeArtwork(imagePath);
            frosted = Frost(artwork);
            MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
                Focus();
                if (!card.Contains(e.GetPosition(this))) return;
                lastPointer = e.GetPosition(this);
                dragging = CaptureMouse();
            };
            MouseMove += delegate(object sender, MouseEventArgs e) {
                if (!dragging) return;
                Point p = e.GetPosition(this);
                card.X = Math.Max(0, Math.Min(SceneWidth - card.Width, card.X + p.X - lastPointer.X));
                card.Y = Math.Max(0, Math.Min(SceneHeight - card.Height, card.Y + p.Y - lastPointer.Y));
                lastPointer = p;
                InvalidateVisual(); // Move the crop, not the wallpaper or its cached blur.
            };
            MouseLeftButtonUp += delegate { dragging = false; ReleaseMouseCapture(); };
            LostMouseCapture += delegate { dragging = false; };
            KeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.Key == Key.Space) { material = (material + 1) % 3; InvalidateVisual(); }
            };
        }

        static SolidColorBrush Brush(string color)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }

        static void Text(DrawingContext dc, string text, double size, string color, double x, double y, bool bold)
        {
            var typeface = new Typeface(new FontFamily("Segoe UI, Microsoft YaHei UI"),
                FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal);
            dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, size, Brush(color), 1.0), new Point(x, y));
        }

        static BitmapSource Render(Visual visual, int width, int height)
        {
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        static BitmapSource MakeArtwork(string imagePath)
        {
            var scene = new DrawingVisual();
            using (DrawingContext dc = scene.RenderOpen())
            {
                dc.DrawRectangle(Brush("#14191F"), null, new Rect(0, 0, SceneWidth, SceneHeight));
                if (!String.IsNullOrEmpty(imagePath))
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(imagePath);
                    image.EndInit();
                    image.Freeze();
                    dc.DrawRectangle(new ImageBrush(image) { Stretch = Stretch.UniformToFill }, null,
                        new Rect(0, 0, SceneWidth, SceneHeight));
                }
                else
                {
                    // Synthetic artwork with saturated colors and fine details exposes blur quality.
                    var red = new RadialGradientBrush(Color.FromRgb(189, 49, 35), Colors.Transparent);
                    dc.DrawEllipse(red, null, new Point(130, 245), 260, 310);
                    var blue = new RadialGradientBrush(Color.FromRgb(28, 127, 181), Colors.Transparent);
                    dc.DrawEllipse(blue, null, new Point(390, 80), 250, 280);
                    for (int i = -10; i < 27; i++)
                        dc.DrawLine(new Pen(Brush("#303BD1EF"), 2), new Point(i * 23, 0), new Point(i * 23 - 230, SceneHeight));
                    var ridge = new StreamGeometry();
                    using (StreamGeometryContext g = ridge.Open())
                    {
                        g.BeginFigure(new Point(0, 480), true, true);
                        g.LineTo(new Point(145, 120), true, false);
                        g.LineTo(new Point(260, 385), true, false);
                        g.LineTo(new Point(350, 225), true, false);
                        g.LineTo(new Point(420, 480), true, false);
                    }
                    dc.DrawGeometry(Brush("#60101A28"), new Pen(Brush("#807FA1AF"), 2), ridge);
                }
            }
            return Render(scene, SceneWidth, SceneHeight);
        }

        static BitmapSource Frost(BitmapSource source)
        {
            // One half-resolution backdrop shared by all sample cards; 16 px here = 32 DIP at display size.
            var small = new TransformedBitmap(source, new ScaleTransform(0.5, 0.5));
            int w = small.PixelWidth, h = small.PixelHeight, stride = w * 4;
            byte[] pixels = new byte[stride * h];
            small.CopyPixels(pixels, stride, 0);
            for (int i = 0; i < pixels.Length; i += 4)
            {
                // Source is opaque. Adjust luminance/saturation before blur; never affect text.
                double luma = 0.0722 * pixels[i] + 0.7152 * pixels[i + 1] + 0.2126 * pixels[i + 2];
                for (int c = 0; c < 3; c++)
                    pixels[i + c] = (byte)Math.Max(0, Math.Min(255, (luma + (pixels[i + c] - luma) * 0.78) * 0.86));
            }
            var toned = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
            toned.Freeze();
            // Replicate edge pixels before blur so the viewport edges do not fade to transparent black.
            const int pad = 24;
            var padded = new DrawingVisual();
            using (DrawingContext dc = padded.RenderOpen())
            {
                dc.DrawImage(toned, new Rect(pad, pad, w, h));
                dc.DrawImage(new CroppedBitmap(toned, new Int32Rect(0, 0, w, 1)), new Rect(pad, 0, w, pad));
                dc.DrawImage(new CroppedBitmap(toned, new Int32Rect(0, h - 1, w, 1)), new Rect(pad, pad + h, w, pad));
                dc.DrawImage(new CroppedBitmap(toned, new Int32Rect(0, 0, 1, h)), new Rect(0, pad, pad, h));
                dc.DrawImage(new CroppedBitmap(toned, new Int32Rect(w - 1, 0, 1, h)), new Rect(pad + w, pad, pad, h));
                for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
                    dc.DrawImage(new CroppedBitmap(toned, new Int32Rect(x * (w - 1), y * (h - 1), 1, 1)),
                        new Rect(x * (w + pad), y * (h + pad), pad, pad));
            }
            padded.Effect = new BlurEffect { Radius = 16, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality };
            BitmapSource blurred = Render(padded, w + 2 * pad, h + 2 * pad);
            var crop = new CroppedBitmap(blurred, new Int32Rect(pad, pad, w, h));
            crop.Freeze();
            return crop;
        }

        void DrawScene(DrawingContext dc, Rect bounds, int variant)
        {
            dc.DrawImage(artwork, new Rect(0, 0, SceneWidth, SceneHeight));
            dc.PushClip(new RectangleGeometry(bounds, 16, 16));
            if (variant > 0)
            {
                // Draw at the scene origin, then clip. Scaling the whole bitmap into each card is incorrect.
                dc.PushOpacity(variant == 1 ? 1 : 0.86);
                dc.DrawImage(frosted, new Rect(0, 0, SceneWidth, SceneHeight));
                dc.Pop();
            }
            var fill = variant == 0
                ? new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#403D4148"), (Color)ColorConverter.ConvertFromString("#40272A30"), 90)
                : new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#6840464E"), (Color)ColorConverter.ConvertFromString("#6821252C"), 90);
            dc.DrawRectangle(fill, null, bounds);
            dc.Pop();
            var edge = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#24FFFFFF"), (Color)ColorConverter.ConvertFromString("#06FFFFFF"), 90);
            dc.DrawRoundedRectangle(null, new Pen(edge, 1), new Rect(bounds.X + 0.5, bounds.Y + 0.5, bounds.Width - 1, bounds.Height - 1), 16, 16);
            double x = bounds.X + 22, y = bounds.Y + 20;
            Text(dc, "成就", 13, "#C3CCD6", x, y, false);
            Text(dc, "⌄", 16, "#C3CCD6", bounds.Right - 34, y, false);
            Text(dc, "6 / 54", 30, "#F3F5F7", x, y + 36, true);
            Text(dc, "最近解锁 · 新的旅程", 13, "#CED5DF", x, y + 91, false);
            Rect track = new Rect(x, bounds.Bottom - 27, bounds.Width - 44, 4);
            dc.DrawRoundedRectangle(Brush("#28FFFFFF"), null, track, 2, 2);
            track.Width *= 6.0 / 54;
            dc.DrawRoundedRectangle(Brush("#59B5EF"), null, track, 2, 2);
        }

        protected override void OnRender(DrawingContext dc) { DrawScene(dc, card, material); }

        static void Save(Visual visual, int w, int h, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(Render(visual, w, h)));
            using (var file = File.Create(path)) encoder.Save(file);
        }

        public static void Export(string directory, string imagePath)
        {
            var sample = new MaterialLab(imagePath);
            var board = new DrawingVisual();
            using (DrawingContext dc = board.RenderOpen())
            {
                dc.DrawRectangle(Brush("#101318"), null, new Rect(0, 0, 1320, 650));
                Text(dc, "DUNE / 磨砂材质实验", 25, "#EDF1F5", 20, 20, true);
                Text(dc, "相同背景 · 相同采样位置 · 文字独立渲染", 14, "#AEBAC8", 20, 59, false);
                string[] labels = { "01  当前渐变半透明", "02  中性磨砂 / 建议", "03  高透磨砂" };
                for (int i = 0; i < 3; i++)
                {
                    Text(dc, labels[i], 16, "#E1E7EF", 20 + i * 440, 102, true);
                    dc.PushTransform(new TranslateTransform(10 + i * 440, 140));
                    sample.DrawScene(dc, sample.card, i);
                    dc.Pop();
                }
                Text(dc, "独立 WPF 渲染样板；非 Playnite 实机截图。背景可替换为本地游戏图片。", 12, "#92A0B0", 20, 625, false);
            }
            Save(board, 1320, 650, Path.Combine(directory, "comparison.png"));
            var alignment = new DrawingVisual();
            using (DrawingContext dc = alignment.RenderOpen())
            {
                dc.DrawRectangle(Brush("#101318"), null, new Rect(0, 0, 880, 560));
                Text(dc, "位置 A / 同一背景缓存", 16, "#E1E7EF", 20, 15, true);
                Text(dc, "位置 B / 移动后重新裁切", 16, "#E1E7EF", 460, 15, true);
                for (int i = 0; i < 2; i++)
                {
                    dc.PushTransform(new TranslateTransform(10 + i * 440, 55));
                    sample.DrawScene(dc, new Rect(25 + i * 35, 100 + i * 160, 330, 190), 1);
                    dc.Pop();
                }
            }
            Save(alignment, 880, 560, Path.Combine(directory, "alignment.png"));
        }

        public static void Show(string imagePath)
        {
            var sample = new MaterialLab(imagePath);
            var window = new Window { Title = "Dune material lab — 拖动卡片；空格切换材质", Content = sample,
                SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize,
                Background = Brushes.Black, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            window.ShowDialog();
        }
    }
}
