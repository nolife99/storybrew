// https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/Collections/Generic/EnumerableHelpers.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

static class EnumerableHelpers
{
    public static T[] ToArray<T>(IEnumerable<T> source, T[] emptyArray, ArrayPool<T> pool, out int length)
    {
        if (source is ICollection<T> ic)
        {
            var count = ic.Count;
            if (count != 0)
            {
                var arr = pool.Rent(count);
                ic.CopyTo(arr, 0);
                length = count;
                return arr;
            }
        }
        else
            using (var en = source.GetEnumerator())
                if (en.MoveNext())
                {
                    const int DefaultCapacity = 4;
                    var arr = pool.Rent(DefaultCapacity);
                    arr[0] = en.Current;
                    var count = 1;
                    var clearArray = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

                    while (en.MoveNext())
                    {
                        if (count == arr.Length)
                        {
                            var newLength = count << 1;
                            if ((uint)newLength > Array.MaxLength)
                                newLength = Array.MaxLength <= count ? count + 1 : Array.MaxLength;

                            var newArr = pool.Rent(newLength);
                            Array.Copy(arr, newArr, count);
                            pool.Return(arr, clearArray);
                            arr = newArr;
                        }

                        arr[count++] = en.Current;
                    }

                    length = count;
                    return arr;
                }

        length = 0;
        return emptyArray;
    }
}