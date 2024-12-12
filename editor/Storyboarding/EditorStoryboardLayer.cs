namespace StorybrewEditor.Storyboarding;

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Util;
using SixLabors.ImageSharp;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.CommandValues;

public class EditorStoryboardLayer : StoryboardLayer, IComparable<EditorStoryboardLayer>
{
    public readonly Effect Effect;

    readonly EditorStoryboardSegment segment;

    bool diffSpecific, visible = true;
    public int EstimatedSize;

    public bool Highlight;
    string name = "";

    OsbLayer osbLayer = OsbLayer.Background;

    float startTime, endTime;

    public EditorStoryboardLayer(string identifier, Effect effect) : base(identifier)
    {
        Effect = effect;
        segment = new(effect, this, "Root");
    }

    public string Identifier
    {
        get => name;
        set
        {
            if (name == value) return;
            name = value;
            RaiseChanged(nameof(Identifier));
        }
    }

    public bool Visible
    {
        get => visible;
        set
        {
            if (visible == value) return;
            visible = value;
            RaiseChanged(nameof(Visible));
        }
    }

    public OsbLayer OsbLayer
    {
        get => osbLayer;
        set
        {
            if (osbLayer == value) return;
            osbLayer = value;
            RaiseChanged(nameof(OsbLayer));
        }
    }

    public bool DiffSpecific
    {
        get => diffSpecific;
        set
        {
            if (diffSpecific == value) return;
            diffSpecific = value;
            RaiseChanged(nameof(DiffSpecific));
        }
    }

    public override float StartTime => startTime;
    public override float EndTime => endTime;

    public override Vector2 Origin { get => segment.Origin; set => segment.Origin = value; }

    public override Vector2 Position { get => segment.Position; set => segment.Position = value; }

    public override float Rotation { get => segment.Rotation; set => segment.Rotation = value; }

    public override float Scale { get => segment.Scale; set => segment.Scale = value; }

    public override bool ReverseDepth { get => segment.ReverseDepth; set => segment.ReverseDepth = value; }

    public override IEnumerable<StoryboardSegment> NamedSegments => segment.NamedSegments;

    public int CompareTo(EditorStoryboardLayer other)
    {
        var value = osbLayer - other.osbLayer;
        if (value == 0) value = (other.diffSpecific ? 1 : 0) - (diffSpecific ? 1 : 0);
        return value;
    }

    public event Action<object, ChangedEventArgs> OnChanged;

    void RaiseChanged(string propertyName) => EventHelper.InvokeStrict(OnChanged,
        d => ((Action<object, ChangedEventArgs>)d)(this, new(propertyName)));

    public override OsbSprite CreateSprite(string path, OsbOrigin origin, CommandPosition initialPosition)
        => segment.CreateSprite(path, origin, initialPosition);

    public override OsbSprite CreateSprite(string path, OsbOrigin origin) => segment.CreateSprite(path, origin, new(320, 240));

    public override OsbAnimation CreateAnimation(string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        OsbOrigin origin,
        CommandPosition initialPosition)
        => segment.CreateAnimation(path, frameCount, frameDelay, loopType, origin, initialPosition);

    public override OsbAnimation CreateAnimation(string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        OsbOrigin origin = OsbOrigin.Centre) => segment.CreateAnimation(path, frameCount, frameDelay, loopType, origin);

    public override OsbSample CreateSample(string path, float time, float volume) => segment.CreateSample(path, time, volume);

    public override StoryboardSegment CreateSegment(string identifier = null) => segment.CreateSegment(identifier);
    public override StoryboardSegment GetSegment(string identifier) => segment.GetSegment(identifier);
    public override void Discard(StoryboardObject storyboardObject) => segment.Discard(storyboardObject);

    public void TriggerEvents(float fromTime, float toTime)
    {
        if (Visible) segment.TriggerEvents(fromTime, toTime);
    }

    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, FrameStats frameStats)
    {
        if (Visible) segment.Draw(drawContext, camera, bounds, opacity, StoryboardTransform.Identity, Effect.Project, frameStats);
    }

    public void PostProcess()
    {
        segment.PostProcess();

        startTime = segment.StartTime;
        if (startTime == float.MaxValue) startTime = 0;

        endTime = segment.EndTime;
        if (endTime == float.MinValue) endTime = 0;

        EstimatedSize = segment.CalculateSize(osbLayer);
    }

    public void WriteOsb(TextWriter writer, ExportSettings exportSettings)
        => WriteOsb(writer, exportSettings, osbLayer, StoryboardTransform.Identity);

    public override void WriteOsb(TextWriter writer, ExportSettings exportSettings, OsbLayer layer, StoryboardTransform transform)
        => segment.WriteOsb(writer, exportSettings, layer, transform);

    public void CopySettings(EditorStoryboardLayer other)
    {
        DiffSpecific = other.DiffSpecific;
        OsbLayer = other.OsbLayer;
        Visible = other.Visible;
    }
}