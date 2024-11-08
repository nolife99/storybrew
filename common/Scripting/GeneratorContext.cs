namespace StorybrewCommon.Scripting;

using System.Collections.Generic;
using Mapset;
using Storyboarding;

#pragma warning disable CS1591
public abstract class GeneratorContext
{
    public abstract string ProjectPath { get; }
    public abstract string ProjectAssetPath { get; }
    public abstract string MapsetPath { get; }

    public abstract Beatmap Beatmap { get; }
    public abstract IEnumerable<Beatmap> Beatmaps { get; }

    public abstract float AudioDuration { get; }

    public abstract bool Multithreaded { get; set; }

    public abstract void AddDependency(string path);
    public abstract void AppendLog(string message);
    public abstract StoryboardLayer GetLayer(string identifier);
    public abstract float[] GetFft(float time, string path = null, bool splitChannels = false);
    public abstract float GetFftFrequency(string path = null);
}