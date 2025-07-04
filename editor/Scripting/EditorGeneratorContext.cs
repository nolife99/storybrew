namespace StorybrewEditor.Scripting;

using System;
using System.Buffers;
using System.IO;
using System.Text;
using BrewLib.Audio;
using Mapset;
using Storyboarding;
using StorybrewCommon.Mapset;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;
using Tiny.PooledCollections.Generic.StructBased;
using Tiny.PooledCollections.Generic.StructBased.Internals;
using Util;

public sealed class EditorGeneratorContext(Effect effect,
    string projectPath,
    string projectAssetPath,
    string mapsetPath,
    EditorBeatmap beatmap,
    scoped ReadOnlySpan<EditorBeatmap> beatmaps,
    MultiFileWatcher watcher) : GeneratorContext, IDisposable
{
    readonly StringBuilder log = new();
    ValueArray<Beatmap> _beatmaps = getBeatmaps(beatmaps);

    public ReadOnlySpan<EditorStoryboardLayer> EditorLayers => _editorLayers.AsReadOnlySpan();
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

    public override ReadOnlySpan<Beatmap> Beatmaps
    {
        get
        {
            BeatmapDependent = true;
            return _beatmaps.AsReadOnlySpan();
        }
    }

    public bool BeatmapDependent { get; private set; }
    public override bool Multithreaded { get; set; }
    public string Log => log.ToString();

    public void Dispose()
    {
        foreach (var audioStream in fftAudioStreams.Values) audioStream.Dispose();
        fftAudioStreams.Dispose();

        _beatmaps.Dispose();
        _editorLayers.Dispose();
    }

    static ValueArray<Beatmap> getBeatmaps(scoped ReadOnlySpan<EditorBeatmap> beatmaps)
    {
        var result = ValueArray.Create<Beatmap>(beatmaps.Length);
        for (var i = 0; i < beatmaps.Length; ++i) result[i] = beatmaps[i];

        return result;
    }

    public override StoryboardLayer GetLayer(string name)
    {
        foreach (var layer in _editorLayers)
            if (name == layer.Name)
                return layer;

        EditorStoryboardLayer newLayer = new(name, effect);
        _editorLayers.Add(newLayer);
        return newLayer;
    }

    public override void AddDependency(string path) => watcher.Watch(path);
    public override void AppendLog(string message) => log.AppendLine(message);

    #region Audio data

    readonly PooledDictionary<string, FftStream> fftAudioStreams = new();
    readonly PooledList<EditorStoryboardLayer> _editorLayers = new();

    FftStream getFftStream(string path)
    {
        path = Path.GetFullPath(path);

        if (fftAudioStreams.TryGetValue(path, out var audioStream)) return audioStream;

        return fftAudioStreams[path] = new(path);
    }

    public override float AudioDuration => getFftStream(effect.Project.AudioPath).Duration * 1000;

    public override IMemoryOwner<float> GetFft(float time, string path = null, bool splitChannels = false)
        => getFftStream(path ?? effect.Project.AudioPath).GetFft(time * .001f, splitChannels);

    public override float GetFftFrequency(string path = null) => getFftStream(path ?? effect.Project.AudioPath).Frequency;

    #endregion
}