namespace Tiny.PooledCollections;

using System;
using System.Collections.Generic;
using System.Reflection;

static class NonRandomizedStringEqualityComparer
{
    static readonly IEqualityComparer<string> WrappedAroundDefaultComparer =
            getComparer(EqualityComparer<string>.Default),
        WrappedAroundStringComparerOrdinal = getComparer(StringComparer.Ordinal),
        WrappedAroundStringComparerOrdinalIgnoreCase = getComparer(StringComparer.OrdinalIgnoreCase);

    static IEqualityComparer<string> getComparer(IEqualityComparer<string> comparer)
        => (IEqualityComparer<string>)typeof(HashSet<string>)
            .GetField("_comparer", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(new HashSet<string>(comparer));

    public static IEqualityComparer<string> GetStringComparer(this object comparer)
    {
        if (ReferenceEquals(comparer, EqualityComparer<string>.Default)) return WrappedAroundDefaultComparer;
        if (ReferenceEquals(comparer, StringComparer.Ordinal)) return WrappedAroundStringComparerOrdinal;
        if (ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase))
            return WrappedAroundStringComparerOrdinalIgnoreCase;

        return null;
    }

    public static IEqualityComparer<string> GetRandomizedStringComparer(this object comparer)
    {
        if (ReferenceEquals(comparer, WrappedAroundDefaultComparer)) return EqualityComparer<string>.Default;
        if (ReferenceEquals(comparer, WrappedAroundStringComparerOrdinal)) return StringComparer.Ordinal;
        if (ReferenceEquals(comparer, WrappedAroundStringComparerOrdinalIgnoreCase))
            return StringComparer.OrdinalIgnoreCase;

        return null;
    }

    public static bool IsNonRandomizedStringComparer(this object comparer)
        => ReferenceEquals(comparer, WrappedAroundDefaultComparer) ||
            ReferenceEquals(comparer, WrappedAroundStringComparerOrdinal) ||
            ReferenceEquals(comparer, WrappedAroundStringComparerOrdinalIgnoreCase);
}