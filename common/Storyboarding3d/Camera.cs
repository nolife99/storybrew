namespace StorybrewCommon.Storyboarding3d;

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

public readonly record struct CameraState(Matrix4x4 ViewProjection,
    float FocusDistance,
    float ResolutionScale,
    float NearClip,
    float NearFade,
    float FarFade,
    float FarClip)
{
    public static Vector4 ToScreen(scoped ref readonly Matrix4x4 transform, Vector3 point)
    {
        var transformed = Vector4.Transform(new Vector4(point, 1), transform);
        var screenPosition = (transformed.AsVector2() / float.Abs(transformed.W) + Vector2.One) * .5f *
            new Vector2(OsuHitObject.WidescreenStoryboardSize.Width, OsuHitObject.WidescreenStoryboardSize.Height);

        return new(screenPosition.X -
            (OsuHitObject.WidescreenStoryboardSize.Width - OsuHitObject.StoryboardSize.Width) / 2,
            screenPosition.Y,
            transformed.Z / transformed.W,
            transformed.W);
    }

    public float OpacityAt(float distance)
        => distance < NearFade ? float.Clamp((distance - NearClip) / (NearFade - NearClip), 0, 1) :
            distance > FarFade ? float.Clamp((FarClip - distance) / (FarClip - FarFade), 0, 1) : 1;
}
#pragma warning restore CS1591
///<summary> Represents a three-dimensional perspective camera. </summary>
public class PerspectiveCamera : Camera
{
    ///<summary> Represents the distance that distant objects disappear at. </summary>
    public readonly KeyframedValue<float> FarClip = new(float.Lerp);

    ///<summary> Represents the distance that distant objects start fading at. </summary>
    public readonly KeyframedValue<float> FarFade = new(float.Lerp);

    ///<summary> Represents the camera's horizontal field-of-view. </summary>
    public readonly KeyframedValue<float> HorizontalFov = new(float.Lerp);

    ///<summary> Represents the distance that close objects disappear at. </summary>
    public readonly KeyframedValue<float> NearClip = new(float.Lerp);

    ///<summary> Represents the distance that close objects start fading at. </summary>
    public readonly KeyframedValue<float> NearFade = new(float.Lerp);

    ///<summary> Represents the camera's X-position in the 3D world. </summary>
    public readonly KeyframedValue<float> PositionX = new(float.Lerp);

    ///<summary> Represents the camera's Y-position in the 3D world. </summary>
    public readonly KeyframedValue<float> PositionY = new(float.Lerp);

    ///<summary> Represents the camera's Z-position in the 3D world. </summary>
    public readonly KeyframedValue<float> PositionZ = new(float.Lerp);

    ///<summary> Represents the 3D-position the camera is facing towards. </summary>
    public readonly KeyframedValue<Vector3> TargetPosition = new(Vector3.Lerp);

    ///<summary> Represents the camera's up vector. </summary>
    public readonly KeyframedValue<Vector3> Up = new(Vector3.Lerp, new(0, 1, 0));

    ///<summary> Represents the camera's vertical field-of-view. </summary>
    public readonly KeyframedValue<float> VerticalFov = new(float.Lerp);

    /// <summary> Returns the camera's state and information at <paramref name="time"/>. </summary>
    public override CameraState StateAt(float time)
    {
        var aspectRatio = AspectRatio;
        Vector3 cameraPosition = new(PositionX.ValueAt(time), PositionY.ValueAt(time), PositionZ.ValueAt(time));
        var targetPosition = TargetPosition.ValueAt(time);

        var fovY = HorizontalFov.Count > 0 ?
            2 * float.Atan(float.Tan(float.DegreesToRadians(HorizontalFov.ValueAt(time)) * .5f) / aspectRatio) :
            VerticalFov.Count > 0 ? float.DegreesToRadians(VerticalFov.ValueAt(time)) :
                2 * float.Atan(Resolution.Y * .5f / float.Max(.0001f, (cameraPosition - targetPosition).Length()));

        var focusDistance = Resolution.Y * .5f / float.Tan(fovY * .5f);
        var nearClip = NearClip.Count > 0 ? NearClip.ValueAt(time) : float.Min(focusDistance * .5f, 1);
        var farClip = FarClip.Count > 0 ? FarClip.ValueAt(time) : focusDistance * 1.5f;

        return new(
            Matrix4x4.CreateLookAt(cameraPosition, targetPosition, Up.ValueAt(time) * (1 / Up.ValueAt(time).Length())) *
            Matrix4x4.CreatePerspectiveFieldOfView(fovY, aspectRatio, nearClip, farClip),
            focusDistance,
            ResolutionScale,
            nearClip,
            NearFade.Count > 0 ? NearFade.ValueAt(time) : nearClip,
            FarFade.Count > 0 ? FarFade.ValueAt(time) : farClip,
            farClip);
    }
}