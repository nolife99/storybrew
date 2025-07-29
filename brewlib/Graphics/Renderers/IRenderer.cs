namespace BrewLib.Graphics.Renderers;

using BrewLib.Graphics.Cameras;

public interface IRenderer
{
    ICamera Camera { get; set; }

    void BeginRendering();
    void EndRendering();

    void Flush(bool canBuffer = false);
}