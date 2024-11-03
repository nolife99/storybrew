using System;
using System.Drawing;
using System.Numerics;
using StorybrewCommon.Animations;
using StorybrewCommon.Mapset;

namespace StorybrewCommon.Storyboarding3d;

#pragma warning disable CS1591
public abstract class Camera
{
    public static SizeF Resolution = new(1366, 768);
    public static float ResolutionScale = OsuHitObject.WidescreenStoryboardSize.Height / Resolution.Height;
    public static float AspectRatio = Resolution.Width / Resolution.Height;
    public float DistanceForHorizontalFov(float fov) => Resolution.Width / 2 / MathF.Tan(osuTK.MathHelper.DegreesToRadians(fov) / 2);
    public float DistanceForVerticalFov(float fov) => Resolution.Height / 2 / MathF.Tan(osuTK.MathHelper.DegreesToRadians(fov) / 2);
    public abstract CameraState StateAt(double time);
}
public class CameraState(Matrix4x4 viewProjection, float aspectRatio, float focusDistance, float resolutionScale, float nearClip, float nearFade, float farFade, float farClip)
{
    public readonly Matrix4x4 ViewProjection = viewProjection;
    public readonly float AspectRatio = aspectRatio, FocusDistance = focusDistance, ResolutionScale = resolutionScale, NearClip = nearClip, NearFade = nearFade, FarFade = farFade, FarClip = farClip;

    public static Vector4 ToScreen(Matrix4x4 transform, Vector3 point)
    {
        var offset = (OsuHitObject.WidescreenStoryboardSize.Width - OsuHitObject.StoryboardSize.Width) / 2;

        var transformedPoint = Vector4.Transform(new Vector4(point, 1), transform);
        var ndc = new Vector2(transformedPoint.X, transformedPoint.Y) / Math.Abs(transformedPoint.W);

        var screenPosition = (ndc + Vector2.One) / 2 * new Vector2(OsuHitObject.WidescreenStoryboardSize.Width, OsuHitObject.WidescreenStoryboardSize.Height);
        var depth = transformedPoint.Z / transformedPoint.W;

        return new(screenPosition.X - offset, screenPosition.Y, depth, transformedPoint.W);
    }
    public double LinearizeZ(double z) => 2 * NearClip / (FarClip + NearClip - z * (FarClip - NearClip));
    public float OpacityAt(float distance)
    {
        if (distance < NearFade) return (float)Math.Max(0, Math.Min((distance - NearClip) / (NearFade - NearClip), 1));
        else if (distance > FarFade) return (float)Math.Max(0, Math.Min((FarClip - distance) / (FarClip - FarFade), 1));
        return 1;
    }
}
#pragma warning restore CS1591

///<summary> Represents a three-dimensional perspective camera. </summary>
public class PerspectiveCamera : Camera
{
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

    ///<summary> Represents the distance that close objects disappear at. </summary>
    public readonly KeyframedValue<float> NearClip = new(InterpolatingFunctions.Float);

    ///<summary> Represents the distance that close objects start fading at. </summary>
    public readonly KeyframedValue<float> NearFade = new(InterpolatingFunctions.Float);

    ///<summary> Represents the distance that distant objects start fading at. </summary>
    public readonly KeyframedValue<float> FarFade = new(InterpolatingFunctions.Float);

    ///<summary> Represents the distance that distant objects disappear at. </summary>
    public readonly KeyframedValue<float> FarClip = new(InterpolatingFunctions.Float);

    ///<summary> Represents the camera's horizontal field-of-view. </summary>
    public readonly KeyframedValue<float> HorizontalFov = new(InterpolatingFunctions.Float);

    ///<summary> Represents the camera's vertical field-of-view. </summary>
    public readonly KeyframedValue<float> VerticalFov = new(InterpolatingFunctions.Float);

    ///<summary> Returns the camera's state and information at <paramref name="time"/>. </summary>
    public override CameraState StateAt(double time)
    {
        var aspectRatio = AspectRatio;
        Vector3 cameraPosition = new(PositionX.ValueAt(time), PositionY.ValueAt(time), PositionZ.ValueAt(time));
        var targetPosition = TargetPosition.ValueAt(time);
        var up = Up.ValueAt(time) * (1 / Up.ValueAt(time).Length());

        float fovY;
        if (HorizontalFov.Count > 0)
        {
            var fovX = osuTK.MathHelper.DegreesToRadians(HorizontalFov.ValueAt(time));
            fovY = 2 * MathF.Atan(MathF.Tan(fovX / 2) / aspectRatio);
        }
        else fovY = VerticalFov.Count > 0 ? osuTK.MathHelper.DegreesToRadians(VerticalFov.ValueAt(time)) :
            2 * MathF.Atan(Resolution.Height / 2 / Math.Max(.0001f, (cameraPosition - targetPosition).Length()));

        var focusDistance = Resolution.Height / 2 / MathF.Tan(fovY / 2);
        var nearClip = NearClip.Count > 0 ? NearClip.ValueAt(time) : Math.Min(focusDistance / 2, 1);
        var farClip = FarClip.Count > 0 ? FarClip.ValueAt(time) : focusDistance * 1.5f;

        var nearFade = NearFade.Count > 0 ? NearFade.ValueAt(time) : nearClip;
        var farFade = FarFade.Count > 0 ? FarFade.ValueAt(time) : farClip;

        var view = Matrix4x4.CreateLookAt(cameraPosition, targetPosition, up);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(fovY, aspectRatio, nearClip, farClip);

        return new(view * projection, aspectRatio, focusDistance, ResolutionScale, nearClip, nearFade, farFade, farClip);
    }
}