using Honey.Desktop.Runtime;
using Honey.Domain.Model;

namespace Honey.Desktop.Tests;

public sealed class PetRuntimePolicyTests
{
    [Theory]
    [InlineData(0.8, 0.5, 0.2, 0.2, PetMood.Hungry)]
    [InlineData(0.2, 0.1, 0.2, 0.2, PetMood.Sleepy)]
    [InlineData(0.2, 0.8, 0.2, 0.8, PetMood.Alert)]
    [InlineData(0.2, 0.8, 0.8, 0.1, PetMood.Curious)]
    public void ResolveMood_从需求推导情绪(
        double hunger, double energy, double curiosity, double stress, PetMood expected)
    {
        Assert.Equal(
            expected,
            PetRuntimePolicy.ResolveMood(
                new PetNeeds(hunger, energy, curiosity, 0.5, stress),
                PetMode.Normal));
    }

    [Fact]
    public void IntentInterval_专注时翻倍且活动档位有序()
    {
        var active = PetRuntimePolicy.IntentInterval("active", false);
        var quiet = PetRuntimePolicy.IntentInterval("quiet", false);
        Assert.True(active < quiet);
        Assert.Equal(active * 2, PetRuntimePolicy.IntentInterval("active", true));
    }

    [Theory]
    [InlineData("normal", PetMode.Berserk, PetMode.Normal)]
    [InlineData("berserk", PetMode.Normal, PetMode.Berserk)]
    [InlineData("auto", PetMode.Berserk, PetMode.Berserk)]
    public void ApplyModePreference_强制或保留自动模式(
        string preference, PetMode state, PetMode expected)
    {
        Assert.Equal(expected, PetRuntimePolicy.ApplyModePreference(preference, state));
    }
}
