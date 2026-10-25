using System;
using System.Globalization;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DuneMaterials.Rendering
{
    // A template keeps the original content and plugin names in their overview namescope.
    public sealed class ExpansionLayoutTemplateConverter : IValueConverter
    {
        public object Convert(object value, Type type, object parameter, CultureInfo culture)
        {
            var host = value as ContentControl;
            if ((string)parameter != "Metadata" && host != null && !(host.Tag is ExpansionPresentation))
                host.Tag = new ExpansionPresentation(host);
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
        internal static long MeasureCount { get; private set; }
        internal static long TransitionCount { get; private set; }
        static readonly DependencyProperty ReducedMotionProperty = DependencyProperty.Register(
            "ReducedMotion", typeof(bool), typeof(ExpansionLayout),
            new PropertyMetadata(false, delegate(DependencyObject owner, DependencyPropertyChangedEventArgs args) {
                var layout = (ExpansionLayout)owner;
                if (layout.IsLoaded) layout.Settle();
            }));
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
        ExpansionPresentation presentation;
        bool initialized, pending, animatePending, closing, moving, motionFaulted;
        double targetHeight, contentHeight, displayHeight;
        double reservedHeight;
        readonly RectangleGeometry reveal = new RectangleGeometry();
        TranslateTransform followingOffset;
        ExpansionLayout peerCache;
        ScrollViewer scrollCache;
        FrameworkElement headerCache;
        double measuredWidth = double.NaN, measuredViewport = double.NaN;
        int motionRevision;

        public ExpansionLayout()
        {
            ClipToBounds = true;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SetResourceReference(ReducedMotionProperty, "DuneReduceMotion");
        }

        void OnLoaded(object sender, RoutedEventArgs args)
        {
            ClipToBounds = !IsMetadata;
            hasGeometry = false;
            peerCache = null; scrollCache = null; headerCache = null;
            LayoutUpdated += OnLayoutUpdated;
            if (!IsMetadata)
            {
                presentation = Host == null ? null : Host.Tag as ExpansionPresentation;
                if (presentation != null)
                {
                    presentation.TransitionRequested += Transition;
                    presentation.Attach();
                }
                SystemParameters.StaticPropertyChanged += SystemChanged;
            }
            InvalidateMeasure();
        }

        void OnUnloaded(object sender, RoutedEventArgs args)
        {
            LayoutUpdated -= OnLayoutUpdated;
            SystemParameters.StaticPropertyChanged -= SystemChanged;
            StopMotion(); initialized = false;
            if (presentation != null)
            {
                presentation.TransitionRequested -= Transition;
                presentation.Detach(); presentation = null;
            }
            hasGeometry = false;
            peerCache = null; scrollCache = null; headerCache = null;
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
                if (peerCache != null && peerCache.IsLoaded) return peerCache;
                var host = FindOverviewElement(IsMetadata ? "DuneExpansionSizing" : "DuneMetadataSizing") as ContentControl;
                peerCache = host == null || host.Template == null ? null :
                    host.Template.FindName("DuneAdaptiveLayout", host) as ExpansionLayout;
                return peerCache;
            }
        }

        double ViewportHeight
        {
            get
            {
                var scroll = scrollCache ?? (scrollCache = FindOverviewElement("PART_ScrollViewHost") as ScrollViewer);
                if (scroll == null) return 0;
                return scroll.ViewportHeight > 0 && !double.IsInfinity(scroll.ViewportHeight)
                    ? scroll.ViewportHeight : scroll.ActualHeight;
            }
        }

        bool Geometry(ExpansionLayout peer, out double dx, out double dy)
        {
            dx = dy = 0;
            if (peer == null || !HasSurface || !peer.HasSurface || ActualWidth <= 0 || peer.ActualWidth <= 0) return false;
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
            if (!IsVisible || !IsLoaded) return;
            var peer = Peer;
            // Keep render-only motion out of column alignment. Geometry settles
            // once at the end instead of feeding another layout pass each frame.
            if (moving || (peer != null && peer.moving)) return;
            double dx, dy;
            bool geometry = Geometry(peer, out dx, out dy);
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
            MeasureCount++;
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
                double target = dy + peer.naturalHeight + BottomInset - peer.BottomInset;
                if (target > natural && target - natural <= AlignmentThreshold && target <= limit)
                    height = target;
            }
            // Expansion only adds space. A long metadata column never squeezes plugin content.
            if (!IsMetadata && presentation != null) height = PresentHeight(available.Width, height);
            return new Size(Child.DesiredSize.Width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Child != null) Child.Arrange(new Rect(new Size(finalSize.Width,
                !IsMetadata && presentation != null ? Math.Max(finalSize.Height, contentHeight) : finalSize.Height)));
            return finalSize;
        }

        double BottomInset
        { get { var content = Host == null ? null : Host.Content as FrameworkElement; return content == null ? 0 : content.Margin.Bottom; } }
        FrameworkElement Header
        { get { return headerCache ?? (headerCache = FindOverviewElement("DuneDetailHeader")); } }
        bool HasSurface
        {
            get
            {
                if (!IsVisible) return false;
                if (IsMetadata || presentation == null || presentation.IsOpen) return true;
                var header = Header;
                return header != null && header.Visibility == Visibility.Visible;
            }
        }
        bool MotionEnabled { get { return !motionFaulted && !(bool)GetValue(ReducedMotionProperty) && SystemParameters.ClientAreaAnimation; } }
        void SystemChanged(object sender, PropertyChangedEventArgs args)
        { if (args.PropertyName == "ClientAreaAnimation") Settle(); }
        void Transition(bool animate)
        {
            // A collapsed host has no initial measure pass. Its first user-driven
            // expansion still starts from a known zero-height baseline.
            if (animate && !initialized && presentation.Displayed == null && presentation.Requested != null)
            { displayHeight = 0; initialized = true; }
            pending = true; animatePending = animate && MotionEnabled && initialized;
            closing = presentation.Requested == null && presentation.Displayed != null;
            if (!closing || !animatePending) presentation.Show(presentation.Requested);
            InvalidateMeasure();
        }
        void StopMotion()
        {
            motionRevision++; moving = false;
            reveal.BeginAnimation(RectangleGeometry.RectProperty, null);
            Clip = null;
            if (followingOffset != null && !followingOffset.IsFrozen)
            {
                followingOffset.BeginAnimation(TranslateTransform.YProperty, null);
                followingOffset.Y = 0;
            }
            if (Child != null) { Child.BeginAnimation(OpacityProperty, null); Child.Opacity = 1; }
            IsHitTestVisible = true;
        }
        void Settle()
        {
            StopMotion(); pending = false; closing = false;
            if (presentation != null) presentation.Show(presentation.Requested);
            initialized = false; InvalidateMeasure();
        }
        double PresentHeight(double width, double natural)
        {
            double viewport = ViewportHeight;
            bool resized = !double.IsNaN(measuredWidth) &&
                (Math.Abs(width - measuredWidth) > .5 || Math.Abs(viewport - measuredViewport) > .5);
            measuredWidth = width; measuredViewport = viewport;
            if (resized)
            {
                StopMotion(); pending = false; closing = false;
                presentation.Show(presentation.Requested);
                // Re-measure after the delayed outgoing body is released.
                Child.Measure(new Size(width, Math.Min(Host.MaxHeight, Math.Max(160, viewport * ViewportFraction))));
                natural = Child.DesiredSize.Height;
            }
            var header = Header;
            bool visible = presentation.IsOpen || (header != null && header.Visibility == Visibility.Visible);
            double desired = visible ? natural : 0;
            if (pending)
            {
                pending = false;
                double from = initialized ? (moving ? reveal.Rect.Height : displayHeight) : desired;
                targetHeight = closing ? 0 : desired;
                contentHeight = Math.Max(contentHeight, Math.Max(from, desired));
                if (animatePending && !resized && (Math.Abs(targetHeight - from) > .5 || !closing))
                {
                    try { Animate(from, targetHeight, closing); }
                    catch (Exception exception) { FailMotion(exception); }
                }
                else
                {
                    StopMotion(); presentation.Show(presentation.Requested);
                    targetHeight = closing ? 0 : desired; contentHeight = targetHeight;
                    closing = false;
                }
            }
            else if (!moving)
            {
                targetHeight = desired; contentHeight = desired;
            }
            initialized = true;
            // Reserve space once. Clip/transform clocks never invalidate measure.
            displayHeight = moving ? reveal.Rect.Height : targetHeight;
            return moving ? reservedHeight : displayHeight;
        }
        TranslateTransform FollowingTransform()
        {
            var following = FindOverviewElement("DuneFollowingContent");
            if (following == null) return null;
            var transform = following.RenderTransform as TranslateTransform;
            if (transform == null || transform.IsFrozen)
            {
                // WPF freezes Freezables declared in templates. Animate a runtime
                // instance belonging to this view, never the template's shared value.
                transform = transform == null ? new TranslateTransform() : transform.CloneCurrentValue();
                following.SetCurrentValue(RenderTransformProperty, transform);
            }
            return transform;
        }
        void FailMotion(Exception exception)
        {
            motionFaulted = true;
            StopMotion(); pending = false; closing = false; initialized = false;
            if (presentation != null) presentation.Show(presentation.Requested);
            targetHeight = contentHeight = displayHeight = 0;
            InvalidateMeasure();
            Playnite.SDK.LogManager.GetLogger().Error(exception, "Dune plugin motion failed; using immediate panel changes for this view");
        }
        void Animate(double from, double to, bool collapse)
        {
            TransitionCount++;
            double opacity = Child.Opacity;
            StopMotion(); moving = true;
            int revision = ++motionRevision;
            IsHitTestVisible = !collapse;
            reservedHeight = Math.Max(from, to);
            double width = Math.Max(0, double.IsInfinity(measuredWidth) ? ActualWidth : measuredWidth);
            reveal.Rect = new Rect(0, 0, width, from);
            Clip = reveal;
            followingOffset = FollowingTransform();
            if (followingOffset != null) followingOffset.Y = from - reservedHeight;
            var height = new RectAnimation(new Rect(0, 0, width, from), new Rect(0, 0, width, to), TimeSpan.FromMilliseconds(collapse ? 120 : 220)) {
                BeginTime = TimeSpan.FromMilliseconds(collapse ? 60 : 0),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.HoldEnd
            };
            height.Completed += delegate {
                if (revision != motionRevision || !IsLoaded || presentation == null) return;
                try
                {
                    StopMotion(); closing = false;
                    displayHeight = to;
                    contentHeight = targetHeight;
                    presentation.Show(presentation.Requested);
                    InvalidateMeasure();
                    var peer = Peer; if (peer != null) peer.InvalidateMeasure();
                }
                catch (Exception exception) { FailMotion(exception); }
            };
            reveal.BeginAnimation(RectangleGeometry.RectProperty, height, HandoffBehavior.SnapshotAndReplace);
            if (followingOffset != null)
                followingOffset.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(from - reservedHeight, to - reservedHeight, height.Duration) {
                        BeginTime = height.BeginTime,
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.HoldEnd
                    }, HandoffBehavior.SnapshotAndReplace);
            Child.Opacity = collapse ? opacity : 0;
            Child.BeginAnimation(OpacityProperty, collapse
                ? new DoubleAnimation(opacity, 0, TimeSpan.FromMilliseconds(70)) { FillBehavior = FillBehavior.HoldEnd }
                : new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)) {
                    BeginTime = TimeSpan.FromMilliseconds(35), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd
                }, HandoffBehavior.SnapshotAndReplace);
        }
    }
}
