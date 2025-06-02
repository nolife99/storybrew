// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/QueueDebugView.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic;

using System;
using System.Diagnostics;

internal sealed class PooledQueueDebugView<T>
{
    readonly PooledQueue<T> _queue;

    public PooledQueueDebugView(PooledQueue<T> queue) => _queue = queue ?? throw new ArgumentNullException(nameof(queue));

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items => _queue.ToArray();
}