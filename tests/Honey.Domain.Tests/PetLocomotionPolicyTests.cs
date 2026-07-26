using Honey.Domain.Behavior;
using Honey.Domain.Model;
using Honey.Domain.Movement;

namespace Honey.Domain.Tests;

public sealed class PetLocomotionPolicyTests
{
    [Theory]
    [InlineData(BuiltInBehaviorKeys.Observe, BuiltInPhaseKeys.ObserveTrack, LocomotionIntent.Idle)]
    [InlineData(BuiltInBehaviorKeys.Sleep, BuiltInPhaseKeys.SleepBreathe, LocomotionIntent.Anchor)]
    [InlineData(BuiltInBehaviorKeys.Web, BuiltInPhaseKeys.WebRest, LocomotionIntent.Anchor)]
    [InlineData(BuiltInBehaviorKeys.Forage, BuiltInPhaseKeys.ForageApproach, LocomotionIntent.BehaviorTarget)]
    [InlineData(BuiltInBehaviorKeys.Play, BuiltInPhaseKeys.PlayChase, LocomotionIntent.BehaviorTarget)]
    [InlineData("", "", LocomotionIntent.Roam)]
    public void Resolve_把行为阶段映射为运动意图(
        string behavior,
        string phase,
        LocomotionIntent expected)
    {
        var context = CreateContext(behavior, phase);

        Assert.Equal(expected, PetLocomotionPolicy.Resolve(context));
    }

    [Fact]
    public void Resolve_指针快速逼近时优先退避()
    {
        var context = CreateContext(
            BuiltInBehaviorKeys.Observe,
            BuiltInPhaseKeys.ObserveTrack) with
        {
            Pointer = new PointerStimulus(
                Distance: 55,
                Speed: 700,
                InterestSeconds: 2,
                ChaseCooldownRemaining: TimeSpan.Zero)
        };

        Assert.Equal(
            LocomotionIntent.RetreatPointer,
            PetLocomotionPolicy.Resolve(context));
    }

    [Fact]
    public void Resolve_持续关注且冷却结束后接近指针()
    {
        var context = CreateContext(
            BuiltInBehaviorKeys.Observe,
            BuiltInPhaseKeys.ObserveTrack) with
        {
            Pointer = new PointerStimulus(
                Distance: 180,
                Speed: 40,
                InterestSeconds: 1.2,
                ChaseCooldownRemaining: TimeSpan.Zero)
        };

        Assert.Equal(
            LocomotionIntent.ApproachPointer,
            PetLocomotionPolicy.Resolve(context));
    }

    [Fact]
    public void Resolve_追逐冷却期间只观察()
    {
        var context = CreateContext(
            BuiltInBehaviorKeys.Observe,
            BuiltInPhaseKeys.ObserveTrack) with
        {
            Pointer = new PointerStimulus(
                Distance: 180,
                Speed: 40,
                InterestSeconds: 1.2,
                ChaseCooldownRemaining: TimeSpan.FromSeconds(2))
        };

        Assert.Equal(
            LocomotionIntent.Idle,
            PetLocomotionPolicy.Resolve(context));
    }

    private static PetLocomotionContext CreateContext(string behavior, string phase) =>
        new(
            behavior,
            phase,
            PetMood.Curious,
            PetMode.Normal,
            PointerStimulus.None);
}
