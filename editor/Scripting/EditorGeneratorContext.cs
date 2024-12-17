namespace StorybrewEditor.Scripting;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using BrewLib.Audio;
using BrewLib.Util;
using Mapset;
using Storyboarding;
using StorybrewCommon.Mapset;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using Util;

public sealed class EditorGeneratorContext(Effect effect,
    string projectPath,
    string projectAssetPath,
    string mapsetPath,
    EditorBeatmap beatmap,
    IEnumerable<EditorBeatmap> beatmaps,
    MultiFileWatcher watcher) : GeneratorContext, IDisposable
{
    readonly StringBuilder log = new();
    public List<EditorStoryboardLayer> EditorLayers { get; } = [];
    public override string ProjectPath => projectPath;
    public override string ProjectAssetPath => projectAssetPath;

    public override string MapsetPath => Directory.Exists(mapsetPath) ?
        mapsetPath :
        throw new InvalidOperationException($"No existing folder at '{mapsetPath}'");

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

    public bool BeatmapDependent { get; private set; }
    public override bool Multithreaded { get; set; }
    public string Log => log.ToString();

    public void Dispose() => fftAudioStreams.Dispose();

    public override StoryboardLayer GetLayer(string name)
    {
        foreach (var layer in EditorLayers)
            if (name == layer.Name)
                return layer;

        EditorStoryboardLayer newLayer = new(name, effect);
        EditorLayers.Add(newLayer);
        return newLayer;
    }

    public override void AddDependency(string path) => watcher.Watch(path);
    public override void AppendLog(string message) => log.AppendLine(message);

    #region Audio data

    readonly Dictionary<string, FftStream> fftAudioStreams = [];

    FftStream getFftStream(string path)
    {
        path = Path.GetFullPath(path);

        ref var audioStream = ref CollectionsMarshal.GetValueRefOrAddDefault(fftAudioStreams, path, out var exists);
        if (!exists) audioStream = new(path);
        return audioStream;
    }

    public override float AudioDuration => getFftStream(effect.Project.AudioPath).Duration * 1000;

    public override IMemoryOwner<float> GetFft(float time, string path = null, bool splitChannels = false)
        => getFftStream(path ?? effect.Project.AudioPath).GetFft(time * .001f, splitChannels);
    public override float GetFftFrequency(string path = null) => getFftStream(path ?? effect.Project.AudioPath).Frequency;

    #endregion
}