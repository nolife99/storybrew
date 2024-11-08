namespace BrewLib.Graphics.Cameras;

using System.Numerics;

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
}