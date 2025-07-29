namespace Tiny.PooledCollections;

using System.Runtime.CompilerServices;

public static class SystemArray
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNullOrEmpty<T>(this T[] array) => array is null || array.Length == 0;
}