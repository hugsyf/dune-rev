using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace DuneMaterials.Rendering
{
    // A template keeps the original content and plugin names in their overview namescope.
    public sealed class ExpansionLayoutTemplateConverter : IValueConverter
    {
        public object Convert(object value, Type type, object parameter, CultureInfo culture)
        {
            var layout = new FrameworkElementFactory(typeof(ExpansionLayout), "DuneAdaptiveLayout");
            layout.SetValue(ExpansionLayout.IsMetadataProperty, (string)parameter == "Metadata");
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetBinding(ContentPresenter.ContentProperty,
                new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
            layout.AppendChild(presenter);
            return new ControlTemplate(typeof(ContentControl)) { VisualTree = layout };
        }

        public object ConvertBack(object value, Type type, object parameter, CultureInfo culture)
        { return Binding.DoNothing; }
    }

    public sealed class ExpansionLayout : Decorator
    {
        public static readonly DependencyProperty IsMetadataProperty = DependencyProperty.Register(
            "IsMetadata", typeof(bool), typeof(ExpansionLayout), new FrameworkPropertyMetadata(false));
        public bool IsMetadata
        {
            get { return (bool)GetValue(IsMetadataProperty); }
            set { SetValue(IsMetadataProperty, value); }
        }

        const double AlignmentThreshold = 48;
        const double ViewportFraction = .65;
        double naturalHeight;
        double previousDx = double.NaN, previousDy = double.NaN, previousViewport = double.NaN;
        bool hasGeometry;

        public ExpansionLayout()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        void OnLoaded(object sender, RoutedEventArgs args)
        {
            hasGeometry = false;
            LayoutUpdated += OnLayoutUpdated;
            InvalidateMeasure();
        }

        void OnUnloaded(object sender, RoutedEventArgs args)
        {
            LayoutUpdated -= OnLayoutUpdated;
            hasGeometry = false;
        }

        ContentControl Host { get { return TemplatedParent as ContentControl; } }

        FrameworkElement FindOverviewElement(string name)
        {
            var host = Host;
            var overview = host == null ? null : host.TemplatedParent as Control;
            return overview == null || overview.Template == null ? null :
                overview.Template.FindName(name, overview) as FrameworkElement;
        }

        ExpansionLayout Peer
        {
            get
            {
                var host = FindOverviewElement(IsMetadata ? "DuneExpansionSizing" : "DuneMetadataSizing") as ContentControl;
                return host == null || host.Template == null ? null :
                    host.Template.FindName("DuneAdaptiveLayout", host) as ExpansionLayout;
            }
        }

        double ViewportHeight
        {
            get
            {
                var scroll = FindOverviewElement("PART_ScrollViewHost") as ScrollViewer;
                if (scroll == null) return 0;
                return scroll.ViewportHeight > 0 && !double.IsInfinity(scroll.ViewportHeight)
                    ? scroll.ViewportHeight : scroll.ActualHeight;
            }
        }

        bool Geometry(ExpansionLayout peer, out double dx, out double dy)
        {
            dx = dy = 0;
            if (peer == null || !IsVisible || !peer.IsVisible || ActualWidth <= 0 || peer.ActualWidth <= 0) return false;
            var common = FindCommonVisualAncestor(peer) as Visual;
            if (common == null) return false;
            Point self = TranslatePoint(new Point(), common as UIElement);
            Point other = peer.TranslatePoint(new Point(), common as UIElement);
            dx = other.X - self.X;
            dy = other.Y - self.Y;
            // Stacked panels overlap horizontally; only separate columns may align.
            return IsMetadata ? dx + peer.ActualWidth <= 1 : dx >= ActualWidth - 1;
        }

        void OnLayoutUpdated(object sender, EventArgs args)
        {
            double dx, dy;
            bool geometry = Geometry(Peer, out dx, out dy);
            double viewport = ViewportHeight;
            // Relative positions ignore page scrolling. No per-frame property writes.
            if (geometry != hasGeometry || Changed(dx, previousDx) || Changed(dy, previousDy) || Changed(viewport, previousViewport))
            {
                hasGeometry = geometry;
                previousDx = dx; previousDy = dy; previousViewport = viewport;
                InvalidateMeasure();
            }
        }

        static bool Changed(double value, double previous)
        { return double.IsNaN(previous) || Math.Abs(value - previous) > .5; }

        protected override Size MeasureOverride(Size available)
        {
            if (Child == null) return new Size();
            double limit = double.PositiveInfinity;
            if (!IsMetadata)
            {
                limit = Host == null ? 600 : Host.MaxHeight;
                double viewport = ViewportHeight;
                if (viewport > 0) limit = Math.Min(limit, Math.Max(160, viewport * ViewportFraction));
            }
            Child.Measure(new Size(available.Width, limit));
            double natural = Math.Min(limit, Child.DesiredSize.Height);
            var peer = Peer;
            if (Changed(natural, naturalHeight))
            {
                naturalHeight = natural;
                if (peer != null) peer.InvalidateMeasure();
            }
            double height = natural;
            double dx, dy;
            if (Geometry(peer, out dx, out dy) && peer.naturalHeight > 0)
            {
                // Compare actual bottom edges: the offset includes summary cards above expansion.
                double target = dy + peer.naturalHeight;
                if (target > natural && target - natural <= AlignmentThreshold && target <= limit)
                    height = target;
            }
            // Expansion only adds space. A long metadata column never squeezes plugin content.
            return new Size(Child.DesiredSize.Width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Child != null) Child.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}
