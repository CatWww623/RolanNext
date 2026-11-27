using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Rolan.Core;

namespace Rolan.App;

internal sealed partial class MainWindow
{
    private void ImportData(bool legacy)
    {
        Guard(() =>
        {
            var dialog = new OpenFileDialog { Filter = legacy ? "Rolan 配置与备份|*.cfg;*.rcb|所有文件|*.*" : "新工具配置与备份|*.json" };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }
            var imported = legacy ? LegacyImporter.Import(dialog.FileName) : DataStore.Read(dialog.FileName);
            if (!Confirm($"导入 {imported.Groups.Count} 个分组、{imported.Groups.Sum(g => g.Items.Count)} 个启动项？当前新工具数据会先备份，再被替换。"))
            {
                return;
            }
            Store.Backup(Data);
            if (legacy)
            {
                imported.Settings = Data.Settings;
            }
            Data = imported;
            Save();
            ApplyAppearance();
            RefreshDesktopSettings();
        });
    }
    private void ExportData()
    {
        Guard(() =>
        {
            var dialog = new SaveFileDialog { Filter = "配置备份|*.json", FileName = "RolanNext-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") };
            if (dialog.ShowDialog(this) == true)
            {
                File.WriteAllText(dialog.FileName, System.Text.Json.JsonSerializer.Serialize(Data, DataStore.JsonOptions).Replace("\n", "\r\n") + "\r\n", new UTF8Encoding(false));
            }
        });
    }
    private void ConvertPaths(bool portable)
    {
        Guard(() =>
        {
            RequireUnlockedItems();
            if (!Confirm(portable ? "将文件路径转换为相对程序目录的便携路径？跨盘路径仍保留完整路径。" : "将便携路径与环境变量展开为当前电脑的完整路径？"))
            {
                return;
            }
            Store.Backup(Data);
            string Convert(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
                var expanded = Catalog.Expand(path, BaseDirectory);
                if (!Path.IsPathRooted(expanded))
                {
                    return path;
                }
                return portable && string.Equals(Path.GetPathRoot(expanded), Path.GetPathRoot(BaseDirectory), StringComparison.OrdinalIgnoreCase) ? "%rp%\\" + Path.GetRelativePath(BaseDirectory, expanded) : expanded;
            }
            foreach (var item in Data.Groups.SelectMany(g => g.Items))
            {
                item.Target = Convert(item.Target);
                item.IconPath = Convert(item.IconPath);
                item.WorkingDirectory = Convert(item.WorkingDirectory);
            }
            Save();
            Render();
        });
    }
    private void ShowNotes()
    {
        Guard(() => Dialogs.TextEditor(this, "备忘录", Data.Notes, text => { Data.Notes = text; Save(); }));
    }
    private void ShowHosts()
    {
        Guard(() =>
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
            Dialogs.TextEditor(this, "hosts 文件编辑 · 保存前自动备份", File.ReadAllText(path), text =>
            {
                var backup = Path.Combine(Store.DirectoryPath, "SystemBackups");
                Directory.CreateDirectory(backup);
                File.Copy(path, Path.Combine(backup, "hosts-" + DateTime.Now.ToString("yyyyMMddHHmmss")));
                var pending = Path.Combine(Store.DirectoryPath, "hosts-pending.txt");
                File.WriteAllText(pending, text.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false));
                Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--apply-hosts \"" + pending + "\"") { UseShellExecute = true, Verb = "runas" });
            });
        });
    }
    private static bool IsCommand(string query)
    {
        return query.StartsWith('>') || query.StartsWith('<') || query.StartsWith(':');
    }
    private void ShowCommandHelp()
    {
        Guard(() =>
        {
            MessageBox.Show(this, "搜索框输入命令后按回车：\n\n>命令  执行命令提示符命令\n<目标  系统运行目标\n:b关键词  百度搜索\n:g关键词  谷歌搜索\n:settings  设置\n:theme  超级调色盘\n:notes  备忘录\n:hosts  hosts 编辑\n:shutdown  自动关机\n:cancel  取消关机\n:reboot  重启（需确认）\n:logoff  注销（需确认）\n:lock  锁定电脑\n:screenoff  关闭显示器\n:drives  此电脑\n:controls  控制面板\n:recyclebin  回收站\n:emptyrecyclebin  清空回收站（系统确认）", "命令模式");
            search.Visibility = Visibility.Visible;
            search.Focus();
        });
    }
    private void ExecuteCommand(string query)
    {
        Guard(() =>
        {
            if (query.StartsWith('>'))
            {
                Process.Start(new ProcessStartInfo("cmd.exe", "/k " + query[1..]) { UseShellExecute = true });
            }
            else if (query.StartsWith('<'))
            {
                Process.Start(new ProcessStartInfo(query[1..].Trim()) { UseShellExecute = true });
            }
            else if (query.StartsWith(":b") && !query.StartsWith(":backup"))
            {
                Process.Start(new ProcessStartInfo("https://www.baidu.com/s?wd=" + Uri.EscapeDataString(query[2..].Trim())) { UseShellExecute = true });
            }
            else if (query.StartsWith(":g"))
            {
                Process.Start(new ProcessStartInfo("https://www.google.com/search?q=" + Uri.EscapeDataString(query[2..].Trim())) { UseShellExecute = true });
            }
            else
            {
                switch (query.ToLowerInvariant())
                {
                    case ":settings": new SettingsWindow(this).ShowDialog(); break;
                    case ":theme": new PaletteWindow(this).ShowDialog(); break;
                    case ":notes": ShowNotes(); break;
                    case ":hosts": ShowHosts(); break;
                    case ":shutdown": ScheduleShutdown(); break;
                    case ":cancel": CancelShutdown(); break;
                    case ":reboot": if (Confirm("确认重启？请先保存工作。")) { RunSystem("shutdown.exe", "/r /t 30"); } break;
                    case ":logoff": if (Confirm("确认注销？请先保存工作。")) { RunSystem("shutdown.exe", "/l"); } break;
                    case ":lock": LockWorkStation(); break;
                    case ":screenoff": Native.PostMessage(Native.GetShellWindow(), 0x112, (IntPtr)0xf170, (IntPtr)2); break;
                    case ":drives": Process.Start(new ProcessStartInfo("explorer.exe", "shell:MyComputerFolder") { UseShellExecute = true }); break;
                    case ":controls": Process.Start(new ProcessStartInfo("control.exe") { UseShellExecute = true }); break;
                    case ":recyclebin": Process.Start(new ProcessStartInfo("explorer.exe", "shell:RecycleBinFolder") { UseShellExecute = true }); break;
                    case ":emptyrecyclebin": SHEmptyRecycleBin(new System.Windows.Interop.WindowInteropHelper(this).Handle, null, 0); break;
                    default: Dialogs.Notice(this, "未识别的命令", "请在主菜单查看命令列表。"); break;
                }
            }
        });
    }
    private void ScheduleShutdown()
    {
        Guard(() =>
        {
            var value = Dialogs.Prompt(this, "自动关机", "多少分钟后关机？", "60");
            if (value == null)
            {
                return;
            }
            if (!int.TryParse(value, out var minutes) || minutes < 1 || minutes > 43200)
            {
                throw new InvalidOperationException("请输入 1 至 43200 分钟。");
            }
            if (Confirm($"将在 {DateTime.Now.AddMinutes(minutes):MM月dd日 HH:mm} 关机，确认安排？"))
            {
                RunSystem("shutdown.exe", "/s /t " + minutes * 60);
            }
        });
    }
    private void CancelShutdown()
    {
        Guard(() => RunSystem("shutdown.exe", "/a"));
    }
    private static void RunSystem(string executable, string arguments)
    {
        Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true });
    }
    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? root, uint flags);
}
