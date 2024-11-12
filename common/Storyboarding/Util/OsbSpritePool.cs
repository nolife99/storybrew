namespace StorybrewCommon.Storyboarding.Util;

using System;
using System.Collections.Generic;
using CommandValues;

/// <summary> Provides a way to optimize filesize and creates a way for sprites to be reused. </summary>
/// <remarks> Constructs a <see cref="OsbSpritePool"/>. </remarks>
/// <param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
/// <param name="path"> Image path of the available sprite. </param>
/// <param name="origin"> <see cref="OsbOrigin"/> of the sprites in the pool. </param>
/// <param name="position"> Initial <see cref="CommandPosition"/> position of the sprites in the pool. </param>
/// <param name="attributes"> Commands to be run on each sprite in the pool. </param>
public class OsbSpritePool(StoryboardSegment segment,
    string path,
    OsbOrigin origin,
    CommandPosition position,
    Action<OsbSprite, float, float> attributes = null) : IDisposable
{
    readonly List<PooledSprite> pooled = [];

    bool disposed;

    ///<summary> The maximum duration for a sprite to be pooled. </summary>
    public int MaxPoolDuration;

    /// <summary> Constructs a <see cref="OsbSpritePool"/>. </summary>
    /// <param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
    /// <param name="path"> Image path of the available sprite. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of the sprites in the pool. </param>
    /// <param name="attributes"> Commands to be run on each sprite in the pool. </param>
    public OsbSpritePool(StoryboardSegment segment,
        string path,
        OsbOrigin origin,
        Action<OsbSprite, float, float> attributes = null) : this(segment, path, origin, default, attributes) { }

    /// <summary> Constructs a <see cref="OsbSpritePool"/>. </summary>
    /// <param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
    /// <param name="path"> Image path of the available sprite. </param>
    /// <param name="position"> Initial <see cref="CommandPosition"/> position of the sprites in the pool. </param>
    /// <param name="attributes"> Commands to be run on each sprite in the pool. </param>
    public OsbSpritePool(StoryboardSegment segment,
        string path,
        CommandPosition position,
        Action<OsbSprite, float, float> attributes = null) : this(segment, path, OsbOrigin.Centre, position, attributes) { }

    /// <summary> Constructs a <see cref="OsbSpritePool"/>. </summary>
    /// <param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
    /// <param name="path"> Image path of the sprite to be used. </param>
    /// <param name="attributes"> Commands to be run on each sprite in the pool. </param>
    public OsbSpritePool(StoryboardSegment segment, string path, Action<OsbSprite, float, float> attributes = null) : this(
        segment, path, OsbOrigin.Centre, default, attributes) { }

    /// <summary> Constructs a <see cref="OsbSpritePool"/>. </summary>
    /// <param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
    /// <param name="path"> Image path of the sprite to be used. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of the sprites in the pool. </param>
    /// <param name="position"> Initial <see cref="CommandPosition"/> position of the sprites in the pool. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbSpritePool(StoryboardSegment segment, string path, OsbOrigin origin, CommandPosition position, bool additive) :
        this(segment, path, origin, position, additive ? (pS, sT, _) => pS.Additive(sT) : null) { }

    /// <summary> Constructs a <see cref="OsbSpritePool"/>. </summary>
    /// <param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
    /// <param name="path"> Image path of the sprite to be used. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of the sprites in the pool. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbSpritePool(StoryboardSegment segment, string path, OsbOrigin origin, bool additive) : this(segment, path, origin,
        default, additive) { }

    /// <summary> Constructs a <see cref="OsbSpritePool"/>. </summary>
    /// <param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
    /// <param name="path"> Image path of the sprite to be used. </param>
    /// <param name="position"> Initial <see cref="CommandPosition"/> position of the sprites in the pool. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbSpritePool(StoryboardSegment segment, string path, CommandPosition position, bool additive) : this(segment, path,
        OsbOrigin.Centre, position, additive) { }

    /// <summary> Constructs a <see cref="OsbSpritePool"/>. </summary>
    /// <param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
    /// <param name="path"> Image path of the sprite to be used. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbSpritePool(StoryboardSegment segment, string path, bool additive) : this(segment, path, OsbOrigin.Centre, default,
        additive) { }

    /// <inheritdoc/>
    public void Dispose() => Dispose(true);

    /// <summary> Gets an available sprite from the sprite pool. </summary>
    /// <remarks> You must input the correct start time and end time of the sprite for proper pooling. </remarks>
    /// <param name="startTime"> The start time for this sprite. </param>
    /// <param name="endTime"> The end time for this sprite. </param>
    public OsbSprite Get(float startTime, float endTime)
    {
        PooledSprite result = null;
        foreach (var pooledSprite in pooled)
            if ((MaxPoolDuration > 0 ?
                pooledSprite.EndTime <= startTime && startTime < pooledSprite.StartTime + MaxPoolDuration :
                pooledSprite.EndTime <= startTime) && (result is null || pooledSprite.StartTime < result.StartTime))
                result = pooledSprite;

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

    internal void Dispose(bool disposing)
    {
        if (disposed) return;
        if (attributes is not null && disposing)
        {
            foreach (var pooledSprite in pooled)
            {
                var sprite = pooledSprite.Sprite;
                attributes(sprite, sprite.StartTime, pooledSprite.EndTime);
            }

            disposed = true;
        }

        pooled.Clear();
    }

    sealed class PooledSprite(OsbSprite sprite, float startTime, float endTime)
    {
        internal readonly OsbSprite Sprite = sprite;
        internal readonly float StartTime = startTime;
        internal float EndTime = endTime;
    }
}

/// <summary>
///     Provides a way to optimize filesize and creates a way for sprites to be reused at a minor cost of
///     performance.
/// </summary>
/// <remarks> Includes support for animation pools. </remarks>
/// <remarks> Constructs a <see cref="OsbSpritePools"/>. </remarks>
/// <param name="segment"> <see cref="StoryboardSegment"/> of the sprites in the pool. </param>
public sealed class OsbSpritePools(StoryboardSegment segment) : IDisposable
{
    readonly Dictionary<int, OsbAnimationPool> animationPools = [];
    readonly Dictionary<int, OsbSpritePool> pools = [];

    bool disposed;
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

    /// <inheritdoc/>
    public void Dispose() => Dispose(true);

    /// <summary> Gets an available sprite from the sprite pools. </summary>
    /// <param name="startTime"> The start time of the sprite. </param>
    /// <param name="endTime"> The end time of the sprite. </param>
    /// <param name="path"> Image path of the sprite. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of the sprite. </param>
    /// <param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    /// <param name="attributes"> Commands to be run on each sprite in the pool. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(float startTime,
        float endTime,
        string path,
        OsbOrigin origin,
        CommandPosition position,
        Action<OsbSprite, float, float> attributes = null,
        int group = 0) => getPool(path, origin, position, attributes, group).Get(startTime, endTime);

    /// <summary> Gets an available sprite from the sprite pools. </summary>
    /// <param name="startTime"> The start time of the sprite. </param>
    /// <param name="endTime"> The end time of the sprite. </param>
    /// <param name="path"> Image path of the sprite. </param>
    /// <param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    /// <param name="attributes"> Commands to be run on each sprite in the pool. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(float startTime,
        float endTime,
        string path,
        CommandPosition position,
        Action<OsbSprite, float, float> attributes = null,
        int group = 0) => Get(startTime, endTime, path, OsbOrigin.Centre, position, attributes, group);

    /// <summary> Gets an available sprite from the sprite pools. </summary>
    /// <param name="startTime"> The start time of the sprite. </param>
    /// <param name="endTime"> The end time of the sprite. </param>
    /// <param name="path"> Image path of the sprite. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of the sprite. </param>
    /// <param name="attributes"> Commands to be run on each sprite in the pool. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(float startTime,
        float endTime,
        string path,
        OsbOrigin origin,
        Action<OsbSprite, float, float> attributes = null,
        int group = 0) => Get(startTime, endTime, path, origin, default, attributes, group);

    /// <summary> Gets an available sprite from the sprite pools. </summary>
    /// <param name="startTime"> The start time of the sprite. </param>
    /// <param name="endTime"> The end time of the sprite. </param>
    /// <param name="path"> Image path of the sprite. </param>
    /// <param name="attributes"> Commands to be run on each sprite in the pool. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(float startTime,
        float endTime,
        string path,
        Action<OsbSprite, float, float> attributes = null,
        int group = 0) => Get(startTime, endTime, path, OsbOrigin.Centre, default, attributes, group);

    /// <summary> Gets an available sprite from the sprite pools. </summary>
    /// <param name="startTime"> The start time of the sprite. </param>
    /// <param name="endTime"> The end time of the sprite. </param>
    /// <param name="path"> Image path of the sprite. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of the sprite. </param>
    /// <param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(float startTime,
        float endTime,
        string path,
        OsbOrigin origin,
        CommandPosition position,
        bool additive,
        int group = 0)
        => Get(startTime, endTime, path, origin, position, additive ? (pS, sT, _) => pS.Additive(sT) : null, group);

    /// <summary> Gets an available sprite from the sprite pools. </summary>
    /// <param name="startTime"> The start time of the sprite. </param>
    /// <param name="endTime"> The end time of the sprite. </param>
    /// <param name="path"> Image path of the sprite. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of the sprite. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(float startTime, float endTime, string path, OsbOrigin origin, bool additive, int group = 0)
        => Get(startTime, endTime, path, origin, default, additive, group);

    /// <summary> Gets an available sprite from the sprite pools. </summary>
    /// <param name="startTime"> The start time of the sprite. </param>
    /// <param name="endTime"> The end time of the sprite. </param>
    /// <param name="path"> Image path of the  sprite. </param>
    /// <param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(float startTime, float endTime, string path, CommandPosition position, bool additive, int group = 0)
        => Get(startTime, endTime, path, OsbOrigin.Centre, position, additive, group);

    /// <summary> Gets an available sprite from the sprite pools. </summary>
    /// <param name="startTime"> The start time of the sprite. </param>
    /// <param name="endTime"> The end time of the sprite. </param>
    /// <param name="path"> Image path of the sprite. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbSprite Get(float startTime, float endTime, string path, bool additive, int group = 0)
        => Get(startTime, endTime, path, OsbOrigin.Centre, default, additive, group);

    /// <summary> Gets an available animation from the pools. </summary>
    /// <param name="startTime"> The start time of the animation. </param>
    /// <param name="endTime"> The end time of the animation. </param>
    /// <param name="path"> Image path of the animation. </param>
    /// <param name="frameCount"> Frame count of the animation. </param>
    /// <param name="frameDelay"> Delay between frames of the animation. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the animation. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of the sprite. </param>
    /// <param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    /// <param name="attributes"> Commands to be run on each sprite in the pool. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(float startTime,
        float endTime,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        OsbOrigin origin,
        CommandPosition position,
        Action<OsbSprite, float, float> attributes = null,
        int group = 0) => (OsbAnimation)getPool(path, frameCount, frameDelay, loopType, origin, position, attributes, group)
        .Get(startTime, endTime);

    /// <summary> Gets an available animation from the pools. </summary>
    /// <param name="startTime"> The start time of the animation. </param>
    /// <param name="endTime"> The end time of the animation. </param>
    /// <param name="path"> Image path of the animation. </param>
    /// <param name="frameCount"> Frame count of the animation. </param>
    /// <param name="frameDelay"> Delay between frames of the animation. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the animation. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of the sprite. </param>
    /// <param name="attributes"> Commands to be run on each sprite in the pool. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(float startTime,
        float endTime,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        OsbOrigin origin,
        Action<OsbSprite, float, float> attributes = null,
        int group = 0) => Get(startTime, endTime, path, frameCount, frameDelay, loopType, origin, default, attributes, group);

    /// <summary> Gets an available animation from the pools. </summary>
    /// <param name="startTime"> The start time of the animation. </param>
    /// <param name="endTime"> The end time of the animation. </param>
    /// <param name="path"> Image path of the animation. </param>
    /// <param name="frameCount"> Frame count of the animation. </param>
    /// <param name="frameDelay"> Delay between frames of the animation. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the animation. </param>
    /// <param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    /// <param name="attributes"> Commands to be run on each sprite in the pool. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(float startTime,
        float endTime,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        CommandPosition position,
        Action<OsbSprite, float, float> attributes = null,
        int group = 0) => Get(startTime, endTime, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, position, attributes,
        group);

    /// <summary> Gets an available animation from the pools. </summary>
    /// <param name="startTime"> The start time of the animation. </param>
    /// <param name="endTime"> The end time of the animation. </param>
    /// <param name="path"> Image path of the animation. </param>
    /// <param name="frameCount"> Frame count of the animation. </param>
    /// <param name="frameDelay"> Delay between frames of the animation. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the animation. </param>
    /// <param name="attributes"> Commands to be run on each sprite in the pool. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(float startTime,
        float endTime,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        Action<OsbSprite, float, float> attributes = null,
        int group = 0) => Get(startTime, endTime, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, default, attributes,
        group);

    /// <summary> Gets an available animation from the pools. </summary>
    /// <param name="startTime"> The start time of the animation. </param>
    /// <param name="endTime"> The end time of the animation. </param>
    /// <param name="path"> Image path of the animation. </param>
    /// <param name="frameCount"> Frame count of the animation. </param>
    /// <param name="frameDelay"> Delay between frames of the animation. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the animation. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of the sprite. </param>
    /// <param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(float startTime,
        float endTime,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        OsbOrigin origin,
        CommandPosition position,
        bool additive,
        int group = 0) => Get(startTime, endTime, path, frameCount, frameDelay, loopType, origin, position,
        additive ? (pS, sT, _) => pS.Additive(sT) : null, group);

    /// <summary> Gets an available animation from the pools. </summary>
    /// <param name="startTime"> The start time of the animation. </param>
    /// <param name="endTime"> The end time of the animation. </param>
    /// <param name="path"> Image path of the animation. </param>
    /// <param name="frameCount"> Frame count of the animation. </param>
    /// <param name="frameDelay"> Delay between frames of the animation. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the animation. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of the sprite. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(float startTime,
        float endTime,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        OsbOrigin origin,
        bool additive,
        int group = 0) => Get(startTime, endTime, path, frameCount, frameDelay, loopType, origin, default, additive, group);

    /// <summary> Gets an available animation from the pools. </summary>
    /// <param name="startTime"> The start time of the animation. </param>
    /// <param name="endTime"> The end time of the animation. </param>
    /// <param name="path"> Image path of the animation. </param>
    /// <param name="frameCount"> Frame count of the animation. </param>
    /// <param name="frameDelay"> Delay between frames of the animation. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the animation. </param>
    /// <param name="position"> Initial <see cref="CommandPosition"/> position of the sprite. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(float startTime,
        float endTime,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        CommandPosition position,
        bool additive,
        int group = 0) => Get(startTime, endTime, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, position, additive,
        group);

    /// <summary> Gets an available animation from the pools. </summary>
    /// <param name="startTime"> The start time of the animation. </param>
    /// <param name="endTime"> The end time of the animation. </param>
    /// <param name="path"> Image path of the animation. </param>
    /// <param name="frameCount"> Frame count of the animation. </param>
    /// <param name="frameDelay"> Delay between frames of the animation. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the animation. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    /// <param name="group"> Pool group to get a sprite from. </param>
    public OsbAnimation Get(float startTime,
        float endTime,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        bool additive,
        int group = 0) => Get(startTime, endTime, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, default, additive,
        group);

    OsbSpritePool getPool(string path,
        OsbOrigin origin,
        CommandPosition position,
        Action<OsbSprite, float, float> attributes,
        int group)
    {
        var key = getKey(path, origin, attributes, group);
        if (!pools.TryGetValue(key, out var pool))
            pools[key] = pool = new(segment, path, origin, position, attributes) { MaxPoolDuration = maxPoolDuration };

        return pool;
    }

    OsbAnimationPool getPool(string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        OsbOrigin origin,
        CommandPosition position,
        Action<OsbSprite, float, float> attributes,
        int group)
    {
        var key = getKey(path, frameCount, frameDelay, loopType, origin, attributes, group);
        if (!animationPools.TryGetValue(key, out var pool))
            animationPools[key] =
                pool = new(segment, path, frameCount, frameDelay, loopType, origin, position, attributes)
                {
                    MaxPoolDuration = maxPoolDuration
                };

        return pool;
    }

    static int getKey(string path, OsbOrigin origin, Action<OsbSprite, float, float> action, int group)
        => HashCode.Combine(path, origin, action?.Target, action?.Method.Name, group);

    static int getKey(string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        OsbOrigin origin,
        Action<OsbSprite, float, float> action,
        int group)
        => HashCode.Combine(path, frameCount, frameDelay, loopType, origin, action?.Target, action?.Method.Name, group);

    void Dispose(bool disposing)
    {
        if (disposed) return;

        foreach (var pool in pools) pool.Value.Dispose(disposing);
        foreach (var pool in animationPools) pool.Value.Dispose(disposing);

        if (!disposing) return;

        pools.Clear();
        animationPools.Clear();

        disposed = true;
    }
}

/// <summary>
///     Provides a way to optimize filesize and creates a way for animations to be reused at a minor cost of
///     performance.
/// </summary>
/// <remarks> Constructs a new <see cref="OsbAnimationPool"/>. </remarks>
/// <param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
/// <param name="path"> Image path of the available sprite. </param>
/// <param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
/// <param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
/// <param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
/// <param name="origin"> <see cref="OsbOrigin"/> of the <see cref="OsbAnimation"/>. </param>
/// <param name="position"> Initial position of the <see cref="OsbAnimation"/>. </param>
/// <param name="attributes"> Commands to be run on each animation in the pool. </param>
public sealed class OsbAnimationPool(StoryboardSegment segment,
    string path,
    int frameCount,
    float frameDelay,
    OsbLoopType loopType,
    OsbOrigin origin,
    CommandPosition position,
    Action<OsbSprite, float, float> attributes = null) : OsbSpritePool(segment, path, origin, position, attributes)
{
    /// <summary> Constructs a new <see cref="OsbAnimationPool"/>. </summary>
    /// <param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
    /// <param name="path"> Image path of the available sprite. </param>
    /// <param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
    /// <param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of the <see cref="OsbAnimation"/>. </param>
    /// <param name="attributes"> Commands to be run on each animation in the pool. </param>
    public OsbAnimationPool(StoryboardSegment segment,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        OsbOrigin origin,
        Action<OsbSprite, float, float> attributes = null) : this(segment, path, frameCount, frameDelay, loopType, origin,
        default, attributes) { }

    /// <summary> Constructs a new <see cref="OsbAnimationPool"/>. </summary>
    /// <param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
    /// <param name="path"> Image path of the available sprite. </param>
    /// <param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
    /// <param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
    /// <param name="position"> Initial position of the <see cref="OsbAnimation"/>. </param>
    /// <param name="attributes"> Commands to be run on each animation in the pool. </param>
    public OsbAnimationPool(StoryboardSegment segment,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        CommandPosition position,
        Action<OsbSprite, float, float> attributes = null) : this(segment, path, frameCount, frameDelay, loopType,
        OsbOrigin.Centre, position, attributes) { }

    /// <summary> Constructs a new <see cref="OsbAnimationPool"/>. </summary>
    /// <param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
    /// <param name="path"> Image path of the available sprite. </param>
    /// <param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
    /// <param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
    /// <param name="attributes"> Commands to be run on each animation in the pool. </param>
    public OsbAnimationPool(StoryboardSegment segment,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        Action<OsbSprite, float, float> attributes = null) : this(segment, path, frameCount, frameDelay, loopType,
        OsbOrigin.Centre, default, attributes) { }

    /// <summary> Constructs a new <see cref="OsbAnimationPool"/>. </summary>
    /// <param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
    /// <param name="path"> Image path of the available sprite. </param>
    /// <param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
    /// <param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of the <see cref="OsbAnimation"/>. </param>
    /// <param name="position"> Initial position of the <see cref="OsbAnimation"/>. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbAnimationPool(StoryboardSegment segment,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        OsbOrigin origin,
        CommandPosition position,
        bool additive) : this(segment, path, frameCount, frameDelay, loopType, origin, position,
        additive ? (pA, sT, _) => pA.Additive(sT) : null) { }

    /// <summary> Constructs a new <see cref="OsbAnimationPool"/>. </summary>
    /// <param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
    /// <param name="path"> Image path of the available sprite. </param>
    /// <param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
    /// <param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
    /// <param name="origin"> <see cref="OsbOrigin"/> of the <see cref="OsbAnimation"/>. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbAnimationPool(StoryboardSegment segment,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        OsbOrigin origin,
        bool additive) : this(segment, path, frameCount, frameDelay, loopType, origin, default, additive) { }

    /// <summary> Constructs a new <see cref="OsbAnimationPool"/>. </summary>
    /// <param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
    /// <param name="path"> Image path of the available sprite. </param>
    /// <param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
    /// <param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
    /// <param name="position"> Initial position of the <see cref="OsbAnimation"/>. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbAnimationPool(StoryboardSegment segment,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        CommandPosition position,
        bool additive) : this(segment, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, position, additive) { }

    /// <summary> Constructs a new <see cref="OsbAnimationPool"/>. </summary>
    /// <param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="OsbAnimationPool"/>. </param>
    /// <param name="path"> Image path of the available sprite. </param>
    /// <param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
    /// <param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
    /// <param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
    /// <param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
    public OsbAnimationPool(StoryboardSegment segment,
        string path,
        int frameCount,
        float frameDelay,
        OsbLoopType loopType,
        bool additive) : this(segment, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, default, additive) { }

#pragma warning disable CS1591
    protected override OsbSprite CreateSprite(StoryboardSegment segment, string path, OsbOrigin origin, CommandPosition position)
#pragma warning restore CS1591
        => segment.CreateAnimation(path, frameCount, frameDelay, loopType, origin, position);
}