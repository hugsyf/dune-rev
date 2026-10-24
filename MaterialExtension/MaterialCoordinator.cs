using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DuneMaterials
{
    internal sealed class MaterialCoordinator : IDisposable
    {
        readonly Dictionary<Border, MaterialSurface> surfaces = new Dictionary<Border, MaterialSurface>();
        readonly HashSet<Popup> popups = new HashSet<Popup>();
        readonly MaterialTextureCache cache;
        readonly Action<string> info;
        readonly Action<Exception> error;
        FrameworkElement native;
        Window startupWindow;
        bool disposed, paused;

        public MaterialCoordinator(Action<string> diagnostics, Action<Exception> errors)
        { info = diagnostics; error = errors; cache = new MaterialTextureCache(info); }

        public void Start()
        {
            EventManager.RegisterClassHandler(typeof(Border), FrameworkElement.LoadedEvent, new RoutedEventHandler(BorderLoaded), true);
            EventManager.RegisterClassHandler(typeof(Popup), FrameworkElement.LoadedEvent, new RoutedEventHandler(PopupLoaded), true);
            EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler(FlyoutOpened), true);
            EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent, new RoutedEventHandler(FlyoutOpened), true);
            startupWindow = Application.Current.MainWindow;
            if (startupWindow != null) startupWindow.ContentRendered += FirstContentRendered;
            DiscoverMain();
            info("Dune Materials 0.3.1: shared bitmap materials ready; cards/Hero/input/navigation/panels; dark popups");
        }

        void FirstContentRendered(object sender, EventArgs args)
        {
            if (startupWindow != null) startupWindow.ContentRendered -= FirstContentRendered;
            startupWindow = null;
            if (disposed || paused) return;
            DiscoverMain();
            foreach (var surface in surfaces.Values.ToArray()) surface.Reset();
        }

        static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            if (root == null) yield break;
            yield return root;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
        }
        void DiscoverMain()
        {
            if (disposed || paused) return;
            var window = Application.Current.MainWindow;
            if (window == null) return;
            var library = Descendants(window).OfType<Control>().FirstOrDefault(c => c.GetType().Name == "Library");
            native = library == null || library.Template == null ? null : library.Template.FindName("PART_ImageBackground", library) as FrameworkElement;
            Discover(window);
            info("Dune Materials: registered surfaces=" + surfaces.Count + ", native background=" + (native != null));
        }
        void Discover(DependencyObject root)
        {
            foreach (var node in Descendants(root))
            {
                var border = node as Border;
                if (border != null) Register(border);
                var popup = node as Popup;
                if (popup != null) Watch(popup);
            }
        }
        void Register(Border border)
        {
            if (disposed || paused || !border.Uid.StartsWith("Dune.Material.", StringComparison.Ordinal) || surfaces.ContainsKey(border)) return;
            surfaces.Add(border, new MaterialSurface(border, cache, delegate { return native; }, info, error));
            border.Unloaded += BorderUnloaded;
        }
        void BorderLoaded(object sender, RoutedEventArgs args) { if (!disposed && !paused) Register((Border)sender); }
        void BorderUnloaded(object sender, RoutedEventArgs args)
        {
            var border = (Border)sender;
            MaterialSurface surface;
            if (surfaces.TryGetValue(border, out surface)) { surface.Dispose(); surfaces.Remove(border); }
            border.Unloaded -= BorderUnloaded;
        }
        void PopupLoaded(object sender, RoutedEventArgs args) { if (!disposed && !paused) Watch((Popup)sender); }
        void Watch(Popup popup)
        {
            var owner = popup.PlacementTarget as FrameworkElement ?? popup.TemplatedParent as FrameworkElement;
            if (disposed || paused || owner == null || owner.TryFindResource("DuneMaterialsEnabled") == null || !popups.Add(popup)) return;
            popup.Opened += PopupOpened; popup.Unloaded += PopupUnloaded;
            if (popup.IsOpen) Discover(popup.Child);
        }
        void PopupOpened(object sender, EventArgs args) { if (!disposed && !paused) Discover(((Popup)sender).Child); }
        void PopupUnloaded(object sender, RoutedEventArgs args)
        {
            var popup = (Popup)sender;
            popup.Opened -= PopupOpened; popup.Unloaded -= PopupUnloaded; popups.Remove(popup);
        }
        void FlyoutOpened(object sender, RoutedEventArgs args) { if (!disposed && !paused) Discover((DependencyObject)sender); }

        public void SelectionChanged()
        {
            if (disposed || paused) return;
            cache.Reset();
            DiscoverMain();
            foreach (var surface in surfaces.Values.ToArray()) surface.Reset();
        }
        public void Suspend()
        {
            paused = true;
            if (startupWindow != null) startupWindow.ContentRendered -= FirstContentRendered;
            startupWindow = null;
            foreach (var item in surfaces) { item.Key.Unloaded -= BorderUnloaded; item.Value.Dispose(); }
            surfaces.Clear();
            foreach (var popup in popups) { popup.Opened -= PopupOpened; popup.Unloaded -= PopupUnloaded; }
            popups.Clear(); cache.Reset(); native = null;
        }
        public void Resume() { if (disposed || !paused) return; paused = false; DiscoverMain(); }
        public void Dispose() { Suspend(); disposed = true; cache.Dispose(); }
    }
}
