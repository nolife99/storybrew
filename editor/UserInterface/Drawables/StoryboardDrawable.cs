namespace StorybrewEditor.UserInterface.Drawables;

using SixLabors.ImageSharp;
using System.Numerics;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Drawables;
using BrewLib.Graphics.Renderers;
using Storyboarding;

public sealed class StoryboardDrawable(Project project) : Drawable
{
    readonly RenderStates linesRenderStates = new();
    public bool Clip = true, UpdateFrameStats;

    public float Time;
    public Vector2 MinSize => Vector2.Zero;
    public Vector2 PreferredSize => new(854, 480);

    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity = 1)
    {
        project.DisplayTime = Time;
        if (Clip)
            using (DrawState.Clip(bounds, camera))
                project.Draw(drawContext, camera, bounds, opacity, UpdateFrameStats);
        else
        {
            project.Draw(drawContext, camera, bounds, opacity, UpdateFrameStats);
            DrawState.Prepare(drawContext.Get<LineRenderer>(), camera, linesRenderStates)
                .DrawSquare(new(bounds.Left, bounds.Top, 0), new Vector3(bounds.Right, bounds.Bottom, 0), Color.Black);
        }
    }

    public void Dispose() { }
}