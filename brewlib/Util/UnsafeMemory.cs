namespace BrewLib.Util;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public static class UnsafeMemory
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T AsRef<T>(this nint ptr) where T : allows ref struct => ref Unsafe.AddByteOffset(ref Unsafe.NullRef<T>(), ptr);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this nint ptr, int length) where T : struct
        => MemoryMarshal.CreateSpan(ref ptr.AsRef<T>(), length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this nint ptr, int length) where T : struct
        => MemoryMarshal.CreateSpan(ref ptr.AsRef<T>(), length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint AsPointer<T>(this Span<T> pinned) where T : struct
        => Unsafe.ByteOffset(in Unsafe.NullRef<T>(), in MemoryMarshal.GetReference(pinned));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint AsPointer<T>(this ReadOnlySpan<T> pinned) where T : struct
        => Unsafe.ByteOffset(in Unsafe.NullRef<T>(), in MemoryMarshal.GetReference(pinned));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint AsPointer<T>(this scoped ref T pinned) where T : struct, allows ref struct
        => Unsafe.ByteOffset(in Unsafe.NullRef<T>(), in pinned);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint AsPointerUnconstrained<T>(scoped ref readonly T pinned) where T : allows ref struct
        => Unsafe.ByteOffset(in Unsafe.NullRef<T>(), in pinned);
}