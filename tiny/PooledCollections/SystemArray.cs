namespace Tiny.PooledCollections;

using System;
using System.Runtime.CompilerServices;

public static class SystemArray
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNullOrEmpty(this Array array) => array is null || array.Length == 0;
}