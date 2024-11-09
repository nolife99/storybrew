namespace StorybrewCommon.Storyboarding;

using System.Collections.Generic;
using CommandValues;
using osuTK;
using Vector2 = System.Numerics.Vector2;

///<summary> Storyboarding segments for storyboard objects. </summary>
public abstract class StoryboardSegment : StoryboardObject
{
    public abstract string Name { get; }
    public abstract Vector2 Origin { get; set; }
    public abstract Vector2 Position { get; set; }
    public abstract float Rotation { get; set; }

    public float RotationDegrees
    {
        get => MathHelper.RadiansToDegrees(Rotation);
        set => Rotation = MathHelper.DegreesToRadians(value);
    }

    public abstract float Scale { get; set; }

    ///<summary> Reverses the order of sprites, from newer sprites being placed at the bottom of the list. </summary>
    public abstract bool ReverseDepth { get; set; }

    public abstract IEnumerable<StoryboardSegment> NamedSegments { get; }

    ///<summary> Creates a new storyboard segment. </summary>
    public abstract StoryboardSegment CreateSegment(string identifier = null);

    public abstract StoryboardSegment GetSegment(string identifier);

    /// <summary> Creates an <see cref="OsbSprite"/>. </summary>
    /// <param name="path"> Path to the image of this sprite. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of this sprite. </param>
    /// <param name="initialPosition"> The initial <see cref="CommandPosition"/> value of this sprite. </param>
    public abstract OsbSprite CreateSprite(string path, OsbOrigin origin, CommandPosition initialPosition);

    /// <summary> Creates an <see cref="OsbSprite"/>. </summary>
    /// <param name="path"> Path to the image of this sprite. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of this sprite. </param>
    public abstract OsbSprite CreateSprite(string path, OsbOrigin origin = OsbOrigin.Centre);

    /// <summary> Creates an <see cref="OsbAnimation"/>. </summary>
    /// <param name="path"> Path to the image of this animation. </param>
    /// <param name="frameCount"> Amount of frames to loop through in this animation. </param>
    /// <param name="frameDelay"> Delay between frames in this animation. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of this animation. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of this animation. </param>
    /// <param name="initialPosition"> The initial <see cref="CommandPosition"/> value of this animation. </param>
    public abstract OsbAnimation CreateAnimation(string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        OsbOrigin origin,
        CommandPosition initialPosition);

    /// <summary> Creates an <see cref="OsbAnimation"/>. </summary>
    /// <param name="path"> Path to the image of this animation. </param>
    /// <param name="frameCount"> Amount of frames to loop through in this animation. </param>
    /// <param name="frameDelay"> Delay between frames in this animation. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of this animation. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of this animation. </param>
    public abstract OsbAnimation CreateAnimation(string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType = OsbLoopType.LoopForever,
        OsbOrigin origin = OsbOrigin.Centre);

    /// <summary> Creates an <see cref="OsbSample"/>. </summary>
    /// <param name="path"> Path to the audio file of this sample. </param>
    /// <param name="time"> Time for the audio to be played. </param>
    /// <param name="volume"> Volume of the audio sample. </param>
    public abstract OsbSample CreateSample(string path, float time, float volume = 100);

    /// <summary> Removes a storyboard object from the segment. </summary>
    /// <param name="storyboardObject"> The storyboard object to be discarded. </param>
    public abstract void Discard(StoryboardObject storyboardObject);
}