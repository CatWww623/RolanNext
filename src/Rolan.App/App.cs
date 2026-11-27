using System;
using System.IO;
using System.Threading;
using System.Windows;
using Rolan.Core;

namespace Rolan.App;

internal static class Program
{
    private static Mutex? single;
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--apply-hosts")
        {
            try
            {
                var pending = Path.GetFullPath(args[1]);
                if (Path.GetFileName(pending) != "hosts-pending.txt")
                {
                    throw new InvalidOperationException("无效的待保存文件。");
                }
                var hosts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
                File.WriteAllText(hosts, File.ReadAllText(pending), new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "hosts 保存失败");
            }
            return;
        }
        single = new Mutex(true, "Local\\RolanNext-Portable-v1", out var created);
        if (!created)
        {
            var existing = Native.FindWindow(null!, "Rolan Next");
            if (existing != IntPtr.Zero)
            {
                Native.PostMessage(existing, 0x8001, IntPtr.Zero, IntPtr.Zero);
            }
            return;
        }
        var directory = Path.Combine(AppContext.BaseDirectory, "Data");
        var dataFlag = Array.IndexOf(args, "--data");
        if (dataFlag >= 0 && dataFlag + 1 < args.Length)
        {
            directory = Path.GetFullPath(args[dataFlag + 1]);
        }
        var store = new DataStore(directory);
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        Themes.Install(app);
        app.DispatcherUnhandledException += (_, e) =>
        {
            store.Log(e.Exception, "界面异常");
            MessageBox.Show("操作未完成，错误已记录。\n" + e.Exception.Message, "Rolan Next");
            e.Handled = true;
        };
        AppData data;
        try
        {
            data = store.Load();
            if (data.Groups.Count == 0)
            {
                data.Groups.Add(new LaunchGroup());
            }
        }
        catch (Exception ex)
        {
            store.Log(ex, "读取配置");
            MessageBox.Show("配置读取失败，原文件已保留。请从 Data\\Backups 中恢复，或检查 launcher.json.previous。\n" + ex.Message, "Rolan Next");
            return;
        }
        var main = new MainWindow(store, data, Array.Exists(args, a => a == "--autostart"));
        app.Run(main);
        single.ReleaseMutex();
        single.Dispose();
    }
}
