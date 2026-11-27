using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Rolan.Core;

namespace Rolan.App;

internal static class Dialogs
{
    internal static ThemedWindow WindowFor(Window owner, string title, double width = 520, double height = 500)
    {
        return new ThemedWindow { Owner = owner, Title = title, Width = width, Height = height, MinWidth = 380, MinHeight = 240, WindowStartupLocation = WindowStartupLocation.CenterOwner, FontFamily = owner.FontFamily, FontSize = 13 };
    }
    private static Button ButtonFor(string text, Action click)
    {
        var button = new Button { Content = text, Padding = new Thickness(20, 9, 20, 9), Margin = new Thickness(5) };
        button.SetResourceReference(Control.BackgroundProperty, text is "保存" or "确定" or "确认" or "保留" ? "AccentColor" : "InputSurface");
        button.SetResourceReference(Control.ForegroundProperty, text is "保存" or "确定" or "确认" or "保留" ? "AccentTextColor" : "TextColor");
        button.Click += (_, _) => click();
        return button;
    }
    internal static bool Question(Window owner, string title, string message)
    {
        var window = WindowFor(owner, title, 480, 270);
        window.ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(26, 8, 26, 22) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 24, Margin = new Thickness(0, 0, 0, 24) });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = ButtonFor("取消", () => window.DialogResult = false);
        cancel.IsCancel = true;
        actions.Children.Add(cancel);
        var confirm = ButtonFor("确认", () => window.DialogResult = true);
        confirm.SetResourceReference(Control.BackgroundProperty, "AccentColor");
        confirm.SetResourceReference(Control.ForegroundProperty, "AccentTextColor");
        actions.Children.Add(confirm);
        panel.Children.Add(actions);
        window.Content = panel;
        return window.ShowDialog() == true;
    }
    internal static void Notice(Window owner, string title, string message)
    {
        var window = WindowFor(owner, title, 510, 290);
        var panel = new DockPanel { Margin = new Thickness(25, 8, 25, 22) };
        var close = ButtonFor("知道了", () => window.Close());
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.IsCancel = true;
        DockPanel.SetDock(close, Dock.Bottom);
        panel.Children.Add(close);
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 24 });
        window.Content = panel;
        window.ShowDialog();
    }
    internal static string? Prompt(Window owner, string title, string label, string value)
    {
        var window = WindowFor(owner, title, 450, 240);
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = label });
        var input = new TextBox { Text = value };
        panel.Children.Add(input);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ButtonFor("取消", () => window.DialogResult = false));
        var ok = ButtonFor("确定", () => window.DialogResult = true);
        ok.IsDefault = true;
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        window.Content = panel;
        window.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return window.ShowDialog() == true ? input.Text : null;
    }
    internal static LaunchItem? EditItem(Window owner, LaunchItem? original)
    {
        var window = WindowFor(owner, original == null ? "添加启动项" : "编辑启动项", 600, 560);
        var panel = new StackPanel { Margin = new Thickness(24) };
        TextBox Field(string label, string value, bool browse = false)
        {
            panel.Children.Add(new TextBlock { Text = label });
            var grid = new DockPanel();
            var box = new TextBox { Text = value };
            if (browse)
            {
                var button = ButtonFor("选择", () =>
                {
                    var dialog = new OpenFileDialog { Filter = "所有文件|*.*" };
                    if (dialog.ShowDialog(window) == true)
                    {
                        box.Text = dialog.FileName;
                    }
                });
                DockPanel.SetDock(button, Dock.Right);
                grid.Children.Add(button);
            }
            grid.Children.Add(box);
            panel.Children.Add(grid);
            return box;
        }
        var name = Field("名称", original?.Name ?? "");
        var target = Field("目标 · 程序、文件夹、文件或网址", original?.Target ?? "", true);
        var arguments = Field("启动参数", original?.Arguments ?? "");
        var icon = Field("图标路径 · 可为空，也支持文件路径,图标序号", original?.IconPath ?? "", true);
        var directory = Field("工作目录 · 留空使用程序所在目录", original?.WorkingDirectory ?? "");
        panel.Children.Add(new TextBlock { Text = "支持环境变量及 %rp%（启动台目录）、%rr%（所在盘根目录）。", FontSize = 11, Opacity = .65 });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ButtonFor("取消", () => window.DialogResult = false));
        buttons.Children.Add(ButtonFor("保存", () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text))
            {
                Notice(window, "请输入名称", "启动项名称不能为空。");
                return;
            }
            window.DialogResult = true;
        }));
        panel.Children.Add(buttons);
        window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        return window.ShowDialog() == true ? new LaunchItem { Name = name.Text.Trim(), Target = target.Text.Trim(), Arguments = arguments.Text, IconPath = icon.Text.Trim(), WorkingDirectory = directory.Text.Trim() } : null;
    }
    internal static (bool Keep, bool ApplyAll) Duplicate(Window owner, string name)
    {
        var window = WindowFor(owner, "发现重复启动项", 480, 260);
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = $"「{name}」已存在，是否保留重复项？", TextWrapping = TextWrapping.Wrap, FontSize = 15 });
        var all = new CheckBox { Content = "应用于本次其他重复项" };
        panel.Children.Add(all);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ButtonFor("跳过", () => window.DialogResult = false));
        buttons.Children.Add(ButtonFor("保留", () => window.DialogResult = true));
        panel.Children.Add(buttons);
        window.Content = panel;
        return (window.ShowDialog() == true, all.IsChecked == true);
    }
    internal static void TextEditor(Window owner, string title, string text, Action<string> save)
    {
        var window = WindowFor(owner, title, 760, 600);
        var root = new DockPanel { Margin = new Thickness(16) };
        var input = new TextBox { Text = text, AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas, Microsoft YaHei UI") };
        var button = ButtonFor("保存", () =>
        {
            try
            {
                save(input.Text);
                window.Close();
            }
            catch (Exception ex)
            {
                Notice(window, "保存失败", ex.Message);
            }
        });
        DockPanel.SetDock(button, Dock.Bottom);
        root.Children.Add(button);
        root.Children.Add(input);
        window.Content = root;
        window.ShowDialog();
    }
}
