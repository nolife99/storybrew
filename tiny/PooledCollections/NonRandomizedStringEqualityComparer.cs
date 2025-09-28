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
        => ReferenceEquals(comparer, EqualityComparer<string>.Default) ? WrappedAroundDefaultComparer :
            ReferenceEquals(comparer, StringComparer.Ordinal) ? WrappedAroundStringComparerOrdinal :
            ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase) ? WrappedAroundStringComparerOrdinalIgnoreCase :
            null;

    public static IEqualityComparer<string> GetRandomizedStringComparer(this object comparer)
        => ReferenceEquals(comparer, WrappedAroundDefaultComparer) ? EqualityComparer<string>.Default :
            ReferenceEquals(comparer, WrappedAroundStringComparerOrdinal) ? StringComparer.Ordinal :
            ReferenceEquals(comparer, WrappedAroundStringComparerOrdinalIgnoreCase) ? StringComparer.OrdinalIgnoreCase :
            (IEqualityComparer<string>)null;

    public static bool IsNonRandomizedStringComparer(this object comparer)
        => ReferenceEquals(comparer, WrappedAroundDefaultComparer) ||
            ReferenceEquals(comparer, WrappedAroundStringComparerOrdinal) ||
            ReferenceEquals(comparer, WrappedAroundStringComparerOrdinalIgnoreCase);
}