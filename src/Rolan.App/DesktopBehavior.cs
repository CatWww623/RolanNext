using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Rolan.App;

internal sealed partial class MainWindow
{
    private Forms.NotifyIcon? tray;
    private readonly DispatcherTimer desktopTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private IntPtr handle;
    private bool mouseToggleDown;
    private bool collapsed;
    private DateTime lastBackup = DateTime.UtcNow;
    private DateTime edgeEntered = DateTime.MaxValue;
    private bool panelOpen => IsVisible && WindowState != WindowState.Minimized;
    private void InitializeDesktop(bool autostart)
    {
        motionTimer.Tick += (_, _) => AdvancePanelMotion();
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WindowMessage);
            RefreshDesktopSettings();
        };
        Loaded += (_, _) =>
        {
            ClampPosition();
            tray = new Forms.NotifyIcon { Text = "Rolan Next · 点击打开启动台", Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application, Visible = true };
            tray.MouseClick += (_, e) => Dispatcher.Invoke(() =>
            {
                if (e.Button == Forms.MouseButtons.Left)
                {
                    ShowPanel();
                }
                else if (e.Button == Forms.MouseButtons.Right)
                {
                    var menu = new System.Windows.Controls.ContextMenu();
                    menu.Items.Add(Menu("显示启动台", ShowPanel));
                    menu.Items.Add(Menu("超级调色盘", () => { ShowPanel(); Guard(() => new PaletteWindow(this).ShowDialog()); }));
                    menu.Items.Add(Menu("设置", () => { ShowPanel(); Guard(() => new SettingsWindow(this).ShowDialog()); }));
                    menu.Items.Add(new System.Windows.Controls.Separator());
                    menu.Items.Add(Menu("退出", () => { quitting = true; Close(); }));
                    TrackMenu(menu);
                    menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                    Native.SetForegroundWindow(handle);
                    menu.IsOpen = true;
                }
            });
            desktopTimer.Tick += (_, _) => DesktopTick();
            desktopTimer.Start();
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            if (Data.Settings.AutoBackup)
            {
                Guard(() => Store.Backup(Data));
            }
            if (Data.Settings.StartHidden || autostart)
            {
                CollapsePanel();
            }
        };
        StateChanged += (_, _) =>
        {
            if (collapsed && WindowState == WindowState.Normal)
            {
                ShowPanel();
            }
        };
        Deactivated += (_, _) =>
        {
            if (Data.Settings.HideOnLostFocus && !PanelInteractionActive && !pinned && DateTime.UtcNow > dragUntil)
            {
                CollapsePanel();
            }
        };
    }
    private void DisplayChanged(object? sender, EventArgs args)
    {
        Dispatcher.BeginInvoke(() => ClampPosition());
    }
    internal void RefreshDesktopSettings(bool reportFailure = false)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }
        Native.UnregisterHotKey(handle, 1);
        if (Data.Settings.HotkeyEnabled && !Native.RegisterShortcut(handle, Data.Settings.Hotkey))
        {
            if (reportFailure)
            {
                throw new InvalidOperationException("快捷键无效或已被占用，请更换组合。");
            }
            status.Text = "快捷键注册失败，可能已被占用，请在设置中更换";
        }
    }
    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        if (message == 0x312 || message == 0x8001)
        {
            if (panelOpen && message == 0x312 && !(edgeMotion.IsRunning && !edgeMotion.TargetVisible))
            {
                CollapsePanel();
            }
            else
            {
                ShowPanel();
            }
            handled = true;
        }
        else if (message == (int)Native.TaskbarCreated && tray != null)
        {
            tray.Visible = false;
            tray.Visible = true;
        }
        return IntPtr.Zero;
    }
    private void DesktopTick()
    {
        if (!Native.GetCursorPos(out var point))
        {
            return;
        }
        var now = DateTime.UtcNow;
        var mouseDown = Data.Settings.MiddleMouseToggle && (Native.GetAsyncKeyState(4) & 0x8000) != 0 || Data.Settings.LeftRightToggle && (Native.GetAsyncKeyState(1) & 0x8000) != 0 && (Native.GetAsyncKeyState(2) & 0x8000) != 0;
        if (mouseDown && !mouseToggleDown && !PanelInteractionActive)
        {
            if (panelOpen && !(edgeMotion.IsRunning && !edgeMotion.TargetVisible))
            {
                CollapsePanel();
            }
            else
            {
                ShowPanel();
            }
        }
        mouseToggleDown = mouseDown;
        if (PanelInteractionActive || now < dragUntil)
        {
            leaveAt = DateTime.MaxValue;
            return;
        }
        if (Data.Settings.HoverSwitch && hoveredGroup.Length > 0 && now >= hoverAt && draggingGroup == null)
        {
            var id = hoveredGroup;
            hoveredGroup = "";
            hoverAt = DateTime.MaxValue;
            SelectGroup(id);
        }
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y));
        var fullscreen = false;
        var foreground = Native.GetForegroundWindow();
        if (foreground != handle && foreground != IntPtr.Zero && foreground != Native.GetShellWindow() && Native.GetWindowRect(foreground, out var other))
        {
            fullscreen = other.Left <= screen.Bounds.Left && other.Top <= screen.Bounds.Top && other.Right >= screen.Bounds.Right && other.Bottom >= screen.Bounds.Bottom;
        }
        if (Data.Settings.HideForFullscreen && fullscreen)
        {
            if (panelOpen && !pinned)
            {
                CollapsePanel();
            }
            return;
        }
        if (!panelOpen && Data.Settings.EdgeReveal && Data.Settings.Docked && screen.DeviceName == Data.Settings.DockMonitor)
        {
            var bounds = screen.Bounds;
            var near = Data.Settings.Edge switch { "左侧" => point.X <= bounds.Left + 2, "顶部" => point.Y <= bounds.Top + 2, "底部" => point.Y >= bounds.Bottom - 3, _ => point.X >= bounds.Right - 3 };
            if (near && Data.Settings.MonitorPositions.TryGetValue(screen.DeviceName, out var saved) && Rolan.Core.Docking.WithinSpan(Data.Settings.Edge, new Rolan.Core.PixelBounds(saved.Left, saved.Top, saved.Width, saved.Height), point.X, point.Y))
            {
                if (edgeEntered == DateTime.MaxValue)
                {
                    edgeEntered = now;
                }
                if ((now - edgeEntered).TotalMilliseconds >= Data.Settings.HoverDelay)
                {
                    ShowPanel();
                    PlaceOnScreen(screen, true);
                    edgeEntered = DateTime.MaxValue;
                }
            }
            else
            {
                edgeEntered = DateTime.MaxValue;
            }
        }
        if (panelOpen && Data.Settings.AutoHide && Data.Settings.Docked && !pinned && Native.GetWindowRect(handle, out var rect))
        {
            var inside = point.X >= rect.Left - 3 && point.X < rect.Right + 3 && point.Y >= rect.Top - 3 && point.Y < rect.Bottom + 3;
            if (inside)
            {
                leaveAt = DateTime.MaxValue;
                if (reverseHideOnPointerReturn && edgeMotion.IsRunning && !edgeMotion.TargetVisible)
                {
                    ShowPanel();
                }
            }
            else if (leaveAt == DateTime.MaxValue)
            {
                leaveAt = now;
            }
            else if ((now - leaveAt).TotalMilliseconds > 600)
            {
                BeginPanelCollapse(true);
            }
        }
        if (Data.Settings.AutoBackup && (now - lastBackup).TotalMinutes >= 30)
        {
            lastBackup = now;
            try
            {
                Store.Backup(Data);
            }
            catch (Exception ex)
            {
                Store.Log(ex, "自动备份");
            }
        }
    }
    internal void ShowPanel()
    {
        if (!panelOpen)
        {
            edgeMotion.Reset(false);
            ApplyPanelSlide(Data.Settings.Animation && Data.Settings.Docked ? 0 : 1);
        }
        reverseHideOnPointerReturn = false;
        collapsed = false;
        Show();
        WindowState = WindowState.Normal;
        if (Data.Settings.Docked && Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == Data.Settings.DockMonitor) is { } dockScreen)
        {
            PlaceOnScreen(dockScreen, true);
        }
        else if (Data.Settings.FollowMouse)
        {
            Native.GetCursorPos(out var point);
            PlaceOnScreen(Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y)), false);
        }
        else
        {
            ClampPosition();
        }
        if (Data.Settings.ShowFirstGroup)
        {
            Data.Settings.SelectedGroupId = Data.Groups[0].Id;
            search.Clear();
            Render();
        }
        Activate();
        search.Focus();
        leaveAt = DateTime.MaxValue;
        dragUntil = DateTime.UtcNow.AddMilliseconds(700);
        BeginPanelMotion(true);
    }
    private void CollapsePanel()
    {
        BeginPanelCollapse(false);
    }
    private void CompleteWindowDrag()
    {
        SaveGeometry();
        if (!Native.GetWindowRect(handle, out var rect))
        {
            return;
        }
        var hasPointer = Native.GetCursorPos(out var pointer);
        var screen = hasPointer ? Forms.Screen.FromPoint(new System.Drawing.Point(pointer.X, pointer.Y)) : Forms.Screen.FromHandle(handle);
        var area = screen.WorkingArea;
        var bounds = screen.Bounds;
        var pointerEdge = hasPointer ? Rolan.Core.Docking.DetectPointer(new Rolan.Core.PixelBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height), pointer.X, pointer.Y, 16 * Native.MonitorScale(screen)) : "";
        var edge = Rolan.Core.Docking.Detect(new Rolan.Core.PixelBounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top), new Rolan.Core.PixelBounds(area.Left, area.Top, area.Width, area.Height), 16 * Native.MonitorScale(screen));
        edge = pointerEdge.Length > 0 ? pointerEdge : edge;
        Data.Settings.Docked = edge.Length > 0;
        Data.Settings.DockMonitor = edge.Length > 0 ? screen.DeviceName : "";
        if (edge.Length > 0)
        {
            Data.Settings.Edge = edge;
            Data.Settings.EdgeReveal = true;
            Data.Settings.AutoHide = true;
            PlaceOnScreen(screen, true);
            SaveGeometry();
        }
        Save();
        SettingsChanged?.Invoke(nameof(Rolan.Core.Preferences.Docked));
    }
    private void SaveGeometry()
    {
        if (!panelOpen)
        {
            return;
        }
        Data.Settings.Left = Left;
        Data.Settings.Top = Top;
        Data.Settings.Width = Width;
        Data.Settings.Height = Height;
        if (handle != IntPtr.Zero)
        {
            Data.Settings.Monitor = Forms.Screen.FromHandle(handle).DeviceName;
            if (Native.GetWindowRect(handle, out var rect))
            {
                Data.Settings.MonitorPositions[Data.Settings.Monitor] = new Rolan.Core.WindowPlacement { Left = rect.Left, Top = rect.Top, Width = rect.Right - rect.Left, Height = rect.Bottom - rect.Top };
            }
        }
    }
    private void ClampPosition()
    {
        var screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == Data.Settings.Monitor) ?? Forms.Screen.FromHandle(handle);
        PlaceOnScreen(screen, false);
    }
    private void PlaceOnScreen(Forms.Screen screen, bool edge)
    {
        var area = screen.WorkingArea;
        var scale = Native.MonitorScale(screen);
        Native.GetWindowRect(handle, out var current);
        Data.Settings.MonitorPositions.TryGetValue(screen.DeviceName, out var saved);
        var width = Math.Clamp((int)(saved?.Width ?? Width * scale), 320, area.Width);
        var height = Math.Clamp((int)(saved?.Height ?? Height * scale), 240, area.Height);
        var x = (int)(saved?.Left ?? current.Left);
        var y = (int)(saved?.Top ?? current.Top);
        x = edge && Data.Settings.Edge == "左侧" ? area.Left : edge && Data.Settings.Edge == "右侧" ? area.Right - width : Math.Clamp(x, area.Left, area.Right - width);
        y = edge && Data.Settings.Edge == "顶部" ? area.Top : edge && Data.Settings.Edge == "底部" ? area.Bottom - height : Math.Clamp(y, area.Top, area.Bottom - height);
        Native.SetWindowPos(handle, IntPtr.Zero, x, y, width, height, 0x14);
    }
    private void DisposeDesktop()
    {
        desktopTimer.Stop();
        motionTimer.Stop();
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        Native.UnregisterHotKey(handle, 1);
        tray?.Dispose();
        foreach (var process in running.Values)
        {
            process.Dispose();
        }
    }
}
