using StorybrewCommon.Animations;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.Util;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Drawing;

namespace StorybrewCommon.Storyboarding3d;

///<summary> Represents a line segment, containing start and end nodes, with 3D functionality. </summary>
public class Line3d : Node3d, HasOsbSprites
{
    Action<OsbSprite> finalize;
    OsbSprite sprite;

    ///<inheritdoc/>
    public IEnumerable<OsbSprite> Sprites => [sprite];

    ///<summary> The path to the image of this <see cref="Line3d"/>. </summary>
    public string SpritePath;
    
    ///<summary> The <see cref="OsbOrigin"/> of this <see cref="Line3d"/>. </summary>
    public OsbOrigin SpriteOrigin = OsbOrigin.Centre;

    ///<summary> Toggles additive blending on this <see cref="Sprite3d"/>. </summary>
    public bool Additive;

    ///<summary> Whether to fade sprites based on distance from the <see cref="Camera"/>. </summary>
    public bool UseDistanceFade = true;

    ///<summary> The segment start position of this <see cref="Line3d"/>. </summary>
    public readonly KeyframedValue<Vector3> StartPosition = new(InterpolatingFunctions.Vector3);

    ///<summary> The segment end position of this <see cref="Line3d"/>. </summary>
    public readonly KeyframedValue<Vector3> EndPosition = new(InterpolatingFunctions.Vector3);

    ///<summary> The thickness of this <see cref="Line3d"/>, in osu!pixels, relative to 3D transformations. </summary>
    public readonly KeyframedValue<float> Thickness = new(InterpolatingFunctions.Float, 1);

    SizeF spriteBitmap;

    readonly CommandGenerator gen = new();

    ///<inheritdoc/>
    public IEnumerable<CommandGenerator> CommandGenerators => [gen];

    ///<inheritdoc/>
    public void ConfigureGenerators(Action<CommandGenerator> action) => action(gen);

    ///<inheritdoc/>
    public override void GenerateSprite(StoryboardSegment segment)
    {
        sprite ??= segment.CreateSprite(SpritePath, SpriteOrigin);
        spriteBitmap = CommandGenerator.BitmapDimensions(sprite);
    }

    ///<inheritdoc/>
    public override void GenerateStates(double time, CameraState cameraState, Object3dState object3dState)
    {
        var wvp = object3dState.WorldTransform * cameraState.ViewProjection;
        var startVector = CameraState.ToScreen(wvp, StartPosition.ValueAt(time));
        var endVector = CameraState.ToScreen(wvp, EndPosition.ValueAt(time));

        Vector2 delta = new(endVector.X - startVector.X, endVector.Y - startVector.Y);
        if (delta.LengthSquared() == 0) return;

        var opacity = startVector.W < 0 && endVector.W < 0 ? 0 : object3dState.Opacity;
        if (UseDistanceFade) opacity *= Math.Max(cameraState.OpacityAt(startVector.W), cameraState.OpacityAt(endVector.W));

        var position = sprite.Origin switch
        {
            OsbOrigin.TopCentre or OsbOrigin.Centre or OsbOrigin.BottomCentre => new Vector2(startVector.X, startVector.Y) + delta / 2,
            OsbOrigin.TopRight or OsbOrigin.CentreRight or OsbOrigin.BottomRight => new(endVector.X, endVector.Y),
            _ => new(startVector.X, startVector.Y),
        };

        gen.Add(new()
        {
            Time = time,
            Position = position,
            Scale = new(delta.Length() / spriteBitmap.Width, Thickness.ValueAt(time)),
            Rotation = InterpolatingFunctions.DoubleAngle(gen.EndState?.Rotation ?? 0, Math.Atan2(delta.Y, delta.X), 1),
            Color = object3dState.Color,
            Opacity = opacity,
            Additive = Additive
        });
    }

    ///<inheritdoc/>
    public void DoTreeSprite(Action<OsbSprite> action) => finalize = action;

    ///<inheritdoc/>
    public override void GenerateCommands(Action<Action, OsbSprite> action, double? startTime, double? endTime, double timeOffset, bool loopable)
    {
        if (finalize is not null) action += (createCommands, sprite) =>
        {
            createCommands();
            finalize(sprite);
        };
        gen.GenerateCommands(sprite, action, startTime, endTime, timeOffset, loopable);
    }
}

