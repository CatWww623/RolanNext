using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace Rolan.App;

internal class ThemedWindow : Window
{
    private readonly ContentControl body = new();
    internal new object? Content
    {
        get => body.Content;
        set => body.Content = value;
    }
    internal ThemedWindow()
    {
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new FontFamily("Microsoft YaHei UI");
        FontSize = 13;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        SetResourceReference(ForegroundProperty, "TextColor");
        SetResourceReference(BackgroundProperty, "WindowBackground");
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(5), CornerRadius = new CornerRadius(16), GlassFrameThickness = new Thickness(0) });
        var shell = new DockPanel();
        var header = new DockPanel { Height = 56, Margin = new Thickness(22, 2, 12, 0), Background = Brushes.Transparent };
        var close = new Button { Content = "×", Width = 36, Height = 32, Background = Brushes.Transparent, BorderThickness = new Thickness(0), ToolTip = "关闭", VerticalAlignment = VerticalAlignment.Center };
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        var title = new TextBlock { FontSize = 18, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        title.SetBinding(TextBlock.TextProperty, new Binding(nameof(Title)) { Source = this });
        header.Children.Add(title);
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        };
        DockPanel.SetDock(header, Dock.Top);
        shell.Children.Add(header);
        shell.Children.Add(body);
        var frame = new Border { Child = shell, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1) };
        frame.SetResourceReference(Border.BackgroundProperty, "WindowBackground");
        frame.SetResourceReference(Border.BorderBrushProperty, "OutlineColor");
        base.Content = frame;
    }
}
