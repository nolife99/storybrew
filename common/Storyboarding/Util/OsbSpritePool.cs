using System;
using System.Collections.Generic;
using BrewLib.Util;
using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewCommon.Storyboarding.Util;

///<summary> Provides a way to optimize filesize and creates a way for sprites to be reused. </summary>
///<remarks> Constructs a <see cref="OsbSpritePool"/>. </remarks>
///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
///<param name="path"> Image path of the available sprite. </param>
///<param name="origin"> <see cref="OsbOrigin"/> of the sprites in the pool. </param>
///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprites in the pool. </param>
///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
public class OsbSpritePool(StoryboardSegment segment, string path, OsbOrigin origin, CommandPosition position, Action<OsbSprite, double, double> attributes = null) : IDisposable
{
    readonly List<PooledSprite> pooled = [];

    ///<summary> The maximum duration for a sprite to be pooled. </summary>
    public int MaxPoolDuration;

    ///<summary> Constructs a <see cref="OsbSpritePool"/>. </summary>
    ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="origin"> <see cref="OsbOrigin"/> of the sprites in the pool. </param>
    ///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
    public OsbSpritePool(StoryboardSegment segment, string path, OsbOrigin origin, Action<OsbSprite, double, double> attributes = null)
        : this(segment, path, origin, default, attributes) { }

    ///<summary> Constructs a <see cref="OsbSpritePool"/>. </summary>
    ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprites in the pool. </param>
    ///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
    public OsbSpritePool(StoryboardSegment segment, string path, CommandPosition position, Action<OsbSprite, double, double> attributes = null)
        : this(segment, path, OsbOrigin.Centre, position, attributes) { }

    ///<summary> Constructs a <see cref="OsbSpritePool"/>. </summary>
    ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
    ///<param name="path"> Image path of the sprite to be used. </param>
    ///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
    public OsbSpritePool(StoryboardSegment segment, string path, Action<OsbSprite, double, double> attributes = null)
        : this(segment, path, OsbOrigin.Centre, default, attributes) { }

    ///<summary> Constructs a <see cref="OsbSpritePool"/>. </summary>
    ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
    ///<param name="path"> Image path of the sprite to be used. </param>
    ///<param name="origin"> <see cref="OsbOrigin"/> of the sprites in the pool. </param>
    ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprites in the pool. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbSpritePool(StoryboardSegment segment, string path, OsbOrigin origin, CommandPosition position, bool additive)
        : this(segment, path, origin, position, additive ?
        (pS, sT, eT) => pS.Additive(sT) : null)
    { }

    ///<summary> Constructs a <see cref="OsbSpritePool"/>. </summary>
    ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
    ///<param name="path"> Image path of the sprite to be used. </param>
    ///<param name="origin"> <see cref="OsbOrigin"/> of the sprites in the pool. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbSpritePool(StoryboardSegment segment, string path, OsbOrigin origin, bool additive)
        : this(segment, path, origin, default, additive) { }

    ///<summary> Constructs a <see cref="OsbSpritePool"/>. </summary>
    ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
    ///<param name="path"> Image path of the sprite to be used. </param>
    ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprites in the pool. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbSpritePool(StoryboardSegment segment, string path, CommandPosition position, bool additive)
        : this(segment, path, OsbOrigin.Centre, position, additive) { }

    ///<summary> Constructs a <see cref="OsbSpritePool"/>. </summary>
    ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
    ///<param name="path"> Image path of the sprite to be used. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbSpritePool(StoryboardSegment segment, string path, bool additive)
        : this(segment, path, OsbOrigin.Centre, default, additive) { }

