namespace StorybrewEditor.Storyboarding;

using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using SixLabors.ImageSharp;
using StorybrewCommon.Storyboarding;

public interface DisplayableObject
{
    float StartTime { get; }
    float EndTime { get; }

    void Draw(DrawContext drawContext,
        Camera camera,
        RectangleF bounds,
        float opacity,
        StoryboardTransform transform,
        Project project,
        FrameStats frameStats);
}