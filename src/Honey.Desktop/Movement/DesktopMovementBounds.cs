using Honey.Domain.Movement;
using Honey.Integrations.Windows;

namespace Honey.Desktop.Movement;

public static class DesktopMovementBounds
{
    public static LocomotionBounds Create(PixelRect workArea, PixelRect contentBounds)
    {
        var left = workArea.X - contentBounds.X;
        var top = workArea.Y - contentBounds.Y;
        var right = workArea.Right - contentBounds.Right;
        var bottom = workArea.Bottom - contentBounds.Bottom;
        if (right < left)
        {
            right = left;
        }
        if (bottom < top)
        {
            bottom = top;
        }

        return new LocomotionBounds(left, top, right, bottom);
    }

    public static PixelRect SelectRoamingArea(
        PixelRect currentContent,
        IReadOnlyList<PixelRect> workAreas,
        bool allowCrossMonitor,
        double sample)
    {
        ArgumentNullException.ThrowIfNull(workAreas);
        if (workAreas.Count == 0)
        {
            throw new ArgumentException("至少需要一个显示器工作区。", nameof(workAreas));
        }
        if (!allowCrossMonitor || workAreas.Count == 1)
        {
            return DisplayBoundsService.FindNearestWorkArea(currentContent, workAreas);
        }

        var safeSample = double.IsFinite(sample) ? Math.Clamp(sample, 0, 0.999999) : 0;
        return workAreas[(int)(safeSample * workAreas.Count)];
    }
}