    ///<summary> Gets an available sprite from the sprite pool. </summary>
    ///<remarks> You must input the correct start time and end time of the sprite for proper pooling. </remarks>
    ///<param name="startTime"> The start time for this sprite. </param>
    ///<param name="endTime"> The end time for this sprite. </param>
    public OsbSprite Get(double startTime, double endTime)
    {
        PooledSprite result = null;
        pooled.ForEach(sprite =>
        {
            result = sprite;
            return;
        }, sprite => 
            (MaxPoolDuration > 0 ? sprite.EndTime <= startTime && startTime < sprite.StartTime + MaxPoolDuration : sprite.EndTime <= startTime) && 
            (result is null || sprite.StartTime < result.StartTime));

        if (result is not null)
        {
            result.EndTime = endTime;
            return result.Sprite;
        }

        var sprite = CreateSprite(segment, path, origin, position);
        pooled.Add(new(sprite, startTime, endTime));

        return sprite;
    }

#pragma warning disable CS1591
    protected virtual OsbSprite CreateSprite(StoryboardSegment segment, string path, OsbOrigin origin, CommandPosition position)
#pragma warning restore CS1591
        => segment.CreateSprite(path, origin, position);

    class PooledSprite(OsbSprite sprite, double startTime, double endTime)
    {
        internal OsbSprite Sprite = sprite;
        internal double StartTime = startTime, EndTime = endTime;
    }

    bool disposed;

    ///<inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    internal virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (attributes is not null && disposing)
            {
                pooled.ForEach(pooledSprite =>
                {
                    var sprite = pooledSprite.Sprite;
                    attributes(sprite, sprite.StartTime, pooledSprite.EndTime);
                });
                disposed = true;
            }
            pooled.Clear();
        }
    }
}

///<summary> Provides a way to optimize filesize and creates a way for sprites to be reused at a minor cost of performance. </summary>
///<remarks> Includes support for animation pools. </remarks>
///<remarks> Constructs a <see cref="OsbSpritePools"/>. </remarks>
///<param name="segment"> <see cref="StoryboardSegment"/> of the sprites in the pool. </param>
public sealed class OsbSpritePools(StoryboardSegment segment) : IDisposable
{
    readonly Dictionary<string, OsbSpritePool> pools = [];
    readonly Dictionary<string, OsbAnimationPool> animationPools = [];
    int maxPoolDuration;

    ///<summary> The maximum duration for a sprite to be pooled. </summary>
    public int MaxPoolDuration
    {
        get => maxPoolDuration;
        set
        {
            if (maxPoolDuration == value) return;
            maxPoolDuration = value;
            foreach (var pool in pools) pool.Value.MaxPoolDuration = maxPoolDuration;
        }
    }

    ///<summary> Gets an available sprite from the sprite pools. </summary>
    ///<param name="startTime"> The start time of the available sprite. </param>
    ///<param name="endTime"> The end time of the available sprite. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="origin"> <see cref="OsbOrigin"/> of the available sprite. </param>
    ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    ///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(double startTime, double endTime, string path, OsbOrigin origin, CommandPosition position, Action<OsbSprite, double, double> attributes = null, int group = 0)
        => getPool(path, origin, position, attributes, group).Get(startTime, endTime);

    ///<summary> Gets an available sprite from the sprite pools. </summary>
    ///<param name="startTime"> The start time of the available sprite. </param>
    ///<param name="endTime"> The end time of the available sprite. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    ///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(double startTime, double endTime, string path, CommandPosition position, Action<OsbSprite, double, double> attributes = null, int group = 0)
        => Get(startTime, endTime, path, OsbOrigin.Centre, position, attributes, group);

    ///<summary> Gets an available sprite from the sprite pools. </summary>
    ///<param name="startTime"> The start time of the available sprite. </param>
    ///<param name="endTime"> The end time of the available sprite. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="origin"> <see cref="OsbOrigin"/> of the available sprite. </param>
    ///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(double startTime, double endTime, string path, OsbOrigin origin, Action<OsbSprite, double, double> attributes = null, int group = 0)
        => Get(startTime, endTime, path, origin, default, attributes, group);

    ///<summary> Gets an available sprite from the sprite pools. </summary>
    ///<param name="startTime"> The start time of the available sprite. </param>
    ///<param name="endTime"> The end time of the available sprite. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="attributes"> Common commands to be run on each sprite in the pool, in an <see cref="Action{OsbSprite, Double, Double}"/> block. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(double startTime, double endTime, string path, Action<OsbSprite, double, double> attributes = null, int group = 0)
        => Get(startTime, endTime, path, OsbOrigin.Centre, default, attributes, group);

