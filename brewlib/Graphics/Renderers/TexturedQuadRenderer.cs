namespace BrewLib.Graphics.Renderers;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Backend;
using Cameras;
using Shaders;
using SixLabors.ImageSharp.PixelFormats;
using Textures;
using Util;

public sealed class TexturedQuadRenderer : IQuadRenderer
{
    const int VertexPerQuad = 6;

    static readonly ShaderUniformBinding<Matrix4x4> CombinedMatrixUniform = new("u_combinedMatrix",
        ShaderValueType.FloatMat4);

    static readonly ShaderSamplerBinding TexturesSampler = new("u_textures");

    static readonly ShaderAttributeBinding VertexAttribute = new("a_vertex", ShaderValueType.FloatVec2);
    static readonly ShaderAttributeBinding TransformAttribute = new("a_transform", ShaderValueType.FloatMat3x2);
    static readonly ShaderAttributeBinding UvAttribute = new("a_uv", ShaderValueType.FloatVec4);
    static readonly ShaderAttributeBinding ColorAttribute = new("a_color", ShaderValueType.FloatVec4);
    static readonly ShaderAttributeBinding TextureSlotAttribute = new("a_textureSlot", ShaderValueType.Float);

    static readonly Vector2[] UnitQuadVertices =
    [
        new(0, 0),
        new(0, 1),
        new(1, 1),
        new(0, 0),
        new(1, 1),
        new(1, 0)
    ];

    readonly bool bufferTransientDraws;
    readonly IRenderUniform<Matrix4x4> combinedMatrixUniform;
    readonly IGraphicsBuffer indirectBuffer;
    readonly ITransientGraphicsBuffer instanceBuffer;
    readonly int instanceStride, instanceBatchCapacity;
    readonly List<TexturedQuadBatch> pendingBatches;
    readonly IRenderPipeline pipeline;
    readonly IResourceSet resources;
    readonly TextureSlotter textureSlotter;
    readonly bool useIndirectSplitDraws;
    readonly IGraphicsBuffer vertexBuffer;

    ICamera camera;
    bool disposed, rendering;
    IndirectDrawCommand[] indirectCommands;
    TransientBufferAllocation instanceAllocation;
    int instanceCount, primaryInstanceCapacity;
    nint instanceData, instanceDataSecondary;
    bool textureBatchHasSamplerIdentity;
    GraphicsResourceHandle textureBatchSamplerIdentity;
    Matrix4x4 transformMatrix = Matrix4x4.Identity;

    public TexturedQuadRenderer(int initialBatchCapacity)
        : this(null, initialBatchCapacity) { }

