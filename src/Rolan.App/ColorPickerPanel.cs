using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Rolan.App;

internal sealed class ColorSpectrum : FrameworkElement
{
    internal double Hue;
    internal double Saturation;
    internal double Value = 1;
    internal event Action<Color>? Changed;
    internal ColorSpectrum()
    {
        Height = 145;
        Focusable = true;
        Cursor = Cursors.Cross;
        ToolTip = "横向调整色相，纵向调整饱和度；方向键微调";
        MouseLeftButtonDown += (_, e) => { Focus(); CaptureMouse(); Choose(e.GetPosition(this)); e.Handled = true; };
        MouseMove += (_, e) => { if (IsMouseCaptured) { Choose(e.GetPosition(this)); } };
        MouseLeftButtonUp += (_, e) => { if (IsMouseCaptured) { Choose(e.GetPosition(this)); ReleaseMouseCapture(); e.Handled = true; } };
        KeyDown += (_, e) =>
        {
            var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
            if (e.Key is Key.Left or Key.Right)
            {
                Hue = (Hue + (e.Key == Key.Right ? step : -step) + 360) % 360;
            }
            else if (e.Key is Key.Up or Key.Down)
            {
                Saturation = Math.Clamp(Saturation + (e.Key == Key.Up ? step : -step) / 100.0, 0, 1);
            }
            else
            {
                return;
            }
            InvalidateVisual();
            Changed?.Invoke(PaletteColors.FromHsv(Hue, Saturation, Value));
            e.Handled = true;
        };
    }
    private void Choose(Point point)
    {
        Hue = Math.Clamp(point.X / Math.Max(1, ActualWidth), 0, .999999) * 360;
        Saturation = 1 - Math.Clamp(point.Y / Math.Max(1, ActualHeight), 0, 1);
        InvalidateVisual();
        Changed?.Invoke(PaletteColors.FromHsv(Hue, Saturation, Value));
    }
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        var area = new Rect(0, 0, ActualWidth, ActualHeight);
        drawing.PushClip(new RectangleGeometry(area, 9, 9));
        var rainbow = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5) };
        for (var i = 0; i <= 6; i++)
        {
            rainbow.GradientStops.Add(new GradientStop(PaletteColors.FromHsv(i * 60, 1, 1), i / 6.0));
        }
        drawing.DrawRectangle(rainbow, null, area);
        drawing.DrawRectangle(new LinearGradientBrush(Color.FromArgb(0, 255, 255, 255), Colors.White, 90), null, area);
        drawing.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)Math.Round((1 - Value) * 255), 0, 0, 0)), null, area);
        drawing.Pop();
        var cursor = new Point(Math.Clamp(Hue / 360 * ActualWidth, 6, Math.Max(6, ActualWidth - 6)), Math.Clamp((1 - Saturation) * ActualHeight, 6, Math.Max(6, ActualHeight - 6)));
        drawing.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), 4), cursor, 6, 6);
        drawing.DrawEllipse(null, new Pen(Brushes.White, 2), cursor, 6, 6);
    }
}

