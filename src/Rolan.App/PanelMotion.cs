using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Rolan.Core;

namespace Rolan.App;

internal sealed partial class MainWindow
{
    private readonly EdgeMotion edgeMotion = new();
    private readonly TranslateTransform panelSlide = new();
    private readonly Stopwatch motionClock = Stopwatch.StartNew();
    private readonly DispatcherTimer motionTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    private bool reverseHideOnPointerReturn;
    internal void ApplyPanelSlide(double visibility)
    {
        var offset = EdgeMotion.Offset(Data.Settings.Edge, visibility, ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
        var dpi = VisualTreeHelper.GetDpi(this);
        // 仅移动内容并按物理像素对齐；宿主窗口不越界，背景透明度保持原设置。
        panelSlide.X = Math.Round(offset.X * dpi.DpiScaleX) / dpi.DpiScaleX;
        panelSlide.Y = Math.Round(offset.Y * dpi.DpiScaleY) / dpi.DpiScaleY;
    }
    private void BeginPanelMotion(bool visible)
    {
        edgeMotion.SetTarget(visible, motionClock.Elapsed.TotalMilliseconds, Data.Settings.Animation && Data.Settings.Docked);
        ApplyPanelSlide(edgeMotion.Visibility);
        if (edgeMotion.IsRunning)
        {
            motionTimer.Start();
        }
        else
        {
            motionTimer.Stop();
            if (!visible)
            {
                FinishCollapsePanel();
            }
        }
    }
    private void AdvancePanelMotion()
    {
        if (!Data.Settings.Animation || !Data.Settings.Docked)
        {
            edgeMotion.Reset(edgeMotion.TargetVisible);
        }
        else
        {
            edgeMotion.Advance(motionClock.Elapsed.TotalMilliseconds);
        }
        ApplyPanelSlide(edgeMotion.Visibility);
        if (!edgeMotion.IsRunning)
        {
            motionTimer.Stop();
            if (!edgeMotion.TargetVisible)
            {
                FinishCollapsePanel();
            }
        }
    }
    private void StopPanelMotion()
    {
        motionTimer.Stop();
        edgeMotion.Reset(true);
        reverseHideOnPointerReturn = false;
        ApplyPanelSlide(1);
    }
    private void BeginPanelCollapse(bool reverseOnPointerReturn)
    {
        if (!panelOpen || edgeMotion.IsRunning && !edgeMotion.TargetVisible)
        {
            return;
        }
        SaveGeometry();
        reverseHideOnPointerReturn = reverseOnPointerReturn;
        leaveAt = DateTime.MaxValue;
        hoveredGroup = "";
        BeginPanelMotion(false);
    }
    private void FinishCollapsePanel()
    {
        collapsed = true;
        reverseHideOnPointerReturn = false;
        if (Data.Settings.ShowTaskbar)
        {
            WindowState = WindowState.Minimized;
        }
        else
        {
            Hide();
        }
    }
}
