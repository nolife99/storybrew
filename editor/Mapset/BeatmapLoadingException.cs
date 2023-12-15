using System;

namespace StorybrewEditor.Mapset;

public class BeatmapLoadingException : Exception
{
    public BeatmapLoadingException() { }
    public BeatmapLoadingException(string message) : base(message) { }
    public BeatmapLoadingException(string message, Exception innerException) : base(message, innerException) { }
}