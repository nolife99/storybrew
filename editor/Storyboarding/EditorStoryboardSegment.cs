using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Util;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewEditor.Storyboarding;

public class EditorStoryboardSegment(Effect effect, EditorStoryboardLayer layer, string identifier = null) : StoryboardSegment, DisplayableObject, HasPostProcess
{
    public Effect Effect => effect;
    public EditorStoryboardLayer Layer => layer;
    public override string Name => identifier;

    double startTime, endTime;
    public override double StartTime => startTime;
    public override double EndTime => endTime;

    public bool Highlight;

    public override Vector2 Origin { get; set; }
    public override Vector2 Position { get; set; }
    public override float Rotation { get; set; }
    public override float Scale { get; set; } = 1;
    public override bool ReverseDepth { get; set; }

    public event ChangedHandler OnChanged;
    protected void RaiseChanged(string propertyName)
        => EventHelper.InvokeStrict(() => OnChanged, d => ((ChangedHandler)d)(this, new ChangedEventArgs(propertyName)));

    readonly List<StoryboardObject> storyboardObjects = [];
    readonly List<DisplayableObject> displayableObjects = [];
    readonly List<EventObject> eventObjects = [];
    readonly List<EditorStoryboardSegment> segments = [];
    List<DisplayableObject>[] displayableBuckets;

    public override OsbSprite CreateSprite(string path, OsbOrigin origin, CommandPosition initialPosition)
    {
        EditorOsbSprite storyboardObject = new() { TexturePath = path, Origin = origin, InitialPosition = initialPosition };
        storyboardObjects.Add(storyboardObject);
        displayableObjects.Add(storyboardObject);
        displayableBuckets = null;

        return storyboardObject;
    }

    public override OsbSprite CreateSprite(string path, OsbOrigin origin) => CreateSprite(path, origin, OsbSprite.DefaultPosition);
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
        displayableBuckets = null;

        return storyboardObject;
    }

    public override OsbAnimation CreateAnimation(string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin = OsbOrigin.Centre)
        => CreateAnimation(path, frameCount, frameDelay, loopType, origin, OsbSprite.DefaultPosition);

    public override OsbSample CreateSample(string path, double time, double volume)
    {
        EditorOsbSample storyboardObject = new() { AudioPath = path, Time = time, Volume = volume };
        storyboardObjects.Add(storyboardObject);
        eventObjects.Add(storyboardObject);
        return storyboardObject;
    }

    private readonly Dictionary<string, EditorStoryboardSegment> namedSegments = [];
    public override IEnumerable<StoryboardSegment> NamedSegments => namedSegments.Values;

    public override StoryboardSegment CreateSegment(string identifier = null)
    {
        if (identifier is not null)
        {
            var originalName = identifier;
            var count = 0;
            while (namedSegments.ContainsKey(identifier)) identifier = $"{originalName}#{++count}";
        }
        return getSegment(identifier);
    }
    public override StoryboardSegment GetSegment(string identifier) => getSegment(identifier);

    private StoryboardSegment getSegment(string identifier = null)
    {
        if (identifier is not null && Name is null) throw new InvalidOperationException($"Cannot add a named segment to a segment that isn't named ({identifier})");
        if (identifier is null || !namedSegments.TryGetValue(identifier, out var segment))
        {
            segment = new(Effect, Layer, identifier);
            storyboardObjects.Add(segment);
            displayableObjects.Add(segment);
            displayableBuckets = null;

            segments.Add(segment);
            if (identifier is not null) namedSegments.Add(identifier, segment);
        }
        return segment;
    }

    public override void Discard(StoryboardObject storyboardObject)
    {
        storyboardObjects.Remove(storyboardObject);
        if (storyboardObject is DisplayableObject displayableObject)
        {
            displayableObjects.Remove(displayableObject);
            displayableBuckets = null;
        }
        if (storyboardObject is EventObject eventObject) eventObjects.Remove(eventObject);
        if (storyboardObject is EditorStoryboardSegment segment)
        {
            segments.Remove(segment);
            if (segment.Name != null) namedSegments.Remove(segment.Name);
        }
    }
    public void TriggerEvents(double fromTime, double toTime)
    {
        eventObjects.ForEach(eventObject => eventObject.TriggerEvent(Effect.Project, toTime), eventObject => fromTime <= eventObject.EventTime && eventObject.EventTime < toTime);
        segments.ForEach(s => s.TriggerEvents(fromTime, toTime));
    }
    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, StoryboardTransform transform, Project project, FrameStats frameStats)
    {
        var displayTime = project.DisplayTime * 1000;
        if (displayTime < StartTime || EndTime < displayTime) return;
        if (Layer.Highlight || Effect.Highlight) opacity *= (MathF.Sin(drawContext.Get<Editor>().TimeSource.Current * 4) + 1) * 0.5f;

        StoryboardTransform localTransform = new(transform, Origin, Position, Rotation, Scale);
        if (displayableObjects.Count < 1000)
        {
            foreach (var displayableObject in displayableObjects) displayableObject.Draw(drawContext, camera, bounds, opacity, localTransform, project, frameStats);
        }
        else
        {
            var bucketLength = 10000;
            var segmentDuration = EndTime - StartTime;

            var bucketCount = Math.Max(1, (int)Math.Ceiling(segmentDuration / bucketLength));
            var currentBucketIndex = (int)((displayTime - StartTime) / bucketLength);

            if (displayableBuckets is null) displayableBuckets = new List<DisplayableObject>[bucketCount];

            var currentBucket = displayableBuckets[currentBucketIndex];
            if (currentBucket == null)
            {
                var bucketStartTime = StartTime + currentBucketIndex * bucketLength;
                var bucketEndTime = StartTime + (currentBucketIndex + 1) * bucketLength;
                displayableBuckets[currentBucketIndex] = currentBucket = [];

                displayableObjects.ForEach(displayableObject =>
                {
                    currentBucket.Add(displayableObject);
                    displayableObject.Draw(drawContext, camera, bounds, opacity, localTransform, project, frameStats);
                }, displayableObject => displayableObject.StartTime <= bucketEndTime && bucketStartTime <= displayableObject.EndTime);
            }
            else foreach (var displayableObject in currentBucket) displayableObject.Draw(drawContext, camera, bounds, opacity, localTransform, project, frameStats);
        }
    }
    public void PostProcess()
    {
        if (ReverseDepth)
        {
            storyboardObjects.Reverse();
            displayableObjects.Reverse();
        }
        foreach (var storyboardObject in storyboardObjects) (storyboardObject as HasPostProcess)?.PostProcess();

        startTime = double.MaxValue;
        endTime = double.MinValue;

        foreach (var sbo in storyboardObjects)
        {
            startTime = Math.Min(startTime, sbo.StartTime);
            endTime = Math.Max(endTime, sbo.EndTime);
        }
        displayableBuckets = null;
    }
    public override void WriteOsb(TextWriter writer, ExportSettings exportSettings, OsbLayer osbLayer, StoryboardTransform transform)
    {
        StoryboardTransform localTransform = new(transform, Origin, Position, Rotation, Scale);
        foreach (var sbo in storyboardObjects) sbo.WriteOsb(writer, exportSettings, osbLayer, localTransform);
    }
    public int CalculateSize(OsbLayer osbLayer)
    {
        var exportSettings = new ExportSettings { OptimiseSprites = false };

        using var stream = new ByteCounterStream();
        using var writer = new StreamWriter(stream, Project.Encoding);

        foreach (var sbo in storyboardObjects) sbo.WriteOsb(writer, exportSettings, osbLayer, null);
        return (int)stream.Length;
    }
}