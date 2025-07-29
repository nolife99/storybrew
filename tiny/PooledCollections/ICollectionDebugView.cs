// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Tiny.PooledCollections;

using System;
using System.Collections.Generic;
using System.Diagnostics;

sealed class ICollectionDebugView<T>
{
    readonly ICollection<T> _collection;

    public ICollectionDebugView(ICollection<T> collection)
        => _collection = collection ?? throw new ArgumentNullException(nameof(collection));

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items
    {
        get
        {
            var items = new T[_collection.Count];
            _collection.CopyTo(items, 0);
            return items;
        }
    }
}