    ///<summary> Gets an available sprite from the sprite pools. </summary>
    ///<param name="startTime"> The start time of the available sprite. </param>
    ///<param name="endTime"> The end time of the available sprite. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="origin"> <see cref="OsbOrigin"/> of the available sprite. </param>
    ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(double startTime, double endTime, string path, OsbOrigin origin, CommandPosition position, bool additive, int group = 0)
        => Get(startTime, endTime, path, origin, position, additive ?
        (pS, sT, eT) => pS.Additive(sT) : null, group);

    ///<summary> Gets an available sprite from the sprite pools. </summary>
    ///<param name="startTime"> The start time of the available sprite. </param>
    ///<param name="endTime"> The end time of the available sprite. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="origin"> <see cref="OsbOrigin"/> of the available sprite. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(double startTime, double endTime, string path, OsbOrigin origin, bool additive, int group = 0)
        => Get(startTime, endTime, path, origin, default, additive, group);

    ///<summary> Gets an available sprite from the sprite pools. </summary>
    ///<param name="startTime"> The start time of the available sprite. </param>
    ///<param name="endTime"> The end time of the available sprite. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(double startTime, double endTime, string path, CommandPosition position, bool additive, int group = 0)
        => Get(startTime, endTime, path, OsbOrigin.Centre, position, additive, group);

    ///<summary> Gets an available sprite from the sprite pools. </summary>
    ///<param name="startTime"> The start time of the available sprite. </param>
    ///<param name="endTime"> The end time of the available sprite. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(double startTime, double endTime, string path, bool additive, int group = 0)
        => Get(startTime, endTime, path, OsbOrigin.Centre, default, additive, group);

    ///<summary> Gets an available animation from the pools. </summary>
    ///<param name="startTime"> The start time of the available animation. </param>
    ///<param name="endTime"> The end time of the available animation. </param>
    ///<param name="path"> Image path of the available animation. </param>
    ///<param name="frameCount"> Frame count of the available animation. </param>
    ///<param name="frameDelay"> Delay between frames of the available animation. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the available animation. </param>
    ///<param name="origin"> <see cref="OsbOrigin"/> of the available sprite. </param>
    ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    ///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(double startTime, double endTime, string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, CommandPosition position, Action<OsbSprite, double, double> attributes = null, int group = 0)
        => (OsbAnimation)getPool(path, frameCount, frameDelay, loopType, origin, position, attributes, group).Get(startTime, endTime);

    ///<summary> Gets an available animation from the pools. </summary>
    ///<param name="startTime"> The start time of the available animation. </param>
    ///<param name="endTime"> The end time of the available animation. </param>
    ///<param name="path"> Image path of the available animation. </param>
    ///<param name="frameCount"> Frame count of the available animation. </param>
    ///<param name="frameDelay"> Delay between frames of the available animation. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the available animation. </param>
    ///<param name="origin"> <see cref="OsbOrigin"/> of the available sprite. </param>
    ///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(double startTime, double endTime, string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, Action<OsbSprite, double, double> attributes = null, int group = 0)
        => Get(startTime, endTime, path, frameCount, frameDelay, loopType, origin, default, attributes, group);

    ///<summary> Gets an available animation from the pools. </summary>
    ///<param name="startTime"> The start time of the available animation. </param>
    ///<param name="endTime"> The end time of the available animation. </param>
    ///<param name="path"> Image path of the available animation. </param>
    ///<param name="frameCount"> Frame count of the available animation. </param>
    ///<param name="frameDelay"> Delay between frames of the available animation. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the available animation. </param>
    ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    ///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(double startTime, double endTime, string path, int frameCount, double frameDelay, OsbLoopType loopType, CommandPosition position, Action<OsbSprite, double, double> attributes = null, int group = 0)
        => Get(startTime, endTime, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, position, attributes, group);

