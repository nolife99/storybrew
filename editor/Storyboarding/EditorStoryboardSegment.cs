namespace StorybrewEditor.Storyboarding;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Util;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.CommandValues;

public class EditorStoryboardSegment(Effect effect, EditorStoryboardLayer layer, string identifier = null)
    : StoryboardSegment, DisplayableObject, HasPostProcess
{
    readonly List<DisplayableObject> displayableObjects = [];
    readonly List<EventObject> eventObjects = [];
    readonly Dictionary<string, EditorStoryboardSegment> namedSegments = [];
    readonly List<EditorStoryboardSegment> segments = [];
    readonly List<StoryboardObject> storyboardObjects = [];

    float startTime, endTime;
    public Effect Effect => effect;
    public EditorStoryboardLayer Layer => layer;
    public override string Name => identifier;

    public override Vector2 Origin { get; set; }
    public override Vector2 Position { get; set; }
    public override float Rotation { get; set; }
    public override float Scale { get; set; } = 1;
    public override bool ReverseDepth { get; set; }
    public override IEnumerable<StoryboardSegment> NamedSegments => namedSegments.Values;
    public override float StartTime => startTime;
    public override float EndTime => endTime;

    public void Draw(DrawContext drawContext,
        Camera camera,
        RectangleF bounds,
        float opacity,
        StoryboardTransform transform,
        Project project,
        FrameStats frameStats)
    {
        var displayTime = project.DisplayTime * 1000;
        if (displayTime < StartTime || EndTime < displayTime) return;
        if (Layer.Highlight || Effect.Highlight)
            opacity *= (MathF.Sin(drawContext.Get<Editor>().TimeSource.Current * 4) + 1) * .5f;

        StoryboardTransform localTransform = new(transform, Origin, Position, Rotation, Scale);
        foreach (var o in displayableObjects) o.Draw(drawContext, camera, bounds, opacity, localTransform, project, frameStats);
    }

    public void PostProcess()
    {
        if (ReverseDepth)
        {
            storyboardObjects.Reverse();
            displayableObjects.Reverse();
        }

        foreach (var storyboardObject in storyboardObjects) (storyboardObject as HasPostProcess)?.PostProcess();

        startTime = float.MaxValue;
        endTime = float.MinValue;

        foreach (var sbo in storyboardObjects)
        {
            startTime = Math.Min(startTime, sbo.StartTime);
            endTime = Math.Max(endTime, sbo.EndTime);
        }
    }

    public event Action<object, ChangedEventArgs> OnChanged;

    protected void RaiseChanged(string propertyName) => EventHelper.InvokeStrict(() => OnChanged,
        d => ((Action<object, ChangedEventArgs>)d)(this, new(propertyName)));

    public override OsbSprite CreateSprite(string path, OsbOrigin origin, CommandPosition initialPosition)
    {
        EditorOsbSprite storyboardObject = new() { TexturePath = path, Origin = origin, InitialPosition = initialPosition };

        storyboardObjects.Add(storyboardObject);
        displayableObjects.Add(storyboardObject);

        return storyboardObject;
    }

    public override OsbSprite CreateSprite(string path, OsbOrigin origin = OsbOrigin.Centre)
        => CreateSprite(path, origin, OsbSprite.DefaultPosition);

    public override OsbAnimation CreateAnimation(string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        OsbOrigin origin,
        CommandPosition initialPosition)
    {
        if (frameCount < 1)
        {
            var relativePath = Path.GetFileName(path).AsSpan();
            var dotIndex = relativePath.LastIndexOf('.');
            var dirName = Path.GetDirectoryName(path);

            Regex regex = new($@"{relativePath[..dotIndex]}^\d+\{relativePath[dotIndex..]}$");
            bool matchRegex(string filename) => regex.IsMatch(Path.GetFileName(filename));

            var mapsetPath = Path.Combine(StoryboardObjectGenerator.Current.MapsetPath, dirName);
            if (Directory.Exists(mapsetPath))
                frameCount = Directory.EnumerateFiles(mapsetPath, "*", SearchOption.TopDirectoryOnly).Count(matchRegex);

            var assetPath = Path.Combine(StoryboardObjectGenerator.Current.AssetPath, dirName);
            if (frameCount < 1 && Directory.Exists(assetPath))
                frameCount = Directory.EnumerateFiles(assetPath, "*", SearchOption.TopDirectoryOnly).Count(matchRegex);
        }

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

    public override OsbAnimation CreateAnimation(string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        OsbOrigin origin = OsbOrigin.Centre)
        => CreateAnimation(path, frameCount, frameDelay, loopType, origin, OsbSprite.DefaultPosition);

    public override OsbSample CreateSample(string path, float time, float volume)
    {
        EditorOsbSample storyboardObject = new() { AudioPath = path, Time = time, Volume = volume };

        storyboardObjects.Add(storyboardObject);
        eventObjects.Add(storyboardObject);
        return storyboardObject;
    }

    public override StoryboardSegment CreateSegment(string identifier = null)
    {
        if (identifier is null) return getSegment(identifier);
        var originalName = identifier;
        var count = 0;
        while (namedSegments.ContainsKey(identifier)) identifier = $"{originalName}#{++count}";

        return getSegment(identifier);
    }

    public override StoryboardSegment GetSegment(string identifier) => getSegment(identifier);

    EditorStoryboardSegment getSegment(string identifier = null)
    {
        if (identifier is not null && Name is null)
            throw new InvalidOperationException($"Cannot add a named segment to a segment that isn't named ({identifier})");

        if (identifier is not null && namedSegments.TryGetValue(identifier, out var segment)) return segment;
        segment = new(Effect, Layer, identifier);
        storyboardObjects.Add(segment);
        displayableObjects.Add(segment);

        segments.Add(segment);
        if (identifier is not null) namedSegments[identifier] = segment;

        return segment;
    }

    public override void Discard(StoryboardObject storyboardObject)
    {
        storyboardObjects.Remove(storyboardObject);
        switch (storyboardObject)
        {
            case DisplayableObject displayableObject:
                displayableObjects.Remove(displayableObject);
                break;
            case EventObject eventObject:
                eventObjects.Remove(eventObject);
                break;
        }

        if (storyboardObject is not EditorStoryboardSegment segment) return;
        segments.Remove(segment);
        if (segment.Name is not null) namedSegments.Remove(segment.Name);
    }

    public void TriggerEvents(float fromTime, float toTime)
    {
        foreach (var eventObject in eventObjects)
            if (fromTime <= eventObject.EventTime && eventObject.EventTime < toTime)
                eventObject.TriggerEvent(Effect.Project, toTime);

        foreach (var s in segments) s.TriggerEvents(fromTime, toTime);
    }

    public override void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        OsbLayer osbLayer,
        StoryboardTransform transform)
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