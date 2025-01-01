namespace BrewLib.Graphics;

using OpenTK.Graphics.OpenGL;

public class RenderStates
{
    static BlendingFactorState currentState;
    public BlendingFactorState BlendingFactor { get; init; } = BlendingFactorState.Default;

    public void Apply()
    {
        if (currentState.Equals(BlendingFactor)) return;

        DrawState.FlushRenderer();

        BlendingFactor.Apply();
        currentState = BlendingFactor;
    }

    public static void ClearStateCache() => currentState = BlendingFactorState.Default;
}

public readonly record struct BlendingFactorState
{
    public static readonly BlendingFactorState Default = new(BlendingMode.AlphaBlend);

    readonly BlendingFactorDest dest, alphaDest;

    readonly bool enabled = true;
    readonly BlendingFactorSrc src, alphaSrc;

    public BlendingFactorState(BlendingMode mode)
    {
        switch (mode)
        {
            case BlendingMode.Off: enabled = false; break;

            case BlendingMode.AlphaBlend:
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

            case BlendingMode.BlendAdd:
            case BlendingMode.Premultiplied:
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
}