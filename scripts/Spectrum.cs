﻿namespace StorybrewScripts;

using System;
using System.Linq;
using System.Numerics;
using StorybrewCommon.Animations;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;

///<summary> An example of a spectrum effect. </summary>
internal class Spectrum : StoryboardObjectGenerator
{
    [Configurable] public int BarCount = 96;
    [Configurable] public int BeatDivisor = 16;
    [Configurable] public int CommandDecimals = 1;
    [Configurable] public int EndTime = 10000;
    [Configurable] public OsbEasing FftEasing = OsbEasing.InExpo;
    [Configurable] public int FrequencyCutOff = 16000;
    [Configurable] public int LogScale = 600;
    [Configurable] public float MinimalHeight = .05f;

    [Group("Bars"), Configurable] public Vector2 Position = new(0, 400);

    [Configurable] public OsbOrigin SpriteOrigin = OsbOrigin.BottomLeft;

    [Group("Sprite"), Configurable] public string SpritePath = "sb/bar.png";

    [Configurable] public Vector2 SpriteScale = new(1, 100);

    [Group("Timing"), Configurable] public int StartTime;

    [Group("Optimization"), Configurable] public float Tolerance = .2f;

    [Configurable] public float Width = 640;

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

        var heightKeyframes = new KeyframedValue<float>[BarCount];
        for (var i = 0; i < BarCount; ++i) heightKeyframes[i] = [];

        var timeStep = Beatmap.GetTimingPointAt(StartTime).BeatDuration / BeatDivisor;
        var offset = timeStep * .2f;

        for (float time = StartTime; time < EndTime; time += timeStep)
        {
            var fft = GetFft(time + offset, BarCount, null, FftEasing, FrequencyCutOff);
            for (var i = 0; i < BarCount; ++i)
            {
                var height = MathF.Log10(1 + fft[i] * LogScale) * SpriteScale.Y / bitmap.Height;
                if (height < MinimalHeight) height = MinimalHeight;

                heightKeyframes[i].Add(time, height);
            }
        }

        var layer = GetLayer("Spectrum");
        var barWidth = Width / BarCount;
        for (var i = 0; i < BarCount; ++i)
        {
            var keyframes = heightKeyframes[i];
            keyframes.Simplify1dKeyframes(Tolerance, h => h);

            var bar = layer.CreateSprite(SpritePath, SpriteOrigin, new(Position.X + i * barWidth, Position.Y));
            bar.CommandSplitThreshold = 300;
            bar.ColorHsb(StartTime, i * 360f / BarCount + Random(-10f, 10), .6f + Random(.4f), 1);
            bar.Additive(StartTime, EndTime);

            var scaleX = SpriteScale.X * barWidth / bitmap.Width;
            scaleX = MathF.Floor(scaleX * 10) * .1f;

            var hasScale = false;
            keyframes.ForEachPair((start, end) =>
                {
                    hasScale = true;
                    bar.ScaleVec(start.Time, end.Time, scaleX, start.Value, scaleX, end.Value);
                },
                MinimalHeight,
                s => MathF.Round(s, CommandDecimals));

            if (!hasScale) bar.ScaleVec(StartTime, scaleX, MinimalHeight);
        }
    }
}