namespace StorybrewEditor.Mapset;

using System;

public class BeatmapLoadingException(string message, Exception innerException) : Exception(message, innerException);