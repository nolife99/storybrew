namespace StorybrewEditor.Storyboarding;

using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using SixLabors.ImageSharp;
using StorybrewCommon.Storyboarding;

public class EditorOsbAnimation : OsbAnimation, IDisplayable, IPostProcessable
{
    public void Draw(DrawContext drawContext,
        Camera camera,
        RectangleF bounds,
        float opacity,
        StoryboardTransform transform,
        Project project,
        FrameStats frameStats)
        => EditorOsbSprite.Draw(drawContext, camera, bounds, opacity, ref transform, project, frameStats, this);

    public void PostProcess()
    {
        if (InGroup) EndGroup();
    }
}