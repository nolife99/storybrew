namespace BrewLib.Graphics.Renderers;

using Cameras;

public interface Renderer
{
    Camera Camera { get; set; }

    void BeginRendering();
    void EndRendering();

    void Flush(bool canBuffer = false);
}