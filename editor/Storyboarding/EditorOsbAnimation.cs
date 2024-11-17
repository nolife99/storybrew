namespace StorybrewEditor.Storyboarding;

using SixLabors.ImageSharp;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using StorybrewCommon.Storyboarding;

public class EditorOsbAnimation : OsbAnimation, DisplayableObject, HasPostProcess
{
    public void Draw(DrawContext drawContext,
        Camera camera,
        RectangleF bounds,
        float opacity,
        StoryboardTransform transform,
        Project project,
        FrameStats frameStats)
        => EditorOsbSprite.Draw(drawContext, camera, bounds, opacity, transform, project, frameStats, this);

    public void PostProcess()
    {
        if (InGroup) EndGroup();
    }
}