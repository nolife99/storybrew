using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StorybrewCommon.Util
{
    ///<summary> Contains common LINQ methods implemented using multi-threading. </summary>
    public static class ParallelExtensions
    {
        ///<inheritdoc cref="System.Linq.Enumerable.Any{TSource}(IEnumerable{TSource}, Func{TSource, bool})"/>
        public static bool Any<T>(IEnumerable<T> source, Func<T, bool> predicate)
        {
            var boolValue = false;
            Parallel.ForEach(source, (t, state) =>
            {
                if (predicate(t))
                {
                    boolValue = true;
                    state.Stop();
                }
            });

            return boolValue;
        }

        ///<inheritdoc cref="System.Linq.Enumerable.All{TSource}(IEnumerable{TSource}, Func{TSource, bool})"/>
        public static bool All<T>(IEnumerable<T> source, Func<T, bool> predicate)
        {
            var boolValue = true;
            Parallel.ForEach(source, (t, state) =>
            {
                if (!predicate(t))
                {
                    boolValue = false;
                    state.Stop();
                }
            });

            return boolValue;
        }
    }
}