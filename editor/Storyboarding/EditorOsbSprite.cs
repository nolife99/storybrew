using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Textures;
using BrewLib.Util;
using osuTK.Graphics;
using StorybrewCommon.Mapset;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Util;
using System;
using System.Numerics;
using System.IO;
using System.Drawing;

namespace StorybrewEditor.Storyboarding
{
    public class EditorOsbSprite : OsbSprite, DisplayableObject, HasPostProcess
    {
        public readonly static RenderStates AlphaBlendStates = new();
        public readonly static RenderStates AdditiveStates = new() { BlendingFactor = new BlendingFactorState(BlendingMode.Additive) };

        public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, Project project, FrameStats frameStats) => Draw(
            drawContext, camera, bounds, opacity, project, frameStats, this);

        public void PostProcess()
        {
            if (InGroup) EndGroup();
        }
        public static void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, Project project, FrameStats frameStats, OsbSprite sprite)
        {
            var time = project.DisplayTime * 1000;
            var texturePath = sprite is OsbAnimation ? sprite.GetTexturePathAt(time) : sprite.TexturePath;
            if (texturePath == null || !sprite.IsActive(time)) return;

            if (frameStats != null)
            {
                ++frameStats.SpriteCount;
                frameStats.CommandCount += sprite.CommandCost;
                frameStats.IncompatibleCommands |= sprite.HasIncompatibleCommands;
                frameStats.OverlappedCommands |= sprite.HasOverlappedCommands;
            }

            var fade = sprite.OpacityAt(time);
            if (fade < .00001f) return;

            var scale = (Vector2)sprite.ScaleAt(time);
            if (scale == Vector2.Zero) return;
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
            var finalColor = ((Color4)sprite.ColorAt(time)).LerpColor(Color4.Black, project.DimFactor).WithOpacity(opacity * fade);

            var origin = GetOriginVector(sprite.Origin, texture.Width, texture.Height);

            if (frameStats != null)
            {
                var size = (CommandScale)texture.Size * (CommandScale)scale;

                var spriteBox = new OrientedBoundingBox(position, origin * (CommandPosition)scale, size.X, size.Y, rotation);
                if (spriteBox.Intersects(OsuHitObject.WidescreenStoryboardBounds))
                {
                    frameStats.EffectiveCommandCount += sprite.CommandCost;

                    var sBounds = spriteBox.GetAABB();
                    var intersect = RectangleF.Intersect(sBounds, OsuHitObject.WidescreenStoryboardBounds);
                    frameStats.ScreenFill += Math.Min(OsuHitObject.WidescreenStoryboardArea, size.X * size.Y * intersect.X * intersect.Y / (sBounds.X * sBounds.Y)) / OsuHitObject.WidescreenStoryboardArea;
                }
            }

            var boundsScaling = bounds.Height / 480;
            DrawState.Prepare(drawContext.Get<QuadRenderer>(), camera, sprite.AdditiveAt(time) ? AdditiveStates : AlphaBlendStates).Draw(
                texture, bounds.Left + bounds.Width / 2 + (position.X - 320) * boundsScaling, bounds.Top + position.Y * boundsScaling,
                origin.X, origin.Y, scale.X * boundsScaling, scale.Y * boundsScaling, rotation, finalColor);
        }
    }
}