    public TexturedQuadRenderer(IGraphicsBackend backend = null, int initialBatchCapacity = 8192)
    {
        if (initialBatchCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(initialBatchCapacity), initialBatchCapacity, null);

        backend ??= DrawState.Backend ??
            throw new InvalidOperationException("A graphics backend must be initialized before creating renderers");

        instanceStride = Unsafe.SizeOf<TexturedQuadInstance>();
        instanceBatchCapacity = int.Max(1, initialBatchCapacity);
        bufferTransientDraws = backend.PrefersBufferedTransientDraws;
        useIndirectSplitDraws = bufferTransientDraws && backend.Capabilities.Has(GraphicsBackendFeatures.IndirectDraws);
        pendingBatches = bufferTransientDraws ? [] : null;
        textureSlotter = TextureSlotter.ForFragmentShader();

        pipeline = backend.RenderPipelines.CreateRenderPipeline(CreatePipelineDescription(backend.ShaderSourceLanguage,
            textureSlotter.Capacity,
            backend.Capabilities.Has(GraphicsBackendFeatures.NativeNonUniformTextureIndexing),
            backend.Capabilities.Has(GraphicsBackendFeatures.ManualColorCorrection)));

        resources = pipeline.CreateResourceSet();
        combinedMatrixUniform = pipeline.GetUniform(CombinedMatrixUniform);

        vertexBuffer = backend.Buffers.CreateBuffer(new(nameof(TexturedQuadRenderer) + ".Vertices",
            GraphicsBufferTarget.Vertex,
            GraphicsBufferUsage.Static));

        instanceBuffer = backend.TransientBuffers.CreateBuffer(new(nameof(TexturedQuadRenderer) + ".Instances",
                GraphicsBufferTarget.Vertex,
                GraphicsBufferUsage.Stream),
            getRingCapacity(instanceStride * instanceBatchCapacity));

        if (useIndirectSplitDraws)
        {
            indirectCommands = new IndirectDrawCommand[2];
            indirectBuffer = backend.Buffers.CreateBuffer(new(nameof(TexturedQuadRenderer) + ".Indirect",
                GraphicsBufferTarget.Indirect,
                GraphicsBufferUsage.Stream));
        }

        vertexBuffer.SetData(UnitQuadVertices);
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

    public PrimitiveTopology Topology => PrimitiveTopology.Triangles;

    public PrimitiveBatchFeatures BatchFeatures
        => PrimitiveBatchFeatures.PainterOrdered |
            PrimitiveBatchFeatures.Batched |
            PrimitiveBatchFeatures.Instanced |
            PrimitiveBatchFeatures.Textured |
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
        textureSlotter.Clear();
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

    void IQuadRenderer.Draw(scoped ref readonly QuadInstance source, ITextureRegion texture)
    {
        if (instanceCount == instanceBatchCapacity)
            DrawState.FlushRenderer(true);

        var textureResource = texture.Texture;
        if (shouldSplitBatchForSampler(textureResource))
            DrawState.FlushRenderer(true);

        trackSamplerState(textureResource);

        if (!textureSlotter.TryGetSlot(textureResource, out var textureSlot))
        {
            DrawState.FlushRenderer(true);
            trackSamplerState(textureResource);
            if (!textureSlotter.TryGetSlot(textureResource, out textureSlot))
                throw new InvalidOperationException("Unable to allocate a texture slot for the quad batch");
        }

        ensureInstanceBatch();

        TexturedQuadInstance instance = new()
        {
            Transform = source.Transform,
            U = source.U,
            V = source.V,
            UAxis = source.UAxis,
            VAxis = source.VAxis,
            Color = source.Color,
            TextureSlot = textureSlot
        };

        if (instanceCount < primaryInstanceCapacity)
            Unsafe.Add(ref instanceData.AsRef<TexturedQuadInstance>(), instanceCount) = instance;
        else
            Unsafe.Add(ref instanceDataSecondary.AsRef<TexturedQuadInstance>(), instanceCount - primaryInstanceCapacity) = instance;

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

        var textureCount = textureSlotter.Count;
        var textures = ArrayPool<ITexture>.Shared.Rent(textureCount);
        textureSlotter.Textures.CopyTo(textures);

        pendingBatches.Add(new(instanceAllocation,
            instanceCount,
            usedBytes,
            transformMatrix * camera.ProjectionView,
            textures,
            textureCount));

        instanceBuffer.MarkSubmitted(in instanceAllocation, usedBytes);
        resetInstanceBatch();
        clearTextureBatchState();
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
            in combinedMatrix,
            textureSlotter.Textures);

        resetInstanceBatch();
        clearTextureBatchState();
    }

    bool shouldSplitBatchForSampler(ITexture texture)
        => textureBatchHasSamplerIdentity &&
            texture is ITextureSamplerIdentity samplerIdentity &&
            samplerIdentity.SamplerIdentity != textureBatchSamplerIdentity;

    void trackSamplerState(ITexture texture)
    {
        if (textureBatchHasSamplerIdentity || texture is not ITextureSamplerIdentity samplerIdentity)
            return;

        textureBatchSamplerIdentity = samplerIdentity.SamplerIdentity;
        textureBatchHasSamplerIdentity = true;
    }

    void clearTextureBatchState()
    {
        textureSlotter.Clear();
        textureBatchSamplerIdentity = default;
        textureBatchHasSamplerIdentity = false;
    }

