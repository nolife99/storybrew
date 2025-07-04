// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Collections/src/System/Collections/Generic/StackDebugView.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic.StructBased;

using System.Diagnostics;

internal sealed class ValueStackDebugView<T>
{
    ValueStack<T> _stack;

    public ValueStackDebugView(ValueStack<T> stack) => _stack = stack;

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items => _stack.ToArray();
}