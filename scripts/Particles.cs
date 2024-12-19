namespace StorybrewScripts;

using System;
using System.Linq;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.CommandValues;
using Vector2 = System.Numerics.Vector2;

internal class Particles : StoryboardObjectGenerator
{
    [Configurable] public bool Additive = false;

    [Group("Motion"),
     Description(
         "The angle in degrees at which particles will be moving.\n0 is to the right, positive values rotate counterclockwise."),
     Configurable]
    public float Angle = 110;

    [Description("The spread in degrees around Angle."), Configurable]
    public float AngleSpread = 60;

    [Configurable] public Color4 Color = Color4.White;

    [Description("Varies the saturation and brightness of the selected Color for each particle."), Configurable]
    public float ColorVariance = .6f;

    [Description("Eases the motion of particles."), Configurable]
    public OsbEasing Easing = OsbEasing.None;

    [Configurable] public int EndTime;
    [Configurable] public float Lifetime = 1000;
    [Configurable] public OsbOrigin Origin = OsbOrigin.Centre;

    [Group("Spawn"), Configurable] public int ParticleCount = 32;

    [Group("Sprite"), Configurable] public string Path = "sb/particle.png";

    [Description("Rotation of the sprite; does not influences particle motion direction."), Configurable]
    public float Rotation = 0;

    [Configurable] public Vector2 Scale = Vector2.One;

    [Description("The point around which particles will be created."), Configurable]
    public Vector2 SpawnOrigin = new(420, 0);

    [Description("The distance around the Spawn Origin point where particles will be created."), Configurable]
    public float SpawnSpread = 360;

    [Description("The speed at which particles move, in osupixels."), Configurable]
    public float Speed = 480;

    [Group("Timing"), Configurable] public int StartTime;

    protected override void Generate()
    {
        if (StartTime == EndTime && Beatmap.HitObjects.FirstOrDefault() is not null)
        {
            StartTime = (int)Beatmap.HitObjects.First().StartTime;
            EndTime = (int)Beatmap.HitObjects.Last().EndTime;
        }

        EndTime = Math.Min(EndTime, (int)AudioDuration);
        StartTime = Math.Min(StartTime, EndTime);

        var bitmap = GetMapsetBitmap(Path);

        var duration = (float)(EndTime - StartTime);
        var loopCount = Math.Max(1, (int)(duration / Lifetime));

        for (var i = 0; i < ParticleCount; ++i)
        {
            var spawnAngle = Random(MathF.Tau);
            var spawnDistance = SpawnSpread * MathF.Sqrt(Random(1f));

            var moveAngle = MathHelper.DegreesToRadians(Angle + Random(-AngleSpread, AngleSpread) / 2);
            var moveDistance = Speed * Lifetime * .001f;

            var spriteRotation = moveAngle + MathHelper.DegreesToRadians(Rotation);

            var startPosition = SpawnOrigin + new Vector2(MathF.Cos(spawnAngle), MathF.Sin(spawnAngle)) * spawnDistance;
            var endPosition = startPosition + new Vector2(MathF.Cos(moveAngle), MathF.Sin(moveAngle)) * moveDistance;

            var loopDuration = duration / loopCount;
            var startTime = StartTime + i * loopDuration / ParticleCount;
            var endTime = startTime + loopDuration * loopCount;

            if (!isVisible(bitmap, startPosition, endPosition, spriteRotation, loopDuration)) continue;

            var color = Color;
            if (ColorVariance > 0)
            {
                ColorVariance = Math.Clamp(ColorVariance, 0, 1);

                var hsba = Color4.ToHsl(color);
                var sMin = Math.Max(0, hsba.Y - ColorVariance * .5f);
                var sMax = Math.Min(sMin + ColorVariance, 1);
                var vMin = Math.Max(0, hsba.Z - ColorVariance * .5f);
                var vMax = Math.Min(vMin + ColorVariance, 1);

                color = CommandColor.FromHsb(hsba.X, Random(sMin, sMax), Random(vMin, vMax));
            }

            var particle = GetLayer("").CreateSprite(Path, Origin);
            if (spriteRotation != 0) particle.Rotate(startTime, spriteRotation);
            if (color.R != 1 || color.G != 1 || color.B != 1) particle.Color(startTime, color);
            if (Scale.X != 1 || Scale.Y != 1)
            {
                if (Scale.X != Scale.Y) particle.ScaleVec(startTime, Scale.X, Scale.Y);
                else particle.Scale(startTime, Scale.X);
            }

            if (Additive) particle.Additive(startTime, endTime);

            particle.StartLoopGroup(startTime, loopCount);
            particle.Fade(OsbEasing.Out, 0, loopDuration * .2f, 0, color.A);
            particle.Fade(OsbEasing.In, loopDuration * .8f, loopDuration, color.A, 0);
            particle.Move(Easing, 0, loopDuration, startPosition, endPosition);
            particle.EndGroup();
        }
    }

    bool isVisible(Image bitmap, Vector2 startPosition, Vector2 endPosition, float rotation, float duration)
    {
        Vector2 spriteSize = new(bitmap.Width * Scale.X, bitmap.Height * Scale.Y);
        for (var t = 0; t < duration; t += 200)
        {
            var position = Vector2.Lerp(startPosition, endPosition, t / duration);
            if (OsbSprite.InScreenBounds(position, spriteSize, rotation, Origin)) return true;
        }

        return false;
    }
}