using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Rolan.Core;

namespace Rolan.App;

internal sealed class SettingsWindow : ThemedWindow
{
    internal SettingsWindow(MainWindow main, bool attachOwner = true)
    {
        if (attachOwner)
        {
            Owner = main;
        }
        Title = "功能与界面设置";
        Width = 850;
        Height = 730;
        MinWidth = 720;
        MinHeight = 500;
        var root = new Grid { Margin = new Thickness(20, 4, 20, 20) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(162) });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        var navigation = new StackPanel { Margin = new Thickness(0, 8, 16, 0) };
        root.Children.Add(navigation);
        var right = new DockPanel();
        var version = new TextBlock { Text = "当前版本：" + typeof(SettingsWindow).Assembly.GetName().Version!.ToString(3) + "（64 位）", FontSize = 12, Opacity = .65, Margin = new Thickness(10, 12, 10, 0), HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(version, Dock.Bottom);
        right.Children.Add(version);
        Grid.SetColumn(right, 1);
        root.Children.Add(right);
        var error = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(227, 158, 57)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10, 0, 10, 10), Visibility = Visibility.Collapsed };
        DockPanel.SetDock(error, Dock.Bottom);
        right.Children.Add(error);
        var pages = new Grid();
        right.Children.Add(pages);
        var sections = new List<FrameworkElement>();
        var links = new List<Button>();
        var suppress = false;
        bool Commit(string field, object value, Action? rollback = null)
        {
            if (suppress)
            {
                return true;
            }
            try
            {
                main.ApplySetting(field, value);
                error.Visibility = Visibility.Collapsed;
                return true;
            }
            catch (Exception ex)
            {
                main.Store.Log(ex, "设置 " + field);
                suppress = true;
                rollback?.Invoke();
                suppress = false;
                error.Text = "设置未生效：" + ex.Message;
                error.Visibility = Visibility.Visible;
                return false;
            }
        }
        StackPanel Section(string name)
        {
            var panel = new StackPanel { Margin = new Thickness(6, 8, 10, 0) };
            var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = sections.Count == 0 ? Visibility.Visible : Visibility.Collapsed };
            pages.Children.Add(scroll);
            sections.Add(scroll);
            var link = new Button { Content = name, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(14, 12, 14, 12), Background = Brushes.Transparent };
            navigation.Children.Add(link);
            links.Add(link);
            link.Click += (_, _) =>
            {
                for (var i = 0; i < sections.Count; i++)
                {
                    sections[i].Visibility = sections[i] == scroll ? Visibility.Visible : Visibility.Collapsed;
                    links[i].Background = sections[i] == scroll ? (Brush)FindResource("SurfaceColor") : Brushes.Transparent;
                }
            };
            panel.Children.Add(new TextBlock { Text = name, FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 0, 5) });
            panel.Children.Add(new TextBlock { Text = "修改即时生效，自动保存", Opacity = .55, FontSize = 12, Margin = new Thickness(4, 0, 0, 18) });
            return panel;
        }
        StackPanel Card(StackPanel panel, string title)
        {
            var body = new StackPanel { Margin = new Thickness(17, 11, 17, 13) };
            var card = new Border { CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 0, 12), Child = body };
            card.SetResourceReference(Border.BackgroundProperty, "SurfaceColor");
            body.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 0, 10) });
            panel.Children.Add(card);
            return body;
        }
        void Check(StackPanel panel, string label, string field)
        {
            var member = typeof(Preferences).GetField(field)!;
            var box = new CheckBox { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, IsChecked = (bool)member.GetValue(main.Data.Settings)!, Tag = field };
            box.Checked += (_, _) => Commit(field, true, () => box.IsChecked = (bool)member.GetValue(main.Data.Settings)!);
            box.Unchecked += (_, _) => Commit(field, false, () => box.IsChecked = (bool)member.GetValue(main.Data.Settings)!);
            panel.Children.Add(box);
        }
        void Choice(StackPanel panel, string label, string field, params string[] options)
        {
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 0), Opacity = .75 });
            var member = typeof(Preferences).GetField(field)!;
            var combo = new ComboBox { ItemsSource = options, SelectedItem = member.GetValue(main.Data.Settings)?.ToString(), Tag = field };
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is not string selected)
                {
                    return;
                }
                Commit(field, member.FieldType == typeof(int) ? int.Parse(selected) : selected, () => combo.SelectedItem = member.GetValue(main.Data.Settings)?.ToString());
            };
            panel.Children.Add(combo);
        }
        var textTimers = new List<DispatcherTimer>();
        void Text(StackPanel panel, string label, string field)
        {
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 0), Opacity = .75 });
            var member = typeof(Preferences).GetField(field)!;
            var box = new TextBox { Text = member.GetValue(main.Data.Settings)?.ToString(), Tag = field };
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            textTimers.Add(timer);
            void Apply()
            {
                timer.Stop();
                if (member.FieldType == typeof(int))
                {
                    if (int.TryParse(box.Text, out var number))
                    {
                        Commit(field, number);
                    }
                    else
                    {
                        error.Text = "设置未生效：请输入有效数字。";
                        error.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    Commit(field, box.Text ?? "");
                }
            }
            timer.Tick += (_, _) => Apply();
            box.TextChanged += (_, _) => { timer.Stop(); timer.Start(); };
            box.LostKeyboardFocus += (_, _) => Apply();
            panel.Children.Add(box);
        }
        var behavior = Section("常用行为");
        var docking = Card(behavior, "拖动停靠");
        docking.Children.Add(new TextBlock { Text = main.Data.Settings.Docked ? "当前停靠：" + main.Data.Settings.Edge : "当前为浮动面板", FontSize = 14 });
        docking.Children.Add(new TextBlock { Text = "拖住标题栏，将面板放到屏幕边缘即可停靠。\n离开后隐藏，在原停靠位置的边缘呼出；拖离边缘解除停靠。", Opacity = .65, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8), LineHeight = 22 });
        Check(docking, "停靠后允许边缘呼出", nameof(Preferences.EdgeReveal));
        Check(docking, "停靠后鼠标离开自动隐藏", nameof(Preferences.AutoHide));
        var startup = Card(behavior, "启动与呼出");
        Check(startup, "开机自动运行", nameof(Preferences.AutoStart));
        Check(startup, "启动后隐藏", nameof(Preferences.StartHidden));
        Check(startup, "显示任务栏入口", nameof(Preferences.ShowTaskbar));
        Check(startup, "关闭窗口时隐藏到托盘", nameof(Preferences.CloseToTray));
        Check(startup, "呼出后显示第一个分组", nameof(Preferences.ShowFirstGroup));
        Check(startup, "浮动时跟随鼠标所在显示器呼出", nameof(Preferences.FollowMouse));
        var hiding = Card(behavior, "显示与隐藏");
        Check(hiding, "窗口置顶", nameof(Preferences.AlwaysOnTop));
        Check(hiding, "失去焦点时隐藏", nameof(Preferences.HideOnLostFocus));
        Check(hiding, "全屏应用运行时隐藏", nameof(Preferences.HideForFullscreen));
        Check(hiding, "启动项目后隐藏", nameof(Preferences.HideAfterLaunch));
        var display = Section("布局与外观");
        var appearance = Card(display, "主题与不透明度");
        var palette = new Button { Content = "打开超级调色盘", HorizontalContentAlignment = HorizontalAlignment.Center };
        palette.SetResourceReference(Control.BackgroundProperty, "InputSurface");
        palette.Click += (_, _) => main.Guard(() => new PaletteWindow(main).ShowDialog());
        appearance.Children.Add(palette);
        var percent = new TextBlock { Text = $"背景不透明度  {main.Data.Settings.Opacity:P0}", Margin = new Thickness(0, 15, 0, 6) };
        appearance.Children.Add(percent);
        var opacity = new Slider { Minimum = .65, Maximum = 1, Value = main.Data.Settings.Opacity, TickFrequency = .01, IsSnapToTickEnabled = true, Margin = new Thickness(0, 8, 0, 14), Tag = nameof(Preferences.Opacity) };
        var opacityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        var opacityPending = false;
        void SaveOpacity()
        {
            opacityTimer.Stop();
            if (opacityPending)
            {
                opacityPending = false;
                Commit(nameof(Preferences.Opacity), opacity.Value, () => { opacity.Value = main.Data.Settings.Opacity; main.PreviewOpacity(opacity.Value); });
            }
        }
        opacityTimer.Tick += (_, _) => SaveOpacity();
        opacity.ValueChanged += (_, _) =>
        {
            percent.Text = $"背景不透明度  {opacity.Value:P0}";
            if (!suppress)
            {
                main.PreviewOpacity(opacity.Value);
                opacityPending = true;
                opacityTimer.Stop();
                opacityTimer.Start();
            }
        };
        appearance.Children.Add(opacity);
        Check(appearance, "边缘隐藏与呼出动画", nameof(Preferences.Animation));
        var layout = Card(display, "面板布局");
        Choice(layout, "分组栏位置", nameof(Preferences.TabPosition), "顶部", "底部", "左侧", "右侧", "不显示");
        Choice(layout, "启动项布局", nameof(Preferences.Layout), "单列", "双列", "多列");
        Choice(layout, "图标大小", nameof(Preferences.IconSize), "16", "24", "32", "48", "64");
        Check(layout, "文字显示在图标下方", nameof(Preferences.TextBelow));
        Check(layout, "显示搜索框", nameof(Preferences.ShowSearch));
        Check(layout, "显示滚动条", nameof(Preferences.ShowScrollbar));
        Check(layout, "显示路径和启动次数提示", nameof(Preferences.Tooltips));
        Check(layout, "按启动次数优先显示", nameof(Preferences.SortByCount));
        var input = Section("启动与快捷键");
        var launching = Card(input, "启动方式");
        Check(launching, "双击启动项目", nameof(Preferences.DoubleClick));
        Check(launching, "默认以管理员身份启动", nameof(Preferences.RunAsAdmin));
        Check(launching, "允许重复启动同一项目", nameof(Preferences.AllowMultiple));
        Check(launching, "鼠标悬停切换分组", nameof(Preferences.HoverSwitch));
        Text(launching, "悬停延迟（50—2000毫秒）", nameof(Preferences.HoverDelay));
        var keys = Card(input, "键盘与鼠标");
        Check(keys, "启用全局快捷键", nameof(Preferences.HotkeyEnabled));
        Text(keys, "快捷键，例如 Ctrl+Alt+Space", nameof(Preferences.Hotkey));
        Check(keys, "鼠标中键呼出或隐藏", nameof(Preferences.MiddleMouseToggle));
        Check(keys, "鼠标左右键同时按下呼出或隐藏", nameof(Preferences.LeftRightToggle));
        Check(keys, "双击空白区域隐藏", nameof(Preferences.DoubleClickHide));
        var advanced = Section("锁定与备份");
        var locks = Card(advanced, "编辑锁定");
        Check(locks, "锁定窗口位置", nameof(Preferences.LockPosition));
        Check(locks, "锁定窗口大小", nameof(Preferences.LockSize));
        Check(locks, "锁定分组", nameof(Preferences.LockGroups));
        Check(locks, "锁定启动项", nameof(Preferences.LockItems));
        Check(locks, "锁定配置写入", nameof(Preferences.LockConfig));
        Check(locks, "启动时及每30分钟自动备份", nameof(Preferences.AutoBackup));
        Content = root;
        Closing += (_, _) =>
        {
            SaveOpacity();
            foreach (var timer in textTimers)
            {
                timer.Stop();
            }
        };
    }
}