#pragma warning disable CS1591
public class Line3dEx : Node3d, HasOsbSprites
{
    Action<OsbSprite> finalize;
    OsbSprite spriteBody, spriteTopEdge, spriteBottomEdge, spriteStartCap, spriteEndCap;

    ///<inheritdoc/>
    public IEnumerable<OsbSprite> Sprites
    {
        get
        {
            yield return spriteBody;
            if (SpritePathEdge is not null)
            {
                yield return spriteTopEdge;
                yield return spriteBottomEdge;
            }
            if (SpritePathCap is not null)
            {
                yield return spriteStartCap;
                yield return spriteEndCap;
            }
        }
    }

    public string SpritePathBody, SpritePathEdge, SpritePathCap;
    public bool Additive, UseDistanceFade = true, EnableStartCap = true, EnableEndCap = true, OrientedCaps;

    public float EdgeOverlap = .5f, CapOverlap = .2f;

    public readonly KeyframedValue<Vector3> StartPosition = new(InterpolatingFunctions.Vector3), EndPosition = new(InterpolatingFunctions.Vector3);
    public readonly KeyframedValue<float> Thickness = new(InterpolatingFunctions.Float, 1), StartThickness = new(InterpolatingFunctions.Float, 1), EndThickness = new(InterpolatingFunctions.Float, 1);

    readonly CommandGenerator genBody = new(), genTopEdge = new(), genBottomEdge = new(), genStartCap = new(), genEndCap = new();

    ///<inheritdoc/>
    public IEnumerable<CommandGenerator> CommandGenerators => [genBody, genTopEdge, genBottomEdge, genStartCap, genEndCap];

    readonly SizeF[] spriteBitmaps = new SizeF[3];

    ///<inheritdoc/>
    public override void GenerateSprite(StoryboardSegment segment)
    {
        spriteBody ??= segment.CreateSprite(SpritePathBody, OsbOrigin.Centre);
        spriteBitmaps[0] = CommandGenerator.BitmapDimensions(spriteBody);

        if (SpritePathEdge is not null)
        {
            spriteTopEdge ??= segment.CreateSprite(SpritePathEdge, OsbOrigin.BottomCentre);
            spriteBottomEdge ??= segment.CreateSprite(SpritePathEdge, OsbOrigin.TopCentre);
            spriteBitmaps[1] = CommandGenerator.BitmapDimensions(spriteTopEdge);
        }
        if (SpritePathCap is not null)
        {
            spriteStartCap ??= segment.CreateSprite(SpritePathCap, OrientedCaps ? OsbOrigin.CentreLeft : OsbOrigin.Centre);
            spriteEndCap ??= segment.CreateSprite(SpritePathCap, OrientedCaps ? OsbOrigin.CentreRight : OsbOrigin.Centre);
            spriteBitmaps[2] = CommandGenerator.BitmapDimensions(spriteEndCap);
        }
    }

