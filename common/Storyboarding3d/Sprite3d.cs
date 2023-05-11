using OpenTK;
using StorybrewCommon.Animations;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Storyboarding.Util;
using System;
using System.Collections.Generic;
using System.IO;

namespace StorybrewCommon.Storyboarding3d
{
    ///<summary> Represents a basic <see cref="OsbSprite"/> with 3D functionality. </summary>
    public class Sprite3d : Node3d, HasOsbSprites
    {
        OsbSprite sprite;
        Action<OsbSprite> finalize;

        ///<inheritdoc/>
        public IEnumerable<OsbSprite> Sprites { get { yield return sprite; } }

        ///<summary> The path to the image of this <see cref="Sprite3d"/>. </summary>
        public string SpritePath;

        ///<summary> The <see cref="OsbOrigin"/> of this <see cref="Sprite3d"/>. </summary>
        public OsbOrigin SpriteOrigin = OsbOrigin.Centre;

        ///<summary> Toggles additive blending on this <see cref="Sprite3d"/>. </summary>
        public bool Additive;

        ///<summary> Represents method of sprite rotation on this <see cref="Sprite3d"/>. </summary>
        public RotationMode RotationMode = RotationMode.UnitY;

        ///<summary> Whether to fade sprites based on distance from the <see cref="Camera"/>. </summary>
        public bool UseDistanceFade = true;

        ///<summary> If this value is not <see langword="null"/>, scales sprites based on this vector instead of distance from the <see cref="Camera"/>. </summary>
        public Vector2? UseDefaultScale = null;

        ///<summary> A keyframed value representing this sprite's scale keyframes. </summary>
        public readonly KeyframedValue<CommandScale> SpriteScale = new KeyframedValue<CommandScale>(InterpolatingFunctions.Scale, Vector2.One);

        ///<summary> A keyframed value representing this sprite's rotation keyframes. </summary>
        public readonly KeyframedValue<double> SpriteRotation = new KeyframedValue<double>(InterpolatingFunctions.DoubleAngle, 0);

        readonly CommandGenerator gen = new CommandGenerator();

        ///<inheritdoc/>
        public IEnumerable<CommandGenerator> CommandGenerators { get { yield return gen; } }

        ///<inheritdoc/>
        public void DoTreeSprite(Action<OsbSprite> action) => finalize = action;

        ///<inheritdoc/>
        public void ConfigureGenerators(Action<CommandGenerator> action) => action(gen);

        ///<inheritdoc/>
        public override void GenerateSprite(StoryboardSegment segment) => sprite = sprite ?? segment.CreateSprite(SpritePath, SpriteOrigin);

        ///<inheritdoc/>
        public override void GenerateStates(double time, CameraState cameraState, Object3dState object3dState)
        {
            var wvp = object3dState.WorldTransform * cameraState.ViewProjection;
            var screenPosition = cameraState.ToScreen(wvp, Vector3.Zero);

            var angle = 0d;
            switch (RotationMode)
            {
                case RotationMode.UnitX:
                {
                    var unitXPosition = cameraState.ToScreen(wvp, Vector3.UnitX);
                    var delta = unitXPosition - screenPosition;
                    angle += Math.Atan2(delta.Y, delta.X);
                    break;
                }
                case RotationMode.UnitY:
                {
                    var unitYPosition = cameraState.ToScreen(wvp, Vector3.UnitY);
                    var delta = unitYPosition - screenPosition;
                    angle += Math.Atan2(delta.Y, delta.X) - Math.PI / 2;
                    break;
                }
            }

            var previousState = gen.EndState;
            var rotation = InterpolatingFunctions.DoubleAngle(
                previousState?.Rotation ?? -SpriteRotation.ValueAt(previousState?.Time ?? time), angle, 1) + SpriteRotation.ValueAt(time);

            var scale = (Vector2)SpriteScale.ValueAt(time) *
                object3dState.WorldTransform.ExtractScale().Xy *
                (float)(cameraState.FocusDistance / screenPosition.W) *
                (float)cameraState.ResolutionScale;

            var opacity = screenPosition.W < 0 ? 0 : object3dState.Opacity;
            if (UseDistanceFade) opacity *= cameraState.OpacityAt(screenPosition.W);

            gen.Add(new State
            {
                Time = time,
                Position = screenPosition.Xy,
                Scale = UseDefaultScale ?? scale,
                Rotation = rotation,
                Color = object3dState.Color,
                Opacity = opacity,
                Additive = Additive
            });
        }

        ///<inheritdoc/>
        public override void GenerateCommands(Action<Action, OsbSprite> action, double? startTime, double? endTime, double timeOffset, bool loopable)
        {
            if (finalize != null) action += (createCommands, sprite) =>
            {
                createCommands();
                finalize(sprite);
            };
            gen.GenerateCommands(sprite, action, startTime, endTime, timeOffset, loopable);
        }
    }

    ///<summary> Represents the rotation method for a 3D sprite. </summary>
    public enum RotationMode
    {
        ///<summary> No rotation is done. </summary>
        Fixed,

        ///<summary> Rotation is applied on the X-axis. </summary>
        UnitX,

        ///<summary> Rotation is applied on the Y-axis. </summary>
        UnitY
    }
}