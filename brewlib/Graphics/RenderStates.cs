namespace BrewLib.Graphics;

using osuTK.Graphics.OpenGL;

public class RenderStates
{
    static BlendingFactorState currentState;
    public BlendingFactorState BlendingFactor { get; init; } = BlendingFactorState.Default;

    public void Apply()
    {
        if (currentState is not null && currentState.Equals(BlendingFactor)) return;

        DrawState.FlushRenderer();

        BlendingFactor.Apply();
        currentState = BlendingFactor;
    }
    public static void ClearStateCache() => currentState = null;
}

public class BlendingFactorState
{
    public static readonly BlendingFactorState Default = new();

    readonly BlendingFactorDest dest = BlendingFactorDest.OneMinusSrcAlpha, alphaDest = BlendingFactorDest.OneMinusSrcAlpha;

    readonly bool enabled = true;
    readonly BlendingFactorSrc src = BlendingFactorSrc.SrcAlpha, alphaSrc = BlendingFactorSrc.SrcAlpha;

    BlendingFactorState() { }
    public BlendingFactorState(BlendingMode mode)
    {
        switch (mode)
        {
            case BlendingMode.Off:
                enabled = false;
                break;

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