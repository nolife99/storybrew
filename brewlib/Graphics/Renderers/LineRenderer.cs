namespace BrewLib.Graphics.Renderers;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Backend;
using Cameras;
using Shaders;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Util;

public sealed class LineRenderer : ILineRenderer
{
    const int VertexPerLine = 2;

    static readonly ShaderUniformBinding<Matrix4x4> CombinedMatrixUniform = new("u_combinedMatrix",
        ShaderValueType.FloatMat4);

    static readonly ShaderAttributeBinding VertexAttribute = new("a_vertex", ShaderValueType.Float);
    static readonly ShaderAttributeBinding FromAttribute = new("a_from", ShaderValueType.FloatVec3);
    static readonly ShaderAttributeBinding ToAttribute = new("a_to", ShaderValueType.FloatVec3);
    static readonly ShaderAttributeBinding ColorAttribute = new("a_color", ShaderValueType.FloatVec4);

    static readonly float[] UnitLineVertices = [0, 1];
    readonly bool bufferTransientDraws;
    readonly IRenderUniform<Matrix4x4> combinedMatrixUniform;
    readonly List<LineIndirectBatchGroup> indirectBatchGroups;
    readonly IGraphicsBuffer indirectBuffer;
    readonly ITransientGraphicsBuffer instanceBuffer;
    readonly int instanceStride, instanceBatchCapacity;
    readonly List<LineBatch> pendingBatches;

    readonly IRenderPipeline pipeline;
    readonly bool useIndirectMultiDraws;
    readonly IGraphicsBuffer vertexBuffer;

    ICamera camera;
    bool disposed, rendering;
    IndirectDrawCommand[] indirectCommands;
    TransientBufferAllocation instanceAllocation;
    int instanceCount, primaryInstanceCapacity;
    nint instanceData, instanceDataSecondary;
    Matrix4x4 transformMatrix = Matrix4x4.Identity;

    public LineRenderer(IGraphicsBackend backend = null, int initialBatchCapacity = 256)
    {
        if (initialBatchCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(initialBatchCapacity), initialBatchCapacity, null);

        backend ??= DrawState.Backend ??
            throw new InvalidOperationException("A graphics backend must be initialized before creating renderers");

        instanceStride = Unsafe.SizeOf<LineInstance>();
        instanceBatchCapacity = int.Max(1, initialBatchCapacity);
        bufferTransientDraws = backend.PrefersBufferedTransientDraws;
        useIndirectMultiDraws = bufferTransientDraws && backend.Capabilities.Has(GraphicsBackendFeatures.IndirectDraws);
        pendingBatches = bufferTransientDraws ? [] : null;
        indirectBatchGroups = useIndirectMultiDraws ? [] : null;
        pipeline = backend.RenderPipelines.CreateRenderPipeline(CreatePipelineDescription(backend.ShaderSourceLanguage,
            backend.Capabilities.Has(GraphicsBackendFeatures.ManualColorCorrection)));

        combinedMatrixUniform = pipeline.GetUniform(CombinedMatrixUniform);

        vertexBuffer = backend.Buffers.CreateBuffer(new(nameof(LineRenderer) + ".Vertices",
            GraphicsBufferTarget.Vertex,
            GraphicsBufferUsage.Static));

        instanceBuffer = backend.TransientBuffers.CreateBuffer(new(nameof(LineRenderer) + ".Instances",
                GraphicsBufferTarget.Vertex,
                GraphicsBufferUsage.Stream),
            getRingCapacity(instanceStride * instanceBatchCapacity));

        if (useIndirectMultiDraws)
        {
            indirectCommands = new IndirectDrawCommand[2];
            indirectBuffer = backend.Buffers.CreateBuffer(new(nameof(LineRenderer) + ".Indirect",
                GraphicsBufferTarget.Indirect,
                GraphicsBufferUsage.Stream));
        }

        vertexBuffer.SetData(UnitLineVertices);
        pipeline.BindVertexBuffer(0, vertexBuffer);
        pipeline.BindVertexBuffer(1, instanceBuffer.Buffer);
    }

