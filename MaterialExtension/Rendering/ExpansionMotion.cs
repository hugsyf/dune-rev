using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DuneMaterials.Rendering
{
    // Presentation may outlive selection briefly while the original controls fade out.
    // No plugin control is cloned, reparented or re-created by this state object.
    public sealed class ExpansionPresentation : INotifyPropertyChanged
    {
        internal static long ContextResetCount { get; private set; }
        readonly ContentControl host;
        TabControl selector;
        INotifyPropertyChanged model;
        bool attached, resetting, ready;
        PropertyInfo gameProperty;
        object observedGame;
        int modelCheckQueued;
        int contextRevision;
        public bool Managed { get { return true; } }
        public bool KeepVisible { get { return !ready || Requested != null || Displayed != null; } }
        public TabItem Requested { get; private set; }
        public TabItem Displayed { get; private set; }
        public bool IsOpen { get { return Displayed != null; } }
        public bool Achievements { get { return Shows("DuneAchievementsDetails"); } }
        public bool Languages { get { return Shows("DuneLanguagesDetails"); } }
        public bool Dlc { get { return Shows("DuneDlcDetails"); } }
        public bool HowLongToBeat { get { return Shows("DuneHowLongToBeatDetails"); } }
        public bool Activity { get { return Shows("DuneActivityDetails"); } }
        public event PropertyChangedEventHandler PropertyChanged;
        internal event Action<bool> TransitionRequested;

        internal ExpansionPresentation(ContentControl owner) { host = owner; }
        bool Shows(string name) { return Displayed != null && Displayed.Name == name; }
        internal static FrameworkElement Find(ContentControl host, string name)
        {
            var overview = host == null ? null : host.TemplatedParent as Control;
            return overview == null || overview.Template == null ? null :
                overview.Template.FindName(name, overview) as FrameworkElement;
        }
        internal void Attach()
        {
            if (attached) return;
            attached = true;
            selector = Find(host, "DunePluginDetails") as TabControl;
            if (selector != null) selector.SelectionChanged += Selected;
            host.DataContextChanged += ContextChanged;
            ObserveModel(); Request(false);
            ready = true; NotifyVisibility();
        }
        internal void Detach()
        {
            if (!attached) return;
            attached = false; contextRevision++;
            if (selector != null) selector.SelectionChanged -= Selected;
            selector = null;
            host.DataContextChanged -= ContextChanged;
            if (model != null) model.PropertyChanged -= ModelChanged;
            model = null; gameProperty = null; observedGame = null;
            Requested = null; Show(null); NotifyVisibility();
        }
        void Selected(object sender, SelectionChangedEventArgs args)
        { if (ReferenceEquals(args.Source, selector)) Request(!resetting); }
        void ObserveModel()
        {
            if (model != null) model.PropertyChanged -= ModelChanged;
            model = host.DataContext as INotifyPropertyChanged;
            var context = host.DataContext;
            gameProperty = context == null ? null : context.GetType().GetProperty("Game") ?? context.GetType().GetProperty("SelectedGame");
            observedGame = gameProperty == null ? null : gameProperty.GetValue(context, null);
            if (model != null && gameProperty != null) model.PropertyChanged += ModelChanged;
        }
        void ContextChanged(object sender, DependencyPropertyChangedEventArgs args)
        { ObserveModel(); ResetContext(); }
        void ModelChanged(object sender, PropertyChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == "Game" || args.PropertyName == "SelectedGame")
            {
                if (host.Dispatcher.CheckAccess()) CheckGame();
                else if (Interlocked.Exchange(ref modelCheckQueued, 1) == 0)
                    host.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(delegate {
                        Interlocked.Exchange(ref modelCheckQueued, 0);
                        if (attached) CheckGame();
                    }));
            }
        }
        void CheckGame()
        {
            if (gameProperty == null) return;
            object current = gameProperty.GetValue(host.DataContext, null);
            if (ReferenceEquals(current, observedGame)) return;
            observedGame = current; ResetContext();
        }
        void ResetContext()
        {
            ContextResetCount++;
            resetting = true;
            int revision = ++contextRevision;
            Request(false);
            host.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(delegate {
                if (!attached || revision != contextRevision) return;
                Request(false); resetting = false;
            }));
        }
        void Request(bool animate)
        {
            Requested = selector == null ? null : selector.SelectedItem as TabItem;
            if (Requested != null && Requested.Visibility != Visibility.Visible) Requested = null;
            NotifyVisibility();
            var handler = TransitionRequested;
            if (handler != null) handler(animate); else Show(Requested);
        }
        internal void Show(TabItem item)
        {
            if (ReferenceEquals(Displayed, item)) return;
            Displayed = item;
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(string.Empty));
        }
        void NotifyVisibility()
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs("KeepVisible"));
        }
    }

    internal sealed class ChevronMotionRegistration : FrameworkElement
    {
        static readonly DependencyProperty SelectedProperty = DependencyProperty.Register(
            "Selected", typeof(bool), typeof(ChevronMotionRegistration),
            new PropertyMetadata(false, delegate(DependencyObject owner, DependencyPropertyChangedEventArgs args) {
                ((ChevronMotionRegistration)owner).UpdateAngle(true);
            }));
        static readonly DependencyProperty ReducedProperty = DependencyProperty.Register(
            "Reduced", typeof(bool), typeof(ChevronMotionRegistration),
            new PropertyMetadata(false, delegate(DependencyObject owner, DependencyPropertyChangedEventArgs args) {
                ((ChevronMotionRegistration)owner).UpdateAngle(false);
            }));
        readonly FrameworkElement host;
        readonly RotateTransform rotation = new RotateTransform();
        TextBlock target;
        object previousText, previousTransform, previousOrigin;
        bool watching;
        public ChevronMotionRegistration(FrameworkElement owner)
        {
            host = owner; Width = Height = 0; IsHitTestVisible = false;
            Loaded += Attach; Unloaded += Detach;
            SetResourceReference(ReducedProperty, "DuneReduceMotion");
        }
        void Attach(object sender, RoutedEventArgs args)
        {
            if (watching) return;
            watching = true; host.DataContextChanged += ContextChanged;
            SystemParameters.StaticPropertyChanged += SystemChanged;
            SetTarget();
        }
        void ContextChanged(object sender, DependencyPropertyChangedEventArgs args) { SetTarget(); }
        void SystemChanged(object sender, PropertyChangedEventArgs args)
        { if (args.PropertyName == "ClientAreaAnimation") UpdateAngle(false); }
        void SetTarget()
        {
            Restore();
            target = host.DataContext as TextBlock;
            if (target == null) return;
            previousText = target.ReadLocalValue(TextBlock.TextProperty);
            previousTransform = target.ReadLocalValue(RenderTransformProperty);
            previousOrigin = target.ReadLocalValue(RenderTransformOriginProperty);
            target.Text = "\uE70D";
            target.RenderTransformOrigin = new Point(.5, .5);
            target.RenderTransform = rotation;
            SetBinding(SelectedProperty, new Binding("Tag") { Source = target, FallbackValue = false, TargetNullValue = false });
            UpdateAngle(false);
        }
        void UpdateAngle(bool animate)
        {
            if (target == null) return;
            double from = rotation.Angle, to = (bool)GetValue(SelectedProperty) ? 180 : 0;
            rotation.BeginAnimation(RotateTransform.AngleProperty, null);
            rotation.Angle = to;
            if (animate && watching && !(bool)GetValue(ReducedProperty) && SystemParameters.ClientAreaAnimation)
                rotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(140)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop
                });
        }
        static void RestoreValue(DependencyObject owner, DependencyProperty property, object previous)
        {
            var binding = previous as BindingExpressionBase;
            if (binding != null) BindingOperations.SetBinding(owner, property, binding.ParentBindingBase);
            else if (previous == DependencyProperty.UnsetValue) owner.ClearValue(property);
            else owner.SetValue(property, previous);
        }
        void Restore()
        {
            BindingOperations.ClearBinding(this, SelectedProperty);
            rotation.BeginAnimation(RotateTransform.AngleProperty, null);
            if (target == null) return;
            RestoreValue(target, TextBlock.TextProperty, previousText);
            RestoreValue(target, RenderTransformProperty, previousTransform);
            RestoreValue(target, RenderTransformOriginProperty, previousOrigin);
            target = null;
        }
        void Detach(object sender, RoutedEventArgs args)
        {
            if (!watching) return;
            watching = false; host.DataContextChanged -= ContextChanged;
            SystemParameters.StaticPropertyChanged -= SystemChanged;
            Restore();
        }
    }
}