    ///<inheritdoc/>
    public override void GenerateStates(double time, CameraState cameraState, Object3dState object3dState)
    {
        var wvp = object3dState.WorldTransform * cameraState.ViewProjection;
        var startVector = CameraState.ToScreen(wvp, StartPosition.ValueAt(time));
        var endVector = CameraState.ToScreen(wvp, EndPosition.ValueAt(time));

        Vector2 delta = new(endVector.X - startVector.X, endVector.Y - startVector.Y);
        if (delta.LengthSquared() == 0) return;

        var angle = Math.Atan2(delta.Y, delta.X);
        var rotation = InterpolatingFunctions.DoubleAngle(genBody.EndState?.Rotation ?? 0, angle, 1);

        var thickness = Thickness.ValueAt(time);
        var matrix = object3dState.WorldTransform;
        var scaleFactor = new Vector3(matrix.M21, matrix.M22, matrix.M23).Length() * (float)cameraState.ResolutionScale;
        var startScale = scaleFactor * (float)(cameraState.FocusDistance / startVector.W) * thickness * StartThickness.ValueAt(time);
        var endScale = scaleFactor * (float)(cameraState.FocusDistance / endVector.W) * thickness * EndThickness.ValueAt(time);

        var totalHeight = Math.Max(startScale, endScale);
        var bodyHeight = Math.Min(startScale, endScale);
        var edgeHeight = (totalHeight - bodyHeight) / 2;
        var flip = startScale < endScale;

        var ignoreEdges = edgeHeight < EdgeOverlap;
        if (ignoreEdges) bodyHeight += edgeHeight * 2;

        var opacity = startVector.W < 0 && endVector.W < 0 ? 0 : object3dState.Opacity;
        if (UseDistanceFade) opacity *= Math.Max(cameraState.OpacityAt(startVector.W), cameraState.OpacityAt(endVector.W));
        
        var length = delta.Length();

        var positionBody = new Vector2(startVector.X, startVector.Y) + delta / 2;
        genBody.Add(new State
        {
            Time = time,
            Position = positionBody,
            Scale = new(length / spriteBitmaps[0].Width, bodyHeight / spriteBitmaps[0].Height),
            Rotation = rotation,
            Color = object3dState.Color,
            Opacity = opacity,
            Additive = Additive
        });

        if (SpritePathEdge is not null)
        {
            Vector2 edgeScale = new(length / spriteBitmaps[1].Width, edgeHeight / spriteBitmaps[1].Height),
                edgeOffset = new Vector2((float)Math.Cos(angle - Math.PI / 2), (float)Math.Sin(angle - Math.PI / 2)) * (bodyHeight / 2 - EdgeOverlap);

            var positionTop = positionBody + edgeOffset;
            var positionBottom = positionBody - edgeOffset;

            var edgeOpacity = ignoreEdges ? 0 : opacity;

            genTopEdge.Add(new()
            {
                Time = time,
                Position = positionTop,
                Scale = edgeScale,
                Rotation = rotation,
                Color = object3dState.Color,
                Opacity = edgeOpacity,
                FlipH = flip,
                Additive = Additive
            });
            genBottomEdge.Add(new()
            {
                Time = time,
                Position = positionBottom,
                Scale = edgeScale,
                Rotation = rotation,
                Color = object3dState.Color,
                Opacity = edgeOpacity,
                Additive = Additive,
                FlipH = flip,
                FlipV = true
            });
        }
        if (SpritePathCap is not null)
        {
            Vector2 startCapScale = new(startScale / spriteBitmaps[2].Width, startScale / spriteBitmaps[2].Height),
                endCapScale = new(endScale / spriteBitmaps[2].Width, endScale / spriteBitmaps[2].Height);

            var capOffset = OrientedCaps ? new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * CapOverlap : Vector2.Zero;

            if (OrientedCaps)
            {
                startCapScale.X *= .5f;
                endCapScale.X *= .5f;
            }

            genStartCap.Add(new()
            {
                Time = time,
                Position = new Vector2(startVector.X, startVector.Y) + capOffset,
                Scale = startCapScale,
                Rotation = OrientedCaps ? rotation + Math.PI : 0,
                Color = object3dState.Color,
                Opacity = startScale > .5 ? opacity : 0,
                Additive = Additive
            });
            genEndCap.Add(new()
            {
                Time = time,
                Position = new Vector2(endVector.X, endVector.Y) - capOffset,
                Scale = endCapScale,
                Rotation = OrientedCaps ? rotation + Math.PI : 0,
                Color = object3dState.Color,
                Opacity = endScale > .5 ? opacity : 0,
                Additive = Additive,
                FlipH = OrientedCaps
            });
        }
    }

    ///<inheritdoc/>
    public void DoTreeSprite(Action<OsbSprite> action) => finalize = action;

    ///<inheritdoc/>
    public void ConfigureGenerators(Action<CommandGenerator> action)
    {
        action(genBody);
        action(genBottomEdge);
        action(genEndCap);
        action(genStartCap);
        action(genTopEdge);
    }

    ///<inheritdoc/>
    public override void GenerateCommands(Action<Action, OsbSprite> action, double? startTime, double? endTime, double timeOffset, bool loopable)
    {
        if (finalize is not null) action += (createCommands, sprite) =>
        {
            createCommands();
            finalize(sprite);
        };

        genBody.GenerateCommands(spriteBody, action, startTime, endTime, timeOffset, loopable);
        if (SpritePathEdge is not null)
        {
            genTopEdge.GenerateCommands(spriteTopEdge, action, startTime, endTime, timeOffset, loopable);
            genBottomEdge.GenerateCommands(spriteBottomEdge, action, startTime, endTime, timeOffset, loopable);
        }
        if (SpritePathCap is not null)
        {
            if (EnableStartCap) genStartCap.GenerateCommands(spriteStartCap, action, startTime, endTime, timeOffset, loopable);
            if (EnableEndCap) genEndCap.GenerateCommands(spriteEndCap, action, startTime, endTime, timeOffset, loopable);
        }
    }
}