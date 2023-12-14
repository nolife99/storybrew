﻿using System.Drawing;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;

namespace StorybrewEditor.Storyboarding;

public interface DisplayableObject
{
    void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, Project project, FrameStats frameStats);
}