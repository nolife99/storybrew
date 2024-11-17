namespace BrewLib.Graphics.Cameras;

using System;
using System.Numerics;
using SixLabors.ImageSharp;

public abstract class CameraBase : Camera
{
    static readonly Vector3 DefaultForward = new(0, -1, 0), DefaultUp = new(0, 0, 1);

    float farPlane, nearPlane;

    Vector3 forward = DefaultForward, up = DefaultUp, position = Vector3.Zero;
    Rectangle internalViewport, extendedViewport, viewport;
    bool needsUpdate;
    Matrix4x4 projection, view, projectionView, invertedProjectionView;

    public CameraBase()
    {
        viewport = DrawState.Viewport;
        DrawState.ViewportChanged += drawState_ViewportChanged;
        needsUpdate = true;
    }

    public Rectangle InternalViewport
    {
        get
        {
            Validate();
            return internalViewport;
        }
    }

    public Rectangle ExtendedViewport
    {
        get
        {
            Validate();
            return extendedViewport;
        }
    }

    public Matrix4x4 Projection
    {
        get
        {
            Validate();
            return projection;
        }
    }

    public Matrix4x4 View
    {
        get
        {
            Validate();
            return view;
        }
    }

    public Matrix4x4 ProjectionView
    {
        get
        {
            Validate();
            return projectionView;
        }
    }

    public Matrix4x4 InvertedProjectionView
    {
        get
        {
            Validate();
            return invertedProjectionView;
        }
    }

    public event EventHandler Changed;

    public float NearPlane
    {
        get => nearPlane;
        set
        {
            if (nearPlane == value) return;
            nearPlane = value;
            Invalidate();
        }
    }

    public float FarPlane
    {
        get => farPlane;
        set
        {
            if (farPlane == value) return;
            farPlane = value;
            Invalidate();
        }
    }

    public Rectangle Viewport
    {
        get => viewport;
        set
        {
            if (viewport == value) return;
            viewport = value;
            Invalidate();
        }
    }

    public Vector3 Position
    {
        get => position;
        set
        {
            if (position == value) return;
            position = value;
            Invalidate();
        }
    }

    public Vector3 Forward
    {
        get => forward;
        set
        {
            if (forward == value) return;
            forward = value;
            Invalidate();
        }
    }

    public Vector3 Up
    {
        get => up;
        set
        {
            if (up == value) return;
            up = value;
            Invalidate();
        }
    }

    public void Dispose() => DrawState.ViewportChanged -= drawState_ViewportChanged;

    public Vector3 FromScreen(Vector2 screenCoords)
    {
        Validate();
        // TODO Vector3.Unproject() ?

        var deviceX = 2 * (screenCoords.X / viewport.Width) - 1;
        var deviceY = -2 * (screenCoords.Y / viewport.Height) + 1;

        var nearBase = Vector4.Transform(new Vector4(deviceX, deviceY, NearPlane, 1), invertedProjectionView);
        var near = new Vector3(nearBase.X, nearBase.Y, nearBase.Z);

        var farBase = Vector4.Transform(new Vector4(deviceX, deviceY, FarPlane, 1), invertedProjectionView);
        var far = new Vector3(farBase.X, farBase.Y, farBase.Z);

        var direction = Vector3.Normalize(far - near);
        if (direction.Z == 0) return Vector3.Zero;
        return near - direction * (near.Z / direction.Z);
    }

    public RectangleF FromScreen(RectangleF screenBox2)
    {
        var topLeft = FromScreen(new Vector2(screenBox2.Left, screenBox2.Top));
        var bottomRight = FromScreen(new Vector2(screenBox2.Right, screenBox2.Bottom));
        return RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
    }

    public Vector3 ToScreen(Vector3 worldCoords)
    {
        Validate();
        // TODO Vector3.Project() ?

        var transformedPosition = Vector4.Transform(new Vector4(worldCoords, 1), projectionView);
        var devicePosition = new Vector3(transformedPosition.X, transformedPosition.Y, transformedPosition.Z) /
            Math.Abs(transformedPosition.W);

        return new Vector3((devicePosition.X + 1) * .5f * viewport.Width, (-devicePosition.Y + 1) * .5f * viewport.Height,
            devicePosition.Z);
    }

    public Vector3 ToScreen(Vector2 worldCoords) => ToScreen(new Vector3(worldCoords.X, worldCoords.Y, 0));

    public RectangleF ToScreen(RectangleF worldBox2)
    {
        var topLeft = ToScreen(new Vector2(worldBox2.Left, worldBox2.Top));
        var bottomRight = ToScreen(new Vector2(worldBox2.Right, worldBox2.Bottom));
        return RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
    }

    void Validate()
    {
        if (!needsUpdate) return;

        Recalculate(out view, out projection, out internalViewport, out extendedViewport);
        needsUpdate = false;

        projectionView = view * projection;
        Matrix4x4.Invert(projectionView, out invertedProjectionView);
    }

    protected void Invalidate()
    {
        needsUpdate = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    void drawState_ViewportChanged() => Viewport = DrawState.Viewport;

    protected abstract void Recalculate(out Matrix4x4 view,
        out Matrix4x4 projection,
        out Rectangle internalViewport,
        out Rectangle extendedViewport);
}