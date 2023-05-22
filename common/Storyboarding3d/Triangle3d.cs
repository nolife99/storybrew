using StorybrewCommon.Animations;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.Util;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Drawing;

namespace StorybrewCommon.Storyboarding3d
{
    ///<summary> Represents a triangle with 3D functionality. </summary>
    public class Triangle3d : Node3d, HasOsbSprites
    {
        Action<OsbSprite> finalize;
        OsbSprite sprite0, sprite1;
        
        ///<inheritdoc/>
        public IEnumerable<OsbSprite> Sprites { get { yield return sprite0; yield return sprite1; } }

        ///<summary> The path to the image of this <see cref="Triangle3d"/>. </summary>
        public string SpritePath;

        ///<summary> Toggles additive blending on this <see cref="Triangle3d"/>. </summary>
        public bool Additive;

        ///<summary> Whether to fade sprites based on distance from the <see cref="Camera"/>. </summary>
        public bool UseDistanceFade = true;

        /// <summary> The position of the first vertex of the triangle. </summary>
        public readonly KeyframedValue<Vector3> Position0 = new KeyframedValue<Vector3>(InterpolatingFunctions.Vector3);

        /// <summary> The position of the second vertex of the triangle. </summary>
        public readonly KeyframedValue<Vector3> Position1 = new KeyframedValue<Vector3>(InterpolatingFunctions.Vector3);

        /// <summary> The position of the third vertex of the triangle. </summary>
        public readonly KeyframedValue<Vector3> Position2 = new KeyframedValue<Vector3>(InterpolatingFunctions.Vector3);

        readonly CommandGenerator Generator0 = new CommandGenerator(), Generator1 = new CommandGenerator();

        ///<inheritdoc/>
        public IEnumerable<CommandGenerator> CommandGenerators { get { yield return Generator0; yield return Generator1; } }

        int edgeIndex = 0;

        ///<summary> The index of a vertex/edge to be fixed. </summary>
        public int FixedEdge = -1;

        SizeF spriteBitmap;

        ///<inheritdoc/>
        public override void GenerateSprite(StoryboardSegment segment)
        {
            sprite0 = sprite0 ?? segment.CreateSprite(SpritePath, OsbOrigin.BottomLeft);
            sprite1 = sprite1 ?? segment.CreateSprite(SpritePath, OsbOrigin.BottomRight);
            spriteBitmap = CommandGenerator.BitmapDimensions(sprite0);
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
                    vector0 = cameraState.ToScreen(wvp, Position0.ValueAt(time));
                    vector1 = cameraState.ToScreen(wvp, Position1.ValueAt(time));
                    vector2 = cameraState.ToScreen(wvp, Position2.ValueAt(time));
                    break;
                }
                case 1:
                {
                    vector2 = cameraState.ToScreen(wvp, Position0.ValueAt(time));
                    vector0 = cameraState.ToScreen(wvp, Position1.ValueAt(time));
                    vector1 = cameraState.ToScreen(wvp, Position2.ValueAt(time));
                    break;
                }
                case 2:
                {
                    vector1 = cameraState.ToScreen(wvp, Position0.ValueAt(time));
                    vector2 = cameraState.ToScreen(wvp, Position1.ValueAt(time));
                    vector0 = cameraState.ToScreen(wvp, Position2.ValueAt(time));
                    break;
                }
                default: throw new InvalidOperationException();
            }

            var cross = (vector2.X - vector0.X) * (vector1.Y - vector0.Y) - (vector2.Y - vector0.Y) * (vector1.X - vector0.X);
            if (cross > 0)
            {
                if (Generator0.EndState != null) Generator0.EndState.Opacity = 0;
                if (Generator1.EndState != null) Generator1.EndState.Opacity = 0;
                return;
            }

            var switchedEdge = false;
            for (var i = 0; i < 3; ++i)
            {
                var delta = new Vector2(vector2.X, vector2.Y) - new Vector2(vector0.X, vector0.Y);
                var deltaLength = delta.Length();
                var normalizedDelta = delta / deltaLength;

                var delta2 = new Vector2(vector1.X, vector1.Y) - new Vector2(vector0.X, vector0.Y);
                var dot = Vector2.Dot(normalizedDelta, delta2);

                if (dot <= 0 || dot > deltaLength)
                {
                    if (FixedEdge >= 0)
                    {
                        if (Generator0.EndState != null) Generator0.EndState.Opacity = 0;
                        if (Generator1.EndState != null) Generator1.EndState.Opacity = 0; break;
                    }

                    var temp = vector0;
                    vector0 = vector1;
                    vector1 = vector2;
                    vector2 = temp;
                    edgeIndex = (edgeIndex + 1) % 3;
                    switchedEdge = true;
                    continue;
                }

                var position = project(new Vector2(vector0.X, vector0.Y), new Vector2(vector2.X, vector2.Y), new Vector2(vector1.X, vector1.Y));
                var scale0 = new Vector2((new Vector2(vector2.X, vector2.Y) - position).Length() / spriteBitmap.Width, (new Vector2(vector1.X, vector1.Y) - position).Length() / spriteBitmap.Height);
                var scale1 = new Vector2((new Vector2(vector0.X, vector0.Y) - position).Length() / spriteBitmap.Width, scale0.Y);

                var angle = Math.Atan2(delta.Y, delta.X);
                var rotation = InterpolatingFunctions.DoubleAngle(Generator0.EndState?.Rotation ?? 0, angle, 1);

                var opacity = vector0.W < 0 && vector1.W < 0 && vector2.W < 0 ? 0 : object3dState.Opacity;
                if (UseDistanceFade) opacity *= (cameraState.OpacityAt(vector0.W) + cameraState.OpacityAt(vector1.W) + cameraState.OpacityAt(vector2.W)) / 3;

                if (switchedEdge)
                {
                    if (Generator0.EndState != null) Generator0.EndState.Opacity = 0;
                    if (Generator1.EndState != null) Generator1.EndState.Opacity = 0;
                }

                Generator0.Add(new State
                {
                    Time = time,
                    Position = position,
                    Scale = scale0,
                    Rotation = rotation,
                    Color = object3dState.Color,
                    Opacity = switchedEdge ? 0 : opacity,
                    Additive = Additive
                });
                Generator1.Add(new State
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
            action(Generator0);
            action(Generator1);
        }

        ///<inheritdoc/>
        public override void GenerateCommands(Action<Action, OsbSprite> action, double? startTime, double? endTime, double timeOffset, bool loopable)
        {
            if (finalize != null) action += (createCommands, sprite) =>
            {
                createCommands();
                finalize(sprite);
            };
            Generator0.GenerateCommands(sprite0, action, startTime, endTime, timeOffset, loopable);
            Generator1.GenerateCommands(sprite1, action, startTime, endTime, timeOffset, loopable);
        }
        static Vector2 project(Vector2 line1, Vector2 line2, Vector2 toProject)
        {
            var m = (line2.Y - line1.Y) / (line2.X - line1.X);
            var b = line1.Y - (m * line1.X);

            return new Vector2((m * toProject.Y + toProject.X - m * b) / (m * m + 1), (m * m * toProject.Y + m * toProject.X + b) / (m * m + 1));
        }
    }
}