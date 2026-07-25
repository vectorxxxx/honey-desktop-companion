using Honey.Domain.Model;

namespace Honey.Simulation;

public sealed class ModePolicy
{
    public PetMode Resolve(PetMode current, double stress) =>
        current switch
        {
            PetMode.Normal when stress >= 0.9 => PetMode.Berserk,
            PetMode.Berserk when stress <= 0.3 => PetMode.Normal,
            _ => current
        };
}
