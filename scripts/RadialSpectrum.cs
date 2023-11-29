using StorybrewCommon.Animations;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using System.Numerics;
using System;
using System.Linq;

namespace StorybrewScripts
{
    ///<summary> An example of a radial spectrum effect, using movement instead of scaling. </summary>
    class RadialSpectrum : StoryboardObjectGenerator
    {
        [Group("Timing")]
        [Configurable] public int StartTime = 0;
        [Configurable] public int EndTime = 10000;
        [Configurable] public int BeatDivisor = 8;

        [Group("Sprite")]
        [Configurable] public string SpritePath = "sb/bar.png";
        [Configurable] public OsbOrigin SpriteOrigin = OsbOrigin.Centre;
        [Configurable] public Vector2 SpriteScale = Vector2.One;

        [Group("Bars")]
        [Configurable] public Vector2 Position = new(320, 240);
        [Configurable] public int BarCount = 20;
        [Configurable] public int Radius = 50;
        [Configurable] public float Scale = 50;
        [Configurable] public int LogScale = 600;
        [Configurable] public OsbEasing FftEasing = OsbEasing.InExpo;

        [Group("Optimization")]
        [Configurable] public double Tolerance = 2;
        [Configurable] public int CommandDecimals = 0;
        [Configurable] public int FrequencyCutOff = 16000;

        protected override void Generate()
        {
            if (StartTime == EndTime && Beatmap.HitObjects.FirstOrDefault() is not null)
            {
                StartTime = (int)Beatmap.HitObjects.First().StartTime;
                EndTime = (int)Beatmap.HitObjects.Last().EndTime;
            }
            EndTime = Math.Min(EndTime, (int)AudioDuration);
            StartTime = Math.Min(StartTime, EndTime);

            var bitmap = GetMapsetBitmap(SpritePath);

            var positionKeyframes = new KeyframedValue<Vector2>[BarCount];
            for (var i = 0; i < BarCount; ++i) positionKeyframes[i] = [];

            var timeStep = Beatmap.GetTimingPointAt(StartTime).BeatDuration / BeatDivisor;
            var offset = timeStep * .2;
            for (double time = StartTime; time < EndTime; time += timeStep)
            {
                var fft = GetFft(time + offset, BarCount, null, FftEasing, FrequencyCutOff);
                for (var i = 0; i < BarCount; ++i)
                {
                    var height = Radius + MathF.Log10(1 + fft[i] * LogScale) * Scale;
                    var angle = i * osuTK.MathHelper.TwoPi / BarCount;

                    positionKeyframes[i].Add(time, Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * height);
                }
            }

            var layer = GetLayer("Spectrum");
            var barScale = MathF.Tau * Radius / BarCount / bitmap.Width;
            for (var i = 0; i < BarCount; ++i)
            {
                var keyframes = positionKeyframes[i];
                keyframes.Simplify2dKeyframes(Tolerance, h => h);

                var angle = i * MathF.Tau / BarCount;
                var defaultPosition = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Radius;

                var bar = layer.CreateSprite(SpritePath, SpriteOrigin);
                bar.ColorHsb(StartTime, i * 360f / BarCount + Random(-10f, 10), .6f + Random(.4f), 1);
                if (SpriteScale.X == SpriteScale.Y) bar.Scale(StartTime, barScale * SpriteScale.X);
                else bar.ScaleVec(StartTime, barScale * SpriteScale.X, barScale * SpriteScale.Y);
                bar.Rotate(StartTime, angle);
                bar.Additive(StartTime);

                var hasMove = false;
                keyframes.ForEachPair((start, end) =>
                {
                    hasMove = true;
                    bar.Move(start.Time, end.Time, start.Value, end.Value);
                }, defaultPosition, s => new(MathF.Round(s.X, CommandDecimals), MathF.Round(s.Y, CommandDecimals)));

                if (!hasMove) bar.Move(EndTime, defaultPosition);
            }
        }
    }
}