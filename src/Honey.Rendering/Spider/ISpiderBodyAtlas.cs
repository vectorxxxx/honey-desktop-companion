using Honey.Domain.Model;

namespace Honey.Rendering.Spider;

public interface ISpiderBodyAtlas : IDisposable
{
    bool TryGetFrame(
        PetMode mode,
        SpiderDirection direction,
        out SpiderAtlasFrame frame);
}
