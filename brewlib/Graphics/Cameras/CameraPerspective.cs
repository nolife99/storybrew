using System;
using System.Drawing;
using System.Numerics;

namespace BrewLib.Graphics.Cameras
{
    public class CameraPerspective : CameraBase
    {
        float fieldOfView;
        public float FieldOfView
        {
            get => fieldOfView;
            set
            {
                if (fieldOfView == value) return;
                fieldOfView = value;
                Invalidate();
            }
        }
        public CameraPerspective()
        {
            FieldOfView = 67;
            NearPlane = .001f;
            FarPlane = 1000;
        }

        public float NearPlaneHeight => Viewport.Height / (2 * MathF.Tan(FieldOfView / 2 * MathF.PI / 180));

        // XXX This is supposed to be correct but doesn't work?
        // public float NearPlaneHeight2 => NearPlane * 2 * (float)Math.Tan(fieldOfView / 2 * Math.PI / 180);

        protected override void Recalculate(out Matrix4x4 view, out Matrix4x4 projection, out Rectangle internalViewport, out Rectangle extendedViewport)
        {
            var screenViewport = Viewport;
            var fovRadians = Math.Max(.0001f, Math.Min(fieldOfView * MathF.PI / 180, MathF.PI - .0001f));
            var aspect = (float)screenViewport.Width / screenViewport.Height;

            internalViewport = extendedViewport = screenViewport;
            projection = Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, aspect, NearPlane, FarPlane);
            view = Matrix4x4.CreateLookAt(Position, Position + Forward, Up);
        }
    }
}