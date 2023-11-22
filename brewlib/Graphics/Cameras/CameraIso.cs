using System.Drawing;
using System.Numerics;

namespace BrewLib.Graphics.Cameras
{
    public class CameraIso : CameraBase
    {
        Vector3 target = Vector3.Zero;
        public Vector3 Target
        {
            get => target;
            set
            {
                if (target == value) return;
                target = value;
                Invalidate();
            }
        }
        public CameraIso()
        {
            NearPlane = -1000;
            FarPlane = 1000;
        }

        protected override void Recalculate(out Matrix4x4 view, out Matrix4x4 projection, out Rectangle internalViewport, out Rectangle extendedViewport)
        {
            var screenViewport = Viewport;

            var distanceSqrt = .57735026919f;
            Forward = new Vector3(distanceSqrt, -distanceSqrt, -distanceSqrt);
            Position = target - Forward;
            Up = DefaultUp;

            internalViewport = extendedViewport = screenViewport;
            projection = Matrix4x4.CreateOrthographicOffCenter(
                -screenViewport.Width * .5f, screenViewport.Width * .5f,
                -(screenViewport.Height * .5f), screenViewport.Height * .5f,
                NearPlane, FarPlane);

            view = Matrix4x4.CreateLookAt(Position, Position + Forward, Up);
        }
    }
}