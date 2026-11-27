using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Rolan.App;

internal static class ShellMenu
{
    [ComImport, Guid("000214e6-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        void ParseDisplayName(IntPtr a, IntPtr b, [MarshalAs(UnmanagedType.LPWStr)] string c, IntPtr d, out IntPtr e, IntPtr f);
        void EnumObjects(IntPtr a, uint b, out IntPtr c);
        void BindToObject(IntPtr a, IntPtr b, ref Guid c, out IntPtr d);
        void BindToStorage(IntPtr a, IntPtr b, ref Guid c, out IntPtr d);
        void CompareIDs(IntPtr a, IntPtr b, IntPtr c);
        void CreateViewObject(IntPtr a, ref Guid b, out IntPtr c);
        void GetAttributesOf(uint a, IntPtr b, ref uint c);
        void GetUIObjectOf(IntPtr hwnd, uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] pidls, ref Guid iid, IntPtr reserved, out IntPtr result);
    }
    [ComImport, Guid("000214e4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags);
        void InvokeCommand(ref CommandInfo info);
        void GetCommandString(UIntPtr id, uint flags, IntPtr reserved, IntPtr name, uint count);
    }
    [ComImport, Guid("000214f4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu2
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags);
        void InvokeCommand(ref CommandInfo info);
        void GetCommandString(UIntPtr id, uint flags, IntPtr reserved, IntPtr name, uint count);
        [PreserveSig]
        int HandleMenuMsg(uint message, IntPtr wparam, IntPtr lparam);
    }
    [ComImport, Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu3
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags);
        void InvokeCommand(ref CommandInfo info);
        void GetCommandString(UIntPtr id, uint flags, IntPtr reserved, IntPtr name, uint count);
        [PreserveSig]
        int HandleMenuMsg(uint message, IntPtr wparam, IntPtr lparam);
        [PreserveSig]
        int HandleMenuMsg2(uint message, IntPtr wparam, IntPtr lparam, out IntPtr result);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct CommandInfo
    {
        public uint Size;
        public uint Mask;
        public IntPtr Hwnd;
        public IntPtr Verb;
        public IntPtr Parameters;
        public IntPtr Directory;
        public int Show;
        public uint Hotkey;
        public IntPtr Icon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHParseDisplayName(string name, IntPtr context, out IntPtr pidl, uint mask, out uint attributes);
    [DllImport("shell32.dll", PreserveSig = false)]
    private static extern void SHBindToParent(IntPtr pidl, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellFolder folder, out IntPtr child);
    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr reserved);
    internal static void Show(Window owner, string path)
    {
        IntPtr pidl = IntPtr.Zero;
        IntPtr menu = IntPtr.Zero;
        IShellFolder? folder = null;
        IContextMenu? context = null;
        try
        {
            SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out _);
            var folderIid = typeof(IShellFolder).GUID;
            SHBindToParent(pidl, ref folderIid, out folder, out var child);
            var menuIid = typeof(IContextMenu).GUID;
            var hwnd = new WindowInteropHelper(owner).Handle;
            folder.GetUIObjectOf(hwnd, 1, [child], ref menuIid, IntPtr.Zero, out var raw);
            try
            {
                context = (IContextMenu)Marshal.GetObjectForIUnknown(raw);
            }
            finally
            {
                Marshal.Release(raw);
            }
            menu = CreatePopupMenu();
            Marshal.ThrowExceptionForHR(context.QueryContextMenu(menu, 0, 1, 0x7fff, 0));
            Native.GetCursorPos(out var cursor);
            var source = HwndSource.FromHwnd(hwnd);
            var menu2 = context as IContextMenu2;
            var menu3 = context as IContextMenu3;
            IntPtr Forward(IntPtr window, int message, IntPtr wparam, IntPtr lparam, ref bool handled)
            {
                if (message is 0x117 or 0x2b or 0x2c or 0x120)
                {
                    if (menu3 != null && menu3.HandleMenuMsg2((uint)message, wparam, lparam, out var result) == 0)
                    {
                        handled = true;
                        return result;
                    }
                    if (menu2 != null && message != 0x120 && menu2.HandleMenuMsg((uint)message, wparam, lparam) == 0)
                    {
                        handled = true;
                    }
                }
                return IntPtr.Zero;
            }
            uint command;
            source?.AddHook(Forward);
            try
            {
                command = TrackPopupMenuEx(menu, 0x100 | 2, cursor.X, cursor.Y, hwnd, IntPtr.Zero);
            }
            finally
            {
                source?.RemoveHook(Forward);
            }
            if (command != 0)
            {
                var info = new CommandInfo { Size = (uint)Marshal.SizeOf<CommandInfo>(), Hwnd = hwnd, Verb = (IntPtr)(command - 1), Show = 1 };
                context.InvokeCommand(ref info);
            }
        }
        finally
        {
            if (menu != IntPtr.Zero)
            {
                DestroyMenu(menu);
            }
            if (context != null)
            {
                Marshal.FinalReleaseComObject(context);
            }
            if (folder != null)
            {
                Marshal.FinalReleaseComObject(folder);
            }
            if (pidl != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }
    }
}
