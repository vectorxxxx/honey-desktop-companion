using Honey.Desktop.Rendering;

namespace Honey.Desktop.Tests;

public sealed class PausableAnimationClockTests
{
    [Fact]
    public void Elapsed_暂停和隐藏时段不计入动画时间()
    {
        var now = TimeSpan.Zero;
        var clock = new PausableAnimationClock(() => now);
        now = TimeSpan.FromSeconds(2);
        clock.SetPaused(AnimationPauseReason.User, true);
        now = TimeSpan.FromSeconds(7);
        clock.SetPaused(AnimationPauseReason.Hidden, true);
        clock.SetPaused(AnimationPauseReason.User, false);
        now = TimeSpan.FromSeconds(10);
        clock.SetPaused(AnimationPauseReason.Hidden, false);
        now = TimeSpan.FromSeconds(11);

        Assert.Equal(3, clock.Elapsed.TotalSeconds);
    }
}
