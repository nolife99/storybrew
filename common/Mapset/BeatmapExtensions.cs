namespace StorybrewCommon.Mapset;

using System;

#pragma warning disable CS1591
public static class BeatmapExtensions
{
    /// <summary> Executes the specified <paramref name="tickAction"/> for each timing tick in the osu! beatmap, calculated based on the <paramref name="snapDivisor"/>. </summary>
    public static void ForEachTick(this Beatmap beatmap,
        float startTime,
        float endTime,
        int snapDivisor,
        Action<ControlPoint, float, int, int> tickAction)
    {
        var leftTimingPoint = beatmap.GetTimingPointAt(startTime);
        using var timingPoints = beatmap.TimingPoints.GetEnumerator();

        if (!timingPoints.MoveNext()) return;
        var timingPoint = timingPoints.Current;

        while (timingPoint is not null)
        {
            var nextTimingPoint = timingPoints.MoveNext() ? timingPoints.Current : null;
            if (timingPoint.Offset < leftTimingPoint.Offset)
            {
                timingPoint = nextTimingPoint;
                continue;
            }

            if (timingPoint != leftTimingPoint && endTime + Beatmap.ControlPointLeniency < timingPoint.Offset) break;

            int tickCount = 0, beatCount = 0;
            var step = Math.Max(1, timingPoint.BeatDuration / snapDivisor);
            var sectionStartTime = timingPoint.Offset;
            var sectionEndTime = Math.Min(nextTimingPoint?.Offset ?? endTime, endTime);

            if (timingPoint == leftTimingPoint)
                while (startTime < sectionStartTime)
                {
                    sectionStartTime -= step;
                    --tickCount;
                    if (tickCount % snapDivisor == 0) --beatCount;
                }

            for (var time = sectionStartTime; time < sectionEndTime + Beatmap.ControlPointLeniency; time += step)
            {
                if (startTime < time) tickAction(timingPoint, time, beatCount, tickCount);
                if (tickCount % snapDivisor == 0) ++beatCount;
                ++tickCount;
            }

            timingPoint = nextTimingPoint;
        }
    }
}