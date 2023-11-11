using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Util;
using OpenTK;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.CommandValues;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace StorybrewEditor.Storyboarding
{
    public class EditorStoryboardSegment : StoryboardSegment, DisplayableObject, HasPostProcess
    {
        public Effect Effect { get; }
        public EditorStoryboardLayer Layer { get; }

        double startTime, endTime;
        public override double StartTime => startTime;
        public override double EndTime => endTime;

        public bool Highlight;

        public override bool ReverseDepth { get; set; }

        public event ChangedHandler OnChanged;
        protected void RaiseChanged(string propertyName) => EventHelper.InvokeStrict(() => OnChanged, d => ((ChangedHandler)d)(this, new ChangedEventArgs(propertyName)));

        readonly List<StoryboardObject> storyboardObjects = new();
        readonly List<DisplayableObject> displayableObjects = new();
        readonly List<EventObject> eventObjects = new();
        readonly List<EditorStoryboardSegment> segments = new();

        public EditorStoryboardSegment(Effect effect, EditorStoryboardLayer layer)
        {
            Effect = effect;
            Layer = layer;
        }

        public int GetActiveSpriteCount(double time) => storyboardObjects.Count(o => ((OsbSprite)o)?.IsActive(time) ?? false);
        public int GetCommandCost(double time) => storyboardObjects.Select(o => (OsbSprite)o).Where(s => s?.IsActive(time) ?? false).Sum(s => s.CommandCost);

        public override OsbSprite CreateSprite(string path, OsbOrigin origin, CommandPosition initialPosition)
        {
            var storyboardObject = new EditorOsbSprite
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
            var storyboardObject = new EditorOsbAnimation
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
            var storyboardObject = new EditorOsbSample
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
            var segment = new EditorStoryboardSegment(Effect, Layer);
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
            eventObjects.ForEach(eventObject =>
            {
                if (fromTime <= eventObject.EventTime && eventObject.EventTime < toTime) eventObject.TriggerEvent(Effect.Project, toTime);
            });
            segments.ForEach(s => s.TriggerEvents(fromTime, toTime));
        }
        public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, Project project, FrameStats frameStats)
        {
            var displayTime = project.DisplayTime * 1000;
            if (displayTime < StartTime || EndTime < displayTime) return;

            if (Layer.Highlight || Effect.Highlight) opacity *= (float)((Math.Cos(drawContext.Get<Editor>().TimeSource.Current * 4) + 1) / 2);

            for (var i = 0; i < displayableObjects.Count; ++i) displayableObjects[i].Draw(drawContext, camera, bounds, opacity, project, frameStats);
        }
        public void PostProcess()
        {
            if (ReverseDepth)
            {
                storyboardObjects.Reverse();
                displayableObjects.Reverse();
            }

            storyboardObjects.ForEach(sbo => (sbo as HasPostProcess)?.PostProcess());

            startTime = double.MaxValue;
            endTime = double.MinValue;

            storyboardObjects.ForEach(sbo =>
            {
                startTime = Math.Min(startTime, sbo.StartTime);
                endTime = Math.Max(endTime, sbo.EndTime);
            });
        }
        public override void WriteOsb(TextWriter writer, ExportSettings exportSettings, OsbLayer osbLayer)
            => storyboardObjects.ForEach(sbo => sbo.WriteOsb(writer, exportSettings, osbLayer));

        public long CalculateSize(OsbLayer osbLayer)
        {
            using (var stream = new ByteCounterStream()) using (var writer = new StreamWriter(stream, Project.Encoding))
            {
                for (var i = 0; i < storyboardObjects.Count; ++i) storyboardObjects[i].WriteOsb(writer, Effect.Project.ExportSettings, osbLayer);
                return stream.Length;
            }
        }
    }
}