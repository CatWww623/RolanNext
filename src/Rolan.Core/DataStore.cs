using System.Text;
using System.Text.Json;

namespace Rolan.Core;

public sealed class DataStore
{
    public static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true, WriteIndented = true };
    public string DirectoryPath { get; }
    public string FilePath => Path.Combine(DirectoryPath, "launcher.json");
    private readonly object gate = new();
    public DataStore(string directory)
    {
        DirectoryPath = directory;
        Directory.CreateDirectory(directory);
    }
    public AppData Load()
    {
        if (!File.Exists(FilePath))
        {
            return new AppData();
        }
        return Read(FilePath);
    }
    public static AppData Read(string path)
    {
        var data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(path, new UTF8Encoding(false, true)), JsonOptions) ?? throw new InvalidDataException("配置内容为空。");
        Validate(data);
        return data;
    }
    public static void Validate(AppData data)
    {
        if (data.Version != 1 || data.Groups == null || data.Settings == null)
        {
            throw new InvalidDataException("配置版本或结构不受支持。");
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in data.Groups)
        {
            if (string.IsNullOrWhiteSpace(group.Id) || !ids.Add(group.Id) || group.Items == null || group.Name == null)
            {
                throw new InvalidDataException("分组标识重复或数据不完整。");
            }
            foreach (var item in group.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id) || item.Name == null || item.Target == null || item.IconPath == null || item.Arguments == null || item.Count < 0)
                {
                    throw new InvalidDataException("启动项标识重复或数据不完整。");
                }
            }
        }
        data.Settings.Width = Math.Clamp(data.Settings.Width, 420, 1800);
        data.Settings.Height = Math.Clamp(data.Settings.Height, 320, 1600);
        data.Settings.Opacity = Math.Clamp(data.Settings.Opacity, 0.65, 1);
        data.Settings.HoverDelay = Math.Clamp(data.Settings.HoverDelay, 50, 2000);
        data.Settings.MonitorPositions ??= [];
        data.Settings.CustomPalettes ??= [];
        foreach (var palette in data.Settings.CustomPalettes.Values)
        {
            if (palette == null || palette.ColorCount < 1 || palette.ColorCount > 3 || palette.Colors == null || palette.Colors.Count < palette.ColorCount || palette.Colors.Count > 3 || palette.Colors.Any(c => c == null || c.Length != 7 || c[0] != '#' || !c.Skip(1).All(Uri.IsHexDigit)))
            {
                throw new InvalidDataException("自选配色数据无效，颜色应为六位十六进制值。");
            }
        }
        foreach (var placement in data.Settings.MonitorPositions.Values)
        {
            if (!double.IsFinite(placement.Left) || !double.IsFinite(placement.Top) || !double.IsFinite(placement.Width) || !double.IsFinite(placement.Height) || placement.Width < 100 || placement.Height < 100)
            {
                throw new InvalidDataException("显示器位置数据无效。");
            }
        }
    }
    public void Save(AppData data)
    {
        lock (gate)
        {
            Validate(data);
            var bytes = new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(data, JsonOptions).Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n");
            var temp = FilePath + ".writing";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            if (File.Exists(FilePath))
            {
                File.Replace(temp, FilePath, FilePath + ".previous", true);
            }
            else
            {
                File.Move(temp, FilePath);
            }
        }
    }
    public string Backup(AppData data)
    {
        var directory = Path.Combine(DirectoryPath, "Backups");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, DateTime.Now.ToString("yyyy-MM-dd-HHmmss-fff") + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions).Replace("\n", "\r\n") + "\r\n", new UTF8Encoding(false));
        return path;
    }
    public void Log(Exception error, string operation)
    {
        lock (gate)
        {
            File.AppendAllText(Path.Combine(DirectoryPath, "errors.log"), $"{DateTime.Now:O} {operation}\r\n{error}\r\n", new UTF8Encoding(false));
        }
    }
}
