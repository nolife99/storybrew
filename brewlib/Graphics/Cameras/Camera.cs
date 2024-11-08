namespace BrewLib.Graphics.Cameras;

using System;
using System.Drawing;
using System.Numerics;

public interface Camera : IDisposable
{
    // Inputs 

    Rectangle Viewport { get; set; }
    Vector3 Position { get; set; }
    Vector3 Forward { get; set; }
    Vector3 Up { get; set; }
    float NearPlane { get; }
    float FarPlane { get; }

    // Outputs

    Matrix4x4 Projection { get; }
    Matrix4x4 View { get; }
    Matrix4x4 ProjectionView { get; }
    Matrix4x4 InvertedProjectionView { get; }
    Rectangle InternalViewport { get; }
    Rectangle ExtendedViewport { get; }

    Vector3 FromScreen(Vector2 screenCoords);
    RectangleF FromScreen(RectangleF screenBox2);
    Vector3 ToScreen(Vector3 worldCoords);
    Vector3 ToScreen(Vector2 worldCoords);
    RectangleF ToScreen(RectangleF worldBox2);

    event EventHandler Changed;
}