internal sealed class ColorPickerPanel : Border
{
    private readonly ColorSpectrum spectrum = new();
    private readonly Slider brightness = new() { Minimum = 0, Maximum = 1, TickFrequency = .01, SmallChange = .01, LargeChange = .1 };
    private readonly TextBox hex = new() { Width = 108, Margin = new Thickness(8, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBox[] components = new TextBox[3];
    private readonly Ellipse sample = new() { Width = 24, Height = 24, StrokeThickness = 2 };
    private readonly TextBlock hint = new() { FontSize = 11, Opacity = .68, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
    private bool updating;
    internal Color SelectedColor { get; private set; }
    internal event Action<Color>? ColorChanged;
    internal event Action? EyedropperRequested;
    internal ColorPickerPanel()
    {
        CornerRadius = new CornerRadius(12);
        Padding = new Thickness(16);
        SetResourceReference(BackgroundProperty, "PopupSurface");
        var panel = new StackPanel();
        panel.Children.Add(spectrum);
        var lightRow = new Grid { Margin = new Thickness(0, 10, 0, 12) };
        lightRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        lightRow.ColumnDefinitions.Add(new ColumnDefinition());
        lightRow.Children.Add(new TextBlock { Text = "明暗", VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
        Grid.SetColumn(brightness, 1);
        lightRow.Children.Add(brightness);
        panel.Children.Add(lightRow);
        var values = new DockPanel { LastChildFill = false };
        sample.SetResourceReference(Shape.StrokeProperty, "OutlineColor");
        sample.VerticalAlignment = VerticalAlignment.Center;
        values.Children.Add(sample);
        values.Children.Add(hex);
        var eyedropper = new Button { Content = "屏幕取色", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(9, 6, 9, 6), ToolTip = "从任一显示器选择颜色，退出键或右键取消" };
        eyedropper.Click += (_, _) => EyedropperRequested?.Invoke();
        values.Children.Add(eyedropper);
        panel.Children.Add(values);
        var rgb = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        for (var index = 0; index < 3; index++)
        {
            rgb.ColumnDefinitions.Add(new ColumnDefinition());
            var column = new StackPanel { Margin = new Thickness(index == 0 ? 0 : 8, 0, 0, 0) };
            column.Children.Add(new TextBlock { Text = new[] { "红", "绿", "蓝" }[index] + " · 0–255", FontSize = 11, Opacity = .7 });
            var box = new TextBox { Margin = new Thickness(0, 4, 0, 0), MaxLength = 3 };
            components[index] = box;
            box.TextChanged += (_, _) => ReadComponents();
            column.Children.Add(box);
            Grid.SetColumn(column, index);
            rgb.Children.Add(column);
        }
        panel.Children.Add(rgb);
        panel.Children.Add(hint);
        Child = panel;
        hex.TextChanged += (_, _) =>
        {
            if (updating)
            {
                return;
            }
            if (PaletteColors.TryHex(hex.Text, out var color))
            {
                SetColor(color, true, true);
            }
            else
            {
                hint.Text = "请输入六位十六进制颜色，例如 #66CCAA";
            }
        };
        brightness.ValueChanged += (_, _) =>
        {
            if (!updating)
            {
                spectrum.Value = brightness.Value;
                spectrum.InvalidateVisual();
                SetColor(PaletteColors.FromHsv(spectrum.Hue, spectrum.Saturation, spectrum.Value), true, false, true);
            }
        };
        spectrum.Changed += color => SetColor(color, true, false, true);
        SetColor(Color.FromRgb(102, 204, 170));
    }
    private void ReadComponents()
    {
        if (updating || components.AnyEmpty())
        {
            return;
        }
        var values = new byte[3];
        for (var i = 0; i < 3; i++)
        {
            if (!byte.TryParse(components[i].Text, NumberStyles.None, CultureInfo.InvariantCulture, out values[i]))
            {
                hint.Text = "红、绿、蓝分量应为0至255的整数";
                return;
            }
        }
        SetColor(Color.FromRgb(values[0], values[1], values[2]), true, false, false, true);
    }
    internal void SetColor(Color color, bool notify = false, bool preserveHex = false, bool preserveSpectrum = false, bool preserveComponents = false)
    {
        updating = true;
        try
        {
            SelectedColor = color;
            if (!preserveSpectrum)
            {
                var hsv = PaletteColors.ToHsv(color);
                // 灰色和黑色没有唯一色相，保留用户刚选过的方向，便于恢复饱和度或亮度。
                if (hsv.Saturation > .0001)
                {
                    spectrum.Hue = hsv.Hue;
                }
                spectrum.Saturation = hsv.Saturation;
                spectrum.Value = hsv.Value;
            }
            brightness.Value = spectrum.Value;
            spectrum.InvalidateVisual();
            sample.Fill = new SolidColorBrush(color);
            if (!preserveHex)
            {
                hex.Text = PaletteColors.Hex(color);
            }
            if (!preserveComponents)
            {
                components[0].Text = color.R.ToString(CultureInfo.InvariantCulture);
                components[1].Text = color.G.ToString(CultureInfo.InvariantCulture);
                components[2].Text = color.B.ToString(CultureInfo.InvariantCulture);
            }
            hint.Text = "即时预览并自动保存 · 文字明暗会随配色协调";
        }
        finally
        {
            updating = false;
        }
        if (notify)
        {
            ColorChanged?.Invoke(color);
        }
    }
}

internal static class PickerTextBoxes
{
    internal static bool AnyEmpty(this TextBox[] boxes) => Array.Exists(boxes, b => b == null || b.Text.Length == 0);
}
