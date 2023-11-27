using System;
using System.Drawing;
using System.Numerics;

namespace BrewLib.Graphics.Cameras
{
    public class CameraOrtho : CameraBase
    {
        public static CameraOrtho Default = new();
        readonly bool yDown;

        int virtualWidth;
        public int VirtualWidth
        {
            get => virtualWidth;
            set
            {
                if (virtualWidth == value) return;
                virtualWidth = value;
                Invalidate();
            }
        }

        int virtualHeight;
        public int VirtualHeight
        {
            get => virtualHeight;
            set
            {
                if (virtualHeight == value) return;
                virtualHeight = value;
                Invalidate();
            }
        }

        float zoom = 1;
        public float Zoom
        {
            get => zoom;
            set
            {
                if (zoom == value) return;
                zoom = value;
                Invalidate();
            }
        }
        public float HeightScaling => VirtualHeight != 0 ? (float)Viewport.Height / VirtualHeight : 1;

        public CameraOrtho(bool yDown = true) : this(0, 0, yDown) { }
        public CameraOrtho(int virtualWidth, int virtualHeight, bool yDown = true)
        {
            this.virtualWidth = virtualWidth;
            this.virtualHeight = virtualHeight;
            this.yDown = yDown;

            Up = new(0, yDown ? -1 : 1, 0);
            Forward = new(0, 0, yDown ? 1 : -1);

            NearPlane = -1;
            FarPlane = 1;
        }

        protected override void Recalculate(out Matrix4x4 view, out Matrix4x4 projection, out Rectangle internalViewport, out Rectangle extendedViewport)
        {
            var screenViewport = Viewport;
            var orthoViewport = screenViewport;

            if (virtualHeight != 0)
            {
                var scale = screenViewport.Height == 0 ? 1 : (double)virtualHeight / screenViewport.Height;
                orthoViewport.Width = (int)Math.Round(screenViewport.Width * scale);
                orthoViewport.Height = virtualHeight;
                if (virtualWidth > 0) orthoViewport.X += (orthoViewport.Width - virtualWidth) / 2;

                internalViewport = new(0, 0, virtualWidth > 0 ? virtualWidth : orthoViewport.Width, virtualHeight);
            }
            else internalViewport = screenViewport;
            extendedViewport = orthoViewport;

            projection = Matrix4x4.CreateTranslation(
                orthoViewport.X - extendedViewport.Width * .5f, orthoViewport.Y - (yDown ? -extendedViewport.Height : extendedViewport.Height) * .5f, 0) *
                Matrix4x4.CreateOrthographic(extendedViewport.Width * zoom, extendedViewport.Height * zoom, NearPlane, FarPlane);

            view = Matrix4x4.CreateLookAt(Position, Position + Forward, Up);
        }
    }
}