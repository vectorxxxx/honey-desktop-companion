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

    [Fact]
    public void Clamp_NormalizesNonFiniteValues()
    {
        var needs = new PetNeeds(
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity,
            0.4,
            2.0).Clamp();

        Assert.Equal(new PetNeeds(0, 1, 0, 0.4, 1), needs);
    }
}
