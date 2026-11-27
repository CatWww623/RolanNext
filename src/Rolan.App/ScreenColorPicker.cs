using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Rolan.App;

internal static class ScreenColorPicker
{
    // 仅在用户主动按下屏幕取色时调用；离屏自动验证不进入此方法。
    internal static void Pick(Window owner, Action<Color?> selected)
    {
        var captures = new List<(Forms.Screen Screen, Drawing.Bitmap Bitmap, BitmapSource Image)>();
        var windows = new List<Window>();
        var finished = false;
        void Finish(Color? color)
        {
            if (finished)
            {
                return;
            }
            finished = true;
            foreach (var window in windows)
            {
                window.Close();
            }
            foreach (var capture in captures)
            {
                capture.Bitmap.Dispose();
            }
            selected(color);
        }
        try
        {
            // 先取得所有屏幕快照，再打开取色层，避免把另一块屏幕的取色层捕获进去。
            foreach (var screen in Forms.Screen.AllScreens)
            {
                var bounds = screen.Bounds;
                var bitmap = new Drawing.Bitmap(bounds.Width, bounds.Height, Drawing.Imaging.PixelFormat.Format32bppPArgb);
                try
                {
                    using (var graphics = Drawing.Graphics.FromImage(bitmap))
                    {
                        graphics.CopyFromScreen(bounds.Location, Drawing.Point.Empty, bounds.Size, Drawing.CopyPixelOperation.SourceCopy);
                    }
                    var data = bitmap.LockBits(new Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), Drawing.Imaging.ImageLockMode.ReadOnly, Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    BitmapSource image;
                    try
                    {
                        var pixels = new byte[data.Stride * bitmap.Height];
                        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                        image = BitmapSource.Create(bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Pbgra32, null, pixels, data.Stride);
                        image.Freeze();
                    }
                    finally
                    {
                        bitmap.UnlockBits(data);
                    }
                    captures.Add((screen, bitmap, image));
                }
                catch
                {
                    bitmap.Dispose();
                    throw;
                }
            }
            foreach (var capture in captures)
            {
                var screen = capture.Screen;
                var scale = Native.MonitorScale(screen);
                var window = new Window { Owner = owner, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true, WindowStartupLocation = WindowStartupLocation.Manual, Left = screen.Bounds.Left / scale, Top = screen.Bounds.Top / scale, Width = screen.Bounds.Width / scale, Height = screen.Bounds.Height / scale, Cursor = Cursors.Cross, Title = "屏幕取色 · 退出键取消" };
                window.Content = new ScreenColorSurface(capture.Bitmap, capture.Image, screen.Bounds);
                window.SourceInitialized += (_, _) => Native.SetWindowPos(new WindowInteropHelper(window).Handle, IntPtr.Zero, screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height, 0x14);
                window.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Finish(null); } };
                window.MouseRightButtonDown += (_, e) => { e.Handled = true; Finish(null); };
                window.MouseLeftButtonDown += (_, e) =>
                {
                    e.Handled = true;
                    if (Native.GetCursorPos(out var point))
                    {
                        var pixel = capture.Bitmap.GetPixel(Math.Clamp(point.X - screen.Bounds.Left, 0, capture.Bitmap.Width - 1), Math.Clamp(point.Y - screen.Bounds.Top, 0, capture.Bitmap.Height - 1));
                        Finish(Color.FromRgb(pixel.R, pixel.G, pixel.B));
                    }
                    else
                    {
                        Finish(null);
                    }
                };
                window.Closed += (_, _) => Finish(null);
                windows.Add(window);
            }
            foreach (var window in windows)
            {
                window.Show();
            }
            if (windows.Count == 0)
            {
                Finish(null);
            }
        }
        catch
        {
            finished = true;
            foreach (var window in windows)
            {
                window.Close();
            }
            foreach (var capture in captures)
            {
                capture.Bitmap.Dispose();
            }
            throw;
        }
    }
}

internal sealed class ScreenColorSurface : FrameworkElement
{
    private readonly Drawing.Bitmap bitmap;
    private readonly BitmapSource image;
    private readonly Drawing.Rectangle bounds;
    private Point? pointer;
    internal ScreenColorSurface(Drawing.Bitmap bitmap, BitmapSource image, Drawing.Rectangle bounds)
    {
        this.bitmap = bitmap;
        this.image = image;
        this.bounds = bounds;
        MouseMove += (_, e) => { pointer = e.GetPosition(this); InvalidateVisual(); };
    }
    protected override void OnRender(DrawingContext drawing)
    {
        drawing.DrawImage(image, new Rect(0, 0, ActualWidth, ActualHeight));
        if (pointer is not { } point || !Native.GetCursorPos(out var pixelPoint))
        {
            return;
        }
        var pixel = bitmap.GetPixel(Math.Clamp(pixelPoint.X - bounds.Left, 0, bitmap.Width - 1), Math.Clamp(pixelPoint.Y - bounds.Top, 0, bitmap.Height - 1));
        var color = Color.FromRgb(pixel.R, pixel.G, pixel.B);
        var origin = new Point(Math.Clamp(point.X + 24, 6, Math.Max(6, ActualWidth - 240)), Math.Clamp(point.Y + 24, 6, Math.Max(6, ActualHeight - 74)));
        drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(24, 30, 41)), new Pen(Brushes.White, 1), new Rect(origin, new Size(230, 62)), 9, 9);
        drawing.DrawRoundedRectangle(new SolidColorBrush(color), new Pen(Brushes.White, 1), new Rect(origin.X + 12, origin.Y + 13, 34, 34), 5, 5);
        var text = new FormattedText(PaletteColors.Hex(color) + "\n单击选择 · 右键或退出键取消", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 11, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        drawing.DrawText(text, new Point(origin.X + 57, origin.Y + 11));
    }
}
