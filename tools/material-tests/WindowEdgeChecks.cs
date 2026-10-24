using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DuneMaterials.Rendering;

internal static class WindowEdgeChecks
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        var target = new Grid { Width = 800, Height = 420 };
        var original = new RectangleGeometry(new Rect(0, 0, 800, 420)); target.Clip = original;
        target.Measure(new Size(800, 420)); target.Arrange(new Rect(0, 0, 800, 420));
        var host = new ContentControl { DataContext = target, Tag = new CornerRadius(24) };
        var registration = new RoundedClipRegistration(host);
        registration.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Require(!target.Clip.FillContains(new Point(799, 419)) && target.Clip.FillContains(new Point(400, 210)), "Hero right corners were not clipped in actual frame coordinates");
        host.Tag = new CornerRadius(8);
        Require(((RectangleGeometry)target.Clip).RadiusX == 8, "Clip ignored radius-resource updates");
        host.DataContext = null;
        Require(ReferenceEquals(target.Clip, original), "Context detachment did not restore clip");
        host.DataContext = target;
        target.Width = 500; target.Height = 300;
        target.Measure(new Size(500, 300)); target.Arrange(new Rect(0, 0, 500, 300));
        var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate { frame.Continue = false; })); Dispatcher.PushFrame(frame);
        Require(target.Clip.Bounds.Width == 500 && target.Clip.Bounds.Height == 300 && !target.Clip.FillContains(new Point(499, 299)), "Hero rounded clip did not follow resizing");
        registration.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Require(ReferenceEquals(target.Clip, original), "Unloading did not restore the original Hero clip");
        Console.WriteLine("PASS: actual-size Hero clipping covers right corners, follows resize and restores on unload. No windows or rendering.");
    }
}