    public Matrix4x4 TransformMatrix
    {
        get => transformMatrix;
        set
        {
            if (transformMatrix == value) return;

            DrawState.FlushRenderer();
            transformMatrix = value;
        }
    }

    public PrimitiveTopology Topology => PrimitiveTopology.Lines;

    public PrimitiveBatchFeatures BatchFeatures
        => PrimitiveBatchFeatures.PainterOrdered |
            PrimitiveBatchFeatures.Batched |
            PrimitiveBatchFeatures.Instanced |
            PrimitiveBatchFeatures.VertexColored;

    public ICamera Camera
    {
        get => camera;
        set
        {
            if (camera == value) return;

            if (rendering) DrawState.FlushRenderer();
            camera = value;
        }
    }

    void IRenderer.BeginRendering()
    {
        pipeline.Bind();
        rendering = true;
    }

    void IRenderer.EndRendering()
    {
        if (instanceCount != 0) ((IRenderer)this).Flush(bufferTransientDraws);
        replayPendingBatches();

        pipeline.Unbind();
        rendering = false;
    }

    void IRenderer.Flush(bool canBuffer)
    {
        if (instanceCount == 0)
        {
            if (!canBuffer) replayPendingBatches();
            return;
        }

        if (bufferTransientDraws && canBuffer)
        {
            queueCurrentBatch();
            return;
        }

        replayPendingBatches();
        drawCurrentBatch();
    }

