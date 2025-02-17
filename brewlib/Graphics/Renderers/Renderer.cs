namespace BrewLib.Graphics.Renderers;

using Cameras;

public interface Renderer
{
    ICamera Camera { get; set; }

    void BeginRendering();
    void EndRendering();

    void Flush(bool canBuffer = false);
}