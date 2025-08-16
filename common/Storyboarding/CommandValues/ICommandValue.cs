namespace StorybrewCommon.Storyboarding.CommandValues;

using System;
using System.Numerics;
using Tiny.PooledCollections.Generic.Temporary;

/// <summary> Represents a common interface for different types of command values used in storyboarding. </summary>
public interface ICommandValue<TValue> : IEquatable<TValue>, IAdditionOperators<TValue, TValue, TValue>,
    ISubtractionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, CommandDecimal, TValue>
    where TValue : IAdditionOperators<TValue, TValue, TValue>, ISubtractionOperators<TValue, TValue, TValue>,
    IMultiplyOperators<TValue, CommandDecimal, TValue>
{
    internal TempList<char> ToOsbString(ExportSettings exportSettings);
}