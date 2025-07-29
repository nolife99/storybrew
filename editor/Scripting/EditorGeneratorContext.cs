namespace StorybrewEditor.Scripting;

using System;
using System.Buffers;
using System.IO;
using BrewLib.Audio;
using StorybrewCommon.Mapset;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewEditor.Mapset;
using StorybrewEditor.Storyboarding;
using StorybrewEditor.Util;
using Tiny.PooledCollections.Generic.StructBased;
using Tiny.PooledCollections.Generic.StructBased.Internals;

public sealed class EditorGeneratorContext(Effect effect,
    string projectPath,
    string projectAssetPath,
    string mapsetPath,
    EditorBeatmap beatmap,
    scoped ReadOnlySpan<EditorBeatmap> beatmaps,
    MultiFileWatcher watcher) : GeneratorContext, IDisposable
{
    ValueArray<Beatmap> beatmaps = getBeatmaps(beatmaps);
    ValueList<char> log = ValueList.Create<char>();

    public ReadOnlySpan<EditorStoryboardLayer> EditorLayers => editorLayers.AsReadOnlySpan();
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
            return beatmaps.AsReadOnlySpan();
        }
    }

    public bool BeatmapDependent { get; private set; }
    public override bool Multithreaded { get; set; }
    public ReadOnlySpan<char> Log => log.AsReadOnlySpan();

    public void Dispose()
    {
        log.Dispose();

        foreach (var audioStream in fftAudioStreams.Values) audioStream.Dispose();
        fftAudioStreams.Dispose();

        beatmaps.Dispose();
        editorLayers.Dispose();
    }

    static ValueArray<Beatmap> getBeatmaps(scoped ReadOnlySpan<EditorBeatmap> beatmaps)
    {
        var result = ValueArray.Create<Beatmap>(beatmaps.Length);
        for (var i = 0; i < beatmaps.Length; ++i) result[i] = beatmaps[i];

        return result;
    }

    public override StoryboardLayer GetLayer(string identifier)
    {
        foreach (var layer in editorLayers)
            if (identifier == layer.Name)
                return layer;

        EditorStoryboardLayer newLayer = new(identifier, effect);
        editorLayers.Add(newLayer);
        return newLayer;
    }

    public override void AddDependency(string path) => watcher.Watch(path);

    public override void AppendLog(ReadOnlySpan<char> message)
    {
        log.AddRange(message);
        log.Add('\n');
    }

    #region Audio data

    ValueDictionary<string, FftStream> fftAudioStreams = ValueDictionary.Create<string, FftStream>();
    ValueList<EditorStoryboardLayer> editorLayers = ValueList.Create<EditorStoryboardLayer>();

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