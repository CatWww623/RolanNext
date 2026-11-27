using System;
using System.Windows;
using System.Windows.Input;

namespace Rolan.App;

internal sealed partial class MainWindow
{
    private bool movingPanel;
    private Native.Point panelDragPointer;
    private Native.Rect panelDragBounds;
    private UIElement? panelDragSurface;
    private void StartPanelDrag(UIElement surface, MouseButtonEventArgs e)
    {
        if (Data.Settings.LockPosition || e.ChangedButton != MouseButton.Left || e.ClickCount != 1 || handle == IntPtr.Zero)
        {
            return;
        }
        for (var source = e.OriginalSource as DependencyObject; source != null && source != surface; source = source is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source))
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase)
            {
                return;
            }
        }
        StopPanelMotion();
        var messagePosition = Native.GetMessagePos();
        panelDragPointer = new Native.Point { X = (short)(messagePosition & 0xffff), Y = (short)(messagePosition >> 16) };
        if (!Native.GetWindowRect(handle, out panelDragBounds))
        {
            return;
        }
        if (WindowState != WindowState.Normal)
        {
            WindowState = WindowState.Normal;
            Native.GetWindowRect(handle, out panelDragBounds);
        }
        panelDragSurface = surface;
        movingPanel = true;
        busy++;
        if (!Mouse.Capture(surface, CaptureMode.Element))
        {
            movingPanel = false;
            panelDragSurface = null;
            busy--;
            return;
        }
        e.Handled = true;
    }
    private void MovePanelDrag()
    {
        if (!movingPanel || !Native.GetCursorPos(out var pointer))
        {
            return;
        }
        var x = panelDragBounds.Left + pointer.X - panelDragPointer.X;
        var y = panelDragBounds.Top + pointer.Y - panelDragPointer.Y;
        Native.SetWindowPos(handle, IntPtr.Zero, x, y, panelDragBounds.Right - panelDragBounds.Left, panelDragBounds.Bottom - panelDragBounds.Top, 0x14);
    }
    private void EndPanelDrag()
    {
        if (!movingPanel)
        {
            return;
        }
        MovePanelDrag();
        movingPanel = false;
        var surface = panelDragSurface;
        panelDragSurface = null;
        if (Mouse.Captured == surface)
        {
            Mouse.Capture(null);
        }
        try
        {
            CompleteWindowDrag();
        }
        finally
        {
            busy = Math.Max(0, busy - 1);
            leaveAt = DateTime.MaxValue;
        }
    }
}
