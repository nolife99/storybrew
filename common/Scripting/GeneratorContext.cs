namespace StorybrewCommon.Scripting;

using System.Buffers;
using System.Collections.Generic;
using Mapset;
using Storyboarding;

/// <summary>
///     Represents a generator context, which provides information about the project, mapset, and
///     beatmap being processed, and methods to interact with the storyboard process.
/// </summary>
public abstract class GeneratorContext
{
    /// <summary>
    ///     The file path of the project.
    /// </summary>
    public abstract string ProjectPath { get; }

    /// <summary>
    ///     The file path of the assets folder for the project.
    /// </summary>
    public abstract string ProjectAssetPath { get; }

    /// <summary>
    ///     The file path of the mapset.
    /// </summary>
    public abstract string MapsetPath { get; }

    /// <summary>
    ///     The beatmap currently being processed.
    /// </summary>
    public abstract Beatmap Beatmap { get; }

    /// <summary>
    ///     All beatmaps in the mapset.
    /// </summary>
    public abstract IEnumerable<Beatmap> Beatmaps { get; }

    /// <summary>
    ///     The duration of the audio.
    /// </summary>
    public abstract float AudioDuration { get; }

    /// <summary>
    ///     Indicates whether the context is multithreaded.
    /// </summary>
    public abstract bool Multithreaded { get; set; }

    /// <summary>
    ///     Adds a dependency at the given path.
    /// </summary>
    public abstract void AddDependency(string path);

    /// <summary>
    ///     Appends a message to the log.
    /// </summary>
    public abstract void AppendLog(string message);

    /// <summary>
    ///     Gets the storyboard layer with the given identifier.
    /// </summary>
    /// <param name="identifier">
    ///     The identifier of the layer to get.
    /// </param>
    public abstract StoryboardLayer GetLayer(string identifier);

    /// <summary>
    ///     Gets the Fast Fourier Transform of the song at the given time.
    /// </summary>
    /// <param name="time">
    ///     The time at which to get the Fast Fourier Transform.
    /// </param>
    /// <param name="path">
    ///     The path of the audio file to get the Fast Fourier Transform of.
    /// </param>
    /// <param name="splitChannels">
    ///     A value indicating whether to split the channels of the audio file.
    /// </param>
    public abstract IMemoryOwner<float> GetFft(float time, string path = null, bool splitChannels = false);

    /// <summary>
    ///     Gets the frequency of the Fast Fourier Transform for the given audio file.
    /// </summary>
    /// <param name="path">
    ///     The path of the audio file to get the frequency of the Fast Fourier Transform for.
    /// </param>
    public abstract float GetFftFrequency(string path = null);
}