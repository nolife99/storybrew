using System.Drawing;
using System.Numerics;

namespace BrewLib.Graphics.Cameras;

public static class CameraExtensions
{
    // Vector

    public static Vector3 ToScreen(this Camera camera, Vector2 worldCoords)
    {
        var cast = new Vector3(worldCoords.X, worldCoords.Y, 0);
        var result = camera.ToScreen(cast);
        return new(result.X, result.Y, result.Z);
    }
    public static Vector2 ToCamera(this Camera from, Camera to, Vector2 coords)
    {
        var projCast = from.ToScreen(coords);
        var fromCast = to.FromScreen(new Vector2(projCast.X, projCast.Y));
        return new(fromCast.X, fromCast.Y);
    }
    public static Vector2 ToCamera(this Camera from, Camera to, Vector3 coords)
    {
        var projCast = from.ToScreen(coords);
        var fromCast = to.FromScreen(new Vector2(projCast.X, projCast.Y));
        return new(fromCast.X, fromCast.Y);
    }

    // Box2

    public static RectangleF ToScreen(this Camera camera, RectangleF worldBox2)
    {
        var topLeft = camera.ToScreen(new Vector2(worldBox2.Left, worldBox2.Top));
        var bottomRight = camera.ToScreen(new Vector2(worldBox2.Right, worldBox2.Bottom));
        return RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
    }
    public static RectangleF FromScreen(this Camera camera, RectangleF screenBox2)
    {
        var topLeft = camera.FromScreen(new Vector2(screenBox2.Left, screenBox2.Top));
        var bottomRight = camera.FromScreen(new Vector2(screenBox2.Right, screenBox2.Bottom));
        return RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
    }
    public static RectangleF ToCamera(this Camera from, Camera to, RectangleF box2)
    {
        var topLeft = from.ToCamera(to, new Vector2(box2.Left, box2.Top));
        var bottomRight = from.ToCamera(to, new Vector2(box2.Right, box2.Bottom));
        return RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
    }
}