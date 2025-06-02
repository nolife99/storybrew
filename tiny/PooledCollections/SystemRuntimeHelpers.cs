namespace Tiny.PooledCollections;

using System.Runtime.CompilerServices;

public static class SystemRuntimeHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsReferenceOrContainsReferences<T>() => RuntimeHelpers.IsReferenceOrContainsReferences<T>();
}