    void ILineRenderer.Draw(scoped ref readonly Vector3 start,
        scoped ref readonly Vector3 end,
        scoped ref readonly Color color)
    {
        if (instanceCount == instanceBatchCapacity)
            DrawState.FlushRenderer(true);

        ensureInstanceBatch();

        var instance = new LineInstance
        {
            From = start,
            To = end,
            Color = color.ToPixel<Rgba32>()
        };

        if (instanceCount < primaryInstanceCapacity)
            Unsafe.Add(ref instanceData.AsRef<LineInstance>(), instanceCount) = instance;
        else
            Unsafe.Add(ref instanceDataSecondary.AsRef<LineInstance>(), instanceCount - primaryInstanceCapacity) = instance;

        ++instanceCount;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void queueCurrentBatch()
    {
        var usedBytes = instanceCount * instanceStride;
        instanceBuffer.Commit(in instanceAllocation, usedBytes);

        pendingBatches.Add(new(instanceAllocation,
            instanceCount,
            usedBytes,
            transformMatrix * camera.ProjectionView));

        instanceBuffer.MarkSubmitted(in instanceAllocation, usedBytes);
        resetInstanceBatch();
    }

    void drawCurrentBatch()
    {
        var usedBytes = instanceCount * instanceStride;
        instanceBuffer.Commit(in instanceAllocation, usedBytes);

        var allocation = instanceAllocation;
        var combinedMatrix = transformMatrix * camera.ProjectionView;
        drawBatch(in allocation,
            instanceCount,
            usedBytes,
            in combinedMatrix);

        resetInstanceBatch();
    }

    void replayPendingBatches()
    {
        if (pendingBatches is null || pendingBatches.Count == 0) return;

        if (tryReplayPendingBatchesIndirect())
        {
            pendingBatches.Clear();
            return;
        }

        for (var i = 0; i < pendingBatches.Count; ++i)
        {
            var batch = pendingBatches[i];
            var allocation = batch.Allocation;
            var combinedMatrix = batch.CombinedMatrix;
            drawBatch(in allocation,
                batch.InstanceCount,
                batch.UsedBytes,
                in combinedMatrix,
                false);
        }

        pendingBatches.Clear();
    }

    bool tryReplayPendingBatchesIndirect()
    {
        if (!useIndirectMultiDraws || pendingBatches.Count == 0) return false;

        var totalCommandCount = 0;
        var commandStride = Unsafe.SizeOf<IndirectDrawCommand>();
        indirectBatchGroups.Clear();

        var groupCommandOffset = 0;
        var groupCommandCount = 0;
        var groupMatrix = pendingBatches[0].CombinedMatrix;
        var groupBuffer = pendingBatches[0].Allocation.Buffer;

        for (var i = 0; i < pendingBatches.Count; ++i)
        {
            var batch = pendingBatches[i];
            // Groups must share the underlying instance buffer; otherwise the merged
            // indirect draw reads from the wrong buffer (the transient ring spans
            // multiple pages once exhausted).
            if (batch.CombinedMatrix != groupMatrix ||
                !ReferenceEquals(batch.Allocation.Buffer, groupBuffer))
            {
                if (groupCommandCount != 0)
                    indirectBatchGroups.Add(new(groupMatrix, groupBuffer, groupCommandOffset * commandStride, groupCommandCount));

                groupMatrix = batch.CombinedMatrix;
                groupBuffer = batch.Allocation.Buffer;
                groupCommandOffset = totalCommandCount;
                groupCommandCount = 0;
            }

            var allocation = batch.Allocation;
            var primaryCount = getPrimaryInstanceCount(in allocation, batch.InstanceCount);
            ensureIndirectCommandCapacity(totalCommandCount + 2);
            var commandCount = writeIndirectSplitCommands(in allocation,
                batch.InstanceCount,
                primaryCount,
                indirectCommands.AsSpan(totalCommandCount));

            totalCommandCount += commandCount;
            groupCommandCount += commandCount;
        }

        if (groupCommandCount != 0)
            indirectBatchGroups.Add(new(groupMatrix, groupBuffer, groupCommandOffset * commandStride, groupCommandCount));

        if (totalCommandCount == 0) return false;

        indirectBuffer.SetData(indirectCommands.AsSpan(0, totalCommandCount));

        foreach (var group in indirectBatchGroups)
        {
            var combinedMatrix = group.CombinedMatrix;
            combinedMatrixUniform.SetValue(combinedMatrix);
            pipeline.BindVertexBuffer(1, group.InstanceBuffer, 0);
            pipeline.DrawIndirect(new(indirectBuffer, group.CommandOffset, group.CommandCount));
        }

        return true;
    }

    void drawBatch(scoped ref readonly TransientBufferAllocation allocation,
        int count,
        int usedBytes,
        scoped ref readonly Matrix4x4 combinedMatrix,
        bool markSubmitted = true,
        DrawIndirectCommand preparedIndirectCommand = default,
        bool allowInlineIndirectUpload = true)
    {
        var primaryCount = getPrimaryInstanceCount(in allocation, count);

        var indirectCommand = preparedIndirectCommand;
        if (indirectCommand.DrawCount != 0 ||
            allowInlineIndirectUpload && tryPrepareIndirectSplitDraw(in allocation, count, primaryCount, out indirectCommand))
        {
            combinedMatrixUniform.SetValue(combinedMatrix);
            pipeline.BindVertexBuffer(1, allocation.Buffer, 0);
            pipeline.DrawIndirect(indirectCommand);

            if (markSubmitted) instanceBuffer.MarkSubmitted(in allocation, usedBytes);
            return;
        }

        combinedMatrixUniform.SetValue(combinedMatrix);

        pipeline.BindVertexBuffer(1, allocation.Buffer, allocation.Offset);
        pipeline.DrawInstanced(new(VertexPerLine, primaryCount));

        if (allocation.IsSplit && count > primaryCount)
        {
            pipeline.BindVertexBuffer(1, allocation.Buffer, 0);
            pipeline.DrawInstanced(new(VertexPerLine, count - primaryCount));
        }

        if (markSubmitted) instanceBuffer.MarkSubmitted(in allocation, usedBytes);
    }

    bool tryPrepareIndirectSplitDraw(scoped ref readonly TransientBufferAllocation allocation,
        int count,
        int primaryCount,
        out DrawIndirectCommand command)
    {
        command = default;
        if (!canUseIndirectSplitDraw(in allocation, count, primaryCount))
            return false;

        ensureIndirectCommandCapacity(2);
        var commandCount = writeIndirectSplitCommands(in allocation,
            count,
            primaryCount,
            indirectCommands);

        indirectBuffer.SetData(indirectCommands.AsSpan(0, commandCount));
        command = new(indirectBuffer, 0, commandCount);
        return true;
    }

    int writeIndirectSplitCommands(scoped ref readonly TransientBufferAllocation allocation,
        int count,
        int primaryCount,
        Span<IndirectDrawCommand> commands)
    {
        var commandCount = 0;
        if (primaryCount > 0)
            commands[commandCount++] = new(VertexPerLine,
                (uint)primaryCount,
                0,
                checked((uint)(allocation.Offset / instanceStride)));

        var secondaryCount = count - primaryCount;
        if (secondaryCount > 0)
            commands[commandCount++] = new(VertexPerLine, (uint)secondaryCount);

        return commandCount;
    }

    int getPrimaryInstanceCount(scoped ref readonly TransientBufferAllocation allocation, int count)
        => allocation.IsSplit ? int.Min(count, allocation.PrimarySize / instanceStride) : count;

    bool canUseIndirectSplitDraw(scoped ref readonly TransientBufferAllocation allocation,
        int count,
        int primaryCount)
        => useIndirectMultiDraws && allocation.IsSplit && count > primaryCount;

    void ensureIndirectCommandCapacity(int commandCount)
    {
        if (indirectCommands.Length >= commandCount) return;

        var capacity = indirectCommands.Length;
        while (capacity < commandCount)
            capacity = checked(capacity * 2);

        Array.Resize(ref indirectCommands, capacity);
    }

    static RenderPipelineDescription CreatePipelineDescription(ShaderSourceLanguage language, bool useManualColorCorrection)
    {
        var instanceStride = Unsafe.SizeOf<LineInstance>();

        return new(nameof(LineRenderer),
            createShaderSource(language, useManualColorCorrection),
            new(),
            new(
                new VertexBufferLayout(0,
                    Unsafe.SizeOf<float>(),
                    VertexInputRate.Vertex,
                    new VertexElement(VertexAttribute, VertexAttributeFormat.Float32, 0)),
                new VertexBufferLayout(1,
                    instanceStride,
                    VertexInputRate.Instance,
                    new VertexElement(FromAttribute, VertexAttributeFormat.Float32x3, 0),
                    new VertexElement(ToAttribute, VertexAttributeFormat.Float32x3, OffsetOf(nameof(LineInstance.To))),
                    new VertexElement(ColorAttribute, VertexAttributeFormat.Unorm8x4, OffsetOf(nameof(LineInstance.Color))))),
            PrimitiveTopology.Lines);
    }

    static ShaderProgramSource createShaderSource(ShaderSourceLanguage language, bool useManualColorCorrection)
        => language switch
        {
            ShaderSourceLanguage.Hlsl => new(nameof(LineRenderer),
                createHlslVertexShader(),
                createHlslFragmentShader(useManualColorCorrection)),
            ShaderSourceLanguage.Wgsl => new(nameof(LineRenderer),
                createWgslVertexShader(),
                createWgslFragmentShader(),
                ShaderSourceLanguage.Wgsl),
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
        };

    static string createHlslVertexShader()
        => """
           #pragma pack_matrix(row_major)

           cbuffer TransformUniforms : register(b0, space1)
           {
               float4x4 u_combinedMatrix;
           };

           struct VertexInput
           {
               [[vk::location(0)]] float Vertex : TEXCOORD0;
               [[vk::location(1)]] float3 From : TEXCOORD1;
               [[vk::location(2)]] float3 To : TEXCOORD2;
               [[vk::location(3)]] float4 Color : TEXCOORD3;
           };

           struct VertexOutput
           {
               float4 Position : SV_Position;
               float4 Color : COLOR0;
           };

           VertexOutput main(VertexInput input)
           {
               VertexOutput output;
               float3 position = lerp(input.From, input.To, input.Vertex);
               output.Position = mul(float4(position, 1), u_combinedMatrix);
               output.Color = input.Color;
               return output;
           }
           """;

    static string createWgslVertexShader()
        => """
           struct TransformUniforms {
               rows: array<vec4<f32>, 4>,
           };

           @group(0) @binding(0) var<uniform> u_transform: TransformUniforms;

           struct VertexInput {
               @location(0) vertex: f32,
               @location(1) line_start: vec3<f32>,
               @location(2) line_end: vec3<f32>,
               @location(3) color: vec4<f32>,
           };

           struct VertexOutput {
               @builtin(position) position: vec4<f32>,
               @location(0) color: vec4<f32>,
           };

           fn transform_position(position: vec4<f32>) -> vec4<f32> {
               return vec4<f32>(
                   position.x * u_transform.rows[0].x + position.y * u_transform.rows[1].x + position.z * u_transform.rows[2].x + position.w * u_transform.rows[3].x,
                   position.x * u_transform.rows[0].y + position.y * u_transform.rows[1].y + position.z * u_transform.rows[2].y + position.w * u_transform.rows[3].y,
                   position.x * u_transform.rows[0].z + position.y * u_transform.rows[1].z + position.z * u_transform.rows[2].z + position.w * u_transform.rows[3].z,
                   position.x * u_transform.rows[0].w + position.y * u_transform.rows[1].w + position.z * u_transform.rows[2].w + position.w * u_transform.rows[3].w);
           }

           @vertex
           fn main(input: VertexInput) -> VertexOutput {
               var output: VertexOutput;
               let position = mix(input.line_start, input.line_end, input.vertex);
               output.position = transform_position(vec4<f32>(position, 1.0));
               output.color = input.color;
               return output;
           }
           """;

    static string createWgslFragmentShader()
        => """
           struct FragmentInput {
               @location(0) color: vec4<f32>,
           };

           @fragment
           fn main(input: FragmentInput) -> @location(0) vec4<f32> {
               return input.color;
           }
           """;

    static string createHlslFragmentShader(bool useManualColorCorrection)
        => useManualColorCorrection
            ? """
              struct FragmentInput
              {
                  float4 Position : SV_Position;
                  float4 Color : COLOR0;
              };

              float4 apply_output_color(float4 color)
              {
                  color.rgb = pow(saturate(color.rgb), float3(2.2, 2.2, 2.2));
                  return color;
              }

              float4 main(FragmentInput input) : SV_Target0
              {
                  return apply_output_color(input.Color);
              }
              """
            : """
              struct FragmentInput
              {
                  float4 Position : SV_Position;
                  float4 Color : COLOR0;
              };

              float4 main(FragmentInput input) : SV_Target0
              {
                  return input.Color;
              }
              """;

    ~LineRenderer() => Dispose(false);

    void Dispose(bool disposing)
    {
        if (disposed) return;

        if (rendering) ((IRenderer)this).EndRendering();

        indirectBuffer?.Dispose();
        vertexBuffer.Dispose();
        instanceBuffer.Dispose();
        pipeline.Dispose();

        disposed = true;
    }

    void ensureInstanceBatch()
    {
        if (instanceData != nint.Zero) return;

        instanceAllocation = instanceBuffer.Allocate(instanceStride * instanceBatchCapacity);
        instanceData = instanceAllocation.Data;
        instanceDataSecondary = instanceAllocation.SecondaryData;
        primaryInstanceCapacity = instanceAllocation.PrimarySize / instanceStride;
    }

    void resetInstanceBatch()
    {
        instanceAllocation = default;
        instanceData = nint.Zero;
        instanceDataSecondary = nint.Zero;
        instanceCount = 0;
        primaryInstanceCapacity = 0;
    }

    static int OffsetOf(string fieldName)
        => (int)Marshal.OffsetOf<LineInstance>(fieldName);

    static int getRingCapacity(int batchSizeInBytes)
        => int.Max(checked(batchSizeInBytes * 8), 64 * 1024);

    [StructLayout(LayoutKind.Sequential)]
    struct LineInstance
    {
        public Vector3 From;
        public Vector3 To;
        public Rgba32 Color;
    }

    readonly record struct LineBatch(
        TransientBufferAllocation Allocation,
        int InstanceCount,
        int UsedBytes,
        Matrix4x4 CombinedMatrix);

    readonly record struct LineIndirectBatchGroup(
        Matrix4x4 CombinedMatrix,
        IGraphicsBuffer InstanceBuffer,
        int CommandOffset,
        int CommandCount);
}