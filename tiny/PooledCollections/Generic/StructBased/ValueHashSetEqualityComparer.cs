// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/HashSetEqualityComparer.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CS8632

namespace Tiny.PooledCollections.Generic.StructBased;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>Equality comparer for hashsets of hashsets</summary>
public readonly struct ValueHashSetEqualityComparer<T> : IEqualityComparer<ValueHashSet<T>>
{
    public bool Equals(ValueHashSet<T> x, ValueHashSet<T> y)
    {
        // If they're the exact same instance, they're equal.
        if (ReferenceEquals(x._buckets, y._buckets)) return true;

        // They're not both null, so if either is null, they're not equal.
        if (x._buckets.IsNullOrEmpty() || y._buckets.IsNullOrEmpty()) return false;

        var defaultComparer = EqualityComparer<T>.Default;

        // If both sets use the same comparer, they're equal if they're the same
        // size and one is a "subset" of the other.
        if (ValueHashSet<T>.EqualityComparersAreEqual(x, y))
            return x.Count == y.Count && y.IsSubsetOfHashSetWithSameComparer(x);

        // Otherwise, do an O(N^2) match.
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

    public int GetHashCode(ValueHashSet<T> obj)
    {
        var hashCode = 0; // default to 0 for null/empty set

        if (obj._buckets.IsNullOrEmpty() == false)
            foreach (var t in obj)
                if (t != null)
                    hashCode ^= t.GetHashCode(); // same hashcode as default comparer

        return hashCode;
    }

    // Equals method for the comparer itself.
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is ValueHashSetEqualityComparer<T>;

    public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode();
}