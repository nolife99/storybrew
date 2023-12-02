using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using StorybrewCommon.Storyboarding;
using System.Drawing;

namespace StorybrewEditor.Storyboarding;

public class EditorOsbAnimation : OsbAnimation, DisplayableObject, HasPostProcess
{
    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, Project project, FrameStats frameStats)
        => EditorOsbSprite.Draw(drawContext, camera, bounds, opacity, project, frameStats, this);

    public void PostProcess()
    {
        if (InGroup) EndGroup();
    }
}