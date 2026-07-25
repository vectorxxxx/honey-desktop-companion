using SkiaSharp;

namespace Honey.Rendering;

public interface IRenderScene
{
    void Draw(SKCanvas canvas, RenderSnapshot snapshot);
}
