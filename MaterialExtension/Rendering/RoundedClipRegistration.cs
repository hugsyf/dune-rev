using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DuneMaterials.Rendering
{
    internal sealed class RoundedClipRegistration : FrameworkElement
    {
        readonly FrameworkElement host;
        FrameworkElement target;
        object previous;
        bool watching;
        public RoundedClipRegistration(FrameworkElement owner)
        {
            host = owner; Width = Height = 0; IsHitTestVisible = false;
            Loaded += Attach; Unloaded += Detach;
        }
        internal static Geometry Shape(Size size, CornerRadius corners)
        {
            double width = Math.Max(0, size.Width), height = Math.Max(0, size.Height);
            double radius = Math.Min(Math.Min(width, height) / 2, Math.Max(0, corners.TopLeft));
            var geometry = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
            geometry.Freeze(); return geometry;
        }
        void Attach(object sender, RoutedEventArgs args)
        {
            if (watching) return; watching = true;
            host.DataContextChanged += ContextChanged;
            DependencyPropertyDescriptor.FromProperty(TagProperty, typeof(FrameworkElement)).AddValueChanged(host, Changed);
            RefreshTarget();
        }
        void ContextChanged(object sender, DependencyPropertyChangedEventArgs args) { RefreshTarget(); }
        void RefreshTarget()
        {
            RestoreTarget();
            target = host.DataContext as FrameworkElement; if (target == null) return;
            previous = target.ReadLocalValue(UIElement.ClipProperty);
            target.SizeChanged += Resize;
            Changed(null, EventArgs.Empty);
        }
        void Resize(object sender, SizeChangedEventArgs args) { Changed(sender, EventArgs.Empty); }
        void Changed(object sender, EventArgs args)
        { if (target != null) target.Clip = Shape(target.RenderSize, host.Tag is CornerRadius ? (CornerRadius)host.Tag : new CornerRadius(24)); }
        void Detach(object sender, RoutedEventArgs args)
        {
            if (!watching) return; watching = false;
            host.DataContextChanged -= ContextChanged;
            DependencyPropertyDescriptor.FromProperty(TagProperty, typeof(FrameworkElement)).RemoveValueChanged(host, Changed);
            RestoreTarget();
        }
        void RestoreTarget()
        {
            if (target == null) return;
            target.SizeChanged -= Resize;
            var expression = previous as BindingExpressionBase;
            if (expression != null) BindingOperations.SetBinding(target, ClipProperty, expression.ParentBindingBase);
            else if (previous == DependencyProperty.UnsetValue) target.ClearValue(ClipProperty);
            else target.SetValue(ClipProperty, previous);
            target = null; previous = null;
        }
    }
}
