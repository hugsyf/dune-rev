using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace DuneMaterials.Rendering
{
    public sealed class ScrollFadeConverter : IValueConverter
    {
        public object Convert(object value, Type type, object parameter, CultureInfo culture)
        {
            double offset = value is double ? (double)value : 0;
            if (double.IsNaN(offset)) offset = 0;
            // Grow with the first scroll pixels instead of suddenly masking a full strip.
            return new Point(0, Math.Max(.1, Math.Min(28, offset)));
        }
        public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) { return Binding.DoNothing; }
    }
    // No extension CLR namespace is required in theme XAML: factories use PluginConverter.
    public class MaterialPresenter : FrameworkElement
    {
        internal MaterialSession Session;
        readonly MaterialRole role;
        internal MaterialPresenter(MaterialRuntime runtime, FrameworkElement host, MaterialRole role)
        {
            IsHitTestVisible = false;
            this.role = role;
            Session = new MaterialSession(runtime, this);
            SetBinding(TagProperty, new Binding("Tag") { Source = host });
            Session.Configure(Brushes.Transparent, Tag is bool && (bool)Tag, role);
            Session.Brush.Changed += delegate { InvalidateVisual(); };
        }
        protected override void OnRender(DrawingContext dc) { dc.DrawRectangle(Session.Brush, null, new Rect(RenderSize)); }
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs args)
        { base.OnPropertyChanged(args); if (args.Property == TagProperty && Session != null) Session.Configure(Brushes.Transparent, Tag is bool && (bool)Tag, role); }
    }
    public sealed class BackgroundPresenter : MaterialPresenter
    {
        internal BackgroundPresenter(MaterialRuntime runtime, FrameworkElement host) : base(runtime, host, MaterialRole.Background) { }
    }
    internal sealed class SourceRegistration : FrameworkElement
    {
        readonly MaterialRuntime runtime;
        readonly FrameworkElement host;
        readonly bool live;
        MaterialScope scope;
        FrameworkElement source;
        Window window;
        public SourceRegistration(MaterialRuntime service, FrameworkElement owner, bool dynamicScene)
        {
            runtime = service; host = owner; live = dynamicScene; Width = Height = 0; IsHitTestVisible = false;
            Loaded += OnLoaded; Unloaded += OnUnloaded;
        }
        void OnLoaded(object sender, RoutedEventArgs args)
        {
            DependencyPropertyDescriptor.FromProperty(FrameworkElement.TagProperty, typeof(FrameworkElement)).AddValueChanged(host, Refresh);
            host.DataContextChanged += ContextChanged;
            Refresh(null, EventArgs.Empty);
        }
        void ContextChanged(object sender, DependencyPropertyChangedEventArgs args) { Refresh(sender, EventArgs.Empty); }
        void Refresh(object sender, EventArgs args)
        {
            Detach(); source = host.Tag as FrameworkElement;
            var anchor = host.DataContext as FrameworkElement;
            scope = runtime.RegisterSource(anchor, source, live);
            if (live && source != null) { source.IsVisibleChanged += LiveChanged; source.SizeChanged += LiveSizeChanged;
                DependencyPropertyDescriptor.FromProperty(UIElement.OpacityProperty, typeof(FrameworkElement)).AddValueChanged(source, LiveOpacityChanged); }
            window = anchor as Window ?? (anchor == null ? null : Window.GetWindow(anchor));
            if (window != null) window.StateChanged += StateChanged;
        }
        void StateChanged(object sender, EventArgs args) { runtime.WindowStateChanged(); }
        void LiveChanged(object sender, DependencyPropertyChangedEventArgs args) { runtime.QueueAll(); }
        void LiveSizeChanged(object sender, SizeChangedEventArgs args) { runtime.QueueAll(); }
        void LiveOpacityChanged(object sender, EventArgs args) { runtime.QueueAll(); }
        void Detach()
        {
            if (live && source != null) { source.IsVisibleChanged -= LiveChanged; source.SizeChanged -= LiveSizeChanged;
                DependencyPropertyDescriptor.FromProperty(UIElement.OpacityProperty, typeof(FrameworkElement)).RemoveValueChanged(source, LiveOpacityChanged); }
            if (window != null) window.StateChanged -= StateChanged; window = null;
            runtime.UnregisterSource(scope, source, live); scope = null; source = null;
        }
        void OnUnloaded(object sender, RoutedEventArgs args)
        {
            DependencyPropertyDescriptor.FromProperty(FrameworkElement.TagProperty, typeof(FrameworkElement)).RemoveValueChanged(host, Refresh);
            host.DataContextChanged -= ContextChanged; Detach();
        }
    }
    public sealed class MaterialElementConverter : IValueConverter
    {
        readonly MaterialRuntime runtime;
        internal MaterialElementConverter(MaterialRuntime service) { runtime = service; }
        public object Convert(object value, Type type, object parameter, CultureInfo culture)
        {
            var host = value as FrameworkElement; if (host == null) return null;
            string role = parameter as string;
            if (role == "RoundedClip") return new RoundedClipRegistration(host);
            if (role == "Source" || role == "LiveSource") return new SourceRegistration(runtime, host, role == "LiveSource");
            if (role == "Background") return new BackgroundPresenter(runtime, host);
            MaterialRole profile;
            return Enum.TryParse<MaterialRole>(role, out profile) ? new MaterialPresenter(runtime, host, profile) : null;
        }
        public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) { return Binding.DoNothing; }
    }
    public sealed class MaterialBrushConverter : IValueConverter, IMultiValueConverter
    {
        sealed class BindingState
        {
            public MaterialSession Session;
            public string Parameter;
            public Action Refresh;
        }
        readonly MaterialRuntime runtime;
        readonly ConditionalWeakTable<FrameworkElement, MaterialSession> sessions = new ConditionalWeakTable<FrameworkElement, MaterialSession>();
        readonly ConditionalWeakTable<FrameworkElement, BindingState> bindings = new ConditionalWeakTable<FrameworkElement, BindingState>();
        internal MaterialBrushConverter(MaterialRuntime service) { runtime = service; }
        public object Convert(object[] values, Type type, object parameter, CultureInfo culture)
        {
            if (values.Length < 4) return DependencyProperty.UnsetValue;
            var target = values[0] as FrameworkElement;
            var fallback = values[1] as Brush;
            if (fallback == null && values[1] is string) fallback = (Brush)new BrushConverter().ConvertFromInvariantString((string)values[1]);
            string uid = values[3] as string;
            if (target == null || string.IsNullOrEmpty(uid) || !uid.StartsWith("Dune.Material.", StringComparison.Ordinal)) return fallback;
            var role = MaterialProfiles.RoleFor(uid);
            var session = sessions.GetValue(target, delegate(FrameworkElement key) { return new MaterialSession(runtime, key); });
            session.Configure(fallback, values[2] is bool && (bool)values[2], role);
            return session.Brush;
        }
        object IValueConverter.Convert(object value, Type type, object parameter, CultureInfo culture)
        {
            var target = value as FrameworkElement;
            if (target == null) return DependencyProperty.UnsetValue;
            var state = bindings.GetValue(target, delegate(FrameworkElement key) {
                var result = new BindingState { Session = new MaterialSession(runtime, key) };
                result.Refresh = delegate {
                    string spec = result.Parameter ?? "Transparent";
                    Brush fallback = null;
                    if (spec == "TemplateBackground")
                    {
                        var parent = key.TemplatedParent as Control; if (parent != null) fallback = parent.Background;
                    }
                    else if (spec.StartsWith("Resource:", StringComparison.Ordinal)) fallback = key.TryFindResource(spec.Substring(9)) as Brush;
                    else fallback = (Brush)new BrushConverter().ConvertFromInvariantString(spec);
                    string uid = key.Uid ?? "";
                    bool marked = uid.StartsWith("Dune.Material.", StringComparison.Ordinal);
                    var role = MaterialProfiles.RoleFor(uid);
                    result.Session.Configure(fallback, marked && key.Tag is bool && (bool)key.Tag, role);
                };
                EventHandler refresh = delegate { result.Refresh(); };
                var tag = DependencyPropertyDescriptor.FromProperty(FrameworkElement.TagProperty, typeof(FrameworkElement));
                var uidProperty = DependencyPropertyDescriptor.FromProperty(UIElement.UidProperty, typeof(FrameworkElement));
                bool observing = false;
                Action observe = delegate {
                    if (observing) return; observing = true;
                    tag.AddValueChanged(key, refresh); uidProperty.AddValueChanged(key, refresh);
                    var parent = key.TemplatedParent as Control;
                    if (parent != null) BindingOperations.SetBinding(key, FallbackObserver.ValueProperty,
                        new Binding("Background") { Source = parent });
                    result.Refresh();
                };
                key.Loaded += delegate { observe(); };
                key.SetValue(FallbackObserver.CallbackProperty, result.Refresh);
                key.Unloaded += delegate {
                    if (observing) { tag.RemoveValueChanged(key, refresh); uidProperty.RemoveValueChanged(key, refresh); observing = false; }
                    BindingOperations.ClearBinding(key, FallbackObserver.ValueProperty);
                };
                if (key.IsLoaded) observe();
                return result;
            });
            state.Parameter = parameter as string; state.Refresh(); return state.Session.Brush;
        }
        public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) { return Binding.DoNothing; }
        public object[] ConvertBack(object value, Type[] types, object parameter, CultureInfo culture) { return null; }
    }
    internal static class FallbackObserver
    {
        internal static readonly DependencyProperty CallbackProperty = DependencyProperty.RegisterAttached("Callback", typeof(Action), typeof(FallbackObserver));
        internal static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached("Value", typeof(Brush), typeof(FallbackObserver),
            new PropertyMetadata(null, delegate(DependencyObject target, DependencyPropertyChangedEventArgs args) {
                var callback = target.GetValue(CallbackProperty) as Action; if (callback != null) callback();
            }));
    }
}
