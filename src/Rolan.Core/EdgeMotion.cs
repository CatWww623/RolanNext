namespace Rolan.Core;

// 时间由调用方提供，便于验证中途反向和高频重复触发，无需真实窗口。
public sealed class EdgeMotion
{
    public const double DurationMilliseconds = 200;
    private double startedAt;
    private double startVisibility = 1;
    private double duration;
    public double Visibility { get; private set; } = 1;
    public bool TargetVisible { get; private set; } = true;
    public bool IsRunning { get; private set; }
    public void Reset(bool visible)
    {
        TargetVisible = visible;
        Visibility = visible ? 1 : 0;
        IsRunning = false;
    }
    public void SetTarget(bool visible, double milliseconds, bool animate)
    {
        Advance(milliseconds);
        if (!animate)
        {
            Reset(visible);
            return;
        }
        if (TargetVisible == visible)
        {
            return;
        }
        TargetVisible = visible;
        startVisibility = Visibility;
        startedAt = milliseconds;
        duration = DurationMilliseconds * Math.Abs((visible ? 1 : 0) - Visibility);
        IsRunning = duration > .001;
        if (!IsRunning)
        {
            Reset(visible);
        }
    }
    public void Advance(double milliseconds)
    {
        if (!IsRunning)
        {
            return;
        }
        var progress = Math.Clamp((milliseconds - startedAt) / duration, 0, 1);
        var eased = TargetVisible ? 1 - Math.Pow(1 - progress, 3) : progress * progress;
        Visibility = startVisibility + ((TargetVisible ? 1 : 0) - startVisibility) * eased;
        if (progress >= 1)
        {
            Reset(TargetVisible);
        }
    }
    public static (double X, double Y) Offset(string edge, double visibility, double width, double height)
    {
        var hidden = 1 - Math.Clamp(visibility, 0, 1);
        return edge switch { "左侧" => (-width * hidden, 0), "顶部" => (0, -height * hidden), "底部" => (0, height * hidden), _ => (width * hidden, 0) };
    }
}
