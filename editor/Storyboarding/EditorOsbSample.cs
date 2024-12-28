namespace StorybrewEditor.Storyboarding;

using System;
using System.IO;
using BrewLib.Audio;
using BrewLib.Memory;
using BrewLib.Util;
using StorybrewCommon.Storyboarding;

public class EditorOsbSample : OsbSample, IEvent
{
    public float EventTime => Time * .001f;

    public void TriggerEvent(Project project, float currentTime)
    {
        if (EventTime + 1 < currentTime) return;

        Span<char> span = stackalloc char[project.MapsetPath.Length + AudioPath.Length + 1];
        Path.TryJoin(project.MapsetPath, AudioPath, span, out _);
        PathHelper.WithStandardSeparatorsUnsafe(span);
        var fullPath = StringPool.GetOrAdd(span);

        AudioSample sample;
        try
        {
            sample = project.AudioContainer.Get(fullPath);
            if (sample is null)
            {
                Span<char> span2 = stackalloc char[project.ProjectAssetFolderPath.Length + AudioPath.Length + 1];
                Path.TryJoin(project.ProjectAssetFolderPath, AudioPath, span2, out _);
                PathHelper.WithStandardSeparatorsUnsafe(span2);

                fullPath = StringPool.GetOrAdd(span2);
                sample = project.AudioContainer.Get(fullPath);
            }
        }
        catch (IOException)
        {
            // Happens when another process is writing to the file, will try again later.
            return;
        }

        sample.Play(Volume * .01f);
    }
}