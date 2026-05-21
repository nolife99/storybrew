namespace StorybrewEditor.Storyboarding;

using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using BrewLib.Audio;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Textures;
using BrewLib.Util;
using SixLabors.ImageSharp;
using StorybrewCommon.Mapset;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Util;

public class EditorOsbSprite : OsbSprite, IDisplayable, IPostProcessable
{
    static readonly RenderStates AlphaBlendStates = new(),
        AdditiveStates = new() { BlendingFactor = new(BlendingMode.Additive) };

    internal readonly struct DrawWork
    {
        public readonly OsbSprite Sprite;
        public readonly Matrix3x2 Transform;
        public readonly float RotationOffset;
        public readonly float ScaleFactor;
        public readonly float StartTime, EndTime;
        public readonly string StaticTexturePath;
        public readonly int HighlightDepth;

        public DrawWork(OsbSprite sprite, scoped ref readonly StoryboardTransform transform, int highlightDepth = 0)
        {
            var matrix = transform.Matrix;

            Sprite = sprite;
            Transform = matrix;
            RotationOffset = GetRotationOffset(matrix);
            ScaleFactor = GetScaleFactor(matrix);
            StartTime = sprite.StartTime;
            EndTime = sprite.EndTime;
            StaticTexturePath = sprite is OsbAnimation ? null : sprite.TexturePath;
            HighlightDepth = highlightDepth;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float GetRotationOffset(Matrix3x2 transform)
        => float.Atan2(-transform.M21, transform.M11);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float GetScaleFactor(Matrix3x2 transform)
        => float.Sqrt(transform.M11 * transform.M11 + transform.M12 * transform.M12);

    internal struct DrawResult
    {
        public bool Active;
        public bool Visible;
        public bool ForceVisible;
        public bool Additive;

        public OsbSprite Sprite;
        public string TexturePath;
        public ITextureRegion Texture;
        public bool TextureResolved;

        public Vector2 Position;
        public Vector2 Scale;
        public float Rotation;

        public Color Color;
        public float Fade;
        public float Opacity;
    }

    public void Draw(DrawContext drawContext,
        ICamera camera,
        RectangleF bounds,
        float opacity,
        scoped ref readonly StoryboardTransform transform,
        Project project,
        FrameStats frameStats)
        => Draw(drawContext, camera, bounds, opacity, in transform, project, frameStats, this);

    public void PostProcess()
    {
        if (InGroup) EndGroup();
    }

    public static void Draw(DrawContext drawContext,
        ICamera camera,
        RectangleF bounds,
        float opacity,
        scoped ref readonly StoryboardTransform transform,
        Project project,
        FrameStats frameStats,
        OsbSprite sprite)
    {
        var editor = drawContext.Get<Editor>();
        var work = new DrawWork(sprite, in transform);
        var result = Evaluate(in work,
            (float)project.DisplayTime.TotalMilliseconds,
            editor.InputManager.Alt,
            project.DimFactor,
            Environment.TickCount64,
            opacity,
            1,
            false,
            Matrix3x2.Identity,
            0,
            1,
            true);

        Submit(in result, drawContext, camera, bounds, project, frameStats);
    }

    internal static DrawResult Evaluate(scoped ref readonly DrawWork work,
        float time,
        bool altDown,
        float dimFactor,
        long tickCount,
        float opacity,
        float highlightOpacity,
        bool highlightActive,
        Matrix3x2 parentTransform,
        float parentRotationOffset,
        float parentScaleFactor,
        bool parentTransformIsIdentity,
        TextureContainer textureContainer = null,
        string mapsetPath = null,
        string projectAssetFolderPath = null)
    {
        var sprite = work.Sprite;

        DrawResult result = new()
        {
            Sprite = sprite,
            Opacity = highlightActive ? applyHighlightOpacity(opacity, highlightOpacity, work.HighlightDepth) : opacity
        };

        if (time < work.StartTime || work.EndTime < time || !sprite.IsActive(time)) return result;

        result.Active = true;
        result.TexturePath = work.StaticTexturePath ?? sprite.GetTexturePathAt(time);

        var inDisplayInterval = sprite.InDisplayInterval(time);
        var forceVisible = !inDisplayInterval && altDown;
        result.ForceVisible = forceVisible;

        var fade = (float)sprite.FadeTimeline.ValueAtTime(time);
        if (forceVisible) fade = float.Max(fade, .5f);
        else if (fade < .00001f) return result;

        var scale = (Vector2)sprite.ScaleAt(time);
        if (forceVisible)
        {
            if (scale.X == 0) scale.X = 1;
            if (scale.Y == 0) scale.Y = 1;
        }
        else if (scale.X == 0 || scale.Y == 0) return result;

        var additive = (bool)sprite.AdditiveTimeline.ValueAtTime(time);
        var position = (Vector2)sprite.PositionAt(time);
        var rotation = (float)sprite.RotateTimeline.ValueAtTime(time);

        if (sprite.FlipHTimeline.ValueAtTime(time)) scale.X = -scale.X;
        if (sprite.FlipVTimeline.ValueAtTime(time)) scale.Y = -scale.Y;

        var transform = parentTransformIsIdentity ? work.Transform : Matrix3x2.Multiply(parentTransform, work.Transform);

        position = sprite.HasMoveCommands
            ? new(Vector2.Transform(new(position.X, 0), transform).X,
                Vector2.Transform(new(0, position.Y), transform).Y)
            : Vector2.Transform(position, transform);

        if (sprite.RotateTimeline.HasCommands)
            rotation += parentTransformIsIdentity ? work.RotationOffset : parentRotationOffset + work.RotationOffset;
        if (sprite.HasScalingCommands)
            scale *= parentTransformIsIdentity ? work.ScaleFactor : parentScaleFactor * work.ScaleFactor;

        var color = (Color)sprite.ColorAt(time);
        if (forceVisible)
            color = SixLabors.ImageSharp.Color.FromScaledVector(color.ToScaledVector4() *
                ColorExtensions.FromHsb(new(SoundUtil.TriangleWave(tickCount * .00025f) * .5f + .5f,
                    1,
                    1,
                    1)));

        result.Visible = true;
        result.Fade = fade;
        result.Scale = scale;
        result.Additive = additive;
        result.Position = position;
        result.Rotation = rotation;
        result.Color = color.LerpColor(SixLabors.ImageSharp.Color.Black, dimFactor);
        if (textureContainer is not null)
            result.TextureResolved = tryResolveLoadedTexture(textureContainer,
                mapsetPath,
                projectAssetFolderPath,
                result.TexturePath,
                out result.Texture);

        return result;
    }

    static float applyHighlightOpacity(float opacity, float highlightOpacity, int depth)
    {
        for (var i = 0; i < depth; i++) opacity *= highlightOpacity;
        return opacity;
    }

    internal static void Submit(scoped ref readonly DrawResult result,
        DrawContext drawContext,
        ICamera camera,
        RectangleF bounds,
        Project project,
        FrameStats frameStats)
    {
        if (!result.Active) return;

        var sprite = result.Sprite;
        var texturePath = result.TexturePath;
        var time = (float)project.DisplayTime.TotalMilliseconds;

        if (frameStats is not null)
        {
            ++frameStats.SpriteCount;
            frameStats.CommandCount += sprite.CommandCost;

            if (!sprite.InDisplayInterval(time)) frameStats.ProlongedSprites.Add(sprite);
            if (sprite.HasIncompatibleCommands) frameStats.IncompatibleSprites.Add(sprite);
            if (sprite.HasOverlappedCommands) frameStats.OverlappedSprites.Add(sprite);
        }

        if (!result.Visible) return;
        ITextureRegion texture;
        if (result.TextureResolved)
        {
            texture = result.Texture;
            if (texture is null) return;
        }
        else if (!tryResolveTexture(project, texturePath, out texture)) return;

        var origin = GetOriginVector(sprite.Origin, (SizeF)texture.Size);
        var scale = result.Scale;

        if (frameStats is not null && !result.ForceVisible)
        {
            var size = (SizeF)texture.Size * scale;
            OrientedBoundingBox spriteBox = new(result.Position, origin * scale, size, result.Rotation);
            if (spriteBox.Intersects(OsuHitObject.WidescreenStoryboardBounds))
            {
                frameStats.EffectiveCommandCount += sprite.CommandCost;

                var aabb = spriteBox.GetAABB();
                var intersection = RectangleF.Intersect(aabb, OsuHitObject.WidescreenStoryboardBounds);

                var intersectionArea = size.X * size.Y *
                    (intersection.Width * intersection.Height / (aabb.Width * aabb.Height));

                if (float.IsFinite(intersectionArea))
                    frameStats.ScreenFill += float.Min(OsuHitObject.WidescreenStoryboardArea, intersectionArea) /
                        OsuHitObject.WidescreenStoryboardArea;
            }

            if (texturePath != frameStats.LastTexture)
            {
                frameStats.LastTexture = texturePath;
                ++frameStats.Batches;

                if (frameStats.LoadedPaths.Add(texturePath))
                    frameStats.GpuPixelsFrame += texture.Size.Width * texture.Size.Height;
            }
            else if (frameStats.LastBlendingMode != result.Additive)
            {
                frameStats.LastBlendingMode = result.Additive;
                ++frameStats.Batches;
            }
        }

        var boundsScaling = bounds.Height / 480;
        scale *= boundsScaling;

        DrawState.Prepare(drawContext.Get<IQuadRenderer>(), camera, result.Additive ? AdditiveStates : AlphaBlendStates)
            .Draw(texture,
                new Vector2(bounds.X + bounds.Width * .5f, bounds.Y) +
                new Vector2(result.Position.X - 320, result.Position.Y) * boundsScaling,
                origin,
                scale,
                result.Rotation,
                result.Color.WithOpacity(result.Opacity * result.Fade),
                Vector2.Zero,
                (SizeF)texture.Size);
    }

    static bool tryResolveLoadedTexture(TextureContainer textureContainer,
        string mapsetPath,
        string projectAssetFolderPath,
        string texturePath,
        out ITextureRegion texture)
    {
        Span<char> span = stackalloc char[260];
        Path.TryJoin(mapsetPath, texturePath, span, out var written);

        var splitSpan = span[..written];
        PathHelper.WithStandardSeparatorsUnsafe(splitSpan);

        var mapsetResolved = textureContainer.TryGetLoaded(splitSpan, out texture);
        if (mapsetResolved && texture is not null) return true;

        Path.TryJoin(projectAssetFolderPath, texturePath, span, out written);

        splitSpan = span[..written];
        PathHelper.WithStandardSeparatorsUnsafe(splitSpan);

        if (!textureContainer.TryGetLoaded(splitSpan, out texture)) return false;

        return texture is not null || mapsetResolved;
    }

    static bool tryResolveTexture(Project project, string texturePath, out ITextureRegion texture)
    {
        Span<char> span = stackalloc char[260];
        Path.TryJoin(project.MapsetPath, texturePath, span, out var written);

        var splitSpan = span[..written];
        PathHelper.WithStandardSeparatorsUnsafe(splitSpan);

        try
        {
            texture = project.TextureContainer.Get(splitSpan);
            if (texture is not null) return true;

            Path.TryJoin(project.ProjectAssetFolderPath, texturePath, span, out written);

            splitSpan = span[..written];
            PathHelper.WithStandardSeparatorsUnsafe(splitSpan);

            texture = project.TextureContainer.Get(splitSpan);
            return texture is not null;
        }
        catch (IOException)
        {
            texture = null;
            return false;
        }
    }
}
