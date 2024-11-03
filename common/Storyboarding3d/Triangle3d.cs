using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using StorybrewCommon.Animations;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.Util;

namespace StorybrewCommon.Storyboarding3d;

///<summary> Represents two triangles which form complex 3D geometry. </summary>
public class Triangle3d : Node3d, HasOsbSprites
{
    Action<OsbSprite> finalize;
    OsbSprite sprite0, sprite1;

    ///<inheritdoc/>
    public IEnumerable<OsbSprite> Sprites => [sprite0, sprite1];

    ///<summary> The path to the image of this <see cref="Triangle3d"/>. </summary>
    public string SpritePath;

    ///<summary> Toggles additive blending on this <see cref="Triangle3d"/>. </summary>
    public bool Additive;

    ///<summary> Whether to fade sprites based on distance from the <see cref="Camera"/>. </summary>
    public bool UseDistanceFade = true;

    /// <summary> The position of the first vertex of the triangle. </summary>
    public readonly KeyframedValue<Vector3> Position0 = new(InterpolatingFunctions.Vector3);

    /// <summary> The position of the second vertex of the triangle. </summary>
    public readonly KeyframedValue<Vector3> Position1 = new(InterpolatingFunctions.Vector3);

    /// <summary> The position of the third vertex of the triangle. </summary>
    public readonly KeyframedValue<Vector3> Position2 = new(InterpolatingFunctions.Vector3);

    readonly CommandGenerator gen0 = new(), gen1 = new();

    ///<inheritdoc/>
    public IEnumerable<CommandGenerator> CommandGenerators => [gen0, gen1];

    int edgeIndex;

    ///<summary> The index of a vertex/edge to be fixed. </summary>
    public int FixedEdge = -1;

    SizeF spriteBitmap;

    ///<inheritdoc/>
    public override void GenerateSprite(StoryboardSegment segment)
    {
        sprite0 ??= segment.CreateSprite(SpritePath, OsbOrigin.BottomLeft);
        sprite1 ??= segment.CreateSprite(SpritePath, OsbOrigin.BottomRight);
        spriteBitmap = CommandGenerator.BitmapDimensions(SpritePath);
    }

    ///<inheritdoc/>
    public override void GenerateStates(double time, CameraState cameraState, Object3dState object3dState)
    {
        var wvp = object3dState.WorldTransform * cameraState.ViewProjection;

        if (FixedEdge >= 0) edgeIndex = FixedEdge;

        Vector4 vector0, vector1, vector2;
        switch (edgeIndex)
        {
            case 0:
                {
                    vector0 = CameraState.ToScreen(wvp, Position0.ValueAt(time));
                    vector1 = CameraState.ToScreen(wvp, Position1.ValueAt(time));
                    vector2 = CameraState.ToScreen(wvp, Position2.ValueAt(time));
                    break;
                }
            case 1:
                {
                    vector2 = CameraState.ToScreen(wvp, Position0.ValueAt(time));
                    vector0 = CameraState.ToScreen(wvp, Position1.ValueAt(time));
                    vector1 = CameraState.ToScreen(wvp, Position2.ValueAt(time));
                    break;
                }
            case 2:
                {
                    vector1 = CameraState.ToScreen(wvp, Position0.ValueAt(time));
                    vector2 = CameraState.ToScreen(wvp, Position1.ValueAt(time));
                    vector0 = CameraState.ToScreen(wvp, Position2.ValueAt(time));
                    break;
                }
            default: throw new InvalidOperationException();
        }

        var cross = (vector2.X - vector0.X) * (vector1.Y - vector0.Y) - (vector2.Y - vector0.Y) * (vector1.X - vector0.X);
        if (cross > 0)
        {
            if (gen0.EndState is not null) gen0.EndState.Opacity = 0;
            if (gen1.EndState is not null) gen1.EndState.Opacity = 0;
            return;
        }

        var switchedEdge = false;
        for (var i = 0; i < 3; ++i)
        {
            Vector2 delta = new(vector2.X - vector0.X, vector2.Y - vector0.Y);
            var deltaLength = delta.Length();
            var normalizedDelta = delta / deltaLength;

            Vector2 delta2 = new(vector1.X - vector0.X, vector1.Y - vector0.Y);
            var dot = Vector2.Dot(normalizedDelta, delta2);

            if (dot <= 0 || dot > deltaLength)
            {
                if (FixedEdge >= 0)
                {
                    if (gen0.EndState is not null) gen0.EndState.Opacity = 0;
                    if (gen1.EndState is not null) gen1.EndState.Opacity = 0; break;
                }

                var temp = vector0;
                vector0 = vector1;
                vector1 = vector2;
                vector2 = temp;
                edgeIndex = (edgeIndex + 1) % 3;
                switchedEdge = true;
                continue;
            }

            var position = project(new(vector0.X, vector0.Y), new(vector2.X, vector2.Y), new(vector1.X, vector1.Y));
            Vector2 scale0 = new((new Vector2(vector2.X, vector2.Y) - position).Length() / spriteBitmap.Width, (new Vector2(vector1.X, vector1.Y) - position).Length() / spriteBitmap.Height),
                scale1 = new((new Vector2(vector0.X, vector0.Y) - position).Length() / spriteBitmap.Width, scale0.Y);

            var angle = Math.Atan2(delta.Y, delta.X);
            var rotation = InterpolatingFunctions.DoubleAngle(gen0.EndState?.Rotation ?? 0, angle, 1);

            var opacity = vector0.W < 0 && vector1.W < 0 && vector2.W < 0 ? 0 : object3dState.Opacity;
            if (UseDistanceFade) opacity *= (cameraState.OpacityAt(vector0.W) + cameraState.OpacityAt(vector1.W) + cameraState.OpacityAt(vector2.W)) / 3;

            if (switchedEdge)
            {
                if (gen0.EndState is not null) gen0.EndState.Opacity = 0;
                if (gen1.EndState is not null) gen1.EndState.Opacity = 0;
            }

            gen0.Add(new()
            {
                Time = time,
                Position = position,
                Scale = scale0,
                Rotation = rotation,
                Color = object3dState.Color,
                Opacity = switchedEdge ? 0 : opacity,
                Additive = Additive
            });
            gen1.Add(new()
            {
                Time = time,
                Position = position,
                Scale = scale1,
                Rotation = rotation,
                Color = object3dState.Color,
                Opacity = switchedEdge ? 0 : opacity,
                Additive = Additive,
                FlipH = true
            });
            break;
        }
    }

    ///<inheritdoc/>
    public void DoTreeSprite(Action<OsbSprite> action) => finalize = action;

    ///<inheritdoc/>
    public void ConfigureGenerators(Action<CommandGenerator> action)
    {
        action(gen0);
        action(gen1);
    }

    ///<inheritdoc/>
    public override void GenerateCommands(Action<Action, OsbSprite> action, double? startTime, double? endTime, double timeOffset, bool loopable)
    {
        if (finalize is not null) action += (createCommands, sprite) =>
        {
            createCommands();
            finalize(sprite);
        };
        gen0.GenerateCommands(sprite0, action, startTime, endTime, timeOffset, loopable);
        gen1.GenerateCommands(sprite1, action, startTime, endTime, timeOffset, loopable);
    }
    static Vector2 project(Vector2 line1, Vector2 line2, Vector2 toProject)
    {
        var m = (line2.Y - line1.Y) / (line2.X - line1.X);
        var b = line1.Y - (m * line1.X);

        return new((m * toProject.Y + toProject.X - m * b) / (m * m + 1), (m * m * toProject.Y + m * toProject.X + b) / (m * m + 1));
    }
}