    ///<summary> Gets an available animation from the pools. </summary>
    ///<param name="startTime"> The start time of the available animation. </param>
    ///<param name="endTime"> The end time of the available animation. </param>
    ///<param name="path"> Image path of the available animation. </param>
    ///<param name="frameCount"> Frame count of the available animation. </param>
    ///<param name="frameDelay"> Delay between frames of the available animation. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the available animation. </param>
    ///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(double startTime, double endTime, string path, int frameCount, double frameDelay, OsbLoopType loopType, Action<OsbSprite, double, double> attributes = null, int group = 0)
        => Get(startTime, endTime, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, default, attributes, group);

    ///<summary> Gets an available animation from the pools. </summary>
    ///<param name="startTime"> The start time of the available animation. </param>
    ///<param name="endTime"> The end time of the available animation. </param>
    ///<param name="path"> Image path of the available animation. </param>
    ///<param name="frameCount"> Frame count of the available animation. </param>
    ///<param name="frameDelay"> Delay between frames of the available animation. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the available animation. </param>
    ///<param name="origin"> <see cref="OsbOrigin"/> of the available sprite. </param>
    ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(double startTime, double endTime, string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, CommandPosition position, bool additive, int group = 0)
        => Get(startTime, endTime, path, frameCount, frameDelay, loopType, origin, position, additive ?
        (pS, sT, eT) => pS.Additive(sT) : null, group);

    ///<summary> Gets an available animation from the pools. </summary>
    ///<param name="startTime"> The start time of the available animation. </param>
    ///<param name="endTime"> The end time of the available animation. </param>
    ///<param name="path"> Image path of the available animation. </param>
    ///<param name="frameCount"> Frame count of the available animation. </param>
    ///<param name="frameDelay"> Delay between frames of the available animation. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the available animation. </param>
    ///<param name="origin"> <see cref="OsbOrigin"/> of the available sprite. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(double startTime, double endTime, string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, bool additive, int group = 0)
        => Get(startTime, endTime, path, frameCount, frameDelay, loopType, origin, default, additive, group);

    ///<summary> Gets an available animation from the pools. </summary>
    ///<param name="startTime"> The start time of the available animation. </param>
    ///<param name="endTime"> The end time of the available animation. </param>
    ///<param name="path"> Image path of the available animation. </param>
    ///<param name="frameCount"> Frame count of the available animation. </param>
    ///<param name="frameDelay"> Delay between frames of the available animation. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the available animation. </param>
    ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(double startTime, double endTime, string path, int frameCount, double frameDelay, OsbLoopType loopType, CommandPosition position, bool additive, int group = 0)
        => Get(startTime, endTime, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, position, additive, group);

    ///<summary> Gets an available animation from the pools. </summary>
    ///<param name="startTime"> The start time of the available animation. </param>
    ///<param name="endTime"> The end time of the available animation. </param>
    ///<param name="path"> Image path of the available animation. </param>
    ///<param name="frameCount"> Frame count of the available animation. </param>
    ///<param name="frameDelay"> Delay between frames of the available animation. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the available animation. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    ///<param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(double startTime, double endTime, string path, int frameCount, double frameDelay, OsbLoopType loopType, bool additive, int group = 0)
        => Get(startTime, endTime, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, default, additive, group);

    OsbSpritePool getPool(string path, OsbOrigin origin, CommandPosition position, Action<OsbSprite, double, double> attributes, int group)
    {
        var key = getKey(path, origin, attributes, group);
        if (!pools.TryGetValue(key, out var pool)) pools[key] = pool = new(segment, path, origin, position, attributes) { MaxPoolDuration = maxPoolDuration };
        return pool;
    }
    OsbAnimationPool getPool(string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, CommandPosition position, Action<OsbSprite, double, double> attributes, int group)
    {
        var key = getKey(path, frameCount, frameDelay, loopType, origin, attributes, group);
        if (!animationPools.TryGetValue(key, out var pool)) animationPools[key] =
            pool = new(segment, path, frameCount, frameDelay, loopType, origin, position, attributes) { MaxPoolDuration = maxPoolDuration };

        return pool;
    }

    static string getKey(string path, OsbOrigin origin, Action<OsbSprite, double, double> action, int group)
        => $"{path}#{(int)origin}#{action?.Target}.{action?.Method.Name}#{group}";

    static string getKey(string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, Action<OsbSprite, double, double> action, int group)
        => $"{path}#{frameCount}#{frameDelay}#{(int)loopType}#{(int)origin}#{action?.Target}.{action?.Method.Name}#{group}";

    bool disposed;

    ///<inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing)
    {
        if (!disposed)
        {
            foreach (var pool in pools) pool.Value.Dispose(disposing);
            foreach (var pool in animationPools) pool.Value.Dispose(disposing);

            if (disposing)
            {
                pools.Clear();
                animationPools.Clear();

                disposed = true;
            }
        }
    }
}

///<summary> Provides a way to optimize filesize and creates a way for animations to be reused at a minor cost of performance. </summary>
///<remarks> Constructs a new <see cref="OsbAnimationPool"/>. </remarks>
///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
///<param name="path"> Image path of the available sprite. </param>
///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
///<param name="origin"> <see cref="OsbOrigin"/> of the <see cref="OsbAnimation"/>. </param>
///<param name="position"> Initial position of the <see cref="OsbAnimation"/>. </param>
///<param name="attributes"> Commands to be run on each animation in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
public sealed class OsbAnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, CommandPosition position, Action<OsbSprite, double, double> attributes = null) : OsbSpritePool(segment, path, origin, position, attributes)
{
    ///<summary> Constructs a new <see cref="OsbAnimationPool"/>. </summary>
    ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
    ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
    ///<param name="origin"> <see cref="OsbOrigin"/> of the <see cref="OsbAnimation"/>. </param>
    ///<param name="attributes"> Commands to be run on each animation in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
    public OsbAnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, Action<OsbSprite, double, double> attributes = null)
        : this(segment, path, frameCount, frameDelay, loopType, origin, default, attributes) { }

    ///<summary> Constructs a new <see cref="OsbAnimationPool"/>. </summary>
    ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
    ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
    ///<param name="position"> Initial position of the <see cref="OsbAnimation"/>. </param>
    ///<param name="attributes"> Commands to be run on each animation in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
    public OsbAnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, CommandPosition position, Action<OsbSprite, double, double> attributes = null)
        : this(segment, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, position, attributes) { }

    ///<summary> Constructs a new <see cref="OsbAnimationPool"/>. </summary>
    ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
    ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
    ///<param name="attributes"> Commands to be run on each animation in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
    public OsbAnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, Action<OsbSprite, double, double> attributes = null)
        : this(segment, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, default, attributes) { }

    ///<summary> Constructs a new <see cref="OsbAnimationPool"/>. </summary>
    ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
    ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
    ///<param name="origin"> <see cref="OsbOrigin"/> of the <see cref="OsbAnimation"/>. </param>
    ///<param name="position"> Initial position of the <see cref="OsbAnimation"/>. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbAnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, CommandPosition position, bool additive)
        : this(segment, path, frameCount, frameDelay, loopType, origin, position, additive ?
        (pA, sT, eT) => pA.Additive(sT) : null)
    { }

    ///<summary> Constructs a new <see cref="OsbAnimationPool"/>. </summary>
    ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
    ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
    ///<param name="origin"> <see cref="OsbOrigin"/> of the <see cref="OsbAnimation"/>. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbAnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, bool additive)
        : this(segment, path, frameCount, frameDelay, loopType, origin, default, additive) { }

    ///<summary> Constructs a new <see cref="OsbAnimationPool"/>. </summary>
    ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
    ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
    ///<param name="position"> Initial position of the <see cref="OsbAnimation"/>. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbAnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, CommandPosition position, bool additive)
        : this(segment, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, position, additive) { }

    ///<summary> Constructs a new <see cref="OsbAnimationPool"/>. </summary>
    ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
    ///<param name="path"> Image path of the available sprite. </param>
    ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
    ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
    ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
    ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbAnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, bool additive)
        : this(segment, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, default, additive) { }

#pragma warning disable CS1591
    protected override OsbSprite CreateSprite(StoryboardSegment segment, string path, OsbOrigin origin, CommandPosition position)
#pragma warning restore CS1591
        => segment.CreateAnimation(path, frameCount, frameDelay, loopType, origin, position);
}