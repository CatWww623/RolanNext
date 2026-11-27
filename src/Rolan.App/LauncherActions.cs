using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Rolan.Core;

namespace Rolan.App;

internal sealed partial class MainWindow
{
    private readonly Dictionary<string, Process> running = new(StringComparer.OrdinalIgnoreCase);
    private readonly ContextMenu launchItemMenu = new();
    private static MenuItem Menu(string label, Action action)
    {
        var entry = new MenuItem { Header = label };
        entry.Click += (_, _) => action();
        return entry;
    }
    private void PopulateItemMenu(LaunchItem item, bool isSearchResult)
    {
        var menu = launchItemMenu;
        menu.Items.Clear();
        if (isSearchResult)
        {
            menu.Items.Add(Menu("跳转到所在分组", () => Guard(() => LocateItemGroup(item.Id))));
            menu.Items.Add(new Separator());
        }
        menu.Items.Add(Menu("管理员权限运行(A)", () => Launch(item, true)));
        menu.Items.Add(Menu("最小化运行(N)", () => Launch(item, false, true)));
        menu.Items.Add(Menu("打开文件位置(F)", () => Guard(() => Native.OpenFolder(item))));
        menu.Items.Add(Menu("资源管理器菜单(X)", () => Guard(() => ShellMenu.Show(this, Catalog.Expand(item.Target, BaseDirectory)))));
        menu.Items.Add(new Separator());
        var move = new MenuItem { Header = "移动到" };
        foreach (var group in Data.Groups)
        {
            move.Items.Add(Menu(group.Name, () => Guard(() =>
            {
                RequireUnlockedItems();
                Catalog.Move(Data, item.Id, group.Id, group.Items.Count);
                Save();
                Render();
            })));
        }
        menu.Items.Add(move);
        menu.Items.Add(Menu("编辑(E)", () => EditItem(item)));
        menu.Items.Add(Menu("删除(D)", () => Guard(() =>
        {
            RequireUnlockedItems();
            if (Confirm($"从启动台删除「{item.Name}」？目标文件不会被删除。"))
            {
                Data.Groups.First(g => g.Items.Contains(item)).Items.Remove(item);
                Save();
                Render();
            }
        })));
        menu.Items.Add(new Separator());
        menu.Items.Add(Menu("替换为空项目", () => Guard(() =>
        {
            RequireUnlockedItems();
            if (Confirm("将此启动项替换为空项目？"))
            {
                item.Name = "空项目";
                item.Target = "";
                item.IconPath = "";
                item.Arguments = "";
                item.Count = 0;
                Save();
                Render();
            }
        })));
        menu.Items.Add(Menu("复制完整路径", () => Guard(() => Clipboard.SetText(Catalog.Expand(item.Target, BaseDirectory)))));
        menu.Items.Add(Menu("创建桌面快捷方式", () => Guard(() => Native.DesktopShortcut(item))));
        menu.Items.Add(new Separator());
        menu.Items.Add(Menu("删除全部项目", () => ClearItems(Data.Groups.First(g => g.Items.Contains(item)))));
    }
    private ContextMenu GroupMenu(LaunchGroup? group)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Menu("新建分组", NewGroup));
        if (group == null)
        {
            return menu;
        }
        menu.Items.Add(Menu("重命名分组", () => Guard(() =>
        {
            RequireUnlockedGroups();
            var name = Dialogs.Prompt(this, "重命名分组", "分组名称", group.Name);
            if (!string.IsNullOrWhiteSpace(name))
            {
                group.Name = name.Trim();
                Save();
                Render();
            }
        })));
        menu.Items.Add(Menu("删除分组", () => Guard(() =>
        {
            RequireUnlockedGroups();
            if (Data.Groups.Count == 1)
            {
                throw new InvalidOperationException("至少保留一个分组。");
            }
            var destination = Data.Groups.First(g => g != group);
            if (Confirm($"删除「{group.Name}」？组内项目将移至「{destination.Name}」。"))
            {
                destination.Items.AddRange(group.Items);
                Data.Groups.Remove(group);
                Data.Settings.SelectedGroupId = destination.Id;
                Save();
                Render();
            }
        })));
        menu.Items.Add(Menu("按名称排序", () => SortItems(group, false)));
        menu.Items.Add(Menu("按启动次数排序", () => SortItems(group, true)));
        menu.Items.Add(Menu("清空分组项目", () => ClearItems(group)));
        return menu;
    }
    private void SortItems(LaunchGroup group, bool count)
    {
        Guard(() =>
        {
            RequireUnlockedItems();
            group.Items = count ? group.Items.OrderByDescending(i => i.Count).ToList() : group.Items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            Save();
            Render();
        });
    }
    private ContextMenu ItemsAreaMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(Menu("添加启动项", () => EditItem(null)));
        menu.Items.Add(Menu("选择文件批量添加", ChooseFiles));
        menu.Items.Add(Menu("新建分组", NewGroup));
        menu.Items.Add(Menu("超级调色盘", () => Guard(() => new PaletteWindow(this).ShowDialog())));
        TrackMenu(menu);
        return menu;
    }
    private void ShowMainMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(Menu("功能与界面设置", () => Guard(() => new SettingsWindow(this).ShowDialog())));
        menu.Items.Add(Menu("超级调色盘", () => Guard(() => new PaletteWindow(this).ShowDialog())));
        menu.Items.Add(new Separator());
        menu.Items.Add(Menu("添加启动项", () => EditItem(null)));
        menu.Items.Add(Menu("批量添加文件", ChooseFiles));
        menu.Items.Add(Menu("新建分组", NewGroup));
        menu.Items.Add(new Separator());
        menu.Items.Add(Menu("导入 Rolan 配置 / 备份", () => ImportData(true)));
        menu.Items.Add(Menu("导入新配置 / 从备份恢复", () => ImportData(false)));
        menu.Items.Add(Menu("导出配置", ExportData));
        menu.Items.Add(Menu("立即备份", () => Guard(() => Dialogs.Notice(this, "备份完成", Store.Backup(Data)))));
        menu.Items.Add(Menu("打开数据目录", () => Guard(() => Process.Start(new ProcessStartInfo(Store.DirectoryPath) { UseShellExecute = true }))));
        menu.Items.Add(Menu("转换为便携路径", () => ConvertPaths(true)));
        menu.Items.Add(Menu("转换为完整路径", () => ConvertPaths(false)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Menu("备忘录", ShowNotes));
        menu.Items.Add(Menu("hosts 文件编辑", ShowHosts));
        menu.Items.Add(Menu("命令模式与命令列表", ShowCommandHelp));
        menu.Items.Add(Menu("自动关机计划", ScheduleShutdown));
        menu.Items.Add(Menu("取消关机计划", CancelShutdown));
        menu.Items.Add(new Separator());
        menu.Items.Add(Menu("退出", () => { quitting = true; Close(); }));
        TrackMenu(menu);
        menu.PlacementTarget = this;
        menu.IsOpen = true;
    }
    private void RequireUnlockedItems()
    {
        if (Data.Settings.LockItems)
        {
            throw new InvalidOperationException("启动项已锁定，请在设置中解锁。");
        }
    }
    private void RequireUnlockedGroups()
    {
        if (Data.Settings.LockGroups)
        {
            throw new InvalidOperationException("分组已锁定，请在设置中解锁。");
        }
    }
    private void ClearItems(LaunchGroup group)
    {
        Guard(() =>
        {
            RequireUnlockedItems();
            if (Confirm($"删除「{group.Name}」中的全部 {group.Items.Count} 个启动项？目标文件不会被删除。"))
            {
                group.Items.Clear();
                Save();
                Render();
            }
        });
    }
    private void NewGroup()
    {
        Guard(() =>
        {
            RequireUnlockedGroups();
            var name = Dialogs.Prompt(this, "新建分组", "分组名称", "新分组");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var group = new LaunchGroup { Name = name.Trim() };
                Data.Groups.Add(group);
                Data.Settings.SelectedGroupId = group.Id;
                Save();
                Render();
            }
        });
    }
    private void EditItem(LaunchItem? item)
    {
        Guard(() =>
        {
            RequireUnlockedItems();
            var groupId = item == null ? CurrentGroup.Id : Data.Groups.First(g => g.Items.Contains(item)).Id;
            var edited = Dialogs.EditItem(this, item);
            if (edited == null)
            {
                return;
            }
            if (item == null)
            {
                if (Catalog.IsDuplicate(Data, edited, BaseDirectory) && !Confirm("已存在相同目标和参数的启动项，仍然保留？"))
                {
                    return;
                }
                Data.Groups.First(g => g.Id == groupId).Items.Add(edited);
            }
            else
            {
                item.Name = edited.Name;
                item.Target = edited.Target;
                item.IconPath = edited.IconPath;
                item.Arguments = edited.Arguments;
                item.WorkingDirectory = edited.WorkingDirectory;
            }
            Save();
            Render();
        });
    }
    private void ChooseFiles()
    {
        Guard(() =>
        {
            var dialog = new OpenFileDialog { Multiselect = true, Filter = "所有文件|*.*" };
            var id = CurrentGroup.Id;
            if (dialog.ShowDialog(this) == true)
            {
                AddPaths(dialog.FileNames, id);
            }
        });
    }
    private static bool IsDropSupported(DragEventArgs e)
    {
        return e.Data.GetDataPresent("RolanNext.Item") || e.Data.GetDataPresent("RolanNext.Group") || e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.Text);
    }
    private void HandleDrop(DragEventArgs e, string groupId, int index)
    {
        e.Handled = true;
        e.Effects = DropEffect(e.Data, e.AllowedEffects, Data.Settings.LockItems, Data.Settings.LockGroups);
        if (e.Data.GetDataPresent("RolanNext.Item"))
        {
            TraceDrag("备用松手分支", new { SourceId = e.Data.GetData("RolanNext.Item"), GroupId = groupId, Index = index, Effect = e.Effects.ToString() });
        }
        Guard(() =>
        {
            RequireUnlockedItems();
            var group = Data.Groups.FirstOrDefault(g => g.Id == groupId) ?? throw new InvalidOperationException("目标分组已不存在，请重新拖入。");
            if (e.Data.GetDataPresent("RolanNext.Item"))
            {
                Catalog.Move(Data, (string)e.Data.GetData("RolanNext.Item"), groupId, index);
                Save();
                Render();
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                AddPaths((string[])e.Data.GetData(DataFormats.FileDrop), groupId);
            }
            else
            {
                var text = (e.Data.GetData(DataFormats.UnicodeText) as string ?? e.Data.GetData(DataFormats.Text) as string ?? "").Trim();
                var paths = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (paths.Any(p => !Uri.TryCreate(p, UriKind.Absolute, out _) && !File.Exists(p) && !Directory.Exists(p)))
                {
                    throw new InvalidOperationException("请拖入文件、文件夹或完整网址。");
                }
                AddPaths(paths, groupId);
            }
        });
        dragUntil = DateTime.UtcNow.AddMilliseconds(700);
    }
    private void AddPaths(IEnumerable<string> paths, string groupId)
    {
        RequireUnlockedItems();
        var group = Data.Groups.FirstOrDefault(g => g.Id == groupId) ?? throw new InvalidOperationException("目标分组已不存在。");
        bool? duplicateChoice = null;
        var errors = new List<string>();
        var added = 0;
        foreach (var path in paths)
        {
            try
            {
                var item = Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? new LaunchItem { Name = uri.Host, Target = path, IconPath = path } : Native.FromPath(path);
                if (Catalog.IsDuplicate(Data, item, BaseDirectory))
                {
                    var keep = duplicateChoice;
                    if (keep == null)
                    {
                        var choice = Dialogs.Duplicate(this, item.Name);
                        keep = choice.Keep;
                        if (choice.ApplyAll)
                        {
                            duplicateChoice = keep;
                        }
                    }
                    if (keep != true)
                    {
                        continue;
                    }
                }
                group.Items.Add(item);
                added++;
            }
            catch (Exception ex)
            {
                errors.Add(Path.GetFileName(path) + "：" + ex.Message);
                Store.Log(ex, "添加文件");
            }
        }
        Save();
        Render();
        status.Text = $"已添加 {added} 项";
        if (errors.Count > 0)
        {
            Dialogs.Notice(this, "部分项目未能添加", string.Join("\n", errors.Take(8)));
        }
    }
    private void Launch(LaunchItem item, bool? administrator = null, bool minimize = false)
    {
        Guard(() =>
        {
            if (string.IsNullOrWhiteSpace(item.Target))
            {
                EditItem(item);
                return;
            }
            var target = Catalog.Expand(item.Target, BaseDirectory);
            var key = target + "|" + item.Arguments;
            if (!Data.Settings.AllowMultiple && running.TryGetValue(key, out var previous) && !previous.HasExited)
            {
                status.Text = "此项目已通过启动台运行，多开已关闭";
                return;
            }
            if (Path.IsPathRooted(target) && !File.Exists(target) && !Directory.Exists(target))
            {
                throw new FileNotFoundException("目标暂不可用，请右键编辑路径。", target);
            }
            var directory = string.IsNullOrWhiteSpace(item.WorkingDirectory) ? (File.Exists(target) ? Path.GetDirectoryName(target)! : BaseDirectory) : Catalog.Expand(item.WorkingDirectory, BaseDirectory);
            var info = new ProcessStartInfo(target, item.Arguments) { UseShellExecute = true, WorkingDirectory = directory, Verb = (administrator ?? Data.Settings.RunAsAdmin) ? "runas" : "open", WindowStyle = minimize ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal };
            var process = Process.Start(info);
            if (process != null)
            {
                if (running.TryGetValue(key, out var old))
                {
                    old.Dispose();
                }
                running[key] = process;
            }
            item.Count++;
            Save();
            RenderStatus();
            if (Data.Settings.HideAfterLaunch)
            {
                CollapsePanel();
            }
        });
    }
}
