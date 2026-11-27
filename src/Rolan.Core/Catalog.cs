using System.Globalization;
using System.Reflection;
using System.Text;

namespace Rolan.Core;

public static class Catalog
{
    private static readonly Lazy<Dictionary<char, string>> Pinyin = new(LoadPinyin);
    private static readonly Dictionary<string, string> SearchCache = new(StringComparer.Ordinal);
    public static string Expand(string target, string baseDirectory)
    {
        var expanded = Environment.ExpandEnvironmentVariables(target.Replace("%rp%", baseDirectory, StringComparison.OrdinalIgnoreCase).Replace("%rr%", Path.GetPathRoot(baseDirectory)?.TrimEnd('\\') ?? "", StringComparison.OrdinalIgnoreCase));
        if (Uri.TryCreate(expanded, UriKind.Absolute, out var uri) && uri.Scheme is not "file")
        {
            return expanded;
        }
        return Path.GetFullPath(expanded, baseDirectory);
    }
    public static string TargetKey(string target, string baseDirectory)
    {
        try
        {
            return Expand(target, baseDirectory).TrimEnd('\\');
        }
        catch
        {
            return target.Trim();
        }
    }
    public static bool IsDuplicate(AppData data, LaunchItem item, string baseDirectory)
    {
        var key = TargetKey(item.Target, baseDirectory);
        return data.Groups.SelectMany(g => g.Items).Any(i => string.Equals(TargetKey(i.Target, baseDirectory), key, StringComparison.OrdinalIgnoreCase) && i.Arguments == item.Arguments);
    }
    public static bool Move(AppData data, string itemId, string targetGroupId, int index)
    {
        var source = data.Groups.FirstOrDefault(g => g.Items.Any(i => i.Id == itemId));
        var destination = data.Groups.FirstOrDefault(g => g.Id == targetGroupId);
        if (source == null || destination == null)
        {
            return false;
        }
        var item = source.Items.First(i => i.Id == itemId);
        if (source == destination && source.Items.IndexOf(item) < index)
        {
            index--;
        }
        source.Items.Remove(item);
        destination.Items.Insert(Math.Clamp(index, 0, destination.Items.Count), item);
        return true;
    }
    public static int DropIndex(LaunchGroup destination, string sourceId, string targetId)
    {
        var target = destination.Items.FindIndex(i => i.Id == targetId);
        if (target < 0)
        {
            return destination.Items.Count;
        }
        var source = destination.Items.FindIndex(i => i.Id == sourceId);
        return source >= 0 && source < target ? target + 1 : target;
    }
    public static bool Matches(LaunchItem item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }
        if (!SearchCache.TryGetValue(item.Name, out var phonetics))
        {
            var full = new StringBuilder();
            var initials = new StringBuilder();
            foreach (var c in item.Name)
            {
                if (Pinyin.Value.TryGetValue(c, out var reading))
                {
                    full.Append(reading);
                    initials.Append(reading[0]);
                }
                else
                {
                    full.Append(c);
                    initials.Append(c);
                }
            }
            phonetics = full + " " + initials;
            SearchCache[item.Name] = phonetics;
        }
        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(q => (item.Name + " " + item.Target + " " + phonetics).Contains(q, StringComparison.OrdinalIgnoreCase));
    }
    private static Dictionary<char, string> LoadPinyin()
    {
        var result = new Dictionary<char, string>();
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Rolan.Core.pinyin.txt");
        if (stream == null)
        {
            return result;
        }
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 2)
            {
                result[line[0]] = line[2..];
            }
        }
        return result;
    }
}
