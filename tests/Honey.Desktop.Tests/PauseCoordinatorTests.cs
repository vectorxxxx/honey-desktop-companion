using Honey.Desktop.Interaction;

namespace Honey.Desktop.Tests;

public sealed class PauseCoordinatorTests
{
    [Fact]
    public void Set_手动暂停期间拖动结束不会错误恢复()
    {
        var changes = new List<bool>();
        var coordinator = new PauseCoordinator(changes.Add);

        coordinator.Set(PauseReason.User, true);
        coordinator.Set(PauseReason.Drag, true);
        coordinator.Set(PauseReason.Drag, false);

        Assert.True(coordinator.EffectivePaused);
        Assert.Equal([true], changes);
    }

    [Fact]
    public void Set_拖动期间开启手动暂停后拖动结束仍保持暂停()
    {
        var changes = new List<bool>();
        var coordinator = new PauseCoordinator(changes.Add);

        coordinator.Set(PauseReason.Drag, true);
        coordinator.Set(PauseReason.User, true);
        coordinator.Set(PauseReason.Drag, false);
        coordinator.Set(PauseReason.User, false);

        Assert.False(coordinator.EffectivePaused);
        Assert.Equal([true, false], changes);
    }
}
