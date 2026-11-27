using System;
using System.Globalization;
using System.Windows.Media;

namespace Rolan.App;

internal static class PaletteColors
{
    internal static Color Mix(Color a, Color b, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromRgb((byte)Math.Round(a.R * (1 - amount) + b.R * amount), (byte)Math.Round(a.G * (1 - amount) + b.G * amount), (byte)Math.Round(a.B * (1 - amount) + b.B * amount));
    }
    internal static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    internal static bool TryHex(string text, out Color color)
    {
        color = Colors.White;
        var value = text.Trim();
        value = value.StartsWith('#') ? value[1..] : value;
        if (value.Length != 6 || !uint.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }
        color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }
    internal static double Luminance(Color color)
    {
        static double Linear(byte n)
        {
            var c = n / 255.0;
            return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4);
        }
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    }
    internal static double Contrast(Color a, Color b) => (Math.Max(Luminance(a), Luminance(b)) + .05) / (Math.Min(Luminance(a), Luminance(b)) + .05);
    internal static Color ReadableOn(Color background) => Contrast(Colors.Black, background) >= Contrast(Colors.White, background) ? Colors.Black : Colors.White;
    internal static Color EnsureContrast(Color color, Color against, double ratio)
    {
        if (Contrast(color, against) >= ratio)
        {
            return color;
        }
        var target = ReadableOn(against);
        for (var step = 1; step <= 100; step++)
        {
            var candidate = Mix(color, target, step / 100.0);
            if (Contrast(candidate, against) >= ratio)
            {
                return candidate;
            }
        }
        return target;
    }
    internal static (double Hue, double Saturation, double Value) ToHsv(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var difference = max - min;
        var hue = difference == 0 ? 0 : max == r ? 60 * ((g - b) / difference % 6) : max == g ? 60 * ((b - r) / difference + 2) : 60 * ((r - g) / difference + 4);
        return ((hue + 360) % 360, max == 0 ? 0 : difference / max, max);
    }
    internal static Color FromHsv(double hue, double saturation, double value)
    {
        hue = (hue % 360 + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);
        var c = value * saturation;
        var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = value - c;
        var rgb = hue switch { < 60 => (c, x, 0.0), < 120 => (x, c, 0.0), < 180 => (0.0, c, x), < 240 => (0.0, x, c), < 300 => (x, 0.0, c), _ => (c, 0.0, x) };
        return Color.FromRgb((byte)Math.Round((rgb.Item1 + m) * 255), (byte)Math.Round((rgb.Item2 + m) * 255), (byte)Math.Round((rgb.Item3 + m) * 255));
    }
}
