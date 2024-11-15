namespace StorybrewCommon.Storyboarding3d;

using System;
using System.Drawing;
using System.Numerics;
using Animations;
using Mapset;
using MathHelper = OpenTK.Mathematics.MathHelper;

#pragma warning disable CS1591
public abstract class Camera
{
    public static SizeF Resolution = new(1366, 768);
    public static float ResolutionScale = OsuHitObject.WidescreenStoryboardSize.Height / Resolution.Height;
    public static float AspectRatio = Resolution.Width / Resolution.Height;

    public abstract CameraState StateAt(float time);
}

public class CameraState(Matrix4x4 viewProjection,
    float aspectRatio,
    float focusDistance,
    float resolutionScale,
    float nearClip,
    float nearFade,
    float farFade,
    float farClip)
{
    public float AspectRatio => aspectRatio;
    public float FocusDistance => focusDistance;
    public float ResolutionScale => resolutionScale;
    public float NearClip => nearClip;
    public float NearFade => nearFade;
    public float FarFade => farFade;
    public float FarClip => farClip;
    public Matrix4x4 ViewProjection => viewProjection;

    public static Vector4 ToScreen(Matrix4x4 transform, Vector3 point)
    {
        var transformedPoint = Vector4.Transform(new Vector4(point, 1), transform);
        var screenPosition = (new Vector2(transformedPoint.X, transformedPoint.Y) / Math.Abs(transformedPoint.W) + Vector2.One) /
            2 * new Vector2(OsuHitObject.WidescreenStoryboardSize.Width, OsuHitObject.WidescreenStoryboardSize.Height);

        return new(screenPosition.X - (OsuHitObject.WidescreenStoryboardSize.Width - OsuHitObject.StoryboardSize.Width) / 2,
            screenPosition.Y, transformedPoint.Z / transformedPoint.W, transformedPoint.W);
    }
    public float OpacityAt(float distance)
    {
        if (distance < NearFade) return Math.Max(0, Math.Min((distance - NearClip) / (NearFade - NearClip), 1));
        if (distance > FarFade) return Math.Max(0, Math.Min((FarClip - distance) / (FarClip - FarFade), 1));
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
        {
            var fovX = MathHelper.DegreesToRadians(HorizontalFov.ValueAt(time));
            fovY = 2 * MathF.Atan(MathF.Tan(fovX / 2) / aspectRatio);
        }
        else
            fovY = VerticalFov.Count > 0 ?
                MathHelper.DegreesToRadians(VerticalFov.ValueAt(time)) :
                2 * MathF.Atan(Resolution.Height / 2 / Math.Max(.0001f, (cameraPosition - targetPosition).Length()));

        var focusDistance = Resolution.Height / 2 / MathF.Tan(fovY / 2);
        var nearClip = NearClip.Count > 0 ? NearClip.ValueAt(time) : Math.Min(focusDistance / 2, 1);
        var farClip = FarClip.Count > 0 ? FarClip.ValueAt(time) : focusDistance * 1.5f;

        var view = Matrix4x4.CreateLookAt(cameraPosition, targetPosition, Up.ValueAt(time) * (1 / Up.ValueAt(time).Length()));
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(fovY, aspectRatio, nearClip, farClip);

        return new(view * projection, aspectRatio, focusDistance, ResolutionScale, nearClip,
            NearFade.Count > 0 ? NearFade.ValueAt(time) : nearClip, FarFade.Count > 0 ? FarFade.ValueAt(time) : farClip, farClip);
    }
}