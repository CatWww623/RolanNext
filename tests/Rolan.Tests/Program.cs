using Rolan.Core;
using System.Text;
using System.Text.Json;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var output = Path.Combine(Path.GetTempPath(), "RolanNext.CoreTests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(output);
var source = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) ? Path.GetFullPath(args[1]) : LegacyTestFixture.Write(output);
var checks = 0;
void Check(bool ok, string label)
{
    if (!ok)
    {
        throw new Exception("验证失败：" + label);
    }
    checks++;
    Console.WriteLine("通过：" + label);
}
var originalBytes = File.ReadAllBytes(source);
var data = LegacyImporter.Import(source);
Check(data.Groups.Count > 0 && data.Groups.Sum(g => g.Items.Count) > 0, "旧格式配置可完整导入");
var snapshot = JsonSerializer.Serialize(data, DataStore.JsonOptions);
var store = new DataStore(output);
store.Save(data);
var loaded = store.Load();
Check(JsonSerializer.Serialize(loaded, DataStore.JsonOptions) == snapshot, "导入数据保存并重新加载后逐字段一致");
store.Save(data);
Check(File.Exists(store.FilePath + ".previous"), "原子保存保留前一版本");
var bytes = File.ReadAllBytes(store.FilePath);
Check(!bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }), "新配置为UTF-8无BOM");
Check(!Encoding.UTF8.GetString(bytes).Replace("\r\n", "").Contains('\n'), "新配置使用CRLF");
var ids = data.Groups.SelectMany(g => g.Items).Select(i => i.Id).Order().ToArray();
var counts = data.Groups.SelectMany(g => g.Items).Sum(i => i.Count);
var random = new Random(340);
for (var n = 0; n < 10000; n++)
{
    var id = ids[random.Next(ids.Length)];
    var target = data.Groups[random.Next(data.Groups.Count)];
    if (!Catalog.Move(data, id, target.Id, random.Next(target.Items.Count + 1)))
    {
        throw new Exception("随机移动失败");
    }
}
Check(data.Groups.SelectMany(g => g.Items).Select(i => i.Id).Order().SequenceEqual(ids), "一万次跨组与组内随机移动后条目不丢失、不重复");
Check(counts == data.Groups.SelectMany(g => g.Items).Sum(i => i.Count), "随机移动不改变使用次数");
var beforeMissing = JsonSerializer.Serialize(data, DataStore.JsonOptions);
Check(!Catalog.Move(data, ids[0], "不存在的目标分组", 1), "失效分组标识被拒绝");
Check(beforeMissing == JsonSerializer.Serialize(data, DataStore.JsonOptions), "失效分组不会移除原项目");
Check(Catalog.Matches(new LaunchItem { Name = "资源监视器" }, "ziyuan"), "中文完整拼音搜索");
Check(Catalog.Matches(new LaunchItem { Name = "资源监视器" }, "zyjsq"), "中文拼音首字母搜索");
Check(Catalog.Matches(new LaunchItem { Name = "Hash Tool" }, "hash tool"), "名称多词搜索");
Check(Catalog.Expand(@"%rp%\apps\demo.exe", @"C:\Portable") == @"C:\Portable\apps\demo.exe", "旧版便携路径展开");
Check(Catalog.Expand("https://example.com/test", @"C:\Portable") == "https://example.com/test", "网址保持不变");
var sample = data.Groups.SelectMany(g => g.Items).First();
Check(Catalog.IsDuplicate(data, new LaunchItem { Target = sample.Target.ToUpperInvariant(), Arguments = sample.Arguments }, root), "重复项忽略路径大小写并匹配参数");
Check(!Catalog.IsDuplicate(data, new LaunchItem { Target = sample.Target, Arguments = "__different_arguments__" }, root), "相同目标不同参数可独立保存");
var duplicate = new AppData { Groups = [new LaunchGroup { Items = [new LaunchItem { Id = "duplicate" }, new LaunchItem { Id = "duplicate" }] }] };
var rejected = false;
try
{
    DataStore.Validate(duplicate);
}
catch (InvalidDataException)
{
    rejected = true;
}
Check(rejected, "损坏配置中的重复标识被拒绝");
Check(File.ReadAllBytes(source).SequenceEqual(originalBytes), "原始Rolan配置字节未变化");
var migration = LegacyImporter.Import(source);
var area = new PixelBounds(0, 0, 1920, 1040);
Check(Docking.Detect(new PixelBounds(0, 100, 760, 650), area, 16) == "左侧", "拖到左边缘识别停靠");
Check(Docking.Detect(new PixelBounds(1160, 100, 760, 650), area, 16) == "右侧", "拖到右边缘识别停靠");
Check(Docking.Detect(new PixelBounds(200, 0, 760, 650), area, 16) == "顶部", "拖到顶部识别停靠");
Check(Docking.Detect(new PixelBounds(200, 390, 760, 650), area, 16) == "底部", "拖到工作区底部识别停靠");
Check(Docking.Detect(new PixelBounds(200, 120, 760, 650), area, 16) == "", "拖离边缘恢复浮动");
Check(Docking.Detect(new PixelBounds(-1915, 100, 760, 650), new PixelBounds(-1920, 0, 1920, 1040), 16) == "左侧", "负坐标显示器停靠");
Check(!Docking.WithinSpan("右侧", new PixelBounds(1160, 100, 760, 650), 1919, 800), "停靠位置范围外不呼出");
Check(Docking.WithinSpan("右侧", new PixelBounds(1160, 100, 760, 650), 1919, 350), "停靠位置范围内可呼出");
Check(Docking.DetectPointer(new PixelBounds(0, 0, 1920, 1080), 1919, 350, 16) == "右侧", "从标题中部拖到屏幕右缘仍识别停靠");
Check(Docking.DetectPointer(new PixelBounds(0, 0, 1920, 1080), 500, 0, 16) == "顶部", "指针触顶时保持面板尺寸并识别顶部");
Check(Docking.DetectPointer(new PixelBounds(0, 0, 1920, 1080), 500, 1079, 16) == "底部", "跨任务栏区域的底部停靠检测");
var motion = new EdgeMotion();
motion.Reset(false);
motion.SetTarget(true, 0, true);
motion.Advance(100);
Check(motion.IsRunning && motion.Visibility > .5 && motion.Visibility < 1, "呼出采用200毫秒减速展开");
motion.Advance(200);
Check(!motion.IsRunning && motion.Visibility == 1, "呼出结束精确回到原位置");
motion.SetTarget(false, 200, true);
motion.Advance(300);
Check(motion.Visibility > .5 && motion.Visibility < 1, "隐藏采用加速滑出且不改变不透明度");
var halfway = motion.Visibility;
motion.SetTarget(true, 300, true);
Check(motion.Visibility == halfway, "隐藏中反向呼出不跳位置");
motion.Advance(500);
Check(!motion.IsRunning && motion.Visibility == 1, "反向呼出结束后不执行过期隐藏");
motion.SetTarget(false, 500, true);
motion.SetTarget(false, 600, true);
motion.Advance(700);
Check(!motion.IsRunning && motion.Visibility == 0, "重复隐藏请求不重启动画或延迟结束");
motion.SetTarget(true, 700, false);
Check(!motion.IsRunning && motion.Visibility == 1, "关闭动画后直接显示");
motion.SetTarget(false, 710, true);
motion.SetTarget(false, 730, false);
Check(!motion.IsRunning && motion.Visibility == 0, "动画中关闭开关立即到达隐藏终点");
Check(EdgeMotion.Offset("左侧", 0, 760, 650) == (-760, 0) && EdgeMotion.Offset("右侧", 0, 760, 650) == (760, 0), "左右边缘动画方向及距离正确");
Check(EdgeMotion.Offset("顶部", 0, 760, 650) == (0, -650) && EdgeMotion.Offset("底部", 0, 760, 650) == (0, 650), "上下边缘动画方向及距离正确");
Check(new[] { "左侧", "右侧", "顶部", "底部" }.All(e => EdgeMotion.Offset(e, 1, 760, 650) == (0, 0)), "四边完全展开时均恢复零偏移");
var stable = true;
for (var n = 0; n < 1000; n++)
{
    var now = 1000 + n * 7.0;
    motion.Advance(now);
    var before = motion.Visibility;
    motion.SetTarget(n % 2 == 0, now, true);
    stable &= Math.Abs(before - motion.Visibility) < .000001 && motion.Visibility >= 0 && motion.Visibility <= 1;
}
motion.SetTarget(true, 9000, true);
motion.Advance(9200);
Check(stable && !motion.IsRunning && motion.Visibility == 1, "一千次快速交替呼出隐藏保持连续且最终正确结束");
var oldPaletteJson = "{\"Version\":1,\"Groups\":[],\"Settings\":{\"Theme\":\"自选颜色\",\"CustomColor\":\"#663399\"}}";
var oldPaletteData = JsonSerializer.Deserialize<AppData>(oldPaletteJson, DataStore.JsonOptions)!;
DataStore.Validate(oldPaletteData);
Check(oldPaletteData.Settings.CustomColor == "#663399" && oldPaletteData.Settings.CustomPalettes.Count == 0, "旧版单色配置无需新字段即可加载");
var paletteStore = new DataStore(Path.Combine(output, "Palettes"));
oldPaletteData.Settings.CustomPalettes["水墨国风"] = new CustomPalette { ColorCount = 1, Colors = ["#8DADA0", "#D2E5DA", "#E7DBF2"] };
oldPaletteData.Settings.CustomPalettes["缤纷时刻"] = new CustomPalette { ColorCount = 3, Colors = ["#D8AAFF", "#B7DDFF", "#FFD0CC"] };
paletteStore.Save(oldPaletteData);
var paletteReloaded = paletteStore.Load();
Check(paletteReloaded.Settings.CustomPalettes["水墨国风"].ColorCount == 1 && paletteReloaded.Settings.CustomPalettes["缤纷时刻"].Colors.SequenceEqual(new[] { "#D8AAFF", "#B7DDFF", "#FFD0CC" }), "各分类的主色与三色渐变独立保存并可恢复");
var copiedPalette = paletteReloaded.Settings.CustomPalettes["缤纷时刻"].Copy();
copiedPalette.Colors[0] = "#000000";
Check(paletteReloaded.Settings.CustomPalettes["缤纷时刻"].Colors[0] == "#D8AAFF", "配色副本不改写已保存的颜色列表");
foreach (var invalidPalette in new[] { new CustomPalette { ColorCount = 4 }, new CustomPalette { ColorCount = 3, Colors = ["#FFFFFF"] }, new CustomPalette { Colors = ["#GGGGGG", "#FFFFFF"] } })
{
    var invalidData = new AppData();
    invalidData.Settings.CustomPalettes["测试"] = invalidPalette;
    var invalidRejected = false;
    try
    {
        DataStore.Validate(invalidData);
    }
    catch (InvalidDataException)
    {
        invalidRejected = true;
    }
    Check(invalidRejected, "无效配色结构或颜色值被拒绝：" + invalidPalette.ColorCount + "色");
}
var gesture = new ItemDragGesture();
Check(!gesture.TryBegin("hash-plus", true, true), "未在项目上按下鼠标时不能启动拖动");
gesture.Arm("hash-plus");
Check(!gesture.TryBegin("AnytxtPlus.App", true, true), "经过其他图标不能改变本次按压来源");
Check(gesture.TryBegin("hash-plus", true, true) && gesture.Consumed, "原始项目首次拖动会消耗本次按压");
Check(Enumerable.Range(0, 1000).All(_ => !gesture.TryBegin("hash-plus", true, true)), "拖动返回后一千个旧移动事件均不能启动第二次拖动");
gesture.Arm("hash-plus");
Check(!gesture.TryBegin("hash-plus", true, false) && !gesture.TryBegin("hash-plus", true, true), "鼠标实际已松开时拒绝旧按下事件，并取消该次来源");
gesture.Arm("hash-plus");
Check(!gesture.TryBegin("hash-plus", false, true) && gesture.TryBegin("hash-plus", true, true), "移动事件与实时按键状态都按下才允许启动");
gesture.Arm("AnytxtPlus.App");
Check(gesture.TryBegin("AnytxtPlus.App", true, true), "真正重新按下后可立即进行下一次拖动");
Console.WriteLine($"验证完成：{checks} 项；迁移 {migration.Groups.Count} 组 / {migration.Groups.Sum(g => g.Items.Count)} 项。");
File.WriteAllText(Path.Combine(output, "result.txt"), $"{checks} 项通过；10000次随机移动；{migration.Groups.Count}组/{migration.Groups.Sum(g => g.Items.Count)}项。\r\n", new UTF8Encoding(false));
