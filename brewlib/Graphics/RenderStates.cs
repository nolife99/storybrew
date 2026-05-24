namespace BrewLib.Graphics;

using System;

public struct RenderStates
{
    public static readonly RenderStates Default = new(BlendingMode.AlphaBlend);

    static RenderStates currentState;

    public BlendingFactorState BlendingFactor;

    public RenderStates() => BlendingFactor = new(BlendingMode.AlphaBlend);

    public RenderStates(BlendingMode blendingMode) => BlendingFactor = new(blendingMode);

    public static void ClearStateCache() => currentState = default;

    public void Apply()
    {
        if (currentState.BlendingFactor == BlendingFactor) return;

        DrawState.FlushRendererImmediate();
        DrawState.Device.SetBlendState(BlendingFactor);
        currentState.BlendingFactor = BlendingFactor;
    }
}

public readonly struct BlendingFactorState : IEquatable<BlendingFactorState>
{
    public readonly bool Enabled;
    public readonly BlendFactor Source, Destination, AlphaSource, AlphaDestination;

    public BlendingFactorState(BlendingMode blendingMode)
    {
        switch (blendingMode)
        {
            case BlendingMode.Off:
                Enabled = false;
                Source = Destination = AlphaSource = AlphaDestination = BlendFactor.One;
                break;

            case BlendingMode.Additive:
                Enabled = true;
                Source = BlendFactor.SrcAlpha;
                Destination = BlendFactor.One;
                AlphaSource = BlendFactor.SrcAlpha;
                AlphaDestination = BlendFactor.One;
                break;

            case BlendingMode.Color:
                Enabled = true;
                Source = BlendFactor.SrcAlpha;
                Destination = BlendFactor.OneMinusSrcAlpha;
                AlphaSource = BlendFactor.Zero;
                AlphaDestination = BlendFactor.One;
                break;

            case BlendingMode.Premultiply:
                Enabled = true;
                Source = BlendFactor.SrcAlpha;
                Destination = BlendFactor.OneMinusSrcAlpha;
                AlphaSource = BlendFactor.One;
                AlphaDestination = BlendFactor.OneMinusSrcAlpha;
                break;

            case BlendingMode.BlendAdd:
            case BlendingMode.Premultiplied:
                Enabled = true;
                Source = BlendFactor.One;
                Destination = BlendFactor.OneMinusSrcAlpha;
                AlphaSource = BlendFactor.One;
                AlphaDestination = BlendFactor.OneMinusSrcAlpha;
                break;

            case BlendingMode.AlphaBlend:
            default:
                Enabled = true;
                Source = BlendFactor.SrcAlpha;
                Destination = BlendFactor.OneMinusSrcAlpha;
                AlphaSource = BlendFactor.SrcAlpha;
                AlphaDestination = BlendFactor.OneMinusSrcAlpha;
                break;
        }
    }

    public bool Equals(BlendingFactorState other)
        => !Enabled && !other.Enabled ||
            Enabled == other.Enabled &&
            Source == other.Source &&
            Destination == other.Destination &&
            AlphaSource == other.AlphaSource &&
            AlphaDestination == other.AlphaDestination;

    public override bool Equals(object obj) => obj is BlendingFactorState other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Enabled, Source, Destination, AlphaSource, AlphaDestination);

    public static bool operator ==(BlendingFactorState left, BlendingFactorState right) => left.Equals(right);
    public static bool operator !=(BlendingFactorState left, BlendingFactorState right) => !left.Equals(right);
}

public enum BlendFactor
{
    Zero,
    One,
    SrcAlpha,
    OneMinusSrcAlpha
}