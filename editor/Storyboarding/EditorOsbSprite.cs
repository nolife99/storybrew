namespace StorybrewEditor.Storyboarding;

using System;
using System.IO;
using System.Numerics;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Textures;
using BrewLib.Memory;
using BrewLib.Util;
using SixLabors.ImageSharp;
using StorybrewCommon.Mapset;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Util;

public class EditorOsbSprite : OsbSprite, IDisplayable, IPostProcessable
{
    static readonly RenderStates AlphaBlendStates = new(),
        AdditiveStates = new() { BlendingFactor = new(BlendingMode.Additive) };

    public void Draw(DrawContext drawContext,
        Camera camera,
        RectangleF bounds,
        float opacity,
        StoryboardTransform transform,
        Project project,
        FrameStats frameStats) => Draw(drawContext, camera, bounds, opacity, ref transform, project, frameStats, this);

    public void PostProcess()
    {
        if (InGroup) EndGroup();
    }

    public static void Draw(DrawContext drawContext,
        Camera camera,
        RectangleF bounds,
        float opacity,
        ref readonly StoryboardTransform transform,
        Project project,
        FrameStats frameStats,
        OsbSprite sprite)
    {
        var time = project.DisplayTime * 1000;
        var texturePath = sprite.GetTexturePathAt(time);
        if (texturePath is null || !sprite.IsActive(time)) return;

        if (frameStats is not null)
        {
            ++frameStats.SpriteCount;
            frameStats.CommandCount += sprite.CommandCost;
            frameStats.IncompatibleCommands |= sprite.HasIncompatibleCommands;
            frameStats.OverlappedCommands |= sprite.HasOverlappedCommands;
        }

        var fade = (float)sprite.OpacityAt(time);
        if (fade < .00001f) return;

        var scale = (Vector2)sprite.ScaleAt(time);
        if (scale == default) return;

        Span<char> span = stackalloc char[project.MapsetPath.Length + texturePath.Length + 1];
        Path.TryJoin(project.MapsetPath, texturePath, span, out _);
        PathHelper.WithStandardSeparatorsUnsafe(span);
        var fullPath = StringPool.GetOrAdd(span);

        Texture2dRegion texture;
        try
        {
            texture = project.TextureContainer.Get(fullPath);
            if (texture is null)
            {
                Span<char> span2 = stackalloc char[project.ProjectAssetFolderPath.Length + texturePath.Length + 1];
                Path.TryJoin(project.ProjectAssetFolderPath, texturePath, span2, out _);
                PathHelper.WithStandardSeparatorsUnsafe(span2);

                fullPath = StringPool.GetOrAdd(span2);
                texture = project.TextureContainer.Get(fullPath);
            }
        }
        catch (IOException)
        {
            // Happens when another process is writing to the file, will try again later.
            return;
        }

        if (texture is null) return;

        var additive = (bool)sprite.AdditiveAt(time);
        var position = (Vector2)sprite.PositionAt(time);
        var rotation = (float)sprite.RotationAt(time);

        if (sprite.FlipHAt(time)) scale.X = -scale.X;
        if (sprite.FlipVAt(time)) scale.Y = -scale.Y;

        var origin = GetOriginVector(sprite.Origin, texture.Size);
        if (!transform.IsIdentity)
        {
            position = sprite.HasMoveXYCommands ?
                transform.ApplyToPositionXY(position) :
                transform.ApplyToPosition(position);

            if (sprite.HasRotateCommands) rotation = transform.ApplyToRotation(rotation);
            if (sprite.HasScalingCommands) scale = transform.ApplyToScale(scale);
        }

        if (frameStats is not null)
        {
            var size = texture.Size * scale;
            using (OrientedBoundingBox spriteBox = new(position, origin * scale, size.X, size.Y, rotation))
                if (spriteBox.Intersects(in OsuHitObject.WidescreenStoryboardBounds))
                {
                    frameStats.EffectiveCommandCount += sprite.CommandCost;

                    var aabb = spriteBox.GetAABB();
                    var intersection = RectangleF.Intersect(aabb, OsuHitObject.WidescreenStoryboardBounds);

                    var intersectionArea =
                        size.X * size.Y * (intersection.Width * intersection.Height / (aabb.Width * aabb.Height));

                    if (!float.IsNaN(intersectionArea))
                        frameStats.ScreenFill += Math.Min(OsuHitObject.WidescreenStoryboardArea, intersectionArea) /
                            OsuHitObject.WidescreenStoryboardArea;
                }

            if (frameStats.LastTexture != fullPath)
            {
                frameStats.LastTexture = fullPath;
                ++frameStats.Batches;

                if (frameStats.LoadedPaths.Add(fullPath)) frameStats.GpuPixelsFrame += texture.Size.X * texture.Size.Y;
            }
            else if (frameStats.LastBlendingMode != additive)
            {
                frameStats.LastBlendingMode = additive;
                ++frameStats.Batches;
            }
        }

        var boundsScaling = bounds.Height / 480;
        scale *= boundsScaling;

        DrawState.Prepare(drawContext.Get<IQuadRenderer>(), camera, additive ? AdditiveStates : AlphaBlendStates)
            .Draw(texture,
                new Vector2(bounds.X + bounds.Width * .5f, bounds.Y) +
                position with { X = position.X - 320 } * boundsScaling,
                origin,
                scale,
                rotation,
                ((Color)sprite.ColorAt(time)).LerpColor(in SixLabors.ImageSharp.Color.Black, project.DimFactor)
                .WithOpacity(opacity * fade),
                Vector2.Zero,
                texture.Size);
    }
}