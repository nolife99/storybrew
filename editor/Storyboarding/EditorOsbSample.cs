using System.IO;
using BrewLib.Audio;
using StorybrewCommon.Storyboarding;

namespace StorybrewEditor.Storyboarding;

public class EditorOsbSample : OsbSample, EventObject
{
    public float EventTime => Time * .001f;

    public void TriggerEvent(Project project, float currentTime)
    {
        if (EventTime + 1 < currentTime) return;

        AudioSample sample;
        var fullPath = Path.Combine(project.MapsetPath, AudioPath);
        try
        {
            sample = project.AudioContainer.Get(fullPath);
            if (sample is null)
            {
                fullPath = Path.Combine(project.ProjectAssetFolderPath, AudioPath);
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