    void replayPendingBatches()
    {
        if (pendingBatches is null || pendingBatches.Count == 0) return;

        if (useIndirectSplitDraws && tryReplayMerged())
        {
            foreach (var batch in pendingBatches)
            {
                Array.Clear(batch.Textures, 0, batch.TextureCount);
                ArrayPool<ITexture>.Shared.Return(batch.Textures);
            }

            pendingBatches.Clear();
            return;
        }

        foreach (var batch in pendingBatches)
        {
            try
            {
                var allocation = batch.Allocation;
                var combinedMatrix = batch.CombinedMatrix;
                drawBatch(in allocation,
                    batch.InstanceCount,
                    batch.UsedBytes,
                    in combinedMatrix,
                    batch.Textures.AsSpan(0, batch.TextureCount),
                    false);
            }
            finally
            {
                Array.Clear(batch.Textures, 0, batch.TextureCount);
                ArrayPool<ITexture>.Shared.Return(batch.Textures);
            }
        }

        pendingBatches.Clear();
    }

    bool tryReplayMerged()
    {
        var totalCommands = 0;
        for (var i = 0; i < pendingBatches.Count; ++i)
        {
            var batch = pendingBatches[i];
            var allocation = batch.Allocation;
            var primaryCount = getPrimaryInstanceCount(in allocation, batch.InstanceCount);
            ensureIndirectCommandCapacity(totalCommands + 2);
            totalCommands += writeIndirectSplitCommands(in allocation,
                batch.InstanceCount,
                primaryCount,
                indirectCommands.AsSpan(totalCommands));
        }

        if (totalCommands == 0) return true;

        indirectBuffer.SetData(indirectCommands.AsSpan(0, totalCommands));

        var commandStride = Unsafe.SizeOf<IndirectDrawCommand>();
        var groupCommandStart = 0;
        var groupCommandCount = 0;
        var groupStartBatch = 0;

        for (var i = 0; i < pendingBatches.Count; ++i)
        {
            var batch = pendingBatches[i];
            var allocation = batch.Allocation;
            var primaryCount = getPrimaryInstanceCount(in allocation, batch.InstanceCount);
            var batchCommandCount = (primaryCount > 0 ? 1 : 0) +
                (batch.InstanceCount > primaryCount ? 1 : 0);

            if (i > groupStartBatch && !canMergeBatches(pendingBatches[groupStartBatch], batch))
            {
                var start = pendingBatches[groupStartBatch];
                var startMatrix = start.CombinedMatrix;
                emitMergedDrawGroup(in startMatrix,
                    start.Textures.AsSpan(0, start.TextureCount),
                    start.Allocation.Buffer,
                    groupCommandStart,
                    groupCommandCount,
                    commandStride);

                groupCommandStart += groupCommandCount;
                groupCommandCount = 0;
                groupStartBatch = i;
            }

            groupCommandCount += batchCommandCount;
        }

        if (groupCommandCount > 0)
        {
            var start = pendingBatches[groupStartBatch];
            var startMatrix = start.CombinedMatrix;
            emitMergedDrawGroup(in startMatrix,
                start.Textures.AsSpan(0, start.TextureCount),
                start.Allocation.Buffer,
                groupCommandStart,
                groupCommandCount,
                commandStride);
        }

        return true;
    }

    static bool canMergeBatches(TexturedQuadBatch a, TexturedQuadBatch b)
    {
        // Batches must share the underlying instance buffer; otherwise the merged
        // indirect draw would read instances from the wrong buffer (the transient
        // ring spans multiple pages once exhausted).
        if (!ReferenceEquals(a.Allocation.Buffer, b.Allocation.Buffer)) return false;
        if (a.CombinedMatrix != b.CombinedMatrix) return false;
        if (a.TextureCount != b.TextureCount) return false;

        for (var i = 0; i < a.TextureCount; ++i)
            if (!ReferenceEquals(a.Textures[i], b.Textures[i]))
                return false;

        return true;
    }

    void emitMergedDrawGroup(scoped ref readonly Matrix4x4 matrix,
        scoped ReadOnlySpan<ITexture> textures,
        IGraphicsBuffer instanceBufferToBind,
        int firstCommand,
        int commandCount,
        int commandStride)
    {
        if (commandCount == 0) return;

        combinedMatrixUniform.SetValue(matrix);
        resources.SetTextures(0, textures);
        resources.Bind();
        pipeline.BindVertexBuffer(1, instanceBufferToBind, 0);
        pipeline.DrawIndirect(new(indirectBuffer, firstCommand * commandStride, commandCount));
    }

    void drawBatch(scoped ref readonly TransientBufferAllocation allocation,
        int count,
        int usedBytes,
        scoped ref readonly Matrix4x4 combinedMatrix,
        scoped ReadOnlySpan<ITexture> textures,
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

            resources.SetTextures(0, textures);
            resources.Bind();

            pipeline.BindVertexBuffer(1, allocation.Buffer, 0);
            pipeline.DrawIndirect(indirectCommand);

            if (markSubmitted) instanceBuffer.MarkSubmitted(in allocation, usedBytes);
            return;
        }

        combinedMatrixUniform.SetValue(combinedMatrix);

        resources.SetTextures(0, textures);
        resources.Bind();

        pipeline.BindVertexBuffer(1, allocation.Buffer, allocation.Offset);
        pipeline.DrawInstanced(new(VertexPerQuad, primaryCount));

        if (allocation.IsSplit && count > primaryCount)
        {
            pipeline.BindVertexBuffer(1, allocation.Buffer, 0);
            pipeline.DrawInstanced(new(VertexPerQuad, count - primaryCount));
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
            commands[commandCount++] = new(VertexPerQuad,
                (uint)primaryCount,
                0,
                checked((uint)(allocation.Offset / instanceStride)));

        var secondaryCount = count - primaryCount;
        if (secondaryCount > 0)
            commands[commandCount++] = new(VertexPerQuad, (uint)secondaryCount);

        return commandCount;
    }

    int getPrimaryInstanceCount(scoped ref readonly TransientBufferAllocation allocation, int count)
        => allocation.IsSplit ? int.Min(count, allocation.PrimarySize / instanceStride) : count;

    bool canUseIndirectSplitDraw(scoped ref readonly TransientBufferAllocation allocation,
        int count,
        int primaryCount)
        => useIndirectSplitDraws && allocation.IsSplit && count > primaryCount;

    void ensureIndirectCommandCapacity(int commandCount)
    {
        if (indirectCommands.Length >= commandCount) return;

        var capacity = indirectCommands.Length;
        while (capacity < commandCount)
            capacity = checked(capacity * 2);

        Array.Resize(ref indirectCommands, capacity);
    }

    static RenderPipelineDescription CreatePipelineDescription(ShaderSourceLanguage language,
        int textureSlots,
        bool useNonUniformTextureIndexing,
        bool useManualColorCorrection)
    {
        var instanceStride = Unsafe.SizeOf<TexturedQuadInstance>();

        return new(nameof(TexturedQuadRenderer),
            createShaderSource(language, textureSlots, useNonUniformTextureIndexing, useManualColorCorrection),
            new(new TextureBindingLayout(0, TexturesSampler, textureSlots)),
            new(
                new VertexBufferLayout(0,
                    Unsafe.SizeOf<Vector2>(),
                    VertexInputRate.Vertex,
                    new VertexElement(VertexAttribute, VertexAttributeFormat.Float32x2, 0)),
                new VertexBufferLayout(1,
                    instanceStride,
                    VertexInputRate.Instance,
                    new VertexElement(TransformAttribute, VertexAttributeFormat.Float32Mat3x2, 0),
                    new VertexElement(UvAttribute, VertexAttributeFormat.Float16x4, OffsetOf(nameof(TexturedQuadInstance.U))),
                    new VertexElement(ColorAttribute, VertexAttributeFormat.Unorm8x4, OffsetOf(nameof(TexturedQuadInstance.Color))),
                    new VertexElement(TextureSlotAttribute,
                        VertexAttributeFormat.Float32,
                        OffsetOf(nameof(TexturedQuadInstance.TextureSlot))))),
            PrimitiveTopology.Triangles);
    }

    static ShaderProgramSource createShaderSource(ShaderSourceLanguage language,
        int textureSlots,
        bool useNonUniformTextureIndexing,
        bool useManualColorCorrection)
        => language switch
        {
            ShaderSourceLanguage.Hlsl => new(nameof(TexturedQuadRenderer),
                createHlslVertexShader(),
                createHlslFragmentShader(textureSlots, useNonUniformTextureIndexing, useManualColorCorrection)),
            ShaderSourceLanguage.Wgsl => new(nameof(TexturedQuadRenderer),
                createWgslVertexShader(),
                createWgslFragmentShader(textureSlots, useNonUniformTextureIndexing),
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
               [[vk::location(0)]] float2 Vertex : TEXCOORD0;
               [[vk::location(1)]] float3x2 Transform : TEXCOORD1;
               [[vk::location(4)]] float4 Uv : TEXCOORD4;
               [[vk::location(5)]] float4 Color : TEXCOORD5;
               [[vk::location(6)]] float TextureSlot : TEXCOORD6;
           };

           struct VertexOutput
           {
               float4 Position : SV_Position;
               float2 TextureCoord : TEXCOORD0;
               float4 Color : COLOR0;
               nointerpolation int TextureSlot : TEXCOORD1;
           };

           VertexOutput main(VertexInput input)
           {
               VertexOutput output;
               float2 position = mul(float3(input.Vertex, 1), input.Transform);
               output.Position = mul(float4(position, 0, 1), u_combinedMatrix);
               output.TextureCoord = input.Uv.xy + input.Vertex * input.Uv.zw;
               output.Color = input.Color;
               output.TextureSlot = (int)(input.TextureSlot + 0.5);
               return output;
           }
           """;

    static string createHlslFragmentShader(int textureSlots,
        bool useNonUniformTextureIndexing,
        bool useManualColorCorrection)
    {
        StringBuilder shader = new();
        shader.AppendLine(CultureInfo.InvariantCulture,
            $"Texture2D<float4> {TexturesSampler.Name}[{textureSlots}] : register(t0, space2);");

        shader.AppendLine(CultureInfo.InvariantCulture, $"SamplerState u_samplers[{textureSlots}] : register(s0, space2);");
        shader.AppendLine();
        shader.AppendLine("struct FragmentInput");
        shader.AppendLine("{");
        shader.AppendLine("    float4 Position : SV_Position;");
        shader.AppendLine("    float2 TextureCoord : TEXCOORD0;");
        shader.AppendLine("    float4 Color : COLOR0;");
        shader.AppendLine("    nointerpolation int TextureSlot : TEXCOORD1;");
        shader.AppendLine("};");
        shader.AppendLine();
        if (useManualColorCorrection)
        {
            shader.AppendLine("float4 apply_output_color(float4 color)");
            shader.AppendLine("{");
            shader.AppendLine("    color.rgb = pow(saturate(color.rgb), float3(2.2, 2.2, 2.2));");
            shader.AppendLine("    return color;");
            shader.AppendLine("}");
            shader.AppendLine();
        }

        shader.AppendLine("float4 main(FragmentInput input) : SV_Target0");
        shader.AppendLine("{");
        if (useNonUniformTextureIndexing)
        {
            shader.AppendLine("    uint textureSlot = (uint)input.TextureSlot;");
            shader.AppendLine(CultureInfo.InvariantCulture,
                $"    float4 texel = {TexturesSampler.Name}[NonUniformResourceIndex(textureSlot)].Sample(u_samplers[NonUniformResourceIndex(textureSlot)], input.TextureCoord);");

            shader.AppendLine(useManualColorCorrection
                ? "    return apply_output_color(input.Color * texel);"
                : "    return input.Color * texel;");

            shader.AppendLine("}");
            return shader.ToString();
        }

        shader.AppendLine("    float4 texel = float4(0, 0, 0, 0);");

        shader.AppendLine(CultureInfo.InvariantCulture,
            $"    if (input.TextureSlot == 0) texel = {TexturesSampler.Name}[0].Sample(u_samplers[0], input.TextureCoord);");

        for (var i = 1; i < textureSlots; ++i)
            shader.AppendLine(CultureInfo.InvariantCulture,
                $"    else if (input.TextureSlot == {i}) texel = {TexturesSampler.Name}[{i}].Sample(u_samplers[{i}], input.TextureCoord);");

        shader.AppendLine(useManualColorCorrection
            ? "    return apply_output_color(input.Color * texel);"
            : "    return input.Color * texel;");

        shader.AppendLine("}");
        return shader.ToString();
    }

    static string createWgslVertexShader()
        => """
           struct TransformUniforms {
               rows: array<vec4<f32>, 4>,
           };

           @group(0) @binding(0) var<uniform> u_transform: TransformUniforms;

           struct VertexInput {
               @location(0) vertex: vec2<f32>,
               @location(1) transform0: vec2<f32>,
               @location(2) transform1: vec2<f32>,
               @location(3) transform2: vec2<f32>,
               @location(4) uv: vec4<f32>,
               @location(5) color: vec4<f32>,
               @location(6) texture_slot: f32,
           };

           struct VertexOutput {
               @builtin(position) position: vec4<f32>,
               @location(0) texture_coord: vec2<f32>,
               @location(1) color: vec4<f32>,
               @location(2) @interpolate(flat) texture_slot: u32,
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
               let position = input.transform0 * input.vertex.x +
                   input.transform1 * input.vertex.y +
                   input.transform2;
               output.position = transform_position(vec4<f32>(position, 0.0, 1.0));
               output.texture_coord = input.uv.xy + input.vertex * input.uv.zw;
               output.color = input.color;
               output.texture_slot = u32(input.texture_slot + 0.5);
               return output;
           }
           """;

    static string createWgslFragmentShader(int textureSlots, bool useNonUniformTextureIndexing)
    {
        if (useNonUniformTextureIndexing)
            return $$"""
                     @group(1) @binding(0) var u_textures: binding_array<texture_2d<f32>, {{textureSlots}}>;
                     @group(1) @binding(1) var u_sampler: sampler;

                     struct FragmentInput {
                         @location(0) texture_coord: vec2<f32>,
                         @location(1) color: vec4<f32>,
                         @location(2) @interpolate(flat) texture_slot: u32,
                     };

                     @fragment
                     fn main(input: FragmentInput) -> @location(0) vec4<f32> {
                         let texture_slot = input.texture_slot;
                         let texel = textureSample(u_textures[texture_slot], u_sampler, input.texture_coord);
                         return input.color * texel;
                     }
                     """;

        StringBuilder shader = new();
        for (var i = 0; i < textureSlots; ++i)
        {
            shader.AppendLine(CultureInfo.InvariantCulture,
                $"@group(1) @binding({i * 2}) var u_texture{i}: texture_2d<f32>;");

            shader.AppendLine(CultureInfo.InvariantCulture,
                $"@group(1) @binding({i * 2 + 1}) var u_sampler{i}: sampler;");
        }

        shader.AppendLine();
        shader.AppendLine("""
                          struct FragmentInput {
                              @location(0) texture_coord: vec2<f32>,
                              @location(1) color: vec4<f32>,
                              @location(2) @interpolate(flat) texture_slot: u32,
                          };

                          fn sample_texture(texture_slot: u32, texture_coord: vec2<f32>) -> vec4<f32> {
                          """);

        for (var i = 0; i < textureSlots; ++i)
            shader.AppendLine(CultureInfo.InvariantCulture,
                $"    if (texture_slot == {i}u) {{ return textureSample(u_texture{i}, u_sampler{i}, texture_coord); }}");

        shader.AppendLine("""
                              return vec4<f32>(0.0, 0.0, 0.0, 0.0);
                          }

                          @fragment
                          fn main(input: FragmentInput) -> @location(0) vec4<f32> {
                              let texel = sample_texture(input.texture_slot, input.texture_coord);
                              return input.color * texel;
                          }
                          """);

        return shader.ToString();
    }

    ~TexturedQuadRenderer() => Dispose(false);

    void Dispose(bool disposing)
    {
        if (disposed) return;

        if (rendering) ((IRenderer)this).EndRendering();

        resources.Dispose();
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
        => (int)Marshal.OffsetOf<TexturedQuadInstance>(fieldName);

    static int getRingCapacity(int batchSizeInBytes)
        => int.Max(checked(batchSizeInBytes * 8), 1024 * 1024);

    [StructLayout(LayoutKind.Sequential)]
    struct TexturedQuadInstance
    {
        public Matrix3x2 Transform;
        public Half U, V, UAxis, VAxis;
        public Rgba32 Color;
        public float TextureSlot;
    }

    readonly record struct TexturedQuadBatch(
        TransientBufferAllocation Allocation,
        int InstanceCount,
        int UsedBytes,
        Matrix4x4 CombinedMatrix,
        ITexture[] Textures,
        int TextureCount);
}