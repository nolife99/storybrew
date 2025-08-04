namespace StorybrewEditor.Storyboarding;

using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using SixLabors.ImageSharp;
using StorybrewCommon.Storyboarding;

public class EditorOsbAnimation : OsbAnimation, IDisplayable, IPostProcessable
{
    public void Draw(DrawContext drawContext,
        ICamera camera,
        RectangleF bounds,
        float opacity,
        scoped ref readonly StoryboardTransform transform,
        Project project,
        FrameStats frameStats) => EditorOsbSprite.Draw(
        drawContext,
        camera,
        bounds,
        opacity,
        in transform,
        project,
        frameStats,
        this);

    public void PostProcess()
    {
        if (InGroup) EndGroup();
    }
}