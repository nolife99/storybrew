namespace BrewLib.Graphics.Cameras;

using System.Numerics;
using SixLabors.ImageSharp;

public class CameraOrtho : CameraBase
{
    readonly bool yDown;
    int virtualHeight, virtualWidth;

    public CameraOrtho(bool yDown = true)
    {
        this.yDown = yDown;

        Up = new(0, yDown ? -1 : 1, 0);
        Forward = new(0, 0, yDown ? 1 : -1);

        NearPlane = -1;
        FarPlane = 1;
    }

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

    public float HeightScaling => VirtualHeight != 0 ? (float)Viewport.Height / VirtualHeight : 1;

    protected override void Recalculate(out Matrix4x4 view,
        out Matrix4x4 projection,
        out Rectangle internalViewport,
        out Rectangle extendedViewport)
    {
        var screenViewport = Viewport;
        var orthoViewport = screenViewport;

        if (virtualHeight != 0)
        {
            var scale = screenViewport.Height == 0 ? 1 : (float)virtualHeight / screenViewport.Height;
            orthoViewport.Width = (int)float.Round(screenViewport.Width * scale);
            orthoViewport.Height = virtualHeight;
            if (virtualWidth > 0) orthoViewport.X += (orthoViewport.Width - virtualWidth) / 2;

            internalViewport = new(0, 0, virtualWidth > 0 ? virtualWidth : orthoViewport.Width, virtualHeight);
        }
        else internalViewport = screenViewport;

        extendedViewport = orthoViewport;

        projection = Matrix4x4.Multiply(
            Matrix4x4.CreateTranslation(orthoViewport.X - extendedViewport.Width * .5f,
                orthoViewport.Y - (yDown ? -extendedViewport.Height : extendedViewport.Height) * .5f, 0),
            Matrix4x4.CreateOrthographic(extendedViewport.Width, extendedViewport.Height, NearPlane, FarPlane));

        view = Matrix4x4.CreateLookAt(Position, Position + Forward, Up);
    }
}