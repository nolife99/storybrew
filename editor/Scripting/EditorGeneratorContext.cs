﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BrewLib.Audio;
using BrewLib.Util;
using StorybrewCommon.Mapset;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewEditor.Mapset;
using StorybrewEditor.Storyboarding;
using StorybrewEditor.Util;

namespace StorybrewEditor.Scripting;

public sealed class EditorGeneratorContext(Effect effect, string projectPath, string projectAssetPath, string mapsetPath, EditorBeatmap beatmap, IEnumerable<EditorBeatmap> beatmaps, MultiFileWatcher watcher) : GeneratorContext, IDisposable
{
    public override string ProjectPath => projectPath;
    public override string ProjectAssetPath => projectAssetPath;
    public override string MapsetPath => Directory.Exists(mapsetPath) ? mapsetPath : throw new InvalidOperationException($"No existing folder at '{mapsetPath}'");

    public override Beatmap Beatmap
    {
        get
        {
            BeatmapDependent = true;
            return beatmap;
        }
    }
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
        if (layer is null) EditorLayers.Add(layer = new(name, effect));
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
    public override Span<float> GetFft(double time, string path = null, bool splitChannels = false) => getFftStream(path ?? effect.Project.AudioPath).GetFft(time * .001, splitChannels);
    public override float GetFftFrequency(string path = null) => getFftStream(path ?? effect.Project.AudioPath).Frequency;

    #endregion

    public void Dispose() => fftAudioStreams.Dispose();
}