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

public class EditorStoryboardLayer : StoryboardLayer, IComparable<EditorStoryboardLayer>
{
    public Guid Guid = Guid.NewGuid();

    string name = "";
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

    public readonly Effect Effect;

    bool visible = true;
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

    OsbLayer osbLayer = OsbLayer.Background;
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

    bool diffSpecific;
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

    double startTime, endTime;
    public override double StartTime => startTime;
    public override double EndTime => endTime;

    public override Vector2 Origin
    {
        get => segment.Origin;
        set => segment.Origin = value;
    }
    public override Vector2 Position
    {
        get => segment.Position;
        set => segment.Position = value;
    }
    public override float Rotation
    {
        get => segment.Rotation;
        set => segment.Rotation = value;
    }
    public override float Scale
    {
        get => segment.Scale;
        set => segment.Scale = value;
    }
    public override bool ReverseDepth
    {
        get => segment.ReverseDepth;
        set => segment.ReverseDepth = value;
    }

    public bool Highlight;
    public int EstimatedSize;

    public event ChangedHandler OnChanged;
    protected void RaiseChanged(string propertyName) => EventHelper.InvokeStrict(() => OnChanged, d => ((ChangedHandler)d)(this, new(propertyName)));

    readonly EditorStoryboardSegment segment;

    public EditorStoryboardLayer(string identifier, Effect effect) : base(identifier)
    {
        Effect = effect;
        segment = new(effect, this, "Root");
    }

    public override OsbSprite CreateSprite(string path, OsbOrigin origin, CommandPosition initialPosition) => segment.CreateSprite(path, origin, initialPosition);
    public override OsbSprite CreateSprite(string path, OsbOrigin origin) => segment.CreateSprite(path, origin, new(320, 240));

    public override OsbAnimation CreateAnimation(string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, CommandPosition initialPosition)
        => segment.CreateAnimation(path, frameCount, frameDelay, loopType, origin, initialPosition);

    public override OsbAnimation CreateAnimation(string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin = OsbOrigin.Centre)
        => segment.CreateAnimation(path, frameCount, frameDelay, loopType, origin);

    public override OsbSample CreateSample(string path, double time, double volume)
        => segment.CreateSample(path, time, volume);

    public override IEnumerable<StoryboardSegment> NamedSegments => segment.NamedSegments;
    public override StoryboardSegment CreateSegment(string identifier = null) => segment.CreateSegment(identifier);
    public override StoryboardSegment GetSegment(string identifier) => segment.GetSegment(identifier);
    public override void Discard(StoryboardObject storyboardObject) => segment.Discard(storyboardObject);

    public void TriggerEvents(double fromTime, double toTime)
    {
        if (Visible) segment.TriggerEvents(fromTime, toTime);
    }
    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, FrameStats frameStats)
    {
        if (Visible) segment.Draw(drawContext, camera, bounds, opacity, null, Effect.Project, frameStats);
    }
    public void PostProcess()
    {
        segment.PostProcess();

        startTime = segment.StartTime;
        if (startTime == double.MaxValue) startTime = 0;

        endTime = segment.EndTime;
        if (endTime == double.MinValue) endTime = 0;

        EstimatedSize = segment.CalculateSize(osbLayer);
    }

    public void WriteOsb(TextWriter writer, ExportSettings exportSettings) => WriteOsb(writer, exportSettings, osbLayer, null);
    public override void WriteOsb(TextWriter writer, ExportSettings exportSettings, OsbLayer layer, StoryboardTransform transform)
        => segment.WriteOsb(writer, exportSettings, layer, transform);

    public void CopySettings(EditorStoryboardLayer other, bool copyGuid = false)
    {
        if (copyGuid) Guid = other.Guid;
        DiffSpecific = other.DiffSpecific;
        OsbLayer = other.OsbLayer;
        Visible = other.Visible;
    }
    public int CompareTo(EditorStoryboardLayer other)
    {
        var value = osbLayer - other.osbLayer;
        if (value == 0) value = (other.diffSpecific ? 1 : 0) - (diffSpecific ? 1 : 0);
        return value;
    }

    public override string ToString() => $"name:{name}, id:{Name}, layer:{osbLayer}, diffSpec:{diffSpecific}";
}