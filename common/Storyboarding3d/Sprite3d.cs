namespace StorybrewCommon.Storyboarding3d;

using System;
using System.Collections.Generic;
using System.Numerics;
using Animations;
using Storyboarding;
using Storyboarding.CommandValues;
using Storyboarding.Util;

/// <summary> Represents a basic <see cref="OsbSprite"/> with 3D functionality. </summary>
public class Sprite3d : Node3d, HasOsbSprites
{
    readonly CommandGenerator gen = new();

    ///<summary> A keyframed value representing this sprite's rotation keyframes. </summary>
    public readonly KeyframedValue<float> SpriteRotation = new(InterpolatingFunctions.FloatAngle);

    ///<summary> A keyframed value representing this sprite's scale keyframes. </summary>
    public readonly KeyframedValue<CommandScale> SpriteScale = new(InterpolatingFunctions.Scale, Vector2.One);

    /// <summary> Toggles additive blending on this <see cref="Sprite3d"/>. </summary>
    public bool Additive;

    Action<OsbSprite> finalize;

    /// <summary> Represents method of sprite rotation on this <see cref="Sprite3d"/>. </summary>
    public RotationMode RotationMode = RotationMode.UnitY;

    OsbSprite sprite;

    /// <summary> The <see cref="OsbOrigin"/> of this <see cref="Sprite3d"/>. </summary>
    public OsbOrigin SpriteOrigin = OsbOrigin.Centre;

    /// <summary> The path to the image of this <see cref="Sprite3d"/>. </summary>
    public string SpritePath;

    /// <summary>
    ///     If this value is not <see langword="null"/>, scales sprites based on this vector instead of distance from
    ///     the <see cref="Camera"/>.
    /// </summary>
    public CommandScale? UseDefaultScale;

    /// <summary> Whether to fade sprites based on distance from the <see cref="Camera"/>. </summary>
    public bool UseDistanceFade = true;

    /// <inheritdoc/>
    public IEnumerable<OsbSprite> Sprites { get { yield return sprite; } }

    /// <inheritdoc/>
    public IEnumerable<CommandGenerator> CommandGenerators { get { yield return gen; } }

    /// <inheritdoc/>
    public void DoTreeSprite(Action<OsbSprite> action) => finalize = action;

    /// <inheritdoc/>
    public void ConfigureGenerators(Action<CommandGenerator> action) => action(gen);

    /// <inheritdoc/>
    public override void GenerateSprite(StoryboardSegment segment) => sprite ??= segment.CreateSprite(SpritePath, SpriteOrigin);

    /// <inheritdoc/>
    public override void GenerateStates(float time, CameraState cameraState, Object3dState object3dState)
    {
        var wvp = Matrix4x4.Multiply(object3dState.WorldTransform, cameraState.ViewProjection);
        var screenPosition = CameraState.ToScreen(wvp, Vector3.Zero);

        var angle = 0f;
        switch (RotationMode)
        {
            case RotationMode.UnitX:
            {
                var delta = CameraState.ToScreen(wvp, Vector3.UnitX) - screenPosition;
                angle += float.Atan2(delta.Y, delta.X);
                break;
            }
            case RotationMode.UnitY:
            {
                var delta = CameraState.ToScreen(wvp, Vector3.UnitY) - screenPosition;
                angle += float.Atan2(delta.Y, delta.X) - float.Pi * .5f;
                break;
            }
        }

        var scale = (Vector2)SpriteScale.ValueAt(time) *
            new Vector2(
                new Vector3(object3dState.WorldTransform.M11, object3dState.WorldTransform.M12, object3dState.WorldTransform.M13)
                    .Length(),
                new Vector3(object3dState.WorldTransform.M21, object3dState.WorldTransform.M22, object3dState.WorldTransform.M23)
                    .Length()) *
            (cameraState.FocusDistance / screenPosition.W) *
            cameraState.ResolutionScale;

        var opacity = screenPosition.W < 0 ? 0 : object3dState.Opacity;
        if (UseDistanceFade) opacity *= cameraState.OpacityAt(screenPosition.W);

        gen.Add(new()
        {
            Time = time,
            Position = new(screenPosition.X, screenPosition.Y),
            Scale = UseDefaultScale ?? scale,
            Rotation = angle + SpriteRotation.ValueAt(time),
            Color = object3dState.Color,
            Opacity = opacity,
            Additive = Additive
        });
    }

    /// <inheritdoc/>
    public override void GenerateCommands(Action<Action, OsbSprite> action,
        float? startTime,
        float? endTime,
        float timeOffset,
        bool loopable)
    {
        if (finalize is not null)
            action += (createCommands, sprite) =>
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