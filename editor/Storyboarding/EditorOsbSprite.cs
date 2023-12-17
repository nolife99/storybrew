using System;
using System.Drawing;
using System.IO;
using System.Numerics;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Textures;
using BrewLib.Util;
using StorybrewCommon.Mapset;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Util;

namespace StorybrewEditor.Storyboarding;

public class EditorOsbSprite : OsbSprite, DisplayableObject, HasPostProcess
{
    public readonly static RenderStates AlphaBlendStates = new(), AdditiveStates = new() { BlendingFactor = new(BlendingMode.Additive) };

    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, Project project, FrameStats frameStats) 
        => Draw(drawContext, camera, bounds, opacity, project, frameStats, this);

    public void PostProcess()
    {
        if (InGroup) EndGroup();
    }
    public static void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, Project project, FrameStats frameStats, OsbSprite sprite)
    {
        var time = project.DisplayTime * 1000;
        var texturePath = sprite is OsbAnimation ? sprite.GetTexturePathAt(time) : sprite.TexturePath;
        if (texturePath is null || !sprite.IsActive(time)) return;

        var commandCost = sprite.CommandCost;
        if (frameStats is not null)
        {
            ++frameStats.SpriteCount;
            frameStats.CommandCount += commandCost;
            frameStats.IncompatibleCommands |= sprite.HasIncompatibleCommands;
            frameStats.OverlappedCommands |= sprite.HasOverlappedCommands;
        }

        var fade = sprite.OpacityAt(time);
        if (fade < .00001f) return;

        var scale = (Vector2)sprite.ScaleAt(time);
        if (scale == default) return;

        if (sprite.FlipHAt(time)) scale.X = -scale.X;
        if (sprite.FlipVAt(time)) scale.Y = -scale.Y;

        Texture2dRegion texture;
        var fullPath = Path.Combine(project.MapsetPath, texturePath);
        try
        {
            texture = project.TextureContainer.Get(fullPath);
            if (texture is null)
            {
                fullPath = Path.Combine(project.ProjectAssetFolderPath, texturePath);
                texture = project.TextureContainer.Get(fullPath);
            }
        }
        catch (IOException)
        {
            // Happens when another process is writing to the file, will try again later.
            return;
        }
        if (texture is null) return;

        var position = sprite.PositionAt(time);
        var rotation = sprite.RotationAt(time);
        var finalColor = ((Color)sprite.ColorAt(time)).LerpColor(System.Drawing.Color.Black, project.DimFactor).WithOpacity(opacity * fade);

        var origin = GetOriginVector(sprite.Origin, texture.Width, texture.Height);

        if (frameStats is not null)
        {
            var size = texture.Size * scale;

            OrientedBoundingBox spriteBox = new(position, (Vector2)origin * scale, size.X, size.Y, rotation);
            if (spriteBox.Intersects(OsuHitObject.WidescreenStoryboardBounds))
            {
                frameStats.EffectiveCommandCount += commandCost;

                var aabb = spriteBox.GetAABB();
                var intersection = RectangleF.Intersect(aabb, OsuHitObject.WidescreenStoryboardBounds);

                var intersectionArea = size.X * size.Y * (intersection.Width * intersection.Height / (aabb.Width * aabb.Height));
                frameStats.ScreenFill += Math.Min(OsuHitObject.WidescreenStoryboardArea, intersectionArea) / OsuHitObject.WidescreenStoryboardArea;
            }
        }

        var boundsScaling = bounds.Height / 480;
        DrawState.Prepare(drawContext.Get<QuadRenderer>(), camera, sprite.AdditiveAt(time) ? AdditiveStates : AlphaBlendStates).Draw(
            texture, bounds.Left + bounds.Width / 2 + (position.X - 320) * boundsScaling, bounds.Top + position.Y * boundsScaling,
            origin.X, origin.Y, scale.X * boundsScaling, scale.Y * boundsScaling, rotation, finalColor);
    }
}