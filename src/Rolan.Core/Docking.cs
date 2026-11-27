namespace Rolan.Core;

public readonly record struct PixelBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

public static class Docking
{
    public static string Detect(PixelBounds window, PixelBounds area, double tolerance)
    {
        if (window.Right <= area.Left || window.Left >= area.Right || window.Bottom <= area.Top || window.Top >= area.Bottom)
        {
            return "";
        }
        var candidates = new[] { ("左侧", Math.Abs(window.Left - area.Left)), ("右侧", Math.Abs(window.Right - area.Right)), ("顶部", Math.Abs(window.Top - area.Top)), ("底部", Math.Abs(window.Bottom - area.Bottom)) };
        var nearest = candidates.OrderBy(p => p.Item2).First();
        return nearest.Item2 <= tolerance ? nearest.Item1 : "";
    }
    public static string DetectPointer(PixelBounds screen, double x, double y, double tolerance)
    {
        if (x < screen.Left || x > screen.Right || y < screen.Top || y > screen.Bottom)
        {
            return "";
        }
        var candidates = new[] { ("左侧", x - screen.Left), ("右侧", screen.Right - x), ("顶部", y - screen.Top), ("底部", screen.Bottom - y) };
        var nearest = candidates.OrderBy(p => p.Item2).First();
        return nearest.Item2 <= tolerance ? nearest.Item1 : "";
    }
    public static bool WithinSpan(string edge, PixelBounds saved, double x, double y)
    {
        return edge is "左侧" or "右侧" ? y >= saved.Top && y <= saved.Bottom : x >= saved.Left && x <= saved.Right;
    }
}
