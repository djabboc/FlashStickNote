using System.Windows;

namespace FlashStickNote.Services;

public readonly record struct WindowBoundsRatio(double Left, double Top, double Width, double Height);

public static class WindowBoundsRatioMapper
{
    public static bool TryNormalize(
        Rect bounds,
        Rect workArea,
        double minimumWidth,
        double minimumHeight,
        out WindowBoundsRatio ratio)
    {
        ratio = default;
        if (!IsValidArea(workArea) || !IsFinite(bounds.Left) || !IsFinite(bounds.Top) ||
            !IsFinite(bounds.Width) || !IsFinite(bounds.Height) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        var width = Math.Clamp(bounds.Width / workArea.Width, MinimumRatio(minimumWidth, workArea.Width), 1);
        var height = Math.Clamp(bounds.Height / workArea.Height, MinimumRatio(minimumHeight, workArea.Height), 1);
        var left = Math.Clamp((bounds.Left - workArea.Left) / workArea.Width, 0, 1 - width);
        var top = Math.Clamp((bounds.Top - workArea.Top) / workArea.Height, 0, 1 - height);
        ratio = new WindowBoundsRatio(left, top, width, height);
        return true;
    }

    public static bool TryMap(
        WindowBoundsRatio ratio,
        Rect workArea,
        double minimumWidth,
        double minimumHeight,
        out Rect bounds)
    {
        bounds = Rect.Empty;
        if (!IsValidArea(workArea) || !IsFinite(ratio.Left) || !IsFinite(ratio.Top) ||
            !IsFinite(ratio.Width) || !IsFinite(ratio.Height) || ratio.Width <= 0 || ratio.Height <= 0)
        {
            return false;
        }

        var widthRatio = Math.Clamp(ratio.Width, MinimumRatio(minimumWidth, workArea.Width), 1);
        var heightRatio = Math.Clamp(ratio.Height, MinimumRatio(minimumHeight, workArea.Height), 1);
        var leftRatio = Math.Clamp(ratio.Left, 0, 1 - widthRatio);
        var topRatio = Math.Clamp(ratio.Top, 0, 1 - heightRatio);
        bounds = new Rect(
            workArea.Left + leftRatio * workArea.Width,
            workArea.Top + topRatio * workArea.Height,
            widthRatio * workArea.Width,
            heightRatio * workArea.Height);
        return true;
    }

    private static double MinimumRatio(double minimum, double available)
        => Math.Min(1, Math.Max(0, minimum) / available);

    private static bool IsValidArea(Rect area)
        => IsFinite(area.Left) && IsFinite(area.Top) && IsFinite(area.Width) && IsFinite(area.Height) &&
           area.Width > 0 && area.Height > 0;

    private static bool IsFinite(double value) => double.IsFinite(value);
}
