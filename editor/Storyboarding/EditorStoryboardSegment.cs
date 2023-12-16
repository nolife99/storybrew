using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Util;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewEditor.Storyboarding;

public class EditorStoryboardSegment(Effect effect, EditorStoryboardLayer layer) : StoryboardSegment, DisplayableObject, HasPostProcess
{
    public readonly Effect Effect = effect;
    public readonly EditorStoryboardLayer Layer = layer;

    double startTime, endTime;
    public override double StartTime => startTime;
    public override double EndTime => endTime;

    public bool Highlight;

    public override bool ReverseDepth { get; set; }

    public event ChangedHandler OnChanged;
    protected void RaiseChanged(string propertyName) => EventHelper.InvokeStrict(() => OnChanged, d => ((ChangedHandler)d)(this, new(propertyName)));

    readonly List<StoryboardObject> storyboardObjects = [];
    readonly List<DisplayableObject> displayableObjects = [];
    readonly List<EventObject> eventObjects = [];
    readonly List<EditorStoryboardSegment> segments = [];

    public int GetActiveSpriteCount(double time) => storyboardObjects.Count(o => ((OsbSprite)o)?.IsActive(time) ?? false);
    public int GetCommandCost(double time) => storyboardObjects.Select(o => (OsbSprite)o).Where(s => s?.IsActive(time) ?? false).Sum(s => s.CommandCost);

    public override OsbSprite CreateSprite(string path, OsbOrigin origin, CommandPosition initialPosition)
    {
        EditorOsbSprite storyboardObject = new()
        {
            TexturePath = path,
            Origin = origin,
            InitialPosition = initialPosition
        };
        storyboardObjects.Add(storyboardObject);
        displayableObjects.Add(storyboardObject);
        return storyboardObject;
    }
    public override OsbSprite CreateSprite(string path, OsbOrigin origin = OsbOrigin.Centre) => CreateSprite(path, origin, OsbSprite.DefaultPosition);

    public override OsbAnimation CreateAnimation(string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, CommandPosition initialPosition)
    {
        EditorOsbAnimation storyboardObject = new()
        {
            TexturePath = path,
            Origin = origin,
            FrameCount = frameCount,
            FrameDelay = frameDelay,
            LoopType = loopType,
            InitialPosition = initialPosition
        };

        storyboardObjects.Add(storyboardObject);
        displayableObjects.Add(storyboardObject);

        return storyboardObject;
    }

    public override OsbAnimation CreateAnimation(string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin = OsbOrigin.Centre)
        => CreateAnimation(path, frameCount, frameDelay, loopType, origin, OsbSprite.DefaultPosition);

    public override OsbSample CreateSample(string path, double time, double volume)
    {
        EditorOsbSample storyboardObject = new()
        {
            AudioPath = path,
            Time = time,
            Volume = volume
        };

        storyboardObjects.Add(storyboardObject);
        eventObjects.Add(storyboardObject);

        return storyboardObject;
    }
    public override StoryboardSegment CreateSegment()
    {
        EditorStoryboardSegment segment = new(Effect, Layer);

        storyboardObjects.Add(segment);
        displayableObjects.Add(segment);
        segments.Add(segment);

        return segment;
    }
    public override void Discard(StoryboardObject storyboardObject)
    {
        storyboardObjects.Remove(storyboardObject);
        if (storyboardObject is DisplayableObject displayableObject) displayableObjects.Remove(displayableObject);
        if (storyboardObject is EventObject eventObject) eventObjects.Remove(eventObject);
        if (storyboardObject is EditorStoryboardSegment segment) segments.Remove(segment);
    }
    public void TriggerEvents(double fromTime, double toTime)
    {
        eventObjects.ForEachUnsafe(eventObject =>
        {
            if (fromTime <= eventObject.EventTime && eventObject.EventTime < toTime) eventObject.TriggerEvent(Effect.Project, toTime);
        });
        segments.ForEachUnsafe(s => s.TriggerEvents(fromTime, toTime));
    }
    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, Project project, FrameStats frameStats)
    {
        var displayTime = project.DisplayTime * 1000;
        if (displayTime < StartTime || EndTime < displayTime) return;

        if (Layer.Highlight || Effect.Highlight) opacity *= (float)((Math.Cos(drawContext.Get<Editor>().TimeSource.Current * 4) + 1) * .5);

        displayableObjects.ForEachUnsafe(sprite => sprite.Draw(drawContext, camera, bounds, opacity, project, frameStats));
    }
    public void PostProcess()
    {
        if (ReverseDepth)
        {
            storyboardObjects.Reverse();
            displayableObjects.Reverse();
        }

        storyboardObjects.ForEachUnsafe(sbo => (sbo as HasPostProcess)?.PostProcess());

        startTime = double.MaxValue;
        endTime = double.MinValue;

        storyboardObjects.ForEachUnsafe(sbo =>
        {
            startTime = Math.Min(startTime, sbo.StartTime);
            endTime = Math.Max(endTime, sbo.EndTime);
        });
    }
    public override void WriteOsb(TextWriter writer, ExportSettings exportSettings, OsbLayer osbLayer)
        => storyboardObjects.ForEachUnsafe(sbo => sbo.WriteOsb(writer, exportSettings, osbLayer));

    public int CalculateSize(OsbLayer osbLayer)
    {
        using ByteCounterStream stream = new();
        using (StreamWriter writer = new(stream, Project.Encoding)) storyboardObjects.ForEachUnsafe(sbo => sbo.WriteOsb(writer, Effect.Project.ExportSettings, osbLayer));
        return (int)stream.Length;
    }
}