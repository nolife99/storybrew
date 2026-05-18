namespace StorybrewEditor.Storyboarding;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.IO;
using BrewLib.Util;
using SixLabors.ImageSharp;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.CommandValues;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

public sealed class EditorStoryboardSegment(Effect effect, EditorStoryboardLayer layer, string identifier = null)
    : StoryboardSegment, IDisplayable, IPostProcessable
{
    readonly List<IDisplayable> displayableObjects = [];
    readonly List<IEvent> eventObjects = [];
    readonly Dictionary<string, EditorStoryboardSegment> namedSegments = [];
    readonly List<EditorStoryboardSegment> segments = [];
    readonly List<StoryboardObject> storyboardObjects = [];
    readonly List<EditorOsbSprite.DrawWork> spriteDrawWork = [];

    EditorOsbSprite.DrawResult[] spriteDrawResults = [];
    readonly SpriteEvaluationContext spriteEvaluationContext = new();

    float startTime, endTime;
    public override string Name => identifier;

    public override Vector2 Origin { get; set; }
    public override Vector2 Position { get; set; }
    public override float Rotation { get; set; }
    public override float Scale { get; set; } = 1;
    public override bool ReverseDepth { get; set; }
    public override bool FlipX { get; set; }
    public override bool FlipY { get; set; }
    public override IEnumerable<StoryboardSegment> NamedSegments => namedSegments.Values;
    public override float StartTime => startTime;
    public override float EndTime => endTime;

    public void Draw(DrawContext drawContext,
        ICamera camera,
        RectangleF bounds,
        float opacity,
        scoped ref readonly StoryboardTransform transform,
        Project project,
        FrameStats frameStats)
    {
        var displayTime = project.DisplayTime.TotalMilliseconds;
        if (displayTime < startTime || endTime < displayTime) return;

        if (layer.Highlight || effect.Highlight)
            opacity *= ((float)double.Sin(drawContext.Get<Editor>().TimeSource.Current.TotalSeconds * 4) + 1) * .5f;

        StoryboardTransform newTransform = new(transform, Origin, Position, Rotation, Scale, FlipX, FlipY);
        foreach (var o in displayableObjects)
            o.Draw(drawContext, camera, bounds, opacity, ref newTransform, project, frameStats);
    }

    public void DrawParallel(DrawContext drawContext,
        ICamera camera,
        RectangleF bounds,
        float opacity,
        scoped ref readonly StoryboardTransform transform,
        Project project,
        FrameStats frameStats,
        bool altDown,
        float highlightOpacity)
    {
        var displayTime = project.DisplayTime.TotalMilliseconds;
        if (displayTime < startTime || endTime < displayTime) return;

        var parentTransform = transform.Matrix;
        var parentRotationOffset = EditorOsbSprite.GetRotationOffset(parentTransform);
        var parentScaleFactor = EditorOsbSprite.GetScaleFactor(parentTransform);

        var count = spriteDrawWork.Count;
        if (count <= 0) return;

        if (spriteDrawResults.Length < count)
            Array.Resize(ref spriteDrawResults, count);

        spriteEvaluationContext.Reset(spriteDrawWork,
            spriteDrawResults,
            (float)displayTime,
            altDown,
            project.DimFactor,
            Environment.TickCount64,
            opacity,
            highlightOpacity,
            layer.Highlight || effect.Highlight,
            parentTransform,
            parentRotationOffset,
            parentScaleFactor);

        Parallel.For(0, count, spriteEvaluationContext.Evaluate);

        for (var i = 0; i < count; i++)
            EditorOsbSprite.Submit(in spriteDrawResults[i], drawContext, camera, bounds, project, frameStats);
    }

    sealed class SpriteEvaluationContext
    {
        List<EditorOsbSprite.DrawWork> work;
        EditorOsbSprite.DrawResult[] results;
        Matrix3x2 parentTransform;
        float time, dimFactor, opacity, highlightOpacity, parentRotationOffset, parentScaleFactor;
        bool altDown, highlightActive;
        long tickCount;

        public void Reset(List<EditorOsbSprite.DrawWork> work,
            EditorOsbSprite.DrawResult[] results,
            float time,
            bool altDown,
            float dimFactor,
            long tickCount,
            float opacity,
            float highlightOpacity,
            bool highlightActive,
            Matrix3x2 parentTransform,
            float parentRotationOffset,
            float parentScaleFactor)
        {
            this.work = work;
            this.results = results;
            this.time = time;
            this.altDown = altDown;
            this.dimFactor = dimFactor;
            this.tickCount = tickCount;
            this.opacity = opacity;
            this.highlightOpacity = highlightOpacity;
            this.highlightActive = highlightActive;
            this.parentTransform = parentTransform;
            this.parentRotationOffset = parentRotationOffset;
            this.parentScaleFactor = parentScaleFactor;
        }

        public void Evaluate(int index)
            => results[index] = EditorOsbSprite.Evaluate(work[index],
                time,
                altDown,
                dimFactor,
                tickCount,
                opacity,
                highlightOpacity,
                highlightActive,
                parentTransform,
                parentRotationOffset,
                parentScaleFactor);
    }

    void buildSpriteDrawWork(List<EditorOsbSprite.DrawWork> work,
        scoped ref readonly StoryboardTransform transform,
        int highlightDepth)
    {
        StoryboardTransform newTransform = new(transform, Origin, Position, Rotation, Scale, FlipX, FlipY);
        var childHighlightDepth = highlightDepth + 1;

        foreach (var storyboardObject in storyboardObjects)
            switch (storyboardObject)
            {
                case EditorStoryboardSegment segment:
                    segment.buildSpriteDrawWork(work, in newTransform, childHighlightDepth);
                    break;

                case OsbSprite sprite:
                    work.Add(new(sprite, in newTransform, childHighlightDepth));
                    break;
            }
    }

    public void PostProcess()
    {
        if (ReverseDepth)
        {
            storyboardObjects.Reverse();
            displayableObjects.Reverse();
        }

        startTime = float.MaxValue;
        endTime = float.MinValue;

        foreach (var sbo in storyboardObjects)
        {
            if (sbo is IPostProcessable p) p.PostProcess();

            startTime = float.Min(startTime, sbo.StartTime);
            endTime = float.Max(endTime, sbo.EndTime);
        }

        spriteDrawWork.Clear();
        buildSpriteDrawWork(spriteDrawWork, in StoryboardTransform.Identity, 0);
    }

    public void CollectTexturePaths(ISet<string> texturePaths)
    {
        foreach (var storyboardObject in storyboardObjects)
            switch (storyboardObject)
            {
                case EditorStoryboardSegment segment:
                    segment.CollectTexturePaths(texturePaths);
                    break;

                case OsbAnimation animation:
                    addAnimationTexturePaths(texturePaths, animation);
                    break;

                case OsbSprite sprite:
                    addTexturePath(texturePaths, sprite.TexturePath);
                    break;
            }
    }

    static void addTexturePath(ISet<string> texturePaths, string texturePath)
    {
        if (!string.IsNullOrWhiteSpace(texturePath))
            texturePaths.Add(texturePath);
    }

    static void addAnimationTexturePaths(ISet<string> texturePaths, OsbAnimation animation)
    {
        if (animation.FrameCount <= 0)
        {
            addTexturePath(texturePaths, animation.TexturePath);
            return;
        }

        for (var frame = 0; frame < animation.FrameCount; ++frame)
            addTexturePath(texturePaths, getAnimationFramePath(animation.TexturePath, frame));
    }

    static string getAnimationFramePath(string texturePath, int frame)
    {
        var span = texturePath.AsSpan();
        var dotIndex = span.LastIndexOf('.');
        var digits = StringHelper.GetDigitCount(frame);

        Span<char> chars = stackalloc char[span.Length + digits];
        if (dotIndex < 0)
        {
            span.CopyTo(chars);
            frame.TryFormat(chars[span.Length..], out _, default, CultureInfo.InvariantCulture);
        }
        else
        {
            span[..dotIndex].CopyTo(chars);
            frame.TryFormat(chars[dotIndex..], out _, default, CultureInfo.InvariantCulture);
            span[dotIndex..].CopyTo(chars[(dotIndex + digits)..]);
        }

        return chars.ToString();
    }

    public override OsbSprite CreateSprite(string path, OsbOrigin origin, CommandPosition initialPosition)
    {
        EditorOsbSprite sbo = new() { TexturePath = path, Origin = origin, InitialPosition = initialPosition };

        storyboardObjects.Add(sbo);
        displayableObjects.Add(sbo);

        return sbo;
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
            var relativePath = Path.GetFileName(path.AsSpan());
            var dotIndex = relativePath.LastIndexOf('.');
            var dirName = Path.GetDirectoryName(path.AsSpan());

            Regex regex = new($@"{relativePath[..dotIndex]}^\d+\{relativePath[dotIndex..]}$");
            var matchRegex = (string filename) => regex.IsMatch(Path.GetFileName(filename.AsSpan()));

            var mapsetPath = Path.Join(StoryboardObjectGenerator.Current.MapsetPath, dirName);
            if (Directory.Exists(mapsetPath))
                frameCount = Directory.EnumerateFiles(mapsetPath, "*", SearchOption.TopDirectoryOnly).Count(matchRegex);

            var assetPath = Path.Join(StoryboardObjectGenerator.Current.AssetPath, dirName);
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
        OsbLoopType loopType = OsbLoopType.LoopForever,
        OsbOrigin origin = OsbOrigin.Centre)
        => CreateAnimation(path, frameCount, frameDelay, loopType, origin, OsbSprite.DefaultPosition);

    public override OsbSample CreateSample(string path, float time, float volume = 100)
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

    EditorStoryboardSegment getSegment(string id = null)
    {
        if (id is not null && identifier is null)
            throw new InvalidOperationException($"Cannot add a named segment to an unnamed segment ({id})");

        if (id is not null && namedSegments.TryGetValue(id, out var segment)) return segment;

        segment = new(effect, layer, id);
        storyboardObjects.Add(segment);
        displayableObjects.Add(segment);

        segments.Add(segment);
        if (id is not null) namedSegments[id] = segment;

        return segment;
    }

    public override void Discard(StoryboardObject storyboardObject)
    {
        storyboardObjects.Remove(storyboardObject);
        switch (storyboardObject)
        {
            case IDisplayable displayableObject: displayableObjects.Remove(displayableObject); break;
            case IEvent eventObject: eventObjects.Remove(eventObject); break;
        }

        if (storyboardObject is not EditorStoryboardSegment segment) return;

        segments.Remove(segment);
        if (segment.Name is not null) namedSegments.Remove(segment.Name);
    }

    public void TriggerEvents(TimeSpan fromTime, TimeSpan toTime)
    {
        foreach (var eventObject in eventObjects)
            if (fromTime <= eventObject.EventTime && eventObject.EventTime < toTime)
                eventObject.TriggerEvent(effect.Project, toTime);

        foreach (var s in segments) s.TriggerEvents(fromTime, toTime);
    }

    ValueList<(StoryboardObject StoryboardObject, StoryboardTransform Transform)> Flatten(StoryboardTransform transform)
    {
        var result = ValueList.Create<(StoryboardObject, StoryboardTransform)>();

        StoryboardTransform localTransform = new(transform, Origin, Position, Rotation, Scale, FlipX, FlipY);
        foreach (var storyboardObject in storyboardObjects)
        {
            if (storyboardObject is EditorStoryboardSegment segment)
            {
                using var entries = segment.Flatten(localTransform);
                result.AddRange(entries.AsReadOnlySpan());
            }

            result.Add((storyboardObject, localTransform));
        }

        return result;
    }

    public override void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        OsbLayer layer,
        scoped ref readonly StoryboardTransform transform)
    {
        StoryboardTransform newTransform = new(transform, Origin, Position, Rotation, Scale, FlipX, FlipY);
        foreach (var sbo in storyboardObjects) sbo.WriteOsb(writer, exportSettings, layer, in newTransform);
    }

    public int CalculateSize(OsbLayer osbLayer)
    {
        using ByteCountingTextWriter writer = new(Project.Encoding);
        WriteOsb(writer, ExportSettings.Default, osbLayer, in StoryboardTransform.Identity);

        return (int)writer.ByteCount;
    }
}
