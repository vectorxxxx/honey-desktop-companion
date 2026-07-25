using Honey.Desktop.SingleInstance;

namespace Honey.Desktop.Tests;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public async Task DisposeAsync_重复调用安全且共享同一次关停()
    {
        var coordinator = new SingleInstanceCoordinator();

        await coordinator.DisposeAsync();
        await coordinator.DisposeAsync();
    }
}
