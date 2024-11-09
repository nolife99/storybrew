namespace BrewLib.Graphics;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

public interface RenderState
{
    void Apply();
}

public class RenderStates
{
    static readonly FieldInfo[] fields = typeof(RenderStates).GetFields();
    static readonly Dictionary<Type, RenderState> currentStates = [];

    public void Apply()
    {
        var flushed = false;
        foreach (var field in fields)
        {
            if (field.IsStatic) return;

            var newState = Unsafe.As<RenderState>(field.GetValue(this));
            if (currentStates.TryGetValue(field.FieldType, out var currentState) && currentState.Equals(newState)) return;

            if (!flushed)
            {
                DrawState.FlushRenderer();
                flushed = true;
            }

            newState.Apply();
            currentStates[field.FieldType] = newState;
        }
    }

    public override string ToString() => string.Join('\n', currentStates.Values);
    public static void ClearStateCache() => currentStates.Clear();
}