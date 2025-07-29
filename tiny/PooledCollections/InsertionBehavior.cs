// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/InsertionBehavior.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tiny.PooledCollections;

public enum InsertionBehavior : byte
{
    None = 0, OverwriteExisting = 1, ThrowOnExisting = 2
}