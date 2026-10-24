using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DuneMaterials
{
    // Runs entirely on a worker; accepts only an already-frozen source.
    internal static class PilotTexture
    {
        public static BitmapSource Build(BitmapSource source, CancellationToken cancel)
        { return Build(source, cancel, .018); }

        internal static BitmapSource Build(BitmapSource source, CancellationToken cancel, double blurFraction)
        {
            cancel.ThrowIfCancellationRequested();
            double scale = Math.Min(1, 640.0 / Math.Max(source.PixelWidth, source.PixelHeight));
            BitmapSource small = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            var converted = new FormatConvertedBitmap(small, PixelFormats.Pbgra32, null, 0);
            int w = converted.PixelWidth, h = converted.PixelHeight;
            byte[] pixels = new byte[w * h * 4], scratch = new byte[w * h * 4];
            converted.CopyPixels(pixels, w * 4, 0);
            // Three separable box passes approximate a Gaussian; edge pixels clamp.
            int radius = Math.Max(2, (int)Math.Round(Math.Min(w, h) * blurFraction));
            for (int pass = 0; pass < 3; pass++)
            {
                Blur(pixels, scratch, w, h, radius, true, cancel);
                Blur(scratch, pixels, w, h, radius, false, cancel);
            }
            for (int i = 0; i < pixels.Length; i += 4)
            {
                double grey = pixels[i] * .0722 + pixels[i + 1] * .7152 + pixels[i + 2] * .2126;
                for (int c = 0; c < 3; c++) pixels[i + c] = (byte)((grey + (pixels[i + c] - grey) * .82) * .96);
            }
            cancel.ThrowIfCancellationRequested();
            var result = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixels, w * 4);
            result.Freeze();
            return result;
        }

        static void Blur(byte[] input, byte[] output, int w, int h, int r, bool horizontal, CancellationToken cancel)
        {
            int lines = horizontal ? h : w, length = horizontal ? w : h;
            int step = horizontal ? 4 : w * 4, diameter = r * 2 + 1;
            for (int line = 0; line < lines; line++)
            {
                cancel.ThrowIfCancellationRequested();
                int start = horizontal ? line * w * 4 : line * 4;
                for (int channel = 0; channel < 4; channel++)
                {
                    int sum = 0;
                    for (int p = -r; p <= r; p++) sum += input[start + Math.Max(0, Math.Min(length - 1, p)) * step + channel];
                    for (int p = 0; p < length; p++)
                    {
                        output[start + p * step + channel] = (byte)(sum / diameter);
                        sum += input[start + Math.Min(length - 1, p + r + 1) * step + channel]
                             - input[start + Math.Max(0, p - r) * step + channel];
                    }
                }
            }
        }
    }
}
