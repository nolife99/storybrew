using BrewLib.Audio;
using BrewLib.Util;
using StorybrewCommon.Mapset;
using StorybrewCommon.Storyboarding;
using StorybrewEditor.Mapset;
using StorybrewEditor.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StorybrewEditor.Storyboarding
{
    public sealed class EditorGeneratorContext(Effect effect, string projectPath, string projectAssetPath, string mapsetPath, EditorBeatmap beatmap, IEnumerable<EditorBeatmap> beatmaps, MultiFileWatcher watcher) : GeneratorContext, IDisposable
    {
        readonly Effect effect = effect;
        readonly MultiFileWatcher watcher = watcher;

        readonly string projectPath = projectPath, projectAssetPath = projectAssetPath, mapsetPath = mapsetPath;
        public override string ProjectPath => projectPath;
        public override string ProjectAssetPath => projectAssetPath;
        public override string MapsetPath
        {
            get
            {
                if (!Directory.Exists(mapsetPath)) throw new InvalidOperationException($"The mapset folder at '{mapsetPath}' doesn't exist");
                return mapsetPath;
            }
        }

        readonly EditorBeatmap beatmap = beatmap;
        public override Beatmap Beatmap
        {
            get
            {
                BeatmapDependent = true;
                return beatmap;
            }
        }
        readonly IEnumerable<EditorBeatmap> beatmaps = beatmaps;
        public override IEnumerable<Beatmap> Beatmaps
        {
            get
            {
                BeatmapDependent = true;
                return beatmaps;
            }
        }

        public bool BeatmapDependent { get; set; }
        public override bool Multithreaded { get; set; }

        readonly StringBuilder log = new();
        public string Log => log.ToString();

        public List<EditorStoryboardLayer> EditorLayers = [];

        public override StoryboardLayer GetLayer(string name)
        {
            var layer = EditorLayers.Find(l => l.Name == name);
            if (layer == null) EditorLayers.Add(layer = new(name, effect));
            return layer;
        }

        public override void AddDependency(string path) => watcher.Watch(path);
        public override void AppendLog(string message) => log.AppendLine(message);

        #region Audio data

        readonly Dictionary<string, FftStream> fftAudioStreams = [];
        FftStream getFftStream(string path)
        {
            path = Path.GetFullPath(path);

            if (!fftAudioStreams.TryGetValue(path, out var audioStream)) fftAudioStreams[path] = audioStream = new(path);
            return audioStream;
        }

        public override double AudioDuration => getFftStream(effect.Project.AudioPath).Duration * 1000;
        public override float[] GetFft(double time, string path = null, bool splitChannels = false) => getFftStream(path ?? effect.Project.AudioPath).GetFft(time * .001, splitChannels);
        public override float GetFftFrequency(string path = null) => getFftStream(path ?? effect.Project.AudioPath).Frequency;

        #endregion

        public void Dispose() => fftAudioStreams.Dispose();
    }
}