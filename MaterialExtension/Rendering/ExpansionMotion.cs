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

}
