// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Collections/src/System/Collections/Generic/StackDebugView.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic;

using System;
using System.Diagnostics;

internal sealed class PooledStackDebugView<T>
{
    readonly PooledStack<T> _stack;

    public PooledStackDebugView(PooledStack<T> stack) => _stack = stack ?? throw new ArgumentNullException(nameof(stack));

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items => _stack.ToArray();
}