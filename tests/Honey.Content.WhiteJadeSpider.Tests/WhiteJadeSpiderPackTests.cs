using Honey.Domain.Model;
using Honey.Domain.Species;

namespace Honey.Content.WhiteJadeSpider.Tests;

public sealed class WhiteJadeSpiderPackTests
{
    [Fact]
    public void 物种包_提供稳定清单行为和等级策略()
    {
        var pack = new WhiteJadeSpiderPack();

        Assert.Equal("honey.white-jade-spider", pack.Manifest.SpeciesId);
        Assert.Equal(new Version(1, 0), pack.Manifest.Version);
        Assert.Equal("白玉蜘蛛", pack.Manifest.DisplayName);
        Assert.NotEmpty(pack.Behaviors);
        Assert.Empty(pack.Interactions);
        Assert.NotNull(pack.Progression);
        Assert.Equal(1, pack.Progression.LevelFor(long.MaxValue));
    }

    [Fact]
    public void 行为集合_恰好包含七个唯一稳定键()
    {
        var behaviors = new WhiteJadeSpiderPack().Behaviors;
        var keys = behaviors.Select(behavior => behavior.Key.Value).ToArray();

        Assert.Equal(7, behaviors.Count);
        Assert.Equal(
            ["forage", "web", "play", "observe", "pounce", "groom", "sleep"],
            keys);
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void 扑击_基础效用低于观察且冷却不少于四十五秒()
    {
        var pack = new WhiteJadeSpiderPack();
        var state = pack.CreateInitialState(
            new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero));
        var observe = Assert.Single(pack.Behaviors, behavior => behavior.Key.Value == "observe");
        var pounce = Assert.Single(pack.Behaviors, behavior => behavior.Key.Value == "pounce");

        Assert.True(pounce.Score(state) < observe.Score(state));
        Assert.True(pounce.Cooldown >= TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void 行为评分_会根据需求或模式产生可区分结果()
    {
        var pack = new WhiteJadeSpiderPack();
        var normal = pack.CreateInitialState(DateTimeOffset.UnixEpoch);
        var hungry = normal with
        {
            Needs = normal.Needs with { Hunger = 0.95, Energy = 0.2, Curiosity = 0.1 }
        };
        var berserk = hungry with { Mode = PetMode.Berserk };

        var normalScores = pack.Behaviors.Select(behavior => behavior.Score(normal)).ToArray();
        var hungryScores = pack.Behaviors.Select(behavior => behavior.Score(hungry)).ToArray();
        var berserkScores = pack.Behaviors.Select(behavior => behavior.Score(berserk)).ToArray();

        Assert.True(normalScores.Distinct().Count() > 1);
        Assert.False(normalScores.SequenceEqual(hungryScores));
        Assert.False(hungryScores.SequenceEqual(berserkScores));
    }

    [Fact]
    public void 初始状态_由物种包创建并使用物种清单标识()
    {
        var now = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
        var pack = new WhiteJadeSpiderPack();

        var state = pack.CreateInitialState(now);

        Assert.Equal(pack.Manifest.SpeciesId, state.SpeciesId);
        Assert.Equal(now, state.UpdatedAt);
        Assert.Equal(PetMode.Normal, state.Mode);
    }
}
