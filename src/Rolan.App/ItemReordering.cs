using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using Rolan.Core;

namespace Rolan.App;

internal sealed partial class MainWindow
{
    private bool dragOverRecorded;
    private readonly ItemDragGesture itemGesture = new();
    private bool ItemLayoutReady => tileButtons.All(b => b.IsArrangeValid && b.RenderSize.Width > 0 && b.RenderSize.Height > 0);
    internal static DragDropEffects DropEffect(IDataObject data, DragDropEffects allowed, bool itemsLocked, bool groupsLocked)
    {
        var desired = data.GetDataPresent("RolanNext.Item") ? itemsLocked ? DragDropEffects.None : DragDropEffects.Move : data.GetDataPresent("RolanNext.Group") ? groupsLocked ? DragDropEffects.None : DragDropEffects.Move : data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent(DataFormats.UnicodeText) || data.GetDataPresent(DataFormats.Text) ? DragDropEffects.Copy : DragDropEffects.None;
        return desired & allowed;
    }
    internal void TraceDrag(string stage, object details)
    {
        try
        {
            var record = JsonSerializer.Serialize(new { Time = DateTimeOffset.Now, Stage = stage, Details = details });
            File.AppendAllText(Path.Combine(Store.DirectoryPath, "drag-sort.log"), record + "\r\n", new UTF8Encoding(false));
        }
        catch (IOException)
        {
            // 诊断文件暂时被占用时，保持拖动操作可继续。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
    private void TraceDropPosition(System.Windows.DragEventArgs e)
    {
        var screen = Native.GetCursorPos(out var cursor) ? new Point(cursor.X, cursor.Y) : new Point(double.NaN, double.NaN);
        var nativePoint = handle != IntPtr.Zero && double.IsFinite(screen.X) ? tiles.PointFromScreen(screen) : new Point(double.NaN, double.NaN);
        TraceDrag("窗口预览松手", new { SourceId = e.Data.GetData("RolanNext.Item"), EventSource = e.OriginalSource?.GetType().Name, Allowed = e.AllowedEffects.ToString(), Effect = e.Effects.ToString(), ViewportPoint = e.GetPosition(itemScroll).ToString(), EventTilesPoint = e.GetPosition(tiles).ToString(), ScreenPoint = screen.ToString(), NativeTilesPoint = nativePoint.ToString(), ViewportSize = itemScroll.RenderSize.ToString(), TileBounds = tileButtons.Take(20).Select(b => new { Item = ((LaunchItem)b.Tag).Name, Bounds = new Rect(b.TranslatePoint(new Point(), tiles), b.RenderSize).ToString() }).ToArray() });
    }
    internal bool ReorderItemAt(string sourceId, Point point)
    {
        RequireUnlockedItems();
        if (!ItemLayoutReady)
        {
            TraceDrag("布局未就绪，取消本次排序", new { SourceId = sourceId, Point = point.ToString() });
            return false;
        }
        var target = ResolveItemDrop(sourceId, point);
        TraceDrag("目标已解析", new { SourceId = sourceId, Point = point.ToString(), target.GroupId, target.Index, Before = Data.Groups.First(g => g.Id == target.GroupId).Items.Select(i => i.Name).ToArray() });
        var moved = Catalog.Move(Data, sourceId, target.GroupId, target.Index);
        if (moved)
        {
            Save();
            Render();
            TraceDrag("排序已执行", new { After = Data.Groups.First(g => g.Id == target.GroupId).Items.Select(i => i.Name).ToArray() });
        }
        dragUntil = System.DateTime.UtcNow.AddMilliseconds(700);
        return moved;
    }
    internal (string GroupId, int Index) ResolveItemDrop(string sourceId, Point point)
    {
        // 使用可视图标的真实位置，而非事件来源；图标内容和相邻间隙均指向同一目标。
        foreach (var button in tileButtons)
        {
            var bounds = new Rect(button.TranslatePoint(new Point(), tiles), button.RenderSize);
            bounds.Inflate(5, 5);
            if (bounds.Contains(point) && button.Tag is LaunchItem target)
            {
                var group = Data.Groups.First(g => g.Items.Any(i => i.Id == target.Id));
                return (group.Id, Catalog.DropIndex(group, sourceId, target.Id));
            }
        }
        return (CurrentGroup.Id, CurrentGroup.Items.Count);
    }
}
