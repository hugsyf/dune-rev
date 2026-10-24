using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Playnite.SDK;
using DuneMaterials.Rendering;

namespace DuneMaterials
{
    public sealed class MaterialSettings : ISettings, INotifyPropertyChanged
    {
        readonly Action<MaterialOptions> save;
        readonly Func<string> describe;
        MaterialOptions editing;
        public int MaterialApiVersion { get { return 1; } }
        public MaterialOptions Options { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;
        internal MaterialSettings(MaterialOptions options, Action<MaterialOptions> persist, Func<string> diagnostics)
        { Options = options; save = persist; describe = diagnostics; }
        public void BeginEdit() { editing = Options.Copy(); }
        public void CancelEdit()
        {
            if (editing != null) Options = editing; editing = null;
            var changed = PropertyChanged; if (changed != null) changed(this, new PropertyChangedEventArgs("Options"));
        }
        public void EndEdit() { if (Options.Mode == MaterialMode.Realtime) Options.AllowExperimentalRealtime = true; save(Options.Copy()); editing = null; }
        public bool VerifySettings(out List<string> errors) { errors = new List<string>(); return true; }
        string Text(string chinese, string english)
        { return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? chinese : english; }
        internal UserControl View()
        {
            var panel = new StackPanel { Margin = new Thickness(20), MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };
            panel.Children.Add(new TextBlock { Text = "Dune Materials", FontSize = 24, Margin = new Thickness(0, 0, 0, 8) });
            Hint(panel, Text("为 Dune Rev 提供统一的半透明材质与 Hero 媒体控制。", "Translucent materials and Hero media controls for Dune Rev."));
            Check(panel, Text("使用增强材质（推荐）", "Use enhanced materials (recommended)"), "UseNewRenderer");
            Hint(panel, Text("关闭后使用经典材质。若要完全关闭材质，请使用 ThemeModifier 中的主题材质开关。", "Turn off to use classic materials. To disable materials entirely, use the theme material switch in ThemeModifier."));
            var rendering = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            rendering.SetBinding(UIElement.IsEnabledProperty, new Binding("Options.UseNewRenderer") { Source = this });
            rendering.Children.Add(new TextBlock { Text = Text("材质模式", "Material mode"), Margin = new Thickness(0, 0, 0, 6) });
            var modes = new ComboBox { SelectedValuePath = "Tag", HorizontalContentAlignment = HorizontalAlignment.Left };
            modes.Items.Add(new ComboBoxItem { Content = Text("实时（推荐）", "Realtime (recommended)"), Tag = MaterialMode.Realtime });
            modes.Items.Add(new ComboBoxItem { Content = Text("兼容（静态缓存）", "Compatibility (cached)"), Tag = MaterialMode.Cached });
            modes.SetBinding(ComboBox.SelectedValueProperty, new Binding("Options.Mode") { Source = this, Mode = BindingMode.TwoWay });
            modes.SelectionChanged += delegate { if (Options.Mode == MaterialMode.Realtime) Options.AllowExperimentalRealtime = true; };
            rendering.Children.Add(modes);
            Hint(rendering, Text("实时模式跟随背景变化；不支持时自动使用兼容模式。如果遇到卡顿或显示异常，可手动选择兼容模式。", "Realtime follows background changes and falls back to compatibility mode when unsupported. Choose compatibility mode if you encounter stuttering or display issues."));
            Check(rendering, Text("轻微材质颗粒", "Subtle material grain"), "Noise");
            Check(rendering, Text("减少材质过渡动画", "Reduce material transitions"), "ReduceMotion");
            panel.Children.Add(rendering);
            panel.Children.Add(new TextBlock { Text = Text("Hero 媒体", "Hero media"), FontSize = 18, Margin = new Thickness(0, 20, 0, 4) });
            Hint(panel, Text("使用 Hero 缩略框上方的循环按钮切换自动轮播／播放，默认关闭；自动播放视频时先保持静音。", "Use the repeat button above the Hero thumbnails to toggle automatic slideshow / playback. Off by default; automatically started videos begin muted."));
            var recommended = new Button { Content = Text("恢复推荐设置", "Restore recommended settings"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 20, 0, 0), Padding = new Thickness(12, 6, 12, 6) };
            recommended.Click += delegate {
                Options = new MaterialOptions { HeroAutoPlay = Options.HeroAutoPlay };
                var changed = PropertyChanged; if (changed != null) changed(this, new PropertyChangedEventArgs("Options"));
            };
            panel.Children.Add(recommended);
            var diagnostics = new StackPanel();
            Hint(diagnostics, Text("用于反馈显示与性能问题。这里的耗时仅代表界面更新批次，不代表帧率或 GPU 耗时。保存设置后可刷新查看当前状态。", "Useful when reporting display or performance issues. Timings describe UI update batches, not frame rate or GPU time. Save settings before refreshing to inspect the active configuration."));
            var report = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), MinHeight = 140, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var refresh = new Button { Content = Text("刷新诊断", "Refresh diagnostics"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(12, 6, 12, 6) };
            refresh.Click += delegate { report.Text = describe(); };
            diagnostics.Children.Add(refresh); diagnostics.Children.Add(report);
            report.Text = describe();
            panel.Children.Add(new Expander { Header = Text("诊断信息", "Diagnostics"), Content = diagnostics, IsExpanded = false, Margin = new Thickness(0, 20, 0, 0) });
            return new UserControl { Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } };
        }
        void Hint(Panel panel, string text)
        { panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = .75, Margin = new Thickness(0, 4, 0, 8) }); }
        void Check(Panel panel, string title, string path)
        {
            var check = new CheckBox { Content = title, Margin = new Thickness(0, 8, 0, 0) };
            check.SetBinding(CheckBox.IsCheckedProperty, new Binding("Options." + path) { Source = this, Mode = BindingMode.TwoWay }); panel.Children.Add(check);
        }
    }
}
