using BrewLib.Graphics;
using BrewLib.Graphics.Drawables;
using BrewLib.UserInterface;
using BrewLib.Util;
using System.Numerics;
using System.Drawing;
using osuTK.Input;
using StorybrewCommon.Mapset;
using StorybrewEditor.Storyboarding;
using System;

namespace StorybrewEditor.UserInterface;

public class TimelineSlider : Slider
{
    static readonly Color tickBlue = Color.FromArgb(225, 50, 128, 255),
        tickYellow = Color.FromArgb(225, 255, 255, 0),
        tickRed = Color.FromArgb(225, 255, 0, 0),
        tickViolet = Color.FromArgb(225, 200, 0, 200),
        tickWhite = Color.FromArgb(220, 255, 255, 255),
        tickMagenta = Color.FromArgb(225, 144, 64, 144),
        tickGrey = Color.FromArgb(225, 160, 160, 160),
        
        kiaiColor = Color.FromArgb(140, 255, 146, 18),
        breakColor = Color.FromArgb(140, 255, 255, 255),
        bookmarkColor = Color.FromArgb(240, 58, 110, 170),
        repeatColor = Color.FromArgb(80, 58, 110, 170),
        highlightColor = Color.FromArgb(80, 255, 0, 0);

    Sprite line;
    readonly Label beatmapLabel;

    readonly Project project;
    float timeSpan;

    public int SnapDivisor = 4;

    float dragStart;
    public float RepeatStart, RepeatEnd;
    double highlightStart, highlightEnd;

    public TimelineSlider(WidgetManager manager, Project project) : base(manager)
    {
        this.project = project;
        line = new()
        {
            Texture = DrawState.WhitePixel,
            ScaleMode = ScaleMode.Fill
        };
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

    public void Highlight(double startTime, double endTime)
    {
        highlightStart = startTime;
        highlightEnd = endTime;
    }
    public void ClearHighlight() => highlightStart = highlightEnd = 0;

    void project_OnMainBeatmapChanged(object sender, EventArgs e) => beatmapLabel.Text = project.MainBeatmap.Name;

    protected override void DrawBackground(DrawContext drawContext, float actualOpacity)
    {
        base.DrawBackground(drawContext, actualOpacity);

        Vector2 offset = new(Bounds.Left, Bounds.Top);
        var lineBottomY = project.ShowHitObjects ? Bounds.Height * .7f : Bounds.Height * .6f;
        var hitObjectsY = Bounds.Height * .6f;
        var pixelSize = Manager.PixelSize;

        var currentTimingPoint = project.MainBeatmap.GetTimingPointAt((int)(Value * 1000));
        var targetTimeSpan = (SnapDivisor >= 2 ? SnapDivisor >= 8 ? 1 : 2 : 4) * (170 / (float)currentTimingPoint.BPM);
        timeSpan += (targetTimeSpan - timeSpan) * .01f;

        var leftTime = (int)((Value - timeSpan) * 1000);
        var rightTime = (int)((Value + timeSpan) * 1000);
        var timeScale = Bounds.Width / (rightTime - leftTime);
        var valueLength = MaxValue - MinValue;

        // Repeat
        if (RepeatStart != RepeatEnd)
        {
            line.Color = repeatColor;

            var left = timeToXTop(RepeatStart);
            var right = timeToXTop(RepeatEnd);
            if (right < left + pixelSize) right = left + pixelSize;

            line.Draw(drawContext, Manager.Camera, RectangleF.FromLTRB(left, offset.Y, right, offset.Y + Bounds.Height * .4f), actualOpacity);
        }

        // Kiai
        var inKiai = false;
        var kiaiStartTime = .0;

        line.Color = kiaiColor;
        foreach (var controlPoint in project.MainBeatmap.ControlPoints)
        {
            if (controlPoint.IsKiai == inKiai) continue;

            if (inKiai)
            {
                var kiaiLeft = timeToXTop(kiaiStartTime);
                var kiaiRight = timeToXTop(controlPoint.Offset * .001);
                if (kiaiRight < kiaiLeft + pixelSize) kiaiRight = kiaiLeft + pixelSize;

                line.Draw(drawContext, Manager.Camera, RectangleF.FromLTRB(kiaiLeft, offset.Y + Bounds.Height * .3f, kiaiRight, offset.Y + Bounds.Height * .4f), actualOpacity);
            }
            else kiaiStartTime = controlPoint.Offset * .001;
            inKiai = controlPoint.IsKiai;
        }

        // Breaks
        line.Color = breakColor;
        foreach (var osuBreak in project.MainBeatmap.Breaks)
        {
            var breakLeft = timeToXTop(osuBreak.StartTime * .001);
            var breakRight = timeToXTop(osuBreak.EndTime * .001);
            if (breakRight < breakLeft + pixelSize) breakRight = breakLeft + pixelSize;

            line.Draw(drawContext, Manager.Camera, RectangleF.FromLTRB(breakLeft, offset.Y + Bounds.Height * .3f, breakRight, offset.Y + Bounds.Height * .4f), actualOpacity);
        }

        // Effect / layer highlight
        line.Color = highlightColor;
        if (highlightStart != highlightEnd)
        {
            var left = timeToXTop(highlightStart * .001);
            var right = timeToXTop(highlightEnd * .001);
            line.Draw(drawContext, Manager.Camera, RectangleF.FromLTRB(left, offset.Y + Bounds.Height * .1f, right, offset.Y + Bounds.Height * .4f), actualOpacity);
        }

        // Ticks
        project.MainBeatmap.ForEachTick(leftTime, rightTime, SnapDivisor, (timingPoint, time, beatCount, tickCount) =>
        {
            var tickColor = tickGrey;
            Vector2 lineSize = new(pixelSize, Bounds.Height * .3f);

            var snap = tickCount % SnapDivisor;
            if (snap == 0) tickColor = tickWhite;
            else if (snap * 2 % SnapDivisor == 0) { lineSize.Y *= .8f; tickColor = tickRed; }
            else if (snap * 3 % SnapDivisor == 0) { lineSize.Y *= .4f; tickColor = tickViolet; }
            else if (snap * 4 % SnapDivisor == 0) { lineSize.Y *= .4f; tickColor = tickBlue; }
            else if (snap * 6 % SnapDivisor == 0) { lineSize.Y *= .4f; tickColor = tickMagenta; }
            else if (snap * 8 % SnapDivisor == 0) { lineSize.Y *= .4f; tickColor = tickYellow; }
            else lineSize.Y *= .4f;

            if (snap != 0 || (tickCount == 0 && timingPoint.OmitFirstBarLine) || beatCount % timingPoint.BeatPerMeasure != 0) lineSize.Y *= .5f;

            var tickX = offset.X + (float)Manager.SnapToPixel((time - leftTime) * timeScale);
            var tickOpacity = tickX > beatmapLabel.TextBounds.Left - 8 ? actualOpacity * .2f : actualOpacity;

            drawLine(drawContext, new(tickX, offset.Y + lineBottomY), lineSize, tickColor, tickOpacity);
        });

        // HitObjects
        if (project.ShowHitObjects) foreach (var hitObject in project.MainBeatmap.HitObjects) if (leftTime < hitObject.EndTime && hitObject.StartTime < rightTime)
        {
            var left = Math.Max(0, (hitObject.StartTime - leftTime) * timeScale);
            var right = Math.Min((hitObject.EndTime - leftTime) * timeScale, Bounds.Width);
            var height = Math.Max(Bounds.Height * .1f - pixelSize, pixelSize);

            drawLine(drawContext, offset + new Vector2((float)Manager.SnapToPixel(left - height / 2), hitObjectsY),
                new((float)Manager.SnapToPixel(right - left + height), height), hitObject.Color, actualOpacity);
        }

        // Bookmarks
        foreach (var bookmark in project.MainBeatmap.Bookmarks)
        {
            drawLine(drawContext, new(timeToXTop(bookmark * .001f), offset.Y + Bounds.Height * .1f), new(pixelSize, Bounds.Height * .3f), bookmarkColor, actualOpacity);

            if (leftTime < bookmark && bookmark < rightTime) drawLine(drawContext, 
                offset + new Vector2((float)Manager.SnapToPixel((bookmark - leftTime) * timeScale), lineBottomY), 
                new(pixelSize, Bounds.Height * .5f), bookmarkColor, actualOpacity);
        }

        // Current time (top)
        var x = timeToXTop(Value);
        {
            Vector2 lineSize = new(pixelSize, Bounds.Height * .4f);
            if (RepeatStart != RepeatEnd)
            {
                drawLine(drawContext, new(timeToXTop(RepeatStart) - pixelSize, offset.Y), lineSize, Color.White, actualOpacity);
                drawLine(drawContext, new(x, offset.Y), lineSize * .6f, Color.White, actualOpacity);
                drawLine(drawContext, new(timeToXTop(RepeatEnd) + pixelSize, offset.Y), lineSize, Color.White, actualOpacity);
            }
            else
            {
                drawLine(drawContext, new(x - pixelSize, offset.Y), lineSize, Color.White, actualOpacity);
                drawLine(drawContext, new(x + pixelSize, offset.Y), lineSize, Color.White, actualOpacity);
            }

            // Current time (bottom)
            var centerX = MathF.Round(Bounds.Width * .5f);
            lineSize = new(pixelSize, Bounds.Height * .4f);
            drawLine(drawContext, offset + new Vector2(centerX - pixelSize, lineBottomY), lineSize, Color.White, actualOpacity);
            drawLine(drawContext, offset + new Vector2(centerX + pixelSize, lineBottomY), lineSize, Color.White, actualOpacity);
        }
    }
    float timeToXTop(double time)
    {
        var progress = (time - MinValue) / (MaxValue - MinValue);
        return (float)Manager.SnapToPixel(AbsolutePosition.X + progress * Width);
    }
    void drawLine(DrawContext drawContext, Vector2 position, Vector2 size, Color color, float opacity)
    {
        line.Color = color;
        line.Draw(drawContext, Manager.Camera, new(position.X, position.Y, size.X, size.Y), opacity);
    }
    public void Scroll(float direction)
    {
        var time = Value * 1000d;
        var timingPoint = project.MainBeatmap.GetTimingPointAt((int)time);

        var stepDuration = timingPoint.BeatDuration / SnapDivisor;
        time += stepDuration * direction;

        var steps = (time - timingPoint.Offset) / stepDuration;
        time = timingPoint.Offset + Math.Round(steps) * stepDuration;

        Value = (float)(time * .001);
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