// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/QueueDebugView.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections.Generic.StructBased;

using System.Diagnostics;

internal sealed class ValueQueueDebugView<T>
{
    readonly ValueQueue<T> _queue;

    public ValueQueueDebugView(ValueQueue<T> queue) => _queue = queue;

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items => _queue.ToArray();
}