// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/HashSetEqualityComparer.cs

namespace Tiny.PooledCollections.Generic;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

internal readonly struct HashSetEqualityComparer<T>
    : IEqualityComparer<PooledHashSet<T>>, IEquatable<HashSetEqualityComparer<T>>
{
    public bool Equals(PooledHashSet<T> x, PooledHashSet<T> y)
    {
        if (ReferenceEquals(x, y)) return true;

        if (x is null || y is null) return false;

        var defaultComparer = EqualityComparer<T>.Default;

        if (PooledHashSet<T>.EqualityComparersAreEqual(x, y))
            return x.Count == y.Count && y.IsSubsetOfHashSetWithSameComparer(x);

        foreach (var yi in y)
        {
            var found = false;
            foreach (var xi in x)
                if (defaultComparer.Equals(yi, xi))
                {
                    found = true;
                    break;
                }

            if (!found) return false;
        }

        return true;
    }

    public int GetHashCode(PooledHashSet<T> obj)
    {
        HashCode hashCode = new();

        foreach (var t in obj)
            if (t is not null)
                hashCode.Add(t);

        return hashCode.ToHashCode();
    }

    public override bool Equals([NotNullWhen(true)] object obj) => obj is HashSetEqualityComparer<T>;

    public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode();

    public static bool operator ==(HashSetEqualityComparer<T> left, HashSetEqualityComparer<T> right) => left.Equals(right);

    public static bool operator !=(HashSetEqualityComparer<T> left, HashSetEqualityComparer<T> right) => !(left == right);

    public bool Equals(HashSetEqualityComparer<T> other) => true;
}