using StorybrewCommon.Animations;
using StorybrewCommon.Mapset;
using System.Numerics;
using System;

namespace StorybrewCommon.Storyboarding3d
{
#pragma warning disable CS1591
    public abstract class Camera
    {
        public Vector2 Resolution = new Vector2(1366, 768);
        public double ResolutionScale => OsuHitObject.StoryboardSize.Y / Resolution.Y;
        public double AspectRatio => Resolution.X / Resolution.Y;
        public float DistanceForHorizontalFov(double fov) => (float)(Resolution.X * .5 / Math.Tan(OpenTK.MathHelper.DegreesToRadians(fov) * .5));
        public float DistanceForVerticalFov(double fov) => (float)(Resolution.Y * .5 / Math.Tan(OpenTK.MathHelper.DegreesToRadians(fov) * .5));
        public abstract CameraState StateAt(double time);
    }
    public class CameraState
    {
        public readonly Matrix4x4 ViewProjection;
        public readonly double AspectRatio, FocusDistance, ResolutionScale, NearClip, NearFade, FarFade, FarClip;

        public CameraState(Matrix4x4 viewProjection, double aspectRatio, double focusDistance, double resolutionScale, double nearClip, double nearFade, double farFade, double farClip)
        {
            ViewProjection = viewProjection;
            AspectRatio = aspectRatio;
            FocusDistance = focusDistance;
            ResolutionScale = resolutionScale;
            NearClip = nearClip;
            NearFade = nearFade;
            FarFade = farFade;
            FarClip = farClip;
        }
        public static Vector4 ToScreen(Matrix4x4 transform, Vector3 point)
        {
            var offset = (OsuHitObject.WidescreenStoryboardSize.X - OsuHitObject.StoryboardSize.X) * .5f;

            var transformedPoint = Vector4.Transform(new Vector4(point, 1), transform);
            var ndc = new OpenTK.Vector2(transformedPoint.X, transformedPoint.Y) / Math.Abs(transformedPoint.W);

            var screenPosition = (ndc + OpenTK.Vector2.One) * .5f * OsuHitObject.WidescreenStoryboardSize;
            var depth = transformedPoint.Z / transformedPoint.W;

            return new Vector4(screenPosition.X - offset, screenPosition.Y, depth, transformedPoint.W);
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
        public readonly KeyframedValue<float> PositionX = new KeyframedValue<float>(InterpolatingFunctions.Float);

        ///<summary> Represents the camera's Y-position in the 3D world. </summary>
        public readonly KeyframedValue<float> PositionY = new KeyframedValue<float>(InterpolatingFunctions.Float);

        ///<summary> Represents the camera's Z-position in the 3D world. </summary>
        public readonly KeyframedValue<float> PositionZ = new KeyframedValue<float>(InterpolatingFunctions.Float);

        ///<summary> Represents the 3D-position the camera is facing towards. </summary>
        public readonly KeyframedValue<Vector3> TargetPosition = new KeyframedValue<Vector3>(InterpolatingFunctions.Vector3);

        ///<summary> Represents the camera's up vector. </summary>
        public readonly KeyframedValue<Vector3> Up = new KeyframedValue<Vector3>(InterpolatingFunctions.Vector3, new Vector3(0, 1, 0));

        ///<summary> Represents the distance that close objects disappear at. </summary>
        public readonly KeyframedValue<float> NearClip = new KeyframedValue<float>(InterpolatingFunctions.Float);

        ///<summary> Represents the distance that close objects start fading at. </summary>
        public readonly KeyframedValue<float> NearFade = new KeyframedValue<float>(InterpolatingFunctions.Float);

        ///<summary> Represents the distance that distant objects start fading at. </summary>
        public readonly KeyframedValue<float> FarFade = new KeyframedValue<float>(InterpolatingFunctions.Float);

        ///<summary> Represents the distance that distant objects disappear at. </summary>
        public readonly KeyframedValue<float> FarClip = new KeyframedValue<float>(InterpolatingFunctions.Float);

        ///<summary> Represents the camera's horizontal field-of-view. </summary>
        public readonly KeyframedValue<float> HorizontalFov = new KeyframedValue<float>(InterpolatingFunctions.Float);

        ///<summary> Represents the camera's vertical field-of-view. </summary>
        public readonly KeyframedValue<float> VerticalFov = new KeyframedValue<float>(InterpolatingFunctions.Float);

        ///<summary> Returns the camera's state and information at <paramref name="time"/>. </summary>
        public override CameraState StateAt(double time)
        {
            var aspectRatio = AspectRatio;
            var cameraPosition = new Vector3(PositionX.ValueAt(time), PositionY.ValueAt(time), PositionZ.ValueAt(time));
            var targetPosition = TargetPosition.ValueAt(time);
            var up = Up.ValueAt(time) * (1 / Up.ValueAt(time).Length());

            double fovY;
            if (HorizontalFov.Count > 0)
            {
                var fovX = OpenTK.MathHelper.DegreesToRadians(HorizontalFov.ValueAt(time));
                fovY = 2 * Math.Atan(Math.Tan(fovX * .5) / aspectRatio);
            }
            else
            {
                fovY = VerticalFov.Count > 0 ? OpenTK.MathHelper.DegreesToRadians(VerticalFov.ValueAt(time)) :
                2 * Math.Atan(Resolution.Y / 2D / Math.Max(.0001, (cameraPosition - targetPosition).Length()));
            }

            var focusDistance = Resolution.Y * .5 / Math.Tan(fovY * .5);
            var nearClip = NearClip.Count > 0 ? NearClip.ValueAt(time) : Math.Min(focusDistance * .5, 1);
            var farClip = FarClip.Count > 0 ? FarClip.ValueAt(time) : focusDistance * 1.5;

            var nearFade = NearFade.Count > 0 ? NearFade.ValueAt(time) : nearClip;
            var farFade = FarFade.Count > 0 ? FarFade.ValueAt(time) : farClip;

            var view = Matrix4x4.CreateLookAt(cameraPosition, targetPosition, up);
            var projection = Matrix4x4.CreatePerspectiveFieldOfView((float)fovY, (float)aspectRatio, (float)nearClip, (float)farClip);

            return new CameraState(view * projection, aspectRatio, focusDistance, ResolutionScale, nearClip, nearFade, farFade, farClip);
        }
    }
}