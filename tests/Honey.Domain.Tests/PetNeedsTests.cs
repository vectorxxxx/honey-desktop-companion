using Honey.Domain.Model;

namespace Honey.Domain.Tests;

public sealed class PetNeedsTests
{
    [Fact]
    public void Clamp_KeepsEveryNeedBetweenZeroAndOne()
    {
        var needs = new PetNeeds(1.4, -0.2, 0.4, 0.7, 2.0).Clamp();

        Assert.Equal(new PetNeeds(1, 0, 0.4, 0.7, 1), needs);
    }
}
