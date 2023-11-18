using System;
using System.Collections.Generic;

namespace StorybrewCommon.Storyboarding.Util
{
#pragma warning disable CS1591
    [Obsolete("Use StorybrewCommon.Storyboarding.SpritePools instead for better support.")]
    public class OsbSpritePools(StoryboardSegment segment) : IDisposable
    {
        readonly StoryboardSegment segment = segment;
        readonly Dictionary<string, OsbSpritePool> pools = [];
        readonly Dictionary<string, OsbAnimationPool> animationPools = [];

        int maxPoolDuration = 60000;
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

        public void Clear()
        {
            foreach (var pool in pools) pool.Value.Clear();
            foreach (var pool in animationPools) pool.Value.Clear();
            pools.Clear();
            animationPools.Clear();
        }

        public OsbSprite Get(double startTime, double endTime, string path, OsbOrigin origin = OsbOrigin.Centre, Action<OsbSprite, double, double> finalizeSprite = null, int poolGroup = 0)
            => getPool(path, origin, finalizeSprite, poolGroup).Get(startTime, endTime);

        public OsbSprite Get(double startTime, double endTime, string path, OsbOrigin origin, bool additive, int poolGroup = 0)
            => Get(startTime, endTime, path, origin, additive ?
            (sprite, spriteStartTime, spriteEndTime) => sprite.Additive(spriteStartTime, spriteEndTime) : null, poolGroup);

        public OsbAnimation Get(double startTime, double endTime, string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin = OsbOrigin.Centre, Action<OsbSprite, double, double> finalizeSprite = null, int poolGroup = 0)
            => (OsbAnimation)getPool(path, frameCount, frameDelay, loopType, origin, finalizeSprite, poolGroup).Get(startTime, endTime);

        public OsbAnimation Get(double startTime, double endTime, string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, bool additive, int poolGroup = 0)
            => Get(startTime, endTime, path, frameCount, frameDelay, loopType, origin, additive ?
            (sprite, spriteStartTime, spriteEndTime) => sprite.Additive(spriteStartTime, spriteEndTime) : null, poolGroup);

        OsbSpritePool getPool(string path, OsbOrigin origin, Action<OsbSprite, double, double> finalizeSprite, int poolGroup)
        {
            var key = getKey(path, origin, finalizeSprite, poolGroup);

            if (!pools.TryGetValue(key, out OsbSpritePool pool))
                pools.Add(key, pool = new OsbSpritePool(segment, path, origin, finalizeSprite) { MaxPoolDuration = maxPoolDuration });

            return pool;
        }

        OsbAnimationPool getPool(string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, Action<OsbSprite, double, double> finalizeSprite, int poolGroup)
        {
            var key = getKey(path, frameCount, frameDelay, loopType, origin, finalizeSprite, poolGroup);

            if (!animationPools.TryGetValue(key, out OsbAnimationPool pool))
                animationPools.Add(key, pool = new OsbAnimationPool(segment, path, frameCount, frameDelay, loopType, origin, finalizeSprite) { MaxPoolDuration = maxPoolDuration, });

            return pool;
        }

        string getKey(string path, OsbOrigin origin, Action<OsbSprite, double, double> action, int poolGroup)
            => $"{path}#{origin}#{action?.Target}.{action?.Method.Name}#{poolGroup}";

        string getKey(string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, Action<OsbSprite, double, double> action, int poolGroup)
            => $"{path}#{frameCount}#{frameDelay}#{loopType}#{origin}#{action?.Target}.{action?.Method.Name}#{poolGroup}";

        #region IDisposable Support

        bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing) Clear();
                disposedValue = true;
            }
        }
        public void Dispose() => Dispose(true);

        #endregion
    }
}