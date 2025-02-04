﻿namespace StorybrewEditor.UserInterface;

using System;
using System.Numerics;
using BrewLib.Graphics;
using BrewLib.Graphics.Drawables;
using BrewLib.UserInterface;
using BrewLib.Util;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.ImageSharp;
using Storyboarding;
using StorybrewCommon.Mapset;

public class TimelineSlider : Slider
{
    static readonly Color tickBlue = Color.FromRgba(50, 128, 255, 225), tickYellow = Color.FromRgba(255, 255, 0, 225),
        tickRed = Color.FromRgba(255, 0, 0, 225), tickViolet = Color.FromRgba(200, 0, 200, 225),
        tickWhite = Color.FromRgba(255, 255, 255, 220), tickMagenta = Color.FromRgba(144, 64, 144, 225),
        tickGrey = Color.FromRgba(160, 160, 160, 225), kiaiColor = Color.FromRgba(255, 146, 18, 140),
        breakColor = Color.FromRgba(255, 255, 255, 140), bookmarkColor = Color.FromRgba(58, 110, 170, 240),
        repeatColor = Color.FromRgba(58, 110, 170, 80), highlightColor = Color.FromRgba(255, 0, 0, 80);

    readonly Label beatmapLabel;

    readonly Project project;

    float dragStart, highlightStart, highlightEnd, timeSpan;

    Sprite line;
    public float RepeatStart, RepeatEnd;

    public int SnapDivisor = 4;

    public TimelineSlider(WidgetManager manager, Project project) : base(manager)
    {
        this.project = project;
        line = new() { Texture = DrawState.WhitePixel, ScaleMode = ScaleMode.Fill };

        Add(beatmapLabel = new(manager)
        {
            StyleName = "timelineBeatmapName",
            Text = project.MainBeatmap.Name,
            AnchorFrom = BoxAlignment.BottomRight,
            AnchorTo = BoxAlignment.BottomRight
        });

        StyleName = "timeline";

        project.OnMainBeatmapChanged += project_OnMainBeatmapChanged;
    }

    public void Highlight(float startTime, float endTime)
    {
        highlightStart = startTime;
        highlightEnd = endTime;
    }

    public void ClearHighlight() => highlightStart = highlightEnd = 0;

    void project_OnMainBeatmapChanged(object sender, EventArgs e) => beatmapLabel.Text = project.MainBeatmap.Name;

    protected override void DrawBackground(DrawContext drawContext, float actualOpacity)
    {
        base.DrawBackground(drawContext, actualOpacity);

        Vector2 offset = Bounds.Location;
        var lineBottomY = project.ShowHitObjects ? Bounds.Height * .7f : Bounds.Height * .6f;
        var hitObjectsY = Bounds.Height * .6f;
        var pixelSize = Manager.PixelSize;

        var currentTimingPoint = project.MainBeatmap.GetTimingPointAt(Value * 1000);
        var targetTimeSpan = (SnapDivisor >= 2 ? SnapDivisor >= 8 ? 1 : 2 : 4) * (170 / currentTimingPoint.BPM);
        timeSpan += (targetTimeSpan - timeSpan) * .01f;

        var leftTime = (Value - timeSpan) * 1000;
        var rightTime = (Value + timeSpan) * 1000;
        var timeScale = Bounds.Width / (rightTime - leftTime);

        // Repeat
        if (RepeatStart != RepeatEnd)
        {
            line.Color = repeatColor;

            var left = timeToXTop(RepeatStart);
            var right = timeToXTop(RepeatEnd);
            if (right < left + pixelSize) right = left + pixelSize;

            line.Draw(drawContext,
                Manager.Camera,
                RectangleF.FromLTRB(left, offset.Y, right, offset.Y + Bounds.Height * .4f),
                actualOpacity);
        }

        // Kiai
        var inKiai = false;
        var kiaiStartTime = 0f;

        line.Color = kiaiColor;
        foreach (var controlPoint in project.MainBeatmap.ControlPoints)
        {
            if (controlPoint.IsKiai == inKiai) continue;

            if (inKiai)
            {
                var kiaiLeft = timeToXTop(kiaiStartTime);
                var kiaiRight = timeToXTop(controlPoint.Offset * .001f);
                if (kiaiRight < kiaiLeft + pixelSize) kiaiRight = kiaiLeft + pixelSize;

                line.Draw(drawContext,
                    Manager.Camera,
                    RectangleF.FromLTRB(kiaiLeft, offset.Y + Bounds.Height * .3f, kiaiRight, offset.Y + Bounds.Height * .4f),
                    actualOpacity);
            }
            else kiaiStartTime = controlPoint.Offset * .001f;

            inKiai = controlPoint.IsKiai;
        }

        // Breaks
        line.Color = breakColor;
        foreach (var osuBreak in project.MainBeatmap.Breaks)
        {
            var breakLeft = timeToXTop(osuBreak.StartTime * .001f);
            var breakRight = timeToXTop(osuBreak.EndTime * .001f);
            if (breakRight < breakLeft + pixelSize) breakRight = breakLeft + pixelSize;

            line.Draw(drawContext,
                Manager.Camera,
                RectangleF.FromLTRB(breakLeft, offset.Y + Bounds.Height * .3f, breakRight, offset.Y + Bounds.Height * .4f),
                actualOpacity);
        }

        // Effect / layer highlight
        line.Color = highlightColor;
        if (highlightStart != highlightEnd)
        {
            var left = timeToXTop(highlightStart * .001f);
            var right = timeToXTop(highlightEnd * .001f);
            line.Draw(drawContext,
                Manager.Camera,
                RectangleF.FromLTRB(left, offset.Y + Bounds.Height * .1f, right, offset.Y + Bounds.Height * .4f),
                actualOpacity);
        }

        // Ticks
        project.MainBeatmap.ForEachTick(leftTime,
            rightTime,
            SnapDivisor,
            (timingPoint, time, beatCount, tickCount) =>
            {
                var tickColor = tickGrey;
                Vector2 lineSize = new(pixelSize, Bounds.Height * .3f);

                var snap = tickCount % SnapDivisor;
                if (snap == 0) tickColor = tickWhite;
                else if (snap * 2 % SnapDivisor == 0)
                {
                    lineSize.Y *= .8f;
                    tickColor = tickRed;
                }
                else if (snap * 3 % SnapDivisor == 0)
                {
                    lineSize.Y *= .4f;
                    tickColor = tickViolet;
                }
                else if (snap * 4 % SnapDivisor == 0)
                {
                    lineSize.Y *= .4f;
                    tickColor = tickBlue;
                }
                else if (snap * 6 % SnapDivisor == 0)
                {
                    lineSize.Y *= .4f;
                    tickColor = tickMagenta;
                }
                else if (snap * 8 % SnapDivisor == 0)
                {
                    lineSize.Y *= .4f;
                    tickColor = tickYellow;
                }
                else lineSize.Y *= .4f;

                if (snap != 0 ||
                    tickCount == 0 && timingPoint.OmitFirstBarLine ||
                    beatCount % timingPoint.BeatPerMeasure != 0)
                    lineSize.Y *= .5f;

                var tickX = offset.X + Manager.SnapToPixel((time - leftTime) * timeScale);
                var tickOpacity = tickX > beatmapLabel.TextBounds.Left - 8 ? actualOpacity * .2f : actualOpacity;

                drawLine(drawContext, new(tickX, offset.Y + lineBottomY), lineSize, tickColor, tickOpacity);
            });

        // HitObjects
        if (project.ShowHitObjects)
            foreach (var hitObject in project.MainBeatmap.HitObjects)
                if (leftTime < hitObject.EndTime && hitObject.StartTime < rightTime)
                {
                    var left = Math.Max(0, (hitObject.StartTime - leftTime) * timeScale);
                    var right = Math.Min((hitObject.EndTime - leftTime) * timeScale, Bounds.Width);
                    var height = Math.Max(Bounds.Height * .1f - pixelSize, pixelSize);

                    drawLine(drawContext,
                        offset + new Vector2(Manager.SnapToPixel(left - height / 2), hitObjectsY),
                        new(Manager.SnapToPixel(right - left + height), height),
                        hitObject.Color,
                        actualOpacity);
                }

        // Bookmarks
        foreach (var bookmark in project.MainBeatmap.Bookmarks)
        {
            drawLine(drawContext,
                new(timeToXTop(bookmark * .001f), offset.Y + Bounds.Height * .1f),
                new(pixelSize, Bounds.Height * .3f),
                bookmarkColor,
                actualOpacity);

            if (leftTime < bookmark && bookmark < rightTime)
                drawLine(drawContext,
                    offset + new Vector2(Manager.SnapToPixel((bookmark - leftTime) * timeScale), lineBottomY),
                    new(pixelSize, Bounds.Height * .5f),
                    bookmarkColor,
                    actualOpacity);
        }

        // Current time (top)
        var x = timeToXTop(Value);
        {
            Vector2 lineSize = new(pixelSize, Bounds.Height * .4f);
            if (RepeatStart != RepeatEnd)
            {
                drawLine(drawContext,
                    offset with { X = timeToXTop(RepeatStart) - pixelSize },
                    lineSize,
                    Color.White,
                    actualOpacity);

                drawLine(drawContext, offset with { X = x }, lineSize * .6f, Color.White, actualOpacity);

                drawLine(drawContext,
                    offset with { X = timeToXTop(RepeatEnd) + pixelSize },
                    lineSize,
                    Color.White,
                    actualOpacity);
            }
            else
            {
                drawLine(drawContext, offset with { X = x - pixelSize }, lineSize, Color.White, actualOpacity);

                drawLine(drawContext, offset with { X = x + pixelSize }, lineSize, Color.White, actualOpacity);
            }

            // Current time (bottom)
            var centerX = Bounds.Width * .5f;
            lineSize = new(pixelSize, Bounds.Height * .4f);
            drawLine(drawContext,
                offset + new Vector2(centerX - pixelSize, lineBottomY),
                lineSize,
                Color.White,
                actualOpacity);

            drawLine(drawContext,
                offset + new Vector2(centerX + pixelSize, lineBottomY),
                lineSize,
                Color.White,
                actualOpacity);
        }
    }

    float timeToXTop(float time)
        => Manager.SnapToPixel(AbsolutePosition.X + (time - MinValue) / (MaxValue - MinValue) * Width);

    void drawLine(DrawContext drawContext, Vector2 position, Vector2 size, Color color, float opacity)
    {
        line.Color = color;
        line.Draw(drawContext, Manager.Camera, new(position.X, position.Y, size.X, size.Y), opacity);
    }

    public void Scroll(float direction)
    {
        var time = Value * 1000;
        var timingPoint = project.MainBeatmap.GetTimingPointAt(time);

        var stepDuration = timingPoint.BeatDuration / SnapDivisor;
        time += stepDuration * direction;

        var steps = (time - timingPoint.Offset) / stepDuration;
        time = timingPoint.Offset + float.Round(steps) * stepDuration;

        Value = time * .001f;
    }

    public void Snap() => Scroll(0);

    protected override void DragStart(MouseButton button)
    {
        if (button != MouseButton.Right) return;

        dragStart = Value;
        RepeatStart = dragStart;
        RepeatEnd = dragStart;
    }

    protected override void DragUpdate(MouseButton button)
    {
        if (button != MouseButton.Right) return;

        var value = Value;
        if (value < dragStart)
        {
            RepeatStart = value;
            RepeatEnd = dragStart;
        }
        else
        {
            RepeatStart = dragStart;
            RepeatEnd = value;
        }
    }

    protected override void Layout()
    {
        base.Layout();
        beatmapLabel.Size = new(Size.X * .25f, Size.Y * .4f);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            project.OnMainBeatmapChanged -= project_OnMainBeatmapChanged;
            line.Dispose();
        }

        line = null;

        base.Dispose(disposing);
    }
}