using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using System.Drawing;

namespace StorybrewEditor.Storyboarding
{
    public interface DisplayableObject
    {
        void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, Project project, FrameStats frameStats);
    }
}