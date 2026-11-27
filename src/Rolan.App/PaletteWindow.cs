using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Rolan.Core;

namespace Rolan.App;

internal sealed class PaletteWindow : ThemedWindow
{
    private readonly MainWindow main;
    private readonly Border preview = new();
    private readonly TextBlock previewTitle = new();
    private readonly StackPanel cards = new();
    private readonly Dictionary<string, Button> themeCards = [];
    private readonly Dictionary<string, TextBlock> selectedMarks = [];
    private readonly Popup customPopup = new() { AllowsTransparency = true, StaysOpen = false, Placement = PlacementMode.Bottom, VerticalOffset = 8 };
    private readonly ColorPickerPanel picker = new();
    private readonly Border editorFrame = new();
    private readonly ComboBox colorMode = new() { ItemsSource = new[] { "主色配色 · 自动协调", "双色渐变 · 起点和终点", "三色渐变 · 增加中间色" }, Margin = new Thickness(0, 10, 0, 12) };
    private readonly StackPanel colorPoints = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBlock editorTitle = new() { FontSize = 15, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock modeHint = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Opacity = .7, Margin = new Thickness(0, 0, 0, 12) };
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(160) };
    private string savedTheme;
    private string savedLegacy;
    private Dictionary<string, CustomPalette> savedPalettes;
    private string editingCategory = "水墨国风";
    private CustomPalette editingPalette = new();
    private int editingPoint;
    private bool updatingEditor;
    private bool pending;
    internal PaletteWindow(MainWindow owner, bool attachOwner = true)
    {
        main = owner;
        savedTheme = main.Data.Settings.Theme;
        savedLegacy = main.Data.Settings.CustomColor;
        savedPalettes = CopyPalettes(main.Data.Settings.CustomPalettes);
        if (attachOwner)
        {
            Owner = owner;
        }
        Title = "超级调色盘";
        Width = 1080;
        Height = 880;
        MinWidth = 860;
        MinHeight = 560;
        FontFamily = owner.FontFamily;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(154) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var sidebar = new StackPanel { Margin = new Thickness(18, 24, 12, 16) };
        sidebar.Children.Add(new TextBlock { Text = "你的颜色", FontSize = 21, FontWeight = FontWeights.Bold });
        sidebar.Children.Add(new TextBlock { Text = "每次打开\n都有好心情", LineHeight = 23, Opacity = .6, Margin = new Thickness(0, 12, 0, 24) });
        grid.Children.Add(sidebar);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(8, 16, 22, 20) };
        Grid.SetColumn(scroll, 1);
        scroll.Content = cards;
        grid.Children.Add(scroll);
        preview.CornerRadius = new CornerRadius(16);
        preview.Height = 190;
        preview.BorderThickness = new Thickness(1);
        preview.SetResourceReference(Border.BorderBrushProperty, "OutlineColor");
        preview.Margin = new Thickness(0, 0, 0, 20);
        cards.Children.Add(preview);
        foreach (var category in Themes.All.GroupBy(t => t.Category))
        {
            var inner = new StackPanel { Margin = new Thickness(20, 18, 16, 10) };
            var heading = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            heading.Children.Add(new TextBlock { Text = category.Key, FontSize = 17, FontWeight = FontWeights.SemiBold });
            heading.Children.Add(new TextBlock { Text = CategoryDescription(category.Key), FontSize = 11, Opacity = .6, Margin = new Thickness(14, 4, 0, 0) });
            inner.Children.Add(heading);
            var row = new WrapPanel();
            foreach (var theme in category)
            {
                row.Children.Add(MakeCard(theme.Name, theme.Name, theme));
            }
            var customName = Themes.CustomName(category.Key);
            var customTheme = Themes.CustomTheme(category.Key, GetPalette(category.Key));
            row.Children.Add(MakeCard(customName, "自选颜色", customTheme, category.Key));
            inner.Children.Add(row);
            var section = new Border { CornerRadius = new CornerRadius(14), Child = inner, Margin = new Thickness(0, 0, 0, 16) };
            section.SetResourceReference(Border.BackgroundProperty, "SurfaceColor");
            cards.Children.Add(section);
            var link = new Button { Content = category.Key, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(10, 11, 10, 11), Margin = new Thickness(0, 2, 0, 2) };
            link.Click += (_, _) => section.BringIntoView();
            sidebar.Children.Add(link);
        }
        sidebar.Children.Add(new TextBlock { Text = "选择即生效\n自动保留你的配色", FontSize = 11, LineHeight = 21, Opacity = .55, Margin = new Thickness(10, 24, 0, 0) });
        BuildEditor();
        saveTimer.Tick += (_, _) => CommitPending();
        customPopup.Closed += (_, _) => CommitPending();
        Closing += (_, _) => { customPopup.IsOpen = false; CommitPending(); saveTimer.Stop(); };
        Content = grid;
        Refresh();
    }
    private static string CategoryDescription(string category) => category switch { "精品尝鲜" => "轻盈日常，熟悉的经典", "水墨国风" => "低饱和色调，温润如纸", "神桃美学" => "一点柔光，放松心情", "缤纷时刻" => "把喜欢的颜色揉在一起", "幻彩机核" => "鲜明碰撞，自有态度", _ => "安静的深色，陪伴专注时刻" };
    private static Dictionary<string, CustomPalette> CopyPalettes(Dictionary<string, CustomPalette> source) => source.ToDictionary(pair => pair.Key, pair => pair.Value.Copy());
    private CustomPalette GetPalette(string category)
    {
        var palette = main.Data.Settings.CustomPalettes.TryGetValue(category, out var saved) ? saved.Copy() : Themes.DefaultPalette(category, main.Data.Settings.Theme == "自选颜色" && category == "水墨国风" ? main.Data.Settings.CustomColor : null);
        while (palette.Colors.Count < 3)
        {
            palette.Colors.Add(Themes.DefaultPalette(category).Colors[palette.Colors.Count]);
        }
        return palette;
    }
    private Button MakeCard(string key, string label, Theme theme, string? customCategory = null)
    {
        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition());
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        var mark = new TextBlock { Text = "✓", FontSize = 13, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 1, 0), Visibility = Visibility.Hidden };
        var emblem = new Grid { Width = 40, Height = 40, Opacity = .3, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        for (var i = 0; i < 4; i++)
        {
            emblem.Children.Add(new Ellipse { Width = 21, Height = 21, Fill = new SolidColorBrush(theme.Accent), HorizontalAlignment = i % 2 == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right, VerticalAlignment = i < 2 ? VerticalAlignment.Top : VerticalAlignment.Bottom });
        }
        content.Children.Add(customCategory == null ? emblem : ColorBars(theme));
        content.Children.Add(mark);
        var name = new TextBlock { Text = label, FontSize = 12, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(name, 1);
        content.Children.Add(name);
        var button = new Button { Content = content, Width = 116, Height = 96, Margin = new Thickness(0, 0, 10, 8), Padding = new Thickness(7), BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent, Background = Themes.BackgroundBrush(theme), Foreground = new SolidColorBrush(theme.Text), ToolTip = customCategory == null ? label : "为“" + customCategory + "”选择自己的颜色" };
        if (customCategory == null)
        {
            button.Click += (_, _) => Select(key);
        }
        else
        {
            button.Click += (_, _) => OpenCustom(customCategory);
        }
        themeCards.Add(key, button);
        selectedMarks.Add(key, mark);
        return button;
    }
    private static FrameworkElement ColorBars(Theme theme)
    {
        var bars = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        foreach (var color in new[] { theme.Background, theme.Middle ?? PaletteColors.Mix(theme.Background, theme.Secondary, .5), theme.Secondary })
        {
            bars.Children.Add(new Border { Width = 18, Height = 38, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(color), Margin = new Thickness(1, 0, 1, 0), BorderBrush = new SolidColorBrush(theme.Text) { Opacity = .15 }, BorderThickness = new Thickness(1) });
        }
        return bars;
    }
    private void BuildEditor()
    {
        var editor = new StackPanel { Width = 326, Margin = new Thickness(16) };
        var titleRow = new DockPanel();
        var close = new Button { Content = "×", Width = 28, Height = 28, Padding = new Thickness(0), Margin = new Thickness(8, 0, 0, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), ToolTip = "关闭取色面板" };
        close.Click += (_, _) => customPopup.IsOpen = false;
        DockPanel.SetDock(close, Dock.Right);
        titleRow.Children.Add(close);
        titleRow.Children.Add(editorTitle);
        editor.Children.Add(titleRow);
        editor.Children.Add(colorMode);
        editor.Children.Add(colorPoints);
        editor.Children.Add(modeHint);
        picker.Padding = new Thickness(0);
        editor.Children.Add(picker);
        editorFrame.Child = editor;
        editorFrame.CornerRadius = new CornerRadius(14);
        editorFrame.BorderThickness = new Thickness(1);
        editorFrame.Margin = new Thickness(10);
        editorFrame.Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = .18 };
        editorFrame.SetResourceReference(Border.BackgroundProperty, "PopupSurface");
        editorFrame.SetResourceReference(Border.BorderBrushProperty, "OutlineColor");
        editorFrame.SetResourceReference(TextElement.ForegroundProperty, "TextColor");
        customPopup.Child = editorFrame;
        colorMode.SelectionChanged += (_, _) => { if (!updatingEditor && colorMode.SelectedIndex >= 0) { SetColorCount(colorMode.SelectedIndex + 1); } };
        picker.ColorChanged += color => { if (!updatingEditor) { SetColorPoint(editingPoint, PaletteColors.Hex(color)); } };
        picker.EyedropperRequested += () =>
        {
            customPopup.IsOpen = false;
            Dispatcher.BeginInvoke(() =>
            {
                if (!IsVisible)
                {
                    return;
                }
                try
                {
                    ScreenColorPicker.Pick(this, color =>
                    {
                        if (color.HasValue)
                        {
                            SetColorPoint(editingPoint, PaletteColors.Hex(color.Value));
                        }
                        Activate();
                        customPopup.IsOpen = true;
                    });
                }
                catch (Exception error)
                {
                    main.Store.Log(error, "屏幕取色");
                    Dialogs.Notice(this, "屏幕取色失败", error.Message);
                    customPopup.IsOpen = true;
                }
            }, DispatcherPriority.ApplicationIdle);
        };
    }
    internal void OpenCustom(string category, bool openPopup = true)
    {
        if (!Themes.Categories.Contains(category))
        {
            throw new ArgumentException("配色分类不存在。");
        }
        CommitPending();
        editingCategory = category;
        editingPalette = GetPalette(category);
        editingPoint = 0;
        UpdateEditor();
        QueueCustom();
        if (openPopup)
        {
            customPopup.PlacementTarget = themeCards[Themes.CustomName(category)];
            customPopup.IsOpen = true;
        }
    }
    internal void SetColorCount(int count)
    {
        if (count < 1 || count > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        editingPalette.ColorCount = count;
        editingPoint = Math.Min(editingPoint, count - 1);
        UpdateEditor();
        QueueCustom();
    }
    internal void SetColorPoint(int index, string value)
    {
        if (index < 0 || index >= editingPalette.ColorCount || !PaletteColors.TryHex(value, out var parsed))
        {
            throw new ArgumentException("颜色点或颜色值无效。");
        }
        editingPalette.Colors[index] = PaletteColors.Hex(parsed);
        UpdatePointButtons();
        if (picker.SelectedColor != parsed)
        {
            picker.SetColor(parsed);
        }
        QueueCustom();
    }
    private void UpdateEditor()
    {
        updatingEditor = true;
        try
        {
            editorTitle.Text = editingCategory + " · 自选颜色";
            colorMode.SelectedIndex = editingPalette.ColorCount - 1;
            modeHint.Text = editingPalette.ColorCount == 1 ? "选一个主色，为你生成本分类的协调渐变。" : "分别点选起点、中间或终点，再调整下面的颜色。背景明度会协调文字可读性。";
            UpdatePointButtons();
            PaletteColors.TryHex(editingPalette.Colors[editingPoint], out var color);
            picker.SetColor(color);
        }
        finally
        {
            updatingEditor = false;
        }
    }
    private void UpdatePointButtons()
    {
        colorPoints.Children.Clear();
        for (var i = 0; i < editingPalette.ColorCount; i++)
        {
            var index = i;
            PaletteColors.TryHex(editingPalette.Colors[i], out var color);
            var button = new Button { Content = editingPalette.ColorCount == 1 ? "主色" : i == 0 ? "起点" : i == editingPalette.ColorCount - 1 ? "终点" : "中间", Width = 96, Height = 34, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(6), Background = new SolidColorBrush(color), Foreground = new SolidColorBrush(PaletteColors.ReadableOn(color)), BorderThickness = new Thickness(2), BorderBrush = i == editingPoint ? new SolidColorBrush(PaletteColors.ReadableOn(color)) : Brushes.Transparent, ToolTip = editingPalette.Colors[i] };
            button.Click += (_, _) => { editingPoint = index; UpdateEditor(); };
            colorPoints.Children.Add(button);
        }
    }
    private void QueueCustom()
    {
        var palettes = CopyPalettes(main.Data.Settings.CustomPalettes);
        palettes[editingCategory] = editingPalette.Copy();
        main.Data.Settings.CustomPalettes = palettes;
        main.Data.Settings.CustomColor = editingPalette.Colors[0];
        main.Data.Settings.Theme = Themes.CustomName(editingCategory);
        main.ApplyAppearance(false);
        pending = true;
        Refresh();
        saveTimer.Stop();
        saveTimer.Start();
    }
    internal bool CommitPending(bool reportFailure = true)
    {
        saveTimer.Stop();
        if (!pending)
        {
            return true;
        }
        pending = false;
        try
        {
            if (!main.Data.Settings.LockConfig)
            {
                main.Store.Save(main.Data);
            }
            savedTheme = main.Data.Settings.Theme;
            savedLegacy = main.Data.Settings.CustomColor;
            savedPalettes = CopyPalettes(main.Data.Settings.CustomPalettes);
            return true;
        }
        catch (Exception error)
        {
            main.Data.Settings.Theme = savedTheme;
            main.Data.Settings.CustomColor = savedLegacy;
            main.Data.Settings.CustomPalettes = CopyPalettes(savedPalettes);
            editingPalette = GetPalette(editingCategory);
            editingPoint = Math.Min(editingPoint, editingPalette.ColorCount - 1);
            main.ApplyAppearance(false);
            UpdateEditor();
            Refresh();
            main.Store.Log(error, "保存自选配色");
            if (reportFailure)
            {
                Dialogs.Notice(this, "配色保存失败", "已恢复上次有效配色。\n" + error.Message);
            }
            return false;
        }
    }
    private void Select(string name)
    {
        if (!CommitPending())
        {
            return;
        }
        try
        {
            main.ApplySetting(nameof(Preferences.Theme), name);
            savedTheme = main.Data.Settings.Theme;
            Refresh();
        }
        catch (Exception error)
        {
            main.Store.Log(error, "选择主题");
            Dialogs.Notice(this, "主题设置失败", error.Message);
        }
    }
    private void Refresh()
    {
        var theme = Themes.Current(main.Data.Settings);
        // 弹层关闭时不在窗口可视树中，显式更新其资源域，避免再次展开保留旧主题。
        var popupResources = new ResourceDictionary();
        foreach (var key in new[] { "TextColor", "AccentColor", "AccentTextColor", "WindowBackground", "SurfaceColor", "InputSurface", "OutlineColor", "PopupSurface", "HoverColor", "ScrollThumb" })
        {
            popupResources[key] = Application.Current.FindResource(key);
        }
        editorFrame.Resources = popupResources;
        preview.Background = Themes.BackgroundBrush(theme);
        preview.Child = BuildPreview(theme);
        foreach (var pair in themeCards)
        {
            var selected = pair.Key == main.Data.Settings.Theme || pair.Key == Themes.CustomName("水墨国风") && main.Data.Settings.Theme == "自选颜色";
            pair.Value.BorderBrush = selected ? new SolidColorBrush(theme.Accent) : Brushes.Transparent;
            selectedMarks[pair.Key].Visibility = selected ? Visibility.Visible : Visibility.Hidden;
            if (pair.Key.StartsWith("自选颜色 · ", StringComparison.Ordinal))
            {
                var custom = Themes.CustomTheme(pair.Key[7..], GetPalette(pair.Key[7..]));
                pair.Value.Background = Themes.BackgroundBrush(custom);
                pair.Value.Foreground = new SolidColorBrush(custom.Text);
                var content = (Grid)pair.Value.Content;
                content.Children.RemoveAt(0);
                content.Children.Insert(0, ColorBars(custom));
            }
        }
    }
    private Grid BuildPreview(Theme theme)
    {
        var grid = new Grid { Margin = new Thickness(22) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(248) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var miniature = new StackPanel { Margin = new Thickness(0, 0, 28, 0) };
        var search = new Border { Height = 24, Background = new SolidColorBrush(theme.Surface), CornerRadius = new CornerRadius(6), Padding = new Thickness(9, 4, 9, 4), Child = new TextBlock { Text = "搜索你的启动项", FontSize = 10, Foreground = new SolidColorBrush(theme.Text), Opacity = .65 } };
        miniature.Children.Add(search);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 9, 0, 10) };
        tabs.Children.Add(new Border { Background = new SolidColorBrush(theme.Accent), CornerRadius = new CornerRadius(5), Padding = new Thickness(7, 3, 7, 3), Child = new TextBlock { Text = "常用", FontSize = 10, Foreground = new SolidColorBrush(theme.AccentText) } });
        tabs.Children.Add(new TextBlock { Text = "  工具    文件", FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(theme.Text) });
        miniature.Children.Add(tabs);
        var items = new UniformGrid { Columns = 3 };
        foreach (var symbol in new[] { "◇", "□", "○" })
        {
            var tile = new StackPanel();
            tile.Children.Add(new TextBlock { Text = symbol, FontSize = 23, HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(theme.Accent) });
            tile.Children.Add(new Border { Height = 4, Width = 24, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(theme.Text), Opacity = .35, Margin = new Thickness(0, 5, 0, 0) });
            items.Children.Add(new Border { Background = new SolidColorBrush(theme.Surface), CornerRadius = new CornerRadius(7), Padding = new Thickness(5, 8, 5, 8), Margin = new Thickness(0, 0, 7, 0), Child = tile });
        }
        miniature.Children.Add(items);
        grid.Children.Add(miniature);
        var caption = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        previewTitle.Text = theme.Name;
        previewTitle.FontSize = 22;
        previewTitle.FontWeight = FontWeights.SemiBold;
        previewTitle.TextWrapping = TextWrapping.Wrap;
        previewTitle.Foreground = new SolidColorBrush(theme.Text);
        // 同一个标题控件会移入新的预览树，先解除旧父级。
        if (previewTitle.Parent is Panel oldParent)
        {
            oldParent.Children.Remove(previewTitle);
        }
        caption.Children.Add(previewTitle);
        caption.Children.Add(new TextBlock { Text = "把喜欢的颜色，放进每天打开的桌面。", FontSize = 12, Opacity = .7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), Foreground = new SolidColorBrush(theme.Text) });
        Grid.SetColumn(caption, 1);
        grid.Children.Add(caption);
        return grid;
    }
}
