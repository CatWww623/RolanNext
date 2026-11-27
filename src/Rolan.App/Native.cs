using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rolan.Core;
using Forms = System.Windows.Forms;

namespace Rolan.App;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellExecuteInfo
    {
        public uint Size;
        public uint Mask;
        public IntPtr Window;
        public string? Verb;
        public string? File;
        public string? Parameters;
        public string? Directory;
        public int Show;
        public IntPtr Instance;
        public IntPtr ItemIdList;
        public string? Class;
        public IntPtr ClassKey;
        public uint HotKey;
        public IntPtr IconOrMonitor;
        public IntPtr Process;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellExecuteExW(ref ShellExecuteInfo info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHParseDisplayName(string path, IntPtr context, out IntPtr itemIdList, uint requestedAttributes, IntPtr attributes);
    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern IntPtr ILFindLastID(IntPtr itemIdList);
    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHOpenFolderAndSelectItems(IntPtr folder, uint itemCount, ref IntPtr child, uint flags);
    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void CoUninitialize();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, out FileInfo info, uint size, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string path, int index, out IntPtr large, out IntPtr small, uint count);
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")]
    internal static extern uint GetMessagePos();
    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")]
    internal static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")]
    internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    internal static double MonitorScale(Forms.Screen screen)
    {
        var point = new Point { X = screen.Bounds.Left + screen.Bounds.Width / 2, Y = screen.Bounds.Top + screen.Bounds.Height / 2 };
        return GetDpiForMonitor(MonitorFromPoint(point, 2), 0, out var dpi, out _) == 0 ? dpi / 96.0 : 1;
    }
    [DllImport("user32.dll")]
    internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string text);
    [DllImport("user32.dll")]
    internal static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindow(string className, string windowName);
    [DllImport("user32.dll")]
    internal static extern IntPtr GetShellWindow();
    internal static readonly uint TaskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private static readonly Dictionary<string, ImageSource?> Icons = new(StringComparer.OrdinalIgnoreCase);
    internal static ImageSource? Icon(string target)
    {
        if (Icons.TryGetValue(target, out var cached))
        {
            return cached;
        }
        IntPtr icon = IntPtr.Zero;
        IntPtr small = IntPtr.Zero;
        try
        {
            var path = Catalog.Expand(target, AppContext.BaseDirectory);
            if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                var bitmap = new BitmapImage(new Uri(path));
                bitmap.Freeze();
                Icons[target] = bitmap;
                return bitmap;
            }
            var index = 0;
            var comma = path.LastIndexOf(',');
            if (comma > 0 && int.TryParse(path[(comma + 1)..], out index) && File.Exists(path[..comma]))
            {
                path = path[..comma];
            }
            if (File.Exists(path) && (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                ExtractIconEx(path, index, out icon, out small, 1);
            }
            if (icon == IntPtr.Zero)
            {
                SHGetFileInfo(path, 0x80, out var info, (uint)Marshal.SizeOf<FileInfo>(), 0x100 | (File.Exists(path) || Directory.Exists(path) ? 0u : 0x10u));
                icon = info.Icon;
            }
            if (icon == IntPtr.Zero)
            {
                Icons[target] = null;
                return null;
            }
            var image = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            Icons[target] = image;
            return image;
        }
        catch
        {
            Icons[target] = null;
            return null;
        }
        finally
        {
            if (icon != IntPtr.Zero)
            {
                DestroyIcon(icon);
            }
            if (small != IntPtr.Zero)
            {
                DestroyIcon(small);
            }
        }
    }
    internal static LaunchItem FromPath(string path)
    {
        var item = new LaunchItem { Name = Path.GetFileNameWithoutExtension(path), Target = path, IconPath = path };
        if (Directory.Exists(path))
        {
            item.Name = new DirectoryInfo(path).Name;
        }
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var type = Type.GetTypeFromProgID("WScript.Shell")!;
            dynamic shell = Activator.CreateInstance(type)!;
            dynamic link = shell.CreateShortcut(path);
            try
            {
                if (!string.IsNullOrWhiteSpace((string)link.TargetPath))
                {
                    item.Target = link.TargetPath;
                    item.Arguments = link.Arguments;
                    item.WorkingDirectory = link.WorkingDirectory;
                    item.IconPath = string.IsNullOrWhiteSpace((string)link.IconLocation) ? item.Target : link.IconLocation;
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(link);
                Marshal.FinalReleaseComObject(shell);
            }
        }
        else if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                {
                    item.Target = line[4..];
                }
            }
        }
        return item;
    }
    internal static void DesktopShortcut(LaunchItem item)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(type)!;
        var name = string.Join("_", item.Name.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), name + ".lnk");
        if (File.Exists(path))
        {
            throw new IOException("桌面已存在同名快捷方式，请先改名后再创建。");
        }
        dynamic link = shell.CreateShortcut(path);
        try
        {
            link.TargetPath = Catalog.Expand(item.Target, AppContext.BaseDirectory);
            link.Arguments = item.Arguments;
            link.WorkingDirectory = item.WorkingDirectory;
            link.IconLocation = item.IconPath;
            link.Save();
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
            Marshal.FinalReleaseComObject(shell);
        }
    }
    internal static void OpenFolder(LaunchItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Target))
        {
            throw new InvalidOperationException("启动项没有设置目标路径。");
        }
        var path = Catalog.Expand(item.Target, AppContext.BaseDirectory);
        const uint apartmentThreaded = 0x2;
        const uint disableOle1Dde = 0x4;
        Marshal.ThrowExceptionForHR(CoInitializeEx(IntPtr.Zero, apartmentThreaded | disableOle1Dde));
        try
        {
            DispatchOpenFolder(path, ExploreFolder, SelectFileInFolder);
        }
        finally
        {
            CoUninitialize();
        }
    }
    internal static void DispatchOpenFolder(string path, Action<string> exploreDirectory, Action<string> selectFile)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException("打开文件位置只支持本地或共享文件系统路径。");
        }
        if ((File.GetAttributes(path) & FileAttributes.Directory) != 0)
        {
            exploreDirectory(path);
        }
        else
        {
            selectFile(path);
        }
    }
    private static void ExploreFolder(string path)
    {
        const uint noShellErrorDialog = 0x400;
        const int showNormal = 1;
        var info = new ShellExecuteInfo { Size = (uint)Marshal.SizeOf<ShellExecuteInfo>(), Mask = noShellErrorDialog, Verb = "explore", File = path, Show = showNormal };
        if (!ShellExecuteExW(ref info))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }
    private static void SelectFileInFolder(string path)
    {
        var parentPath = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("未能找到目标文件的父目录。");
        var parent = IntPtr.Zero;
        var file = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(SHParseDisplayName(parentPath, IntPtr.Zero, out parent, 0, IntPtr.Zero));
            Marshal.ThrowExceptionForHR(SHParseDisplayName(path, IntPtr.Zero, out file, 0, IntPtr.Zero));
            // 最后一个标识相对于父目录，指针有效期与完整文件标识相同。
            var child = ILFindLastID(file);
            Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(parent, 1, ref child, 0));
        }
        finally
        {
            Marshal.FreeCoTaskMem(file);
            Marshal.FreeCoTaskMem(parent);
        }
    }
    internal static bool RegisterShortcut(IntPtr hwnd, string text)
    {
        UnregisterHotKey(hwnd, 1);
        uint mods = 0x4000;
        uint key = 0;
        foreach (var token in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (token.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= 2; break;
                case "alt": mods |= 1; break;
                case "shift": mods |= 4; break;
                case "win": mods |= 8; break;
                default:
                    if (Enum.TryParse<Forms.Keys>(token, true, out var value))
                    {
                        key = (uint)value;
                    }
                    break;
            }
        }
        return key != 0 && RegisterHotKey(hwnd, 1, mods, key);
    }
    internal static void SetAutoStart(bool enabled)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "RolanNext.lnk");
        if (!enabled)
        {
            if (File.Exists(path))
            {
                File.Move(path, path + ".disabled-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            }
            return;
        }
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link = shell.CreateShortcut(path);
        try
        {
            link.TargetPath = Environment.ProcessPath;
            link.Arguments = "--autostart";
            link.WorkingDirectory = AppContext.BaseDirectory;
            link.Save();
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
            Marshal.FinalReleaseComObject(shell);
        }
    }
}
