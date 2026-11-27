using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Rolan.App;
using Rolan.Core;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var root = Path.GetFullPath(args[0]);
        var directory = Path.Combine(Path.GetTempPath(), "RolanNext.WindowsTests", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var count = 0;
        void Check(bool ok, string label)
        {
            if (!ok)
            {
                throw new Exception("验证失败：" + label);
            }
            Console.WriteLine("通过：" + label);
            count++;
        }
        var target = Path.Combine(root, "src", "Rolan.App", "bin", "Release", "net10.0-windows", "RolanNext.exe");
        var linkPath = Path.Combine(directory, "启动台测试.lnk");
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link = shell.CreateShortcut(linkPath);
        link.TargetPath = target;
        link.Arguments = "--example \"中文参数\"";
        link.WorkingDirectory = directory;
        link.IconLocation = target + ",0";
        link.Save();
        Marshal.FinalReleaseComObject(link);
        Marshal.FinalReleaseComObject(shell);
        var item = Native.FromPath(linkPath);
        Check(item.Target == target, "系统快捷方式目标解析");
        Check(item.Arguments == "--example \"中文参数\"", "系统快捷方式参数保留");
        Check(item.WorkingDirectory == directory, "系统快捷方式工作目录保留");
        Check(item.IconPath.StartsWith(target, StringComparison.OrdinalIgnoreCase), "系统快捷方式自定义图标保留");
        var urlPath = Path.Combine(directory, "网址测试.url");
        File.WriteAllText(urlPath, "[InternetShortcut]\r\nURL=https://example.com/?a=1\r\n", new System.Text.UTF8Encoding(false));
        Check(Native.FromPath(urlPath).Target == "https://example.com/?a=1", "网址快捷方式解析");
        Check(Native.FromPath(directory).Name == new DirectoryInfo(directory).Name, "文件夹名称完整保留");
        var locationCalls = new List<(string Action, string Path)>();
        void ExploreTestDirectory(string path) => locationCalls.Add(("浏览", path));
        void SelectTestFile(string path) => locationCalls.Add(("选中", path));
        var locationFolder = Directory.CreateDirectory(Path.Combine(directory, "中文 目录")).FullName;
        var locationFile = Path.Combine(locationFolder, "中文 文件.txt");
        File.WriteAllBytes(locationFile, []);
        Native.DispatchOpenFolder(locationFolder, ExploreTestDirectory, SelectTestFile);
        Native.DispatchOpenFolder(locationFile, ExploreTestDirectory, SelectTestFile);
        Check(locationCalls.SequenceEqual(new[] { ("浏览", locationFolder), ("选中", locationFile) }), "目录只请求浏览，文件保留完整路径请求选中，中文和空格路径完整传递");
        try
        {
            Native.DispatchOpenFolder(Path.Combine(locationFolder, "未找到.txt"), ExploreTestDirectory, SelectTestFile);
            throw new Exception("不存在的文件应报告错误");
        }
        catch (FileNotFoundException)
        {
            Check(locationCalls.Count == 2, "不存在的文件不会向系统发送浏览或选中请求");
        }
        Check(Native.Icon(target) != null, "新工具64位程序图标可读取");
        Check(Native.Icon(Path.Combine(directory, "not-found.exe")) != null, "失效路径使用系统备用图标");
        foreach (var theme in Themes.All)
        {
            Check(Themes.Find(theme.Name, "#FFFFFF").Name == theme.Name, "内置主题可解析：" + theme.Name);
        }
        Check(Themes.Find("自选颜色", "#CCEEDD").Name == "自选颜色", "自选颜色可解析");
        Check(Themes.Find("自选颜色", "invalid").Name == Themes.All[0].Name, "损坏的自选颜色可回退");
        var app = new Application();
        Themes.Install(app);
        Check(app.Resources.MergedDictionaries.Count == 1, "界面样式字典可加载");
        var catalog = LegacyImporter.Import(LegacyTestFixture.Write(directory));
        var catalogItemCount = catalog.Groups.Sum(g => g.Items.Count);
        var main = new MainWindow(new DataStore(Path.Combine(directory, "Data")), catalog, false);
        Check(main.Content is System.Windows.Controls.Border, "主窗口控件树可构造（未显示窗口）");
        foreach (var theme in Themes.All)
        {
            catalog.Settings.Theme = theme.Name;
            main.ApplyAppearance();
        }
        Check(catalog.Groups.Sum(g => g.Items.Count) == catalogItemCount, "切换全部主题不改变启动项数据");
        catalog.Settings.TabPosition = "左侧";
        main.ApplyAppearance();
        catalog.Settings.Layout = "单列";
        main.ApplyAppearance();
        Check(main.Content != null, "侧边分组与单列布局可构造");
        var groupField = typeof(MainWindow).GetField("groups", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var groupPanel = (System.Windows.Controls.WrapPanel)groupField.GetValue(main)!;
        var groupControls = groupPanel.Children.Cast<System.Windows.UIElement>().ToArray();
        var select = typeof(MainWindow).GetMethod("SelectGroup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        foreach (var group in catalog.Groups)
        {
            select.Invoke(main, [group.Id]);
        }
        Check(groupControls.SequenceEqual(groupPanel.Children.Cast<System.Windows.UIElement>()), "连续切换全部分组保留原分组控件");
        var tileField = typeof(MainWindow).GetField("tiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var oldTiles = ((System.Windows.Controls.WrapPanel)tileField.GetValue(main)!).Children.Cast<System.Windows.UIElement>().ToArray();
        main.ApplySetting(nameof(Preferences.Opacity), .8);
        Check(main.Opacity == 1 && main.PanelBackgroundOpacity == .8 && main.Store.Load().Settings.Opacity == .8, "不透明度即时应用并自动保存");
        Check(oldTiles.SequenceEqual(((System.Windows.Controls.WrapPanel)tileField.GetValue(main)!).Children.Cast<System.Windows.UIElement>()), "不透明度调整不重建项目控件");
        using (var blocker = new FileStream(main.Store.FilePath + ".writing", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            var failed = false;
            try
            {
                main.ApplySetting(nameof(Preferences.Opacity), .9);
            }
            catch (IOException)
            {
                failed = true;
            }
            Check(failed && main.Opacity == 1 && main.PanelBackgroundOpacity == .8 && catalog.Settings.Opacity == .8, "保存失败时回退不透明度及设置值");
        }
        main.ApplySetting(nameof(Preferences.Opacity), 1.0);
        Check(main.TargetUnavailable(new LaunchItem { Target = Path.Combine(directory, "missing.exe") }), "不存在的本地目标显示不可用状态");
        Check(!main.TargetUnavailable(new LaunchItem { Target = "https://example.com/" }), "网址不误判为失效文件");
        var settings = new SettingsWindow(main, false);
        Check(settings.Content != null && settings is ThemedWindow, "即时设置窗口使用统一主题框架");
        Check(main.AllowsTransparency && main.Background == System.Windows.Media.Brushes.Transparent && main.Opacity == 1, "透明宿主启用，文字与图标不跟随背景变淡");
        var makeButton = typeof(Dialogs).GetMethod("ButtonFor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        double Luminance(System.Windows.Media.Color color)
        {
            double Linear(byte n)
            {
                var c = n / 255.0;
                return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4);
            }
            return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        }
        foreach (var theme in Themes.All)
        {
            catalog.Settings.Theme = theme.Name;
            main.ApplyAppearance();
            foreach (var label in new[] { "选择", "取消", "保存" })
            {
                var button = (System.Windows.Controls.Button)makeButton.Invoke(null, [label, (Action)(() => { })])!;
                button.ApplyTemplate();
                var text = Luminance(((System.Windows.Media.SolidColorBrush)button.Foreground).Color);
                var surface = Luminance(((System.Windows.Media.SolidColorBrush)button.Background).Color);
                if ((Math.Max(text, surface) + .05) / (Math.Min(text, surface) + .05) < 4.5)
                {
                    throw new Exception(theme.Name + "中的" + label + "按钮对比度不足");
                }
            }
        }
        Check(true, "全部预设主题的选择、取消、保存按钮文字对比度均达到4.5:1");
        var previews = Path.Combine(root, "artifacts", "previews");
        Directory.CreateDirectory(previews);
        catalog.Settings.TabPosition = "顶部";
        catalog.Settings.Layout = "多列";
        catalog.Settings.SelectedGroupId = catalog.Groups[0].Id;
        void RenderPreview(System.Windows.FrameworkElement visual, int width, int height, string path)
        {
            visual.Measure(new Size(width, height));
            visual.Arrange(new Rect(0, 0, width, height));
            visual.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
        foreach (var name in new[] { "极光青", "不再焦绿" })
        {
            catalog.Settings.Theme = name;
            main.ApplyAppearance();
            RenderPreview((System.Windows.FrameworkElement)main.Content!, 760, 650, Path.Combine(previews, name + ".png"));
        }
        Check(File.Exists(Path.Combine(previews, "极光青.png")), "实际控件树可离屏渲染为预览图");
        var viewport = (System.Windows.Controls.Border)main.Content!;
        var frame = (System.Windows.Controls.Border)viewport.Child;
        var transform = (System.Windows.Media.TranslateTransform)frame.RenderTransform;
        var originalSize = new Size(main.Width, main.Height);
        var originalOpacity = main.PanelBackgroundOpacity;
        catalog.Settings.Edge = "右侧";
        main.ApplyPanelSlide(.5);
        Check(viewport.ClipToBounds && transform.X == 380 && transform.Y == 0, "滑动限于原宿主范围，不移动窗口到相邻显示器");
        Check(main.Width == originalSize.Width && main.Height == originalSize.Height && main.PanelBackgroundOpacity == originalOpacity && main.Opacity == 1, "动画保持尺寸、背景不透明度及文字图标不透明度");
        RenderPreview(viewport, 760, 650, Path.Combine(previews, "边缘动画-半展开.png"));
        var halfImage = new System.Windows.Media.Imaging.BitmapImage(new Uri(Path.Combine(previews, "边缘动画-半展开.png")));
        var pixels = new byte[760 * 650 * 4];
        halfImage.CopyPixels(pixels, 760 * 4, 0);
        Check(pixels[(325 * 760 + 100) * 4 + 3] == 0 && pixels[(325 * 760 + 600) * 4 + 3] > 0, "半展开帧的滑出区域真实透明，面板区域仍可见");
        foreach (var edge in new[] { "左侧", "右侧", "顶部", "底部" })
        {
            catalog.Settings.Edge = edge;
            main.ApplyPanelSlide(0);
            RenderPreview(viewport, 760, 650, Path.Combine(directory, edge + "-隐藏.png"));
            var hiddenImage = new System.Windows.Media.Imaging.BitmapImage(new Uri(Path.Combine(directory, edge + "-隐藏.png")));
            hiddenImage.CopyPixels(pixels, 760 * 4, 0);
            Check(Enumerable.Range(0, 760 * 650).All(i => pixels[i * 4 + 3] == 0), edge + "滑出终点整帧透明，无残留或越界显示");
        }
        main.ApplyPanelSlide(1);
        Check(transform.X == 0 && transform.Y == 0, "展开终点恢复原布局且不残留位移");
        foreach (var size in new[] { 16, 24, 32, 48, 64 })
        {
            var cross = UnavailableIcon.Create(size);
            Check(cross.Width == size && cross.Height == size && !cross.IsHitTestVisible && cross.Source is System.Windows.Media.DrawingImage, size + "像素的全图标黄色叉为矢量且不拦截操作");
        }
        var repairedPath = Path.Combine(directory, "修复路径示例.txt");
        var overlayGroup = new LaunchGroup { Items = [new LaunchItem { Name = "目标不可用", Target = repairedPath }, new LaunchItem { Name = "目标可用", Target = target }] };
        var overlayData = new AppData { Groups = [overlayGroup] };
        overlayData.Settings.SelectedGroupId = overlayGroup.Id;
        overlayData.Settings.IconSize = 64;
        var overlayMain = new MainWindow(new DataStore(Path.Combine(directory, "OverlayData")), overlayData, false);
        System.Windows.Controls.Grid IconArea(int index)
        {
            var buttons = (System.Windows.Controls.WrapPanel)tileField.GetValue(overlayMain)!;
            var tile = (System.Windows.Controls.Grid)((System.Windows.Controls.Button)buttons.Children[index]).Content;
            return (System.Windows.Controls.Grid)((System.Windows.Controls.StackPanel)tile.Children[0]).Children[0];
        }
        Check(IconArea(0).Children.Count == 2 && IconArea(1).Children.Count == 1 && IconArea(0).Width == 64, "失效标记仅覆盖图标区域，有效项不覆盖，名称独立显示");
        overlayData.Settings.Theme = "极光青";
        overlayMain.ApplyAppearance();
        RenderPreview((System.Windows.FrameworkElement)overlayMain.Content!, 760, 650, Path.Combine(previews, "全图标黄色叉-深色.png"));
        overlayData.Settings.Theme = "不再焦绿";
        overlayMain.ApplyAppearance();
        RenderPreview((System.Windows.FrameworkElement)overlayMain.Content!, 760, 650, Path.Combine(previews, "全图标黄色叉-浅色.png"));
        File.WriteAllText(repairedPath, "用于验证修正目标后黄色叉消失。\r\n", new System.Text.UTF8Encoding(false));
        overlayMain.ApplyAppearance();
        Check(IconArea(0).Children.Count == 1, "目标恢复可用后重新显示分组会移除黄色叉");
        var palette = new PaletteWindow(main, false);
        RenderPreview((System.Windows.FrameworkElement)((Window)palette).Content!, 1080, 1400, Path.Combine(previews, "超级调色盘.png"));
        Check(File.Exists(Path.Combine(previews, "超级调色盘.png")), "超级调色盘控件可离屏渲染");
        Check(Themes.All.Length == 24 && Themes.Categories.Length == 6, "截图21套预设加3套深色全部保留，分类顺序完整");
        Check(new[] { "香芋啵啵", "海盐粉荔", "橘子汽水", "绿豆冰沙", "霓虹灯光", "粉墨都市", "废土朋克", "魔幻丛林" }.All(name => Themes.All.Any(t => t.Name == name)), "截图中缺少的8套主题全部补齐");
        Check(PaletteColors.FromHsv(0, 1, 1) == System.Windows.Media.Colors.Red && PaletteColors.FromHsv(120, 1, 1) == System.Windows.Media.Colors.Lime && PaletteColors.FromHsv(240, 1, 1) == System.Windows.Media.Colors.Blue, "色相区域的红绿蓝基准颜色正确");
        Check(PaletteColors.FromHsv(360, 1, 1) == System.Windows.Media.Colors.Red && PaletteColors.FromHsv(99, 0, 0) == System.Windows.Media.Colors.Black && PaletteColors.FromHsv(99, 0, 1) == System.Windows.Media.Colors.White, "色相循环及纯黑纯白边界正确");
        var colorRandom = new Random(1330);
        var roundtrip = true;
        for (var i = 0; i < 1000; i++)
        {
            var color = System.Windows.Media.Color.FromRgb((byte)colorRandom.Next(256), (byte)colorRandom.Next(256), (byte)colorRandom.Next(256));
            var hsv = PaletteColors.ToHsv(color);
            var restored = PaletteColors.FromHsv(hsv.Hue, hsv.Saturation, hsv.Value);
            roundtrip &= Math.Abs(color.R - restored.R) <= 1 && Math.Abs(color.G - restored.G) <= 1 && Math.Abs(color.B - restored.B) <= 1;
        }
        Check(roundtrip, "一千种颜色在色相面板与颜色值之间往返不产生明显偏差");
        Check(PaletteColors.TryHex("#66ccaa", out var parsedColor) && PaletteColors.Hex(parsedColor) == "#66CCAA" && !PaletteColors.TryHex("#GG0000", out _) && !PaletteColors.TryHex("#AA", out _), "颜色输入支持大小写并拒绝非法或未完成值");
        var groupSnapshot = System.Text.Json.JsonSerializer.Serialize(catalog.Groups, DataStore.JsonOptions);
        var stableTileControls = ((System.Windows.Controls.WrapPanel)tileField.GetValue(main)!).Children.Cast<UIElement>().ToArray();
        palette.OpenCustom("缤纷时刻", false);
        palette.SetColorCount(3);
        palette.SetColorPoint(0, "#C6F6D5");
        palette.SetColorPoint(1, "#B8DDFF");
        palette.SetColorPoint(2, "#FBCFE8");
        var gradient = Themes.BackgroundBrush(Themes.Current(catalog.Settings));
        Check(gradient.GradientStops.Count == 3 && gradient.GradientStops[1].Offset == .5 && gradient.GradientStops[1].Color == System.Windows.Media.Color.FromRgb(184, 221, 255), "用户选的三种颜色对应起点、中点和终点");
        Check(main.Store.Load().Settings.Theme != Themes.CustomName("缤纷时刻"), "拖动预览先在内存生效，合并保存前不反复写盘");
        Check(palette.CommitPending(false) && main.Store.Load().Settings.CustomPalettes["缤纷时刻"].Colors.SequenceEqual(new[] { "#C6F6D5", "#B8DDFF", "#FBCFE8" }), "三色渐变无需保存按钮即可自动持久化");
        Check(stableTileControls.SequenceEqual(((System.Windows.Controls.WrapPanel)tileField.GetValue(main)!).Children.Cast<UIElement>()), "连续取色预览不重建启动项或分组布局");
        var savedGradient = System.Text.Json.JsonSerializer.Serialize(catalog.Settings.CustomPalettes["缤纷时刻"], DataStore.JsonOptions);
        palette.OpenCustom("水墨国风", false);
        palette.SetColorCount(1);
        palette.SetColorPoint(0, "#668D86");
        Check(palette.CommitPending(false) && System.Text.Json.JsonSerializer.Serialize(catalog.Settings.CustomPalettes["缤纷时刻"], DataStore.JsonOptions) == savedGradient, "切换分类自选不覆盖其他分类的颜色");
        var savedTheme = catalog.Settings.Theme;
        var savedCustom = System.Text.Json.JsonSerializer.Serialize(catalog.Settings.CustomPalettes, DataStore.JsonOptions);
        palette.OpenCustom("幻彩机核", false);
        palette.SetColorCount(2);
        palette.SetColorPoint(0, "#AABBCC");
        palette.SetColorPoint(1, "#CCBBAA");
        using (var blocker = new FileStream(main.Store.FilePath + ".writing", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            Check(!palette.CommitPending(false) && catalog.Settings.Theme == savedTheme && System.Text.Json.JsonSerializer.Serialize(catalog.Settings.CustomPalettes, DataStore.JsonOptions) == savedCustom, "自选保存失败同时回退主题、颜色点和预览");
        }
        Check(groupSnapshot == System.Text.Json.JsonSerializer.Serialize(catalog.Groups, DataStore.JsonOptions), "所有取色和失败回退均不改变分组、启动项及使用次数");
        foreach (var category in Themes.Categories)
        {
            foreach (var sampleColors in new[] { new[] { "#000000", "#FFFFFF", "#FF00FF" }, new[] { "#FFFF00", "#0000FF", "#00FF00" }, new[] { "#C6F6D5", "#B8DDFF", "#FBCFE8" } })
            {
                var theme = Themes.CustomTheme(category, new CustomPalette { ColorCount = 3, Colors = [.. sampleColors] });
                var stops = Themes.BackgroundBrush(theme).GradientStops;
                for (var step = 0; step <= 32; step++)
                {
                    var progress = step / 32.0;
                    var background = progress < .5 ? PaletteColors.Mix(stops[0].Color, stops[1].Color, progress * 2) : PaletteColors.Mix(stops[1].Color, stops[2].Color, (progress - .5) * 2);
                    if (PaletteColors.Contrast(theme.Text, background) < 4.5 || PaletteColors.Contrast(theme.AccentText, theme.Accent) < 4.5 || PaletteColors.Contrast(theme.Text, theme.Surface) < 4.5)
                    {
                        throw new Exception(category + "自选配色对比度不足");
                    }
                }
            }
        }
        Check(true, "全部分类的极端黑白、鲜艳撞色、浅色渐变沿途文字及按钮均可读");
        var colorPicker = new ColorPickerPanel();
        var changedColors = 0;
        colorPicker.ColorChanged += _ => changedColors++;
        colorPicker.SetColor(System.Windows.Media.Colors.Purple);
        colorPicker.SetColor(System.Windows.Media.Colors.Teal, true);
        Check(changedColors == 1 && colorPicker.SelectedColor == System.Windows.Media.Colors.Teal, "取色面板程序刷新不递归触发保存，用户修改触发一次更新");
        var hexField = typeof(ColorPickerPanel).GetField("hex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var hexBox = (System.Windows.Controls.TextBox)hexField.GetValue(colorPicker)!;
        hexBox.Text = "#GGGGGG";
        Check(changedColors == 1 && colorPicker.SelectedColor == System.Windows.Media.Colors.Teal, "实际取色输入框中的无效值保持原有效颜色");
        hexBox.Text = "#A1B2C3";
        Check(changedColors == 2 && PaletteColors.Hex(colorPicker.SelectedColor) == "#A1B2C3", "实际颜色输入事件会更新色盘、分量和预览");
        Check(!PaletteColors.TryHex("##FFFFFF", out _) && !PaletteColors.TryHex("# FFFFF", out _), "重复井号或不足六位的输入被拒绝");
        RenderPreview(colorPicker, 326, 328, Path.Combine(previews, "现代取色面板.png"));
        palette.OpenCustom("缤纷时刻", false);
        palette.CommitPending(false);
        RenderPreview((System.Windows.FrameworkElement)((Window)palette).Content!, 1080, 1400, Path.Combine(previews, "三色自选调色盘.png"));
        RenderPreview((System.Windows.FrameworkElement)main.Content!, 760, 650, Path.Combine(previews, "自选三色启动台.png"));
        var editorField = typeof(PaletteWindow).GetField("customPopup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var popup = (System.Windows.Controls.Primitives.Popup)editorField.GetValue(palette)!;
        RenderPreview((System.Windows.FrameworkElement)popup.Child, 378, 560, Path.Combine(previews, "自选颜色面板.png"));
        palette.OpenCustom("深夜工作室", false);
        palette.SetColorCount(3);
        palette.SetColorPoint(0, "#152536");
        palette.SetColorPoint(1, "#25304D");
        palette.SetColorPoint(2, "#392C43");
        palette.CommitPending(false);
        Check(((System.Windows.Media.SolidColorBrush)((System.Windows.Controls.Border)popup.Child).Background).Color == Themes.Current(catalog.Settings).Surface, "关闭的取色弹层也同步深色主题，再打开不会保留浅色背景");
        RenderPreview((System.Windows.FrameworkElement)popup.Child, 378, 560, Path.Combine(previews, "自选颜色面板-深色.png"));
        RenderPreview((System.Windows.FrameworkElement)((Window)palette).Content!, 1080, 1400, Path.Combine(previews, "超级调色盘-深色.png"));
        RenderPreview((System.Windows.FrameworkElement)((Window)settings).Content!, 850, 730, Path.Combine(previews, "即时设置.png"));
        var sortGroup = new LaunchGroup { Name = "自己工具", Items = [new LaunchItem { Name = "Ascii", Count = 7 }, new LaunchItem { Name = "MTFileBrowser", Count = 3 }, new LaunchItem { Name = "工具3" }, new LaunchItem { Name = "工具4" }, new LaunchItem { Name = "工具5" }] };
        var originalItems = sortGroup.Items.ToArray();
        var sortData = new AppData { Groups = [sortGroup] };
        sortData.Settings.SelectedGroupId = sortGroup.Id;
        var sortMain = new MainWindow(new DataStore(Path.Combine(directory, "Reordering")), sortData, false);
        var sortTiles = (System.Windows.Controls.WrapPanel)tileField.GetValue(sortMain)!;
        void ArrangeSort()
        {
            var view = (FrameworkElement)sortMain.Content!;
            view.Measure(new Size(760, 650));
            view.Arrange(new Rect(0, 0, 760, 650));
            view.UpdateLayout();
        }
        void DropOn(LaunchItem source, LaunchItem target, bool gap)
        {
            ArrangeSort();
            var button = sortTiles.Children.Cast<System.Windows.Controls.Button>().First(b => b.Tag == target);
            var position = button.TranslatePoint(new Point(gap ? button.ActualWidth + 3 : button.ActualWidth / 2, button.ActualHeight / 2), sortTiles);
            sortMain.ReorderItemAt(source.Id, position);
        }
        foreach (var layoutName in new[] { "单列", "双列", "多列" })
        {
            sortGroup.Items = [.. originalItems];
            sortData.Settings.Layout = layoutName;
            sortMain.ApplyAppearance();
            DropOn(originalItems[0], originalItems[1], false);
            Check(sortGroup.Items.Take(2).SequenceEqual(new[] { originalItems[1], originalItems[0] }), layoutName + "中Ascii向右拖到MTFileBrowser后面，不追加到末尾");
            DropOn(originalItems[0], originalItems[1], false);
            Check(sortGroup.Items.SequenceEqual(originalItems), layoutName + "中从右向左拖回原顺序");
            DropOn(originalItems[0], originalItems[1], true);
            Check(sortGroup.Items.Take(2).SequenceEqual(new[] { originalItems[1], originalItems[0] }), layoutName + "中目标右侧间隙仍属于目标，不追加到末尾");
            DropOn(originalItems[0], originalItems[0], false);
            Check(sortGroup.Items.Take(2).SequenceEqual(new[] { originalItems[1], originalItems[0] }), layoutName + "拖到自身不改变顺序");
            Check(sortMain.Store.Load().Groups[0].Items.Select(i => i.Id).SequenceEqual(sortGroup.Items.Select(i => i.Id)), layoutName + "拖动后的顺序立即持久化");
        }
        Check(sortGroup.Items.Sum(i => i.Count) == 10 && sortGroup.Items.Count == 5, "排序不改变条目或使用次数");
        var versionText = ((System.Windows.Controls.DockPanel)((System.Windows.Controls.Grid)settings.Content!).Children[1]).Children.OfType<System.Windows.Controls.TextBlock>().First(t => t.Text.StartsWith("当前版本："));
        Check(versionText.Text == "当前版本：" + typeof(MainWindow).Assembly.GetName().Version!.ToString(3) + "（64 位）", "功能与界面底部显示实际程序集版本");
        RenderPreview((FrameworkElement)((Window)settings).Content!, 850, 730, Path.Combine(previews, "版本号设置.png"));
        var itemDrag = new DataObject("RolanNext.Item", originalItems[0].Id);
        Check(MainWindow.DropEffect(itemDrag, DragDropEffects.Move, false, false) == DragDropEffects.Move, "内部排序只返回源端允许的移动，不混入复制标记");
        Check(MainWindow.DropEffect(itemDrag, DragDropEffects.Copy, false, false) == DragDropEffects.None && MainWindow.DropEffect(itemDrag, DragDropEffects.Move, true, false) == DragDropEffects.None, "未获允许或锁定时拒绝内部移动");
        Check(MainWindow.DropEffect(new DataObject("RolanNext.Group", "group"), DragDropEffects.Move, false, false) == DragDropEffects.Move && MainWindow.DropEffect(new DataObject("RolanNext.Group", "group"), DragDropEffects.Move, false, true) == DragDropEffects.None, "分组拖动使用移动并遵守分组锁定");
        Check(MainWindow.DropEffect(new DataObject(DataFormats.FileDrop, new[] { target }), DragDropEffects.Copy | DragDropEffects.Move, false, false) == DragDropEffects.Copy, "外部文件拖入仅声明复制");
        var dragConstructor = typeof(DragEventArgs).GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Single(c => c.GetParameters().Length == 5);
        var dragOver = (DragEventArgs)dragConstructor.Invoke([itemDrag, DragDropKeyStates.None, DragDropEffects.Move, sortMain, new Point()]);
        dragOver.RoutedEvent = DragDrop.PreviewDragOverEvent;
        sortTiles.RaiseEvent(dragOver);
        Check(dragOver.Handled && dragOver.Effects == DragDropEffects.Move, "真实预览经过事件处理器统一返回移动反馈");
        sortMain.TraceDrag("验证记录", new { Source = "hash-plus", Target = "AnytxtPlus.App" });
        var tracePath = Path.Combine(sortMain.Store.DirectoryPath, "drag-sort.log");
        using (var trace = System.Text.Json.JsonDocument.Parse(File.ReadLines(tracePath).Last()))
        {
            Check(trace.RootElement.GetProperty("Stage").GetString() == "验证记录" && trace.RootElement.GetProperty("Details").GetProperty("Source").GetString() == "hash-plus", "诊断记录包含事件阶段及条目，格式可读取");
        }
        sortMain.Render();
        var unchangedOrder = sortGroup.Items.Select(i => i.Id).ToArray();
        var unchangedFile = File.ReadAllBytes(sortMain.Store.FilePath);
        Check(!sortMain.ReorderItemAt(originalItems[4].Id, new Point(200, 50)), "界面重建后尚未布局时拒绝排序");
        Check(unchangedOrder.SequenceEqual(sortGroup.Items.Select(i => i.Id)) && unchangedFile.SequenceEqual(File.ReadAllBytes(sortMain.Store.FilePath)), "无效布局不会追加到末尾，也不改写已保存顺序");
        sortGroup.Items = [.. originalItems];
        sortMain.ApplyAppearance();
        ArrangeSort();
        var targetButton = sortTiles.Children.Cast<System.Windows.Controls.Button>().First(b => b.Tag == originalItems[1]);
        var targetPoint = targetButton.TranslatePoint(new Point(targetButton.ActualWidth / 2, targetButton.ActualHeight / 2), sortTiles);
        var liveGesture = (ItemDragGesture)typeof(MainWindow).GetField("itemGesture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(sortMain)!;
        var sourceButton = sortTiles.Children.Cast<System.Windows.Controls.Button>().First(b => b.Tag == originalItems[0]);
        sourceButton.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        Check(liveGesture.TryBegin(originalItems[0].Id, true, true) && sortMain.ReorderItemAt(originalItems[0].Id, targetPoint), "真实按下处理器启用一次拖动，第一次按目标排序成功");
        Check(Enumerable.Range(0, 1000).All(_ => !liveGesture.TryBegin(originalItems[0].Id, true, true)) && sortGroup.Items.Take(2).SequenceEqual(new[] { originalItems[1], originalItems[0] }), "首次排序触发重建后，旧事件不能覆盖正确结果");
        ArrangeSort();
        sourceButton = sortTiles.Children.Cast<System.Windows.Controls.Button>().First(b => b.Tag == originalItems[0]);
        sourceButton.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        Check(liveGesture.TryBegin(originalItems[0].Id, true, true), "重建后的新图标收到新按下，可重新拖动");
        var sameName = new LaunchItem { Name = "定位测试工具" };
        var locateTarget = new LaunchItem { Name = sameName.Name };
        var currentGroup = new LaunchGroup { Name = "原分组", Items = [sameName, new LaunchItem { Name = "其他工具" }] };
        var targetGroup = new LaunchGroup { Name = "目标分组", Items = [.. Enumerable.Range(0, 30).Select(i => new LaunchItem { Name = "工具" + i }), locateTarget] };
        var locateMain = new MainWindow(new DataStore(Path.Combine(directory, "SearchLocation")), new AppData { Groups = [currentGroup, targetGroup] }, false);
        locateMain.Data.Settings.Layout = "单列";
        var locateSearch = (System.Windows.Controls.TextBox)typeof(MainWindow).GetField("search", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(locateMain)!;
        var locateTiles = (System.Windows.Controls.WrapPanel)tileField.GetValue(locateMain)!;
        var locateView = (FrameworkElement)locateMain.Content!;
        var locateScroll = (System.Windows.Controls.ScrollViewer)locateTiles.Parent;
        void LocateLayout()
        {
            locateView.Measure(new Size(600, 430));
            locateView.Arrange(new Rect(0, 0, 600, 430));
            locateView.UpdateLayout();
        }
        System.Windows.Controls.Button FindLocationTile(LaunchItem item) => locateTiles.Children.OfType<System.Windows.Controls.Button>().Single(b => ReferenceEquals(b.Tag, item));
        System.Windows.Controls.MenuItem? LocateCommand(LaunchItem item)
        {
            var tile = FindLocationTile(item);
            var openingConstructor = typeof(System.Windows.Controls.ContextMenuEventArgs).GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, [typeof(object), typeof(bool)])!;
            tile.RaiseEvent((System.Windows.Controls.ContextMenuEventArgs)openingConstructor.Invoke([tile, true]));
            return tile.ContextMenu.Items.OfType<System.Windows.Controls.MenuItem>().SingleOrDefault(m => Equals(m.Header, "跳转到所在分组"));
        }
        void PickLocateCommand(LaunchItem item)
        {
            var menu = LocateCommand(item) ?? throw new Exception("搜索结果应提供跳转菜单");
            menu.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
            LocateLayout();
            locateMain.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            locateMain.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            LocateLayout();
        }
        System.Windows.Media.SolidColorBrush CardBackground(LaunchItem item) => (System.Windows.Media.SolidColorBrush)FindLocationTile(item).Background;
        var locateTheme = Themes.Current(locateMain.Data.Settings);
        bool IsLocated(LaunchItem item) => CardBackground(item).Color != locateTheme.Surface;

        locateSearch.Text = sameName.Name;
        Check(ReferenceEquals(FindLocationTile(sameName).ContextMenu, FindLocationTile(locateTarget).ContextMenu) && FindLocationTile(locateTarget).ContextMenu.Items.Count == 0, "输入搜索时共享空菜单，打开右键菜单前不创建任何菜单项");
        PickLocateCommand(locateTarget);
        Check(locateMain.CurrentGroup == targetGroup && locateSearch.Text == "" && locateTiles.Children.Count == targetGroup.Items.Count, "跨组搜索右键精确跳转同名结果所属的分组，恢复完整列表");
        Check(IsLocated(locateTarget) && !IsLocated(targetGroup.Items[0]) && LocateCommand(locateTarget) == null, "只高亮定位目标，分组列表不显示搜索专用菜单");
        Check(locateScroll.VerticalOffset > 0 && FindLocationTile(locateTarget).TranslatePoint(new Point(0, FindLocationTile(locateTarget).ActualHeight / 2), locateScroll).Y <= locateScroll.ViewportHeight, "跳转后长列表滚动到可视范围内的目标卡片");
        locateSearch.Text = locateTarget.Name;
        PickLocateCommand(locateTarget);
        Check(locateMain.CurrentGroup == targetGroup && locateSearch.Text == "" && locateTiles.Children.Count == targetGroup.Items.Count && IsLocated(locateTarget), "搜索命中当前分组时也清除搜索、恢复列表并高亮");
        var locatedButton = FindLocationTile(locateTarget);
        var cardTemplate = (System.Windows.Controls.Grid)System.Windows.Media.VisualTreeHelper.GetChild(locatedButton, 0);
        var cardSurface = cardTemplate.Children.OfType<System.Windows.Controls.Border>().First();
        Check(ReferenceEquals(cardSurface.Background, locatedButton.Background) && IsLocated(locateTarget) && PaletteColors.Contrast(CardBackground(locateTarget).Color, locateTheme.Text) >= 4.5, "高亮填满圆角卡片底色并保持文字对比度");
        var fadeFrame = new System.Windows.Threading.DispatcherFrame();
        var fadeTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.ApplicationIdle, locateMain.Dispatcher) { Interval = TimeSpan.FromSeconds(2.6) };
        fadeTimer.Tick += (_, _) =>
        {
            fadeTimer.Stop();
            fadeFrame.Continue = false;
        };
        fadeTimer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(fadeFrame);
        Check(CardBackground(locateTarget).Color == locateTheme.Surface, "短暂提示结束后自动恢复卡片底色");
        var trackedMenu = locatedButton.ContextMenu;
        trackedMenu.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.ContextMenu.OpenedEvent, trackedMenu));
        trackedMenu.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.ContextMenu.OpenedEvent, trackedMenu));
        trackedMenu.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.ContextMenu.ClosedEvent, trackedMenu));
        Check(!locateMain.PanelInteractionActive, "重复的菜单开启通知在关闭后不会残留隐藏拦截");
        trackedMenu.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.ContextMenu.OpenedEvent, trackedMenu));
        locateMain.Render();
        Check(!locateMain.PanelInteractionActive, "宿主重建后无实际打开菜单时，缺失关闭通知也不阻止自动隐藏");
        var guardedInteraction = false;
        locateMain.Guard(() =>
        {
            trackedMenu.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.ContextMenu.ClosedEvent, trackedMenu));
            guardedInteraction = locateMain.PanelInteractionActive;
        });
        Check(guardedInteraction && !locateMain.PanelInteractionActive, "菜单关闭不会清除仍在执行的操作，操作结束后自动恢复");
        Console.WriteLine($"Windows集成验证通过：{count}项；未显示窗口、未启动目标程序、未操作系统设置。");
    }
}
