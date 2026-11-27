namespace Rolan.Core;

public sealed class LaunchItem
{
    public string Id = Guid.NewGuid().ToString("N");
    public string Name = "新项目";
    public string Target = "";
    public string IconPath = "";
    public string Arguments = "";
    public string WorkingDirectory = "";
    public long Count;
}

public sealed class LaunchGroup
{
    public string Id = Guid.NewGuid().ToString("N");
    public string Name = "默认分组";
    public List<LaunchItem> Items = [];
}

public sealed class Preferences
{
    public string Theme = "极光青";
    public string CustomColor = "#45B8AC";
    public Dictionary<string, CustomPalette> CustomPalettes = [];
    public double Opacity = 1;
    public bool Animation = true;
    public bool AutoHide = true;
    public bool EdgeReveal = true;
    public string Edge = "右侧";
    public bool Docked;
    public string DockMonitor = "";
    public string TabPosition = "顶部";
    public string Layout = "多列";
    public int IconSize = 32;
    public bool TextBelow = true;
    public bool AlwaysOnTop;
    public bool DoubleClick;
    public bool RunAsAdmin;
    public bool AllowMultiple = true;
    public bool HideAfterLaunch = true;
    public bool StartHidden;
    public bool CloseToTray = true;
    public bool HoverSwitch = true;
    public int HoverDelay = 200;
    public bool ShowTaskbar = true;
    public bool FollowMouse;
    public bool ShowFirstGroup;
    public bool HideOnLostFocus;
    public bool HideForFullscreen = true;
    public bool DoubleClickHide;
    public bool LockPosition;
    public bool LockSize;
    public bool LockGroups;
    public bool LockItems;
    public bool LockConfig;
    public bool Tooltips = true;
    public bool ShowSearch = true;
    public bool ShowScrollbar = true;
    public bool SortByCount;
    public bool HotkeyEnabled = true;
    public string Hotkey = "Ctrl+Alt+Space";
    public bool MiddleMouseToggle;
    public bool LeftRightToggle;
    public bool AutoStart;
    public bool AutoBackup = true;
    public double Left = 150;
    public double Top = 100;
    public double Width = 760;
    public double Height = 650;
    public string Monitor = "";
    public string SelectedGroupId = "";
    public Dictionary<string, WindowPlacement> MonitorPositions = [];
}

public sealed class CustomPalette
{
    public int ColorCount = 2;
    public List<string> Colors = ["#9DDCC8", "#B9B5EB", "#F2C3DB"];
    public CustomPalette Copy() => new() { ColorCount = ColorCount, Colors = [.. Colors] };
}

public sealed class WindowPlacement
{
    public double Left;
    public double Top;
    public double Width = 760;
    public double Height = 650;
}

public sealed class AppData
{
    public int Version = 1;
    public List<LaunchGroup> Groups = [];
    public Preferences Settings = new();
    public string Notes = "";
}
