using System.Text;
using System.Text.RegularExpressions;

namespace Rolan.Core;

public static class LegacyImporter
{
    private const string LegacyAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789+/";
    private const string StandardAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    private static readonly Regex Pattern = new("^Title:\"(.*?)\",Path:\"(.*?)\",IcoPath:\"(.*?)\",Parm:\"(.*?)\",Count:\"([0-9]+)\",$", RegexOptions.Singleline);
    public static AppData Import(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var result = new AppData();
        var section = "";
        var activated = 0;
        foreach (var line in File.ReadAllLines(path, encoding))
        {
            if (line.StartsWith('['))
            {
                section = line.Trim();
                continue;
            }
            var equals = line.IndexOf('=');
            if (equals < 0)
            {
                continue;
            }
            var key = line[..equals];
            var value = line[(equals + 1)..];
            if (section == "[Config]")
            {
                if (key == "ActivatedTab")
                {
                    int.TryParse(value, out activated);
                }
                continue;
            }
            if (section != "[Data]")
            {
                continue;
            }
            var group = new LaunchGroup { Name = key };
            foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var normalized = new string(token.Select(c => LegacyAlphabet.IndexOf(c) is var n && n >= 0 ? StandardAlphabet[n] : c).ToArray());
                var decoded = encoding.GetString(Convert.FromBase64String(normalized));
                var match = Pattern.Match(decoded);
                if (!match.Success)
                {
                    throw new InvalidDataException($"分组「{key}」第 {group.Items.Count + 1} 项无法解析，迁移已停止。");
                }
                group.Items.Add(new LaunchItem { Name = match.Groups[1].Value, Target = match.Groups[2].Value, IconPath = match.Groups[3].Value, Arguments = match.Groups[4].Value, Count = long.Parse(match.Groups[5].Value) });
            }
            result.Groups.Add(group);
        }
        if (result.Groups.Count == 0)
        {
            throw new InvalidDataException("未找到 Rolan 分组数据。");
        }
        result.Settings.SelectedGroupId = result.Groups[Math.Clamp(activated, 0, result.Groups.Count - 1)].Id;
        DataStore.Validate(result);
        return result;
    }
}
