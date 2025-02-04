﻿namespace StorybrewCommon.Storyboarding;

using System.IO;

///<summary> Basic class for storyboarding objects. </summary>
public abstract class StoryboardObject
{
    ///<summary> Start time of this storyboard object. </summary>
    public abstract float StartTime { get; }

    ///<summary> End time of this storyboard object. </summary>
    public abstract float EndTime { get; }
#pragma warning disable CS1591
    public abstract void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        OsbLayer layer,
        StoryboardTransform transform);
}