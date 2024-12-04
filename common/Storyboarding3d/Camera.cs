namespace StorybrewCommon.Storyboarding3d;

using System;
using System.Numerics;
using Animations;
using Mapset;

#pragma warning disable CS1591
public abstract class Camera
{
    public static readonly Vector2 Resolution = new(1366, 768);
    public static readonly float ResolutionScale = OsuHitObject.WidescreenStoryboardSize.Height / Resolution.Y;
    public static readonly float AspectRatio = Resolution.X / Resolution.Y;

    public abstract CameraState StateAt(float time);
}

public record CameraState(Matrix4x4 ViewProjection,
    float AspectRatio,
    float FocusDistance,
    float ResolutionScale,
    float NearClip,
    float NearFade,
    float FarFade,
    float FarClip)
{
    public static Vector4 ToScreen(Matrix4x4 transform, Vector3 point)
    {
        var transformed = Vector4.Transform(new Vector4(point, 1), transform);
        var screenPosition = (new Vector2(transformed.X, transformed.Y) / Math.Abs(transformed.W) + Vector2.One) / 2 *
            new Vector2(OsuHitObject.WidescreenStoryboardSize.Width, OsuHitObject.WidescreenStoryboardSize.Height);

        return new(screenPosition.X - (OsuHitObject.WidescreenStoryboardSize.Width - OsuHitObject.StoryboardSize.Width) / 2,
            screenPosition.Y, transformed.Z / transformed.W, transformed.W);
    }
    public float OpacityAt(float distance)
    {
        if (distance < NearFade) return Math.Clamp((distance - NearClip) / (NearFade - NearClip), 0, 1);
        if (distance > FarFade) return Math.Clamp((FarClip - distance) / (FarClip - FarFade), 0, 1);
        return 1;
    }
}
#pragma warning restore CS1591
///<summary> Represents a three-dimensional perspective camera. </summary>
public class PerspectiveCamera : Camera
{
    ///<summary> Represents the distance that distant objects disappear at. </summary>
    public readonly KeyframedValue<float> FarClip = new(InterpolatingFunctions.Float);

    ///<summary> Represents the distance that distant objects start fading at. </summary>
    public readonly KeyframedValue<float> FarFade = new(InterpolatingFunctions.Float);

    ///<summary> Represents the camera's horizontal field-of-view. </summary>
    public readonly KeyframedValue<float> HorizontalFov = new(InterpolatingFunctions.Float);

    ///<summary> Represents the distance that close objects disappear at. </summary>
    public readonly KeyframedValue<float> NearClip = new(InterpolatingFunctions.Float);

    ///<summary> Represents the distance that close objects start fading at. </summary>
    public readonly KeyframedValue<float> NearFade = new(InterpolatingFunctions.Float);

    ///<summary> Represents the camera's X-position in the 3D world. </summary>
    public readonly KeyframedValue<float> PositionX = new(InterpolatingFunctions.Float);

    ///<summary> Represents the camera's Y-position in the 3D world. </summary>
    public readonly KeyframedValue<float> PositionY = new(InterpolatingFunctions.Float);

    ///<summary> Represents the camera's Z-position in the 3D world. </summary>
    public readonly KeyframedValue<float> PositionZ = new(InterpolatingFunctions.Float);

    ///<summary> Represents the 3D-position the camera is facing towards. </summary>
    public readonly KeyframedValue<Vector3> TargetPosition = new(InterpolatingFunctions.Vector3);

    ///<summary> Represents the camera's up vector. </summary>
    public readonly KeyframedValue<Vector3> Up = new(InterpolatingFunctions.Vector3, new(0, 1, 0));

    ///<summary> Represents the camera's vertical field-of-view. </summary>
    public readonly KeyframedValue<float> VerticalFov = new(InterpolatingFunctions.Float);

    /// <summary> Returns the camera's state and information at <paramref name="time"/>. </summary>
    public override CameraState StateAt(float time)
    {
        var aspectRatio = AspectRatio;
        Vector3 cameraPosition = new(PositionX.ValueAt(time), PositionY.ValueAt(time), PositionZ.ValueAt(time));
        var targetPosition = TargetPosition.ValueAt(time);

        float fovY;
        if (HorizontalFov.Count > 0)
            fovY = 2 * float.Atan(float.Tan(float.DegreesToRadians(HorizontalFov.ValueAt(time)) * .5f) / aspectRatio);
        else
            fovY = VerticalFov.Count > 0 ?
                float.DegreesToRadians(VerticalFov.ValueAt(time)) :
                2 * float.Atan(Resolution.Y * .5f / Math.Max(.0001f, (cameraPosition - targetPosition).Length()));

        var focusDistance = Resolution.Y * .5f / float.Tan(fovY * .5f);
        var nearClip = NearClip.Count > 0 ? NearClip.ValueAt(time) : Math.Min(focusDistance * .5f, 1);
        var farClip = FarClip.Count > 0 ? FarClip.ValueAt(time) : focusDistance * 1.5f;

        var view = Matrix4x4.CreateLookAt(cameraPosition, targetPosition, Up.ValueAt(time) * (1 / Up.ValueAt(time).Length()));
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(fovY, aspectRatio, nearClip, farClip);

        return new(Matrix4x4.Multiply(view, projection), aspectRatio, focusDistance, ResolutionScale, nearClip,
            NearFade.Count > 0 ? NearFade.ValueAt(time) : nearClip, FarFade.Count > 0 ? FarFade.ValueAt(time) : farClip, farClip);
    }
}