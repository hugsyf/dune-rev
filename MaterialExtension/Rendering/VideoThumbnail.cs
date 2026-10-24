using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DuneMaterials.Rendering
{
    // A separate, muted first-frame preview. It never borrows screenshot artwork,
    // starts playback, captures WPF pixels, or opens a second network stream.
    internal sealed class VideoThumbnail : Grid, IDisposable
    {
        readonly Image frame = new Image { Stretch = Stretch.UniformToFill };
        MediaPlayer player;
        Uri source;
        internal ImageSource Frame { get { return frame.Source; } }
        public VideoThumbnail()
        {
            Width = 90; Height = 50; ClipToBounds = true;
            Background = new LinearGradientBrush(Color.FromRgb(35, 46, 59), Color.FromRgb(15, 21, 30), 90);
            Children.Add(new TextBlock {
                Text = "\uE714", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 22, Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            });
            Children.Add(frame);
            var badge = new Border { Background = new SolidColorBrush(Color.FromArgb(190, 10, 14, 20)), CornerRadius = new CornerRadius(3), Padding = new Thickness(4, 1, 4, 1), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(3) };
            badge.Child = new TextBlock { Text = "视频", FontSize = 11, Foreground = Brushes.White };
            Children.Add(badge);
        }
        internal void Update(Uri next)
        {
            if (Equals(source, next)) return;
            Close(); source = next;
            if (next == null || !next.IsAbsoluteUri || !next.IsFile || next.IsUnc || !File.Exists(next.LocalPath)) return;
            var candidate = new MediaPlayer { IsMuted = true, Volume = 0, ScrubbingEnabled = true };
            player = candidate;
            candidate.MediaOpened += delegate {
                if (player != candidate) return;
                candidate.Pause();
                if (candidate.NaturalVideoWidth <= 0 || candidate.NaturalVideoHeight <= 0) { Close(); return; }
                if (candidate.NaturalDuration.HasTimeSpan && candidate.NaturalDuration.TimeSpan.TotalSeconds > 0)
                    candidate.Position = TimeSpan.FromSeconds(Math.Min(2, candidate.NaturalDuration.TimeSpan.TotalSeconds * .1));
                frame.Source = new DrawingImage(new VideoDrawing {
                    Player = candidate, Rect = new Rect(0, 0, candidate.NaturalVideoWidth, candidate.NaturalVideoHeight)
                });
            };
            candidate.MediaFailed += delegate { if (player == candidate) Close(); };
            try { candidate.Open(next); candidate.Pause(); }
            catch (InvalidOperationException) { Close(); }
            catch (NotSupportedException) { Close(); }
            catch (IOException) { Close(); }
        }
        void Close()
        {
            frame.Source = null;
            var previous = player; player = null;
            if (previous != null) previous.Close();
        }
        public void Dispose() { Close(); source = null; }
    }
}
