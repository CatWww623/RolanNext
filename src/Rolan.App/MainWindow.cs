using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using Rolan.Core;

namespace Rolan.App;

internal sealed partial class MainWindow : Window
{
    internal readonly DataStore Store;
    internal AppData Data;
    private readonly Border frame = new();
    private readonly DockPanel root = new();
    private readonly DockPanel content = new();
    private readonly TextBox search = new();
    private readonly Grid searchArea = new();
    private readonly TextBlock searchHint = new() { Text = "搜索名称、拼音或首字母    /    直接输入即可搜索", IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(35, 0, 32, 2), Opacity = .5 };
    private readonly TextBlock status = new();
    private readonly WrapPanel groups = new();
    private readonly ScrollViewer groupScroll = new();
    private readonly ScrollViewer itemScroll = new();
    private readonly WrapPanel tiles = new();
    private readonly List<Button> tileButtons = [];
    private readonly Dictionary<string, Button> groupButtons = [];
    private readonly HashSet<ContextMenu> openContextMenus = [];
    private bool suppressSearchRender;
    internal event Action<string>? SettingsChanged;
    private readonly List<LaunchItem> shownItems = [];
    private int busy;
    private bool quitting;
    private bool pinned;
    private DateTime dragUntil;
    private DateTime leaveAt = DateTime.MaxValue;
    private DateTime hoverAt = DateTime.MaxValue;
    private string hoveredGroup = "";
    private Point dragStart;
    private string? draggingItem;
    private string? draggingGroup;
    internal string BaseDirectory => AppContext.BaseDirectory;
    internal LaunchGroup CurrentGroup => Data.Groups.FirstOrDefault(g => g.Id == Data.Settings.SelectedGroupId) ?? Data.Groups[0];
    internal MainWindow(DataStore store, AppData data, bool autostart)
    {
        Store = store;
        Data = data;
        Title = "Rolan Next";
        FontFamily = new FontFamily("Microsoft YaHei UI");
        FontSize = 13;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.CanResize;
        MinWidth = 420;
        MinHeight = 320;
        Width = data.Settings.Width;
        Height = data.Settings.Height;
        Left = data.Settings.Left;
        Top = data.Settings.Top;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), CornerRadius = new CornerRadius(18), GlassFrameThickness = new Thickness(0) });
        frame.CornerRadius = new CornerRadius(18);
        frame.BorderThickness = new Thickness(1);
        frame.Child = root;
        frame.RenderTransform = panelSlide;
        Content = new Border { ClipToBounds = true, Child = frame };
        SizeChanged += (_, _) => ApplyPanelSlide(edgeMotion.Visibility);
        BuildLayout();
        ApplyAppearance();
        InitializeDesktop(autostart);
        Closing += (_, e) =>
        {
            if (!quitting && Data.Settings.CloseToTray)
            {
                e.Cancel = true;
                CollapsePanel();
                return;
            }
            SaveGeometry();
            Save();
            DisposeDesktop();
        };
        PreviewTextInput += (_, e) =>
        {
            if (Keyboard.FocusedElement != search && !string.IsNullOrWhiteSpace(e.Text) && !PanelInteractionActive)
            {
                searchArea.Visibility = Visibility.Visible;
                search.Visibility = Visibility.Visible;
                search.Focus();
                search.Text += e.Text;
                search.CaretIndex = search.Text.Length;
                e.Handled = true;
            }
        };
        PreviewKeyDown += OnWindowKey;
        MouseDoubleClick += (_, e) =>
        {
            if (Data.Settings.DoubleClickHide && e.OriginalSource is Panel)
            {
                CollapsePanel();
            }
        };
        AllowDrop = true;
        PreviewDrop += (_, e) =>
        {
            if (e.Data.GetDataPresent("RolanNext.Item"))
            {
                TraceDropPosition(e);
                e.Effects = DropEffect(e.Data, e.AllowedEffects, Data.Settings.LockItems, Data.Settings.LockGroups);
            }
            if (e.Data.GetDataPresent("RolanNext.Item") && new Rect(itemScroll.RenderSize).Contains(e.GetPosition(itemScroll)))
            {
                e.Handled = true;
                var point = e.GetPosition(tiles);
                var moved = false;
                Guard(() => moved = ReorderItemAt((string)e.Data.GetData("RolanNext.Item"), point));
                if (!moved)
                {
                    e.Effects = DragDropEffects.None;
                }
            }
        };
        PreviewDragOver += (_, e) =>
        {
            dragUntil = DateTime.UtcNow.AddSeconds(2);
            e.Effects = DropEffect(e.Data, e.AllowedEffects, Data.Settings.LockItems, Data.Settings.LockGroups);
            if (e.Data.GetDataPresent("RolanNext.Item") && !dragOverRecorded)
            {
                dragOverRecorded = true;
                TraceDrag("首次经过面板", new { Allowed = e.AllowedEffects.ToString(), Effect = e.Effects.ToString(), Point = e.GetPosition(tiles).ToString() });
            }
            e.Handled = true;
        };
        Drop += (_, e) => HandleDrop(e, CurrentGroup.Id, CurrentGroup.Items.Count);
    }
    private void BuildLayout()
    {
        var header = new Grid { Background = Brushes.Transparent, Height = 56, Margin = new Thickness(20, 6, 12, 0) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock { Text = "◈", FontSize = 24, Foreground = Brushes.DeepSkyBlue, Margin = new Thickness(0, 0, 10, 0) });
        title.Children.Add(new TextBlock { Text = "Rolan Next", FontSize = 19, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
        title.Children.Add(new TextBlock { Text = "  /  桌面启动台", Opacity = .55, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        header.Children.Add(title);
        header.PreviewMouseLeftButtonDown += (_, e) => StartPanelDrag(header, e);
        header.PreviewMouseMove += (_, _) => MovePanelDrag();
        header.PreviewMouseLeftButtonUp += (_, _) => EndPanelDrag();
        header.LostMouseCapture += (_, _) => EndPanelDrag();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var pin = SmallButton("固定", (_, _) => { pinned = !pinned; RenderStatus(); });
        pin.ToolTip = "临时固定面板，暂停自动隐藏";
        actions.Children.Add(pin);
        actions.Children.Add(SmallButton("☷", (_, _) => ShowMainMenu()));
        actions.Children.Add(SmallButton("—", (_, _) => CollapsePanel()));
        actions.Children.Add(SmallButton("×", (_, _) => Close()));
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        search.Margin = new Thickness(20, 10, 20, 12);
        search.Padding = new Thickness(14, 10, 14, 10);
        search.FontSize = 14;
        search.BorderThickness = new Thickness(1);
        search.ToolTip = "直接输入名称、拼音、首字母搜索；输入 >命令、<运行、:b关键词、:g关键词";
        search.TextChanged += (_, _) => { searchHint.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; if (!suppressSearchRender) { RenderItems(); } };
        searchArea.Children.Add(search);
        searchArea.Children.Add(searchHint);
        DockPanel.SetDock(searchArea, Dock.Top);
        root.Children.Add(searchArea);
        var footer = new DockPanel { Margin = new Thickness(20, 10, 20, 14) };
        status.Opacity = .65;
        status.FontSize = 11;
        status.VerticalAlignment = VerticalAlignment.Center;
        var add = SmallButton("＋ 添加", (_, _) => EditItem(null));
        DockPanel.SetDock(add, Dock.Right);
        footer.Children.Add(add);
        footer.Children.Add(status);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        groupScroll.Content = groups;
        groupScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        groupScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        groupScroll.Margin = new Thickness(12, 0, 12, 8);
        groupScroll.ContextMenu = GroupMenu(null);
        TrackMenu(groupScroll.ContextMenu);
        groupScroll.MouseDoubleClick += (_, e) =>
        {
            if (e.OriginalSource == groups)
            {
                NewGroup();
            }
        };
        content.Children.Add(groupScroll);
        itemScroll.Content = tiles;
        itemScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        itemScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        itemScroll.Margin = new Thickness(14, 0, 10, 0);
        itemScroll.ContextMenu = ItemsAreaMenu();
        TrackMenu(launchItemMenu);
        content.Children.Add(itemScroll);
        root.Children.Add(content);
        itemScroll.SizeChanged += (_, _) => UpdateTileWidths();
    }
    private Button SmallButton(string text, RoutedEventHandler click)
    {
        var button = new Button { Content = text, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(2), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        button.Click += click;
        return button;
    }
    internal void ApplyAppearance(bool rebuild = true)
    {
        var theme = Themes.Current(Data.Settings);
        Application.Current.Resources["TextColor"] = new SolidColorBrush(theme.Text);
        Application.Current.Resources["AccentColor"] = new SolidColorBrush(theme.Accent);
        Application.Current.Resources["AccentTextColor"] = new SolidColorBrush(theme.AccentText);
        Application.Current.Resources["WindowBackground"] = Themes.BackgroundBrush(theme);
        Application.Current.Resources["SurfaceColor"] = new SolidColorBrush(theme.Surface);
        Application.Current.Resources["InputSurface"] = new SolidColorBrush(Color.FromRgb(theme.Surface.R, theme.Surface.G, theme.Surface.B));
        Application.Current.Resources["PopupSurface"] = new SolidColorBrush(Color.FromRgb(theme.Surface.R, theme.Surface.G, theme.Surface.B));
        Color Blend(Color a, Color b, double amount) => Color.FromRgb((byte)(a.R * (1 - amount) + b.R * amount), (byte)(a.G * (1 - amount) + b.G * amount), (byte)(a.B * (1 - amount) + b.B * amount));
        Application.Current.Resources["HoverColor"] = new SolidColorBrush(Blend(theme.Background, theme.Accent, .18));
        Application.Current.Resources["ScrollThumb"] = new SolidColorBrush(Blend(theme.Background, theme.Text, .40));
        Application.Current.Resources["OutlineColor"] = new SolidColorBrush(theme.Accent) { Opacity = .35 };
        Background = Brushes.Transparent;
        Foreground = new SolidColorBrush(theme.Text);
        frame.Background = Themes.BackgroundBrush(theme);
        frame.BorderBrush = new SolidColorBrush(theme.Accent) { Opacity = .4 };
        search.Background = new SolidColorBrush(theme.Surface);
        search.Foreground = Foreground;
        search.CaretBrush = Foreground;
        search.BorderBrush = new SolidColorBrush(theme.Accent) { Opacity = .45 };
        searchArea.Visibility = Data.Settings.ShowSearch ? Visibility.Visible : Visibility.Collapsed;
        BeginAnimation(OpacityProperty, null);
        PreviewOpacity(Data.Settings.Opacity);
        Topmost = Data.Settings.AlwaysOnTop;
        ShowInTaskbar = Data.Settings.ShowTaskbar;
        ResizeMode = Data.Settings.LockSize ? ResizeMode.NoResize : ResizeMode.CanResize;
        itemScroll.VerticalScrollBarVisibility = Data.Settings.ShowScrollbar ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden;
        var position = Data.Settings.TabPosition;
        var side = position is "左侧" or "右侧";
        DockPanel.SetDock(groupScroll, position switch { "底部" => Dock.Bottom, "左侧" => Dock.Left, "右侧" => Dock.Right, _ => Dock.Top });
        groupScroll.Width = side ? 140 : double.NaN;
        groupScroll.MaxHeight = side ? double.PositiveInfinity : 155;
        groupScroll.Visibility = position == "不显示" ? Visibility.Collapsed : Visibility.Visible;
        groups.Orientation = side ? Orientation.Vertical : Orientation.Horizontal;
        if (rebuild)
        {
            Render();
        }
        else
        {
            UpdateGroupAppearance();
            foreach (var button in tileButtons)
            {
                button.Background = new SolidColorBrush(theme.Surface);
                button.Foreground = Foreground;
            }
        }
    }
    internal void Render()
    {
        var expected = Data.Groups.Select(g => g.Id).ToArray();
        var existing = groups.Children.OfType<Button>().Select(b => (string)b.Tag).ToArray();
        if (existing.SequenceEqual(expected))
        {
            UpdateGroupAppearance();
            RenderItems();
            return;
        }
        groups.Children.Clear();
        groupButtons.Clear();
        var theme = Themes.Current(Data.Settings);
        foreach (var group in Data.Groups)
        {
            var button = new Button { Content = group.Name, Tag = group.Id, Padding = new Thickness(13, 8, 13, 8), Margin = new Thickness(3), BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Foreground = group.Id == CurrentGroup.Id ? new SolidColorBrush(theme.AccentText) : Foreground, Background = group.Id == CurrentGroup.Id ? new SolidColorBrush(theme.Accent) : new SolidColorBrush(theme.Surface) };
            button.Click += (_, _) => SelectGroup(group.Id);
            button.MouseEnter += (_, _) => { hoveredGroup = group.Id; hoverAt = DateTime.UtcNow.AddMilliseconds(Data.Settings.HoverDelay); };
            button.MouseLeave += (_, _) => { hoveredGroup = ""; hoverAt = DateTime.MaxValue; };
            button.ContextMenu = GroupMenu(group);
            button.PreviewMouseLeftButtonDown += (_, e) => dragStart = e.GetPosition(this);
            button.PreviewMouseMove += (_, e) =>
            {
                var p = e.GetPosition(this);
                if (e.LeftButton == MouseButtonState.Pressed && !Data.Settings.LockGroups && (Math.Abs(p.X - dragStart.X) > 8 || Math.Abs(p.Y - dragStart.Y) > 8))
                {
                    draggingGroup = group.Id;
                    busy++;
                    try
                    {
                        DragDrop.DoDragDrop(button, new DataObject("RolanNext.Group", group.Id), DragDropEffects.Move);
                    }
                    finally
                    {
                        draggingGroup = null;
                        busy--;
                    }
                }
            };
            button.AllowDrop = true;
            button.Drop += (_, e) =>
            {
                if (e.Data.GetDataPresent("RolanNext.Group") && !Data.Settings.LockGroups)
                {
                    var moving = Data.Groups.FirstOrDefault(g => g.Id == (string)e.Data.GetData("RolanNext.Group"));
                    if (moving != null && moving != group)
                    {
                        var index = Data.Groups.IndexOf(group);
                        Data.Groups.Remove(moving);
                        Data.Groups.Insert(Math.Clamp(index, 0, Data.Groups.Count), moving);
                        Save();
                        Render();
                    }
                    e.Handled = true;
                }
                else
                {
                    HandleDrop(e, group.Id, group.Items.Count);
                }
            };
            TrackMenu(button.ContextMenu);
            groupButtons[group.Id] = button;
            groups.Children.Add(button);
        }
        RenderItems();
    }
    private void SelectGroup(string id)
    {
        if (CurrentGroup.Id == id)
        {
            return;
        }
        Data.Settings.SelectedGroupId = id;
        suppressSearchRender = true;
        search.Clear();
        suppressSearchRender = false;
        Save();
        UpdateGroupAppearance();
        RenderItems();
    }
    private void UpdateGroupAppearance()
    {
        var theme = Themes.Current(Data.Settings);
        foreach (var group in Data.Groups)
        {
            if (groupButtons.TryGetValue(group.Id, out var button))
            {
                button.Content = group.Name;
                button.Foreground = group.Id == CurrentGroup.Id ? new SolidColorBrush(theme.AccentText) : Foreground;
                button.Background = group.Id == CurrentGroup.Id ? new SolidColorBrush(theme.Accent) : new SolidColorBrush(theme.Surface);
            }
        }
    }
    private void LocateItemGroup(string itemId)
    {
        var group = Data.Groups.FirstOrDefault(candidate => candidate.Items.Any(item => item.Id == itemId));
        if (group == null)
        {
            throw new InvalidOperationException("该启动项已不存在，请重新搜索。");
        }

        hoveredGroup = "";
        hoverAt = DateTime.MaxValue;
        if (CurrentGroup.Id == group.Id)
        {
            search.Clear();
        }
        else
        {
            SelectGroup(group.Id);
        }

        var targetButton = tileButtons.FirstOrDefault(button => button.Tag is LaunchItem item && item.Id == itemId);
        if (targetButton == null)
        {
            return;
        }

        // 分组切换后先完成新卡片的布局，再把目标滚到可视范围内。
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (!tileButtons.Contains(targetButton))
            {
                return;
            }
            if (groupScroll.Visibility == Visibility.Visible && groupButtons.TryGetValue(group.Id, out var groupButton))
            {
                groupButton.BringIntoView();
            }
            targetButton.BringIntoView();
            var theme = Themes.Current(Data.Settings);
            var tint = PaletteColors.Mix(theme.Surface, theme.Accent, .58);
            var highlight = PaletteColors.EnsureContrast(tint, theme.Text, 4.5);
            var background = new SolidColorBrush(theme.Surface);
            var fade = new ColorAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
            fade.KeyFrames.Add(new DiscreteColorKeyFrame(highlight, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            fade.KeyFrames.Add(new DiscreteColorKeyFrame(highlight, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.7))));
            fade.KeyFrames.Add(new LinearColorKeyFrame(theme.Surface, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.3))));
            targetButton.Background = background;
            background.BeginAnimation(SolidColorBrush.ColorProperty, fade);
        }, DispatcherPriority.Loaded);
    }
    internal bool TargetUnavailable(LaunchItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Target))
        {
            return false;
        }
        try
        {
            var target = Catalog.Expand(item.Target, BaseDirectory);
            if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && !uri.IsFile)
            {
                return false;
            }
            return !File.Exists(target) && !Directory.Exists(target);
        }
        catch
        {
            return true;
        }
    }
    private void RenderItems()
    {
        if (Data.Groups.Count == 0)
        {
            Data.Groups.Add(new LaunchGroup());
        }
        tiles.Children.Clear();
        tileButtons.Clear();
        shownItems.Clear();
        var query = search.Text.Trim();
        var commands = query.StartsWith('>') || query.StartsWith('<') || query.StartsWith(":b") || query.StartsWith(":g");
        var items = string.IsNullOrEmpty(query) ? CurrentGroup.Items.AsEnumerable() : Data.Groups.SelectMany(g => g.Items).Where(i => Catalog.Matches(i, query));
        if (commands)
        {
            items = [];
        }
        if (Data.Settings.SortByCount)
        {
            items = items.OrderByDescending(i => i.Count);
        }
        var theme = Themes.Current(Data.Settings);
        foreach (var item in items)
        {
            shownItems.Add(item);
            var button = new Button { Height = Data.Settings.TextBelow ? Data.Settings.IconSize + 64 : Math.Max(54, Data.Settings.IconSize + 20), Padding = new Thickness(9), Margin = new Thickness(5), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(0), Background = new SolidColorBrush(theme.Surface), Foreground = Foreground, Cursor = Cursors.Hand, Tag = item };
            var panel = new StackPanel { Orientation = Data.Settings.TextBelow ? Orientation.Vertical : Orientation.Horizontal };
            var iconArea = new Grid { Width = Data.Settings.IconSize, Height = Data.Settings.IconSize, Margin = Data.Settings.TextBelow ? new Thickness(0, 2, 0, 9) : new Thickness(0, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Center };
            var icon = new Image { Source = Native.Icon(string.IsNullOrWhiteSpace(item.IconPath) ? item.Target : item.IconPath), Width = Data.Settings.IconSize, Height = Data.Settings.IconSize };
            if (icon.Source != null)
            {
                iconArea.Children.Add(icon);
            }
            else
            {
                iconArea.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(item.Target) ? "＋" : "◇", FontSize = Data.Settings.IconSize, HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(theme.Accent) });
            }
            if (TargetUnavailable(item))
            {
                iconArea.Children.Add(UnavailableIcon.Create(Data.Settings.IconSize));
            }
            panel.Children.Add(iconArea);
            panel.Children.Add(new TextBlock { Text = item.Name, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = Data.Settings.TextBelow ? TextWrapping.WrapWithOverflow : TextWrapping.NoWrap, MaxHeight = Data.Settings.TextBelow ? 34 : 20, TextAlignment = Data.Settings.TextBelow ? TextAlignment.Center : TextAlignment.Left, VerticalAlignment = VerticalAlignment.Center, MaxWidth = Data.Settings.TextBelow ? 180 : 600 });
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
            var tile = new Grid();
            tile.Children.Add(panel);
            button.Content = tile;
            if (Data.Settings.Tooltips)
            {
                var state = "";
                try
                {
                    var target = Catalog.Expand(item.Target, BaseDirectory);
                    if (Path.IsPathRooted(target) && !File.Exists(target) && !Directory.Exists(target))
                    {
                        state = "\n目标暂不可用，可右键编辑路径";
                    }
                }
                catch
                {
                    state = "\n路径格式需要检查";
                }
                button.ToolTip = $"{item.Name}\n{item.Target}\n启动次数：{item.Count}{state}";
            }
            button.Click += (_, _) =>
            {
                if (!Data.Settings.DoubleClick && draggingItem == null && !itemGesture.Consumed)
                {
                    Launch(item);
                }
            };
            button.MouseDoubleClick += (_, e) =>
            {
                if (Data.Settings.DoubleClick)
                {
                    Launch(item);
                    e.Handled = true;
                }
            };
            button.PreviewMouseLeftButtonDown += (_, e) =>
            {
                dragStart = e.GetPosition(this);
                itemGesture.Arm(item.Id);
                TraceDrag("新的鼠标按压", new { item.Id, item.Name });
            };
            button.PreviewMouseMove += (_, e) =>
            {
                var p = e.GetPosition(this);
                if (draggingItem == null && !Data.Settings.LockItems && (Math.Abs(p.X - dragStart.X) > 8 || Math.Abs(p.Y - dragStart.Y) > 8) && itemGesture.TryBegin(item.Id, e.LeftButton == MouseButtonState.Pressed, (Native.GetAsyncKeyState(1) & 0x8000) != 0))
                {
                    draggingItem = item.Id;
                    busy++;
                    try
                    {
                        button.ReleaseMouseCapture();
                        dragOverRecorded = false;
                        TraceDrag("开始拖动", new { item.Id, item.Name, Version = typeof(MainWindow).Assembly.GetName().Version!.ToString(3) });
                        var result = DragDrop.DoDragDrop(button, new DataObject("RolanNext.Item", item.Id), DragDropEffects.Move);
                        TraceDrag("拖动结束", new { item.Id, Result = result.ToString() });
                    }
                    finally
                    {
                        draggingItem = null;
                        busy--;
                    }
                    e.Handled = true;
                }
            };
            button.ContextMenu = launchItemMenu;
            button.ContextMenuOpening += (_, _) => PopulateItemMenu(item, query.Length > 0);
            button.AllowDrop = true;
            button.Drop += (_, e) =>
            {
                var group = Data.Groups.First(g => g.Items.Contains(item));
                var index = group == CurrentGroup && search.Text.Length == 0 ? group.Items.IndexOf(item) : group.Items.Count;
                HandleDrop(e, group.Id, index);
            };
            tiles.Children.Add(button);
            tileButtons.Add(button);
        }
        if (shownItems.Count == 0)
        {
            tiles.Children.Add(new TextBlock { Text = commands ? "按回车执行命令，Esc 退出" : query.Length > 0 ? "没有匹配项目，试试名称、拼音或首字母" : "将程序、文件或网址拖入这里\n也可以右键添加启动项", Margin = new Thickness(24, 50, 20, 40), Opacity = .6, FontSize = 15 });
        }
        UpdateTileWidths();
        RenderStatus();
    }
    private void UpdateTileWidths()
    {
        var available = Math.Max(300, itemScroll.ActualWidth - 30);
        var columns = Data.Settings.Layout switch { "单列" => 1, "双列" => 2, _ => Math.Max(1, (int)(available / (Data.Settings.TextBelow ? 114 : 210))) };
        foreach (var button in tileButtons)
        {
            button.Width = Math.Max(80, available / columns - 10);
        }
    }
    private void RenderStatus()
    {
        status.Text = $"{CurrentGroup.Name}  ·  {shownItems.Count} 项  /  共 {Data.Groups.Sum(g => g.Items.Count)} 项" + (pinned ? "  ·  已固定" : "");
    }
    private void OnWindowKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (search.Text.Length > 0)
            {
                search.Clear();
                search.Focus();
            }
            else
            {
                CollapsePanel();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            searchArea.Visibility = Visibility.Visible;
            search.Visibility = Visibility.Visible;
            search.Focus();
            search.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            var index = Data.Groups.IndexOf(CurrentGroup);
            var direction = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1;
            SelectGroup(Data.Groups[(index + direction + Data.Groups.Count) % Data.Groups.Count].Id);
            e.Handled = true;
        }
        else if (e.Key == Key.Down && Keyboard.FocusedElement == search && tileButtons.Count > 0)
        {
            tileButtons[0].Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Return && Keyboard.FocusedElement == search)
        {
            if (IsCommand(search.Text.Trim()))
            {
                ExecuteCommand(search.Text.Trim());
            }
            else if (shownItems.Count > 0)
            {
                Launch(shownItems[0]);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Return && Keyboard.FocusedElement is Button { Tag: LaunchItem item })
        {
            Launch(item);
            e.Handled = true;
        }
        else if (Keyboard.FocusedElement is Button { Tag: LaunchItem } button && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            var index = tileButtons.IndexOf(button);
            var columns = Math.Max(1, (int)(itemScroll.ActualWidth / (button.ActualWidth + 10)));
            var next = index + (e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : e.Key == Key.Up ? -columns : columns);
            tileButtons[Math.Clamp(next, 0, tileButtons.Count - 1)].Focus();
            e.Handled = true;
        }
    }
    internal void Save()
    {
        if (Data.Settings.LockConfig)
        {
            return;
        }
        try
        {
            Store.Save(Data);
        }
        catch (Exception ex)
        {
            Store.Log(ex, "保存配置");
            status.Text = "保存失败，请检查数据目录权限；当前数据仍在内存中";
        }
    }
    internal void Guard(Action action)
    {
        busy++;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Store.Log(ex, "用户操作");
            Dialogs.Notice(this, "操作未完成", ex.Message);
        }
        finally
        {
            busy--;
            leaveAt = DateTime.MaxValue;
        }
    }
    internal bool Confirm(string text)
    {
        return Dialogs.Question(this, "请确认", text);
    }
    internal bool PanelInteractionActive
    {
        get
        {
            if (busy > 0)
            {
                return true;
            }
            // 菜单宿主可能在切换分组时被重建。以菜单自身状态为准，不依赖开关事件恰好成对。
            openContextMenus.RemoveWhere(static menu => !menu.IsOpen);
            return openContextMenus.Count > 0;
        }
    }
    private void TrackMenu(ContextMenu menu)
    {
        menu.Opened += (_, _) => openContextMenus.Add(menu);
        menu.Closed += (_, _) =>
        {
            openContextMenus.Remove(menu);
            leaveAt = DateTime.MaxValue;
        };
    }
}
