namespace StorybrewCommon.Storyboarding3d;

using System;
using System.Collections.Generic;
using Storyboarding;
using Storyboarding.Util;

///<summary> Represents a 3D object that can generate and manage sprites. </summary>
public interface HasOsbSprites
{
    /// <summary> Gets this 3D sprite's list of <see cref="OsbSprite"/>s. </summary>
    IEnumerable<OsbSprite> Sprites { get; }

    /// <summary> Gets this instance's <see cref="CommandGenerator"/>s. </summary>
    IEnumerable<CommandGenerator> CommandGenerators { get; }

    ///<summary> Runs an action on this instance's base sprite/sprites. </summary>
    void DoTreeSprite(Action<OsbSprite> action);

    /// <summary> Configure the fields for this instance's <see cref="CommandGenerators"/>. </summary>
    void ConfigureGenerators(Action<CommandGenerator> action);
}