using System.Windows.Controls;
using System.Windows.Media;

namespace Rolan.App;

internal static class UnavailableIcon
{
    internal static Image Create(double size)
    {
        var geometry = Geometry.Parse("M 5,5 L 27,27 M 27,5 L 5,27");
        var outline = new Pen(new SolidColorBrush(Color.FromRgb(88, 61, 9)), 6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var yellow = new Pen(new SolidColorBrush(Color.FromRgb(255, 211, 48)), 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var drawing = new DrawingGroup();
        // 固定矢量画布为32×32，使大小不同的图标始终得到相同比例的覆盖。
        drawing.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new System.Windows.Rect(0, 0, 32, 32))));
        drawing.Children.Add(new GeometryDrawing(null, outline, geometry));
        drawing.Children.Add(new GeometryDrawing(null, yellow, geometry));
        drawing.Freeze();
        return new Image { Source = new DrawingImage(drawing), Width = size, Height = size, Stretch = Stretch.Fill, IsHitTestVisible = false };
    }
}
