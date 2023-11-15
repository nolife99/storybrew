using StorybrewCommon.Storyboarding.CommandValues;
using System;
using System.Collections.Generic;
using StorybrewCommon.Util;
using BrewLib.Util;

namespace StorybrewCommon.Storyboarding
{
    ///<summary> Provides a way to optimize filesize and creates a way for sprites to be reused. </summary>
    public class SpritePool : IDisposable
    {
        readonly StoryboardSegment segment;
        readonly string path;
        readonly OsbOrigin origin;
        readonly CommandPosition position;
        readonly Action<OsbSprite, double, double> attributes;
        readonly List<PooledSprite> pooled;

        ///<summary> The maximum duration for a sprite to be pooled. </summary>
        public int MaxPoolDuration;

        ///<summary> Constructs a <see cref="SpritePool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
        ///<param name="path"> Image path of the available sprite. </param>
        ///<param name="origin"> <see cref="OsbOrigin"/> of the sprites in the pool. </param>
        ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprites in the pool. </param>
        ///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
        public SpritePool(StoryboardSegment segment, string path, OsbOrigin origin, CommandPosition position, Action<OsbSprite, double, double> attributes = null)
        {
            this.segment = segment;
            this.path = path;
            this.origin = origin;
            this.position = position;
            this.attributes = attributes;
            pooled = new List<PooledSprite>();
        }

        ///<summary> Constructs a <see cref="SpritePool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
        ///<param name="path"> Image path of the available sprite. </param>
        ///<param name="origin"> <see cref="OsbOrigin"/> of the sprites in the pool. </param>
        ///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
        public SpritePool(StoryboardSegment segment, string path, OsbOrigin origin, Action<OsbSprite, double, double> attributes = null)
            : this(segment, path, origin, default, attributes) { }

        ///<summary> Constructs a <see cref="SpritePool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
        ///<param name="path"> Image path of the available sprite. </param>
        ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprites in the pool. </param>
        ///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
        public SpritePool(StoryboardSegment segment, string path, CommandPosition position, Action<OsbSprite, double, double> attributes = null)
            : this(segment, path, OsbOrigin.Centre, position, attributes) { }

        ///<summary> Constructs a <see cref="SpritePool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
        ///<param name="path"> Image path of the sprite to be used. </param>
        ///<param name="attributes"> Commands to be run on each sprite in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
        public SpritePool(StoryboardSegment segment, string path, Action<OsbSprite, double, double> attributes = null)
            : this(segment, path, OsbOrigin.Centre, default, attributes) { }

        ///<summary> Constructs a <see cref="SpritePool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
        ///<param name="path"> Image path of the sprite to be used. </param>
        ///<param name="origin"> <see cref="OsbOrigin"/> of the sprites in the pool. </param>
        ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprites in the pool. </param>
        ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
        public SpritePool(StoryboardSegment segment, string path, OsbOrigin origin, CommandPosition position, bool additive)
            : this(segment, path, origin, position, additive ?
            (pS, sT, eT) => pS.Additive(sT) : null) { }

        ///<summary> Constructs a <see cref="SpritePool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
        ///<param name="path"> Image path of the sprite to be used. </param>
        ///<param name="origin"> <see cref="OsbOrigin"/> of the sprites in the pool. </param>
        ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
        public SpritePool(StoryboardSegment segment, string path, OsbOrigin origin, bool additive)
            : this(segment, path, origin, default, additive) { }

        ///<summary> Constructs a <see cref="SpritePool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
        ///<param name="path"> Image path of the sprite to be used. </param>
        ///<param name="position"> Initial <see cref="CommandPosition"/> position of the sprites in the pool. </param>
        ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
        public SpritePool(StoryboardSegment segment, string path, CommandPosition position, bool additive)
            : this(segment, path, OsbOrigin.Centre, position, additive) { }

        ///<summary> Constructs a <see cref="SpritePool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the pool. </param>
        ///<param name="path"> Image path of the sprite to be used. </param>
        ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
        public SpritePool(StoryboardSegment segment, string path, bool additive)
            : this(segment, path, OsbOrigin.Centre, default, additive) { }

        ///<summary> Gets an available sprite from the sprite pool. </summary>
        ///<remarks> You must input the correct start time and end time of the sprite for correct execution. </remarks>
        ///<param name="startTime"> The start time for this sprite. </param>
        ///<param name="endTime"> The end time for this sprite. </param>
        public OsbSprite Get(double startTime, double endTime)
        {
            PooledSprite result = null;
            for (var i = 0; i < pooled.Count; ++i) if (validateDur(startTime, pooled[i]) && (result is null || pooled[i].StartTime < result.StartTime))
            { 
                result = pooled[i];
                break;
            }

            if (result is not null)
            {
                result.EndTime = endTime;
                return result.Sprite;
            }

            var sprite = CreateSprite(segment, path, origin, position);
            pooled.Add(new PooledSprite(sprite, startTime, endTime));

            return sprite;
        }
        bool validateDur(double startTime, PooledSprite sprite) => MaxPoolDuration > 0 ? sprite.EndTime <= startTime && startTime < sprite.StartTime + MaxPoolDuration : sprite.EndTime <= startTime;

#pragma warning disable CS1591
        protected virtual OsbSprite CreateSprite(StoryboardSegment segment, string path, OsbOrigin origin, CommandPosition position)
#pragma warning restore CS1591
            => segment.CreateSprite(path, origin, position);

        class PooledSprite
        {
            internal OsbSprite Sprite;
            internal double StartTime, EndTime;

            internal PooledSprite(OsbSprite sprite, double startTime, double endTime)
            {
                Sprite = sprite;
                StartTime = startTime;
                EndTime = endTime;
            }
        }

        bool disposed;
        void Dispose(bool dispose)
        {
            if (!disposed)
            {
                if (dispose)
                {
                    if (attributes != null) pooled.ForEach(pooledSprite =>
                    {
                        var sprite = pooledSprite.Sprite;
                        attributes(sprite, sprite.CommandsStartTime, pooledSprite.EndTime);
                    });
                    pooled.Clear();
                }
                disposed = true;
            }
        }

        ///<summary/>
        public void Dispose() => Dispose(true);
    }

    ///<summary> Provides a way to optimize filesize and creates a way for sprites to be reused at a minor cost of performance. </summary>
    ///<remarks> Includes support for animation pools. </remarks>
    public sealed class SpritePools : IDisposable
    {
        readonly StoryboardSegment segment;
        readonly Dictionary<string, SpritePool> pools = new();
        readonly Dictionary<string, AnimationPool> animationPools = new();

        ///<summary> Constructs a <see cref="SpritePools"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the sprites in the pool. </param>
        public SpritePools(StoryboardSegment segment) => this.segment = segment;

        int maxPoolDuration;

        ///<summary> The maximum duration for a sprite to be pooled. </summary>
        public int MaxPoolDuration
        {
            get => maxPoolDuration;
            set
            {
                if (maxPoolDuration == value) return;
                maxPoolDuration = value;
                foreach (var pool in pools.Values) pool.MaxPoolDuration = maxPoolDuration;
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

        SpritePool getPool(string path, OsbOrigin origin, CommandPosition position, Action<OsbSprite, double, double> attributes, int group)
        {
            var key = getKey(path, origin, attributes, group);
            if (!pools.TryGetValue(key, out SpritePool pool))
                pools[key] = pool = new SpritePool(segment, path, origin, position, attributes) { MaxPoolDuration = maxPoolDuration };

            return pool;
        }
        AnimationPool getPool(string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, CommandPosition position, Action<OsbSprite, double, double> attributes, int group)
        {
            var key = getKey(path, frameCount, frameDelay, loopType, origin, attributes, group);
            if (!animationPools.TryGetValue(key, out AnimationPool pool)) animationPools[key] = 
                pool = new AnimationPool(segment, path, frameCount, frameDelay, loopType, origin, position, attributes) { MaxPoolDuration = maxPoolDuration };

            return pool;
        }

        static string getKey(string path, OsbOrigin origin, Action<OsbSprite, double, double> action, int group)
            => $"{path}#{(int)origin}#{action?.Target}.{action?.Method.Name}#{group}";

        static string getKey(string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, Action<OsbSprite, double, double> action, int group)
            => $"{path}#{frameCount}#{frameDelay}#{(int)loopType}#{(int)origin}#{action?.Target}.{action?.Method.Name}#{group}";

        void Clear()
        {
            pools.Dispose();
            animationPools.Dispose();
        }

        bool disposed;
        void Dispose(bool dispose)
        {
            if (!disposed)
            {
                if (dispose) Clear();
                disposed = true;
            }
        }
        ///<summary/>
        public void Dispose() => Dispose(true);
    }

    ///<summary> Provides a way to optimize filesize and creates a way for animations to be reused at a minor cost of performance. </summary>
    public sealed class AnimationPool : SpritePool
    {
        readonly int frameCount;
        readonly double frameDelay;
        readonly OsbLoopType loopType;

        ///<summary> Constructs a new <see cref="AnimationPool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="AnimationPool"/>. </param>
        ///<param name="path"> Image path of the available sprite. </param>
        ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
        ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
        ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
        ///<param name="origin"> <see cref="OsbOrigin"/> of the <see cref="OsbAnimation"/>. </param>
        ///<param name="position"> Initial position of the <see cref="OsbAnimation"/>. </param>
        ///<param name="attributes"> Commands to be run on each animation in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
        public AnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, CommandPosition position, Action<OsbSprite, double, double> attributes = null)
            : base(segment, path, origin, position, attributes)
        {
            this.frameCount = frameCount;
            this.frameDelay = frameDelay;
            this.loopType = loopType;
        }

        ///<summary> Constructs a new <see cref="AnimationPool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="AnimationPool"/>. </param>
        ///<param name="path"> Image path of the available sprite. </param>
        ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
        ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
        ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
        ///<param name="origin"> <see cref="OsbOrigin"/> of the <see cref="OsbAnimation"/>. </param>
        ///<param name="attributes"> Commands to be run on each animation in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
        public AnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, Action<OsbSprite, double, double> attributes = null)
            : this(segment, path, frameCount, frameDelay, loopType, origin, default, attributes) { }

        ///<summary> Constructs a new <see cref="AnimationPool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="AnimationPool"/>. </param>
        ///<param name="path"> Image path of the available sprite. </param>
        ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
        ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
        ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
        ///<param name="position"> Initial position of the <see cref="OsbAnimation"/>. </param>
        ///<param name="attributes"> Commands to be run on each animation in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
        public AnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, CommandPosition position, Action<OsbSprite, double, double> attributes = null)
            : this(segment, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, position, attributes) { }

        ///<summary> Constructs a new <see cref="AnimationPool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="AnimationPool"/>. </param>
        ///<param name="path"> Image path of the available sprite. </param>
        ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
        ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
        ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
        ///<param name="attributes"> Commands to be run on each animation in the pool, using <see cref="Action"/>&#60;<see cref="OsbSprite"/> (pooled sprite), <see cref="double"/> (start time), <see cref="double"/> (end time)&#62;. </param>
        public AnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, Action<OsbSprite, double, double> attributes = null)
            : this(segment, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, default, attributes) { }

        ///<summary> Constructs a new <see cref="AnimationPool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="AnimationPool"/>. </param>
        ///<param name="path"> Image path of the available sprite. </param>
        ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
        ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
        ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
        ///<param name="origin"> <see cref="OsbOrigin"/> of the <see cref="OsbAnimation"/>. </param>
        ///<param name="position"> Initial position of the <see cref="OsbAnimation"/>. </param>
        ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
        public AnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, CommandPosition position, bool additive)
            : this(segment, path, frameCount, frameDelay, loopType, origin, position, additive ?
            (pA, sT, eT) => pA.Additive(sT) : null)
        { }

        ///<summary> Constructs a new <see cref="AnimationPool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="AnimationPool"/>. </param>
        ///<param name="path"> Image path of the available sprite. </param>
        ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
        ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
        ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
        ///<param name="origin"> <see cref="OsbOrigin"/> of the <see cref="OsbAnimation"/>. </param>
        ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
        public AnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, bool additive)
            : this(segment, path, frameCount, frameDelay, loopType, origin, default, additive) { }

        ///<summary> Constructs a new <see cref="AnimationPool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="AnimationPool"/>. </param>
        ///<param name="path"> Image path of the available sprite. </param>
        ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
        ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
        ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
        ///<param name="position"> Initial position of the <see cref="OsbAnimation"/>. </param>
        ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
        public AnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, CommandPosition position, bool additive)
            : this(segment, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, position, additive) { }

        ///<summary> Constructs a new <see cref="AnimationPool"/>. </summary>
        ///<param name="segment"> <see cref="StoryboardSegment"/> of the <see cref="AnimationPool"/>. </param>
        ///<param name="path"> Image path of the available sprite. </param>
        ///<param name="frameCount"> Amount of frames in the <see cref="OsbAnimation"/>. </param>
        ///<param name="frameDelay"> Delay between frames of the <see cref="OsbAnimation"/>. </param>
        ///<param name="loopType"> <see cref="OsbLoopType"/> of the <see cref="OsbAnimation"/>. </param>
        ///<param name="additive"> <see cref="bool"/> toggle for the sprite's additive blending. </param>
        public AnimationPool(StoryboardSegment segment, string path, int frameCount, double frameDelay, OsbLoopType loopType, bool additive)
            : this(segment, path, frameCount, frameDelay, loopType, OsbOrigin.Centre, default, additive) { }

#pragma warning disable CS1591
        protected override OsbSprite CreateSprite(StoryboardSegment segment, string path, OsbOrigin origin, CommandPosition position)
#pragma warning restore CS1591
            => segment.CreateAnimation(path, frameCount, frameDelay, loopType, origin, position);
    }
}