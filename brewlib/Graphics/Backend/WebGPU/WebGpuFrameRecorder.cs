namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

sealed class WebGpuFrameRecorder
{
    RecordedDraw[] draws = new RecordedDraw[256];

    byte[] pushArena = new byte[8192];
    int pushCount;
    int vbCount;

    VbBinding[] vbs = new VbBinding[512];

    public int DrawCount { get; private set; }

    public void Reset()
    {
        DrawCount = 0;
        vbCount = 0;
        pushCount = 0;
    }

    public int BeginVertexBindings() => vbCount;

    public void AddVertexBinding(uint slot, WgpuBuffer buffer, ulong offset)
    {
        if (vbCount == vbs.Length) Array.Resize(ref vbs, vbs.Length * 2);
        vbs[vbCount++] = new(slot, buffer, offset);
    }

    public int AddPushConstants(scoped ReadOnlySpan<byte> bytes)
    {
        var start = pushCount;
        var end = start + bytes.Length;
        if (end > pushArena.Length)
        {
            var cap = pushArena.Length;
            while (cap < end) cap *= 2;
            Array.Resize(ref pushArena, cap);
        }

        bytes.CopyTo(pushArena.AsSpan(start));
        pushCount = end;
        return start;
    }

    public void AddDraw(in RecordedDraw draw)
    {
        if (DrawCount == 0)
            DrawState.CountRenderPass();

        if (DrawCount == draws.Length) Array.Resize(ref draws, draws.Length * 2);
        draws[DrawCount++] = draw;
    }

    public void Replay(CommandEncoder encoder,
        TextureView targetView,
        in WGPUColor clearColor,
        uint targetWidth,
        uint targetHeight)
    {
        using var pass = encoder.BeginRenderPass([new(targetView, WGPULoadOp.Clear, WGPUStoreOp.Store, clearColor)]);

        var lastVpX = 0f;
        var lastVpY = 0f;
        var lastVpW = (float)targetWidth;
        var lastVpH = (float)targetHeight;
        var lastScX = 0u;
        var lastScY = 0u;
        var lastScW = targetWidth;
        var lastScH = targetHeight;
        var firstDraw = true;

        for (var i = 0; i < DrawCount; ++i)
        {
            ref readonly var draw = ref draws[i];
            pass.SetPipeline(draw.Pipeline);

            var vpX = draw.HasViewport ? draw.VpX : 0f;
            var vpY = draw.HasViewport ? draw.VpY : 0f;
            var vpW = draw.HasViewport ? draw.VpW : targetWidth;
            var vpH = draw.HasViewport ? draw.VpH : targetHeight;
            if (vpW <= 0f || vpH <= 0f)
                continue;

            if (firstDraw || vpX != lastVpX || vpY != lastVpY || vpW != lastVpW || vpH != lastVpH)
            {
                pass.SetViewport(vpX, vpY, vpW, vpH);
                lastVpX = vpX;
                lastVpY = vpY;
                lastVpW = vpW;
                lastVpH = vpH;
            }

            var scX = draw.ScissorEnabled ? draw.ScX : 0u;
            var scY = draw.ScissorEnabled ? draw.ScY : 0u;
            var scW = draw.ScissorEnabled ? draw.ScW : targetWidth;
            var scH = draw.ScissorEnabled ? draw.ScH : targetHeight;
            if (scW == 0u || scH == 0u)
                continue;

            if (firstDraw || scX != lastScX || scY != lastScY || scW != lastScW || scH != lastScH)
            {
                pass.SetScissorRect(scX, scY, scW, scH);
                lastScX = scX;
                lastScY = scY;
                lastScW = scW;
                lastScH = scH;
            }

            firstDraw = false;

            var vbEnd = draw.VbStart + draw.VbCount;
            for (var v = draw.VbStart; v < vbEnd; ++v)
            {
                ref readonly var vb = ref vbs[v];
                pass.SetVertexBuffer(vb.Slot, vb.Buffer, vb.Offset);
            }

            if (draw.HasTexture)
                pass.SetBindGroup(draw.TextureGroupIndex, draw.TextureGroup);

            switch (draw.UniformKind)
            {
                case UniformKind.PushConstants:
                    pass.SetPushConstants(0, pushArena.AsSpan(draw.PushStart, draw.PushLen));
                    break;

                case UniformKind.DynamicOffset:
                    pass.SetBindGroup(draw.UniformGroupIndex, draw.UniformGroup, [draw.UniformOffset]);
                    break;

                case UniformKind.None:
                default:
                    break;
            }

            pass.Draw(draw.VertexCount, draw.InstanceCount, draw.FirstVertex, draw.FirstInstance);
        }
    }

    internal readonly record struct VbBinding(uint Slot, WgpuBuffer Buffer, ulong Offset);
}

enum UniformKind : byte
{
    None,
    PushConstants,
    DynamicOffset
}

struct RecordedDraw
{
    public RenderPipeline Pipeline;

    public int VbStart;
    public int VbCount;

    public bool HasTexture;
    public uint TextureGroupIndex;
    public BindGroup TextureGroup;

    public UniformKind UniformKind;
    public int PushStart;
    public int PushLen;
    public uint UniformGroupIndex;
    public BindGroup UniformGroup;
    public uint UniformOffset;

    public uint VertexCount;
    public uint InstanceCount;
    public uint FirstVertex;
    public uint FirstInstance;

    public bool HasViewport;
    public float VpX, VpY, VpW, VpH;

    public bool ScissorEnabled;
    public uint ScX, ScY, ScW, ScH;
}