﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using osuTK.Graphics.OpenGL;

namespace BrewLib.Graphics;

public interface RenderState
{
    void Apply();
}
public class RenderStates
{
    public static readonly RenderStates Default = new();
    public BlendingFactorState BlendingFactor = BlendingFactorState.Default;

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
public class BlendingFactorState : RenderState, IEquatable<BlendingFactorState>
{
    readonly bool enabled = true;
    readonly BlendingFactorSrc src = BlendingFactorSrc.SrcAlpha, alphaSrc = BlendingFactorSrc.SrcAlpha;
    readonly BlendingFactorDest dest = BlendingFactorDest.OneMinusSrcAlpha, alphaDest = BlendingFactorDest.OneMinusSrcAlpha;

    public readonly static BlendingFactorState Default = new();

    public BlendingFactorState() { }
    public BlendingFactorState(BlendingMode mode)
    {
        switch (mode)
        {
            case BlendingMode.Off:
                enabled = false;
                break;

            case BlendingMode.Alphablend:
                src = alphaSrc = BlendingFactorSrc.SrcAlpha;
                dest = alphaDest = BlendingFactorDest.OneMinusSrcAlpha;
                break;

            case BlendingMode.Color:
                src = BlendingFactorSrc.SrcAlpha;
                dest = BlendingFactorDest.OneMinusSrcAlpha;
                alphaSrc = BlendingFactorSrc.Zero;
                alphaDest = BlendingFactorDest.One;
                break;

            case BlendingMode.Additive:
                src = alphaSrc = BlendingFactorSrc.SrcAlpha;
                dest = alphaDest = BlendingFactorDest.One;
                break;

            case BlendingMode.Premultiply:
                src = BlendingFactorSrc.SrcAlpha;
                dest = BlendingFactorDest.OneMinusSrcAlpha;
                alphaSrc = BlendingFactorSrc.One;
                alphaDest = BlendingFactorDest.OneMinusSrcAlpha;
                break;

            case BlendingMode.BlendAdd: case BlendingMode.Premultiplied:
                src = alphaSrc = BlendingFactorSrc.One;
                dest = alphaDest = BlendingFactorDest.OneMinusSrcAlpha;
                break;
        }
    }

    public void Apply()
    {
        DrawState.SetCapability(EnableCap.Blend, enabled);
        if (enabled) GL.BlendFuncSeparate(src, dest, alphaSrc, alphaDest);
    }

    public override bool Equals(object obj) => Equals(obj as BlendingFactorState);
    public bool Equals(BlendingFactorState other)
    {
        if (!enabled && !other.enabled) return true;
        return enabled == other.enabled && src == other.src && dest == other.dest && alphaSrc == other.alphaSrc && alphaDest == other.alphaDest;
    }
    public override int GetHashCode() => HashCode.Combine(src, dest, alphaSrc, alphaDest, enabled);
}