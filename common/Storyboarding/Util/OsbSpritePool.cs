using System;
using System.Collections.Generic;

namespace StorybrewCommon.Storyboarding.Util
{
#pragma warning disable CS1591
    [Obsolete("Use StorybrewCommon.Storyboarding.SpritePool instead for better support.")]
    public class OsbSpritePool(StoryboardSegment segment, string path, OsbOrigin origin, Action<OsbSprite, double, double> finalizeSprite = null) : IDisposable
    {
        readonly StoryboardSegment segment = segment;
        readonly string path = path;
        readonly OsbOrigin origin = origin;
        readonly Action<OsbSprite, double, double> finalizeSprite = finalizeSprite;
        readonly List<PooledSprite> pooledSprites = [];

        public int MaxPoolDuration = 60000;

        public OsbSpritePool(StoryboardSegment segment, string path, OsbOrigin origin, bool additive) : this(segment, path, origin, additive ?
            (pS, sT, e) => pS.Additive(sT) : null)
        { }

        public OsbSprite Get(double startTime, double endTime)
        {
            PooledSprite result = null;

            foreach (var pooledSprite in pooledSprites)
                if (getMaxPoolDuration(startTime, endTime, MaxPoolDuration, pooledSprite) &&
                (result == null || pooledSprite.StartTime < result.StartTime)) result = pooledSprite;

            if (result is not null)
            {
                result.EndTime = endTime;
                return result.Sprite;
            }

            var sprite = CreateSprite(segment, path, origin);
            pooledSprites.Add(new PooledSprite(sprite, startTime, endTime));

            return sprite;
        }

        static bool getMaxPoolDuration(double startTime, double endTime, int value, PooledSprite sprite) => value > 0 ?
            sprite.EndTime <= startTime && startTime < sprite.StartTime + value : sprite.EndTime <= startTime;

        public void Clear()
        {
            if (finalizeSprite is not null)
                foreach (var pooledSprite in pooledSprites)
                {
                    var sprite = pooledSprite.Sprite;
                    finalizeSprite(sprite, sprite.CommandsStartTime, pooledSprite.EndTime);
                }
            pooledSprites.Clear();
        }

        protected virtual OsbSprite CreateSprite(StoryboardSegment segment, string path, OsbOrigin origin)
            => segment.CreateSprite(path, origin);

        class PooledSprite(OsbSprite sprite, double startTime, double endTime)
        {
            public OsbSprite Sprite = sprite;
            public double StartTime = startTime;
            public double EndTime = endTime;
        }

        #region IDisposable Support

        bool disposed;

        protected virtual void Dispose(bool dispose)
        {
            if (!disposed)
            {
                if (dispose) Clear();
                disposed = true;
            }
        }
        public void Dispose() => Dispose(true);

        #endregion
    }
}