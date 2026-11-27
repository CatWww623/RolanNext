using System.IO;
using System.Text;

internal static class LegacyTestFixture
{
    internal static string Write(string directory)
    {
        const string standard = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        const string legacy = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789+/";
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(936);
        var folder = Path.GetFullPath(directory);
        // 路径、参数和使用次数是人工生成的测试值。
        string EncodeEntry(string name, string target, string arguments, int count)
        {
            var text = $"Title:\"{name}\",Path:\"{target}\",IcoPath:\"{target},0\",Parm:\"{arguments}\",Count:\"{count}\",";
            var encoded = Convert.ToBase64String(encoding.GetBytes(text));
            return new string(encoded.Select(c => standard.IndexOf(c) is var index && index >= 0 ? legacy[index] : c).ToArray());
        }

        var lines = new List<string> { "[Config]", "ActivatedTab=1", "[Data]" };
        for (var group = 1; group <= 3; group++)
        {
            var entries = Enumerable.Range(1, 8).Select(i => EncodeEntry($"测试工具{group}-{i}", $@"%rp%\fixtures\example-{group}-{i}.exe", "--demo", i));
            lines.Add($"示例分组{group}={string.Join(',', entries)}");
        }
        var path = Path.Combine(folder, "legacy-example.cfg");
        File.WriteAllLines(path, lines, encoding);
        return path;
    }
}
