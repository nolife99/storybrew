namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Text;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using Shaders;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using AhjoVertexAttribute = Ahjo.Wgpu.VertexAttribute;
using AhjoVertexBufferLayout = Ahjo.Wgpu.VertexBufferLayout;

sealed class WebGpuRenderPipelineFactory : IRenderPipelineFactory
{
    const uint UniformGroupIndex = 0;
    const uint TextureGroupIndex = 1;
    readonly WebGpuBackend backend;
    readonly WebGpuDeviceContext deviceContext;

    public WebGpuRenderPipelineFactory(WebGpuBackend backend, WebGpuDeviceContext deviceContext)
    {
        this.backend = backend;
        this.deviceContext = deviceContext;
    }

    public IRenderPipeline CreateRenderPipeline(RenderPipelineDescription description)
    {
        var shaderSource = description.ShaderSource;
        if (shaderSource.Language != ShaderSourceLanguage.Wgsl)
            throw new NotSupportedException($"The WebGPU backend only accepts WGSL shaders (got {shaderSource.Language})");

        var payloadSize = DetermineUniformPayloadSize(shaderSource);
        var usePushConstants = payloadSize > 0 &&
            deviceContext.CanUseImmediates &&
            payloadSize <= deviceContext.MaxImmediateSize;

        var vertexSource = shaderSource.VertexSource;
        if (usePushConstants)
        {
            var rewrite = WgslPushConstantRewriter.TryRewrite(vertexSource);
            if (rewrite.Rewritten) vertexSource = rewrite.TransformedSource;
            else usePushConstants = false;
        }

        var vertexModule = CompileWgsl(vertexSource);
        var fragmentModule = CompileWgsl(shaderSource.FragmentSource);

        var vertexPlan = BuildVertexPlan(description.VertexInput);

        var textureBindings = description.PipelineLayout.TextureBindings;
        var hasTextureGroup = textureBindings.Length > 0;

        BindGroupLayout textureLayout = default;
        var ownsTextureLayout = false;
        if (hasTextureGroup)
        {
            textureLayout = BuildTextureBindGroupLayout(textureBindings);
            ownsTextureLayout = true;
        }

        BindGroupLayout uniformLayout = default;
        var ownsUniformLayout = false;
        BindGroupLayout emptyLayout = default;
        var ownsEmptyLayout = false;

        var strategy = usePushConstants ? WebGpuUniformState.Strategy.PushConstants :
            payloadSize > 0 ? WebGpuUniformState.Strategy.DynamicOffsetBuffer : WebGpuUniformState.Strategy.None;

        var pushConstantBytes = 0u;

        using var layoutList = TempList.Create<BindGroupLayout>();
        switch (strategy)
        {
            case WebGpuUniformState.Strategy.PushConstants:
                pushConstantBytes = payloadSize;
                if (hasTextureGroup)
                {
                    emptyLayout = deviceContext.Device.CreateBindGroupLayout(ReadOnlySpan<BindGroupLayoutEntry>.Empty);
                    ownsEmptyLayout = true;
                    layoutList.Add(emptyLayout);
                    layoutList.Add(textureLayout);
                }

                break;

            case WebGpuUniformState.Strategy.DynamicOffsetBuffer:
                uniformLayout = BuildUniformBindGroupLayout(payloadSize);
                ownsUniformLayout = true;
                layoutList.Add(uniformLayout);
                if (hasTextureGroup) layoutList.Add(textureLayout);

                break;

            case WebGpuUniformState.Strategy.None:
                if (hasTextureGroup)
                {
                    emptyLayout = deviceContext.Device.CreateBindGroupLayout(ReadOnlySpan<BindGroupLayoutEntry>.Empty);
                    ownsEmptyLayout = true;
                    layoutList.Add(emptyLayout);
                    layoutList.Add(textureLayout);
                }

                break;
        }

        if (pushConstantBytes != 0 && !deviceContext.CanUseImmediates)
            throw new InvalidOperationException($"Immediate uniform payload {pushConstantBytes} selected, but the created device exposes no immediate-data capacity.");

        if (pushConstantBytes > deviceContext.MaxImmediateSize)
            throw new InvalidOperationException($"Immediate uniform payload {pushConstantBytes} exceeds created-device immediate data limit {deviceContext.MaxImmediateSize}.");

        var wgpuPipelineLayout = deviceContext.Device.CreatePipelineLayout(layoutList.AsReadOnlySpan(), pushConstantBytes);

        var uniformState = new WebGpuUniformState(deviceContext, strategy, payloadSize);

        var pipeline = new WebGpuRenderPipeline(backend,
            deviceContext,
            description,
            vertexModule,
            fragmentModule,
            shaderSource.VertexEntryPoint,
            shaderSource.FragmentEntryPoint,
            wgpuPipelineLayout,
            textureLayout,
            hasTextureGroup,
            ownsTextureLayout,
            uniformLayout,
            ownsUniformLayout,
            emptyLayout,
            ownsEmptyLayout,
            TextureGroupIndex,
            UniformGroupIndex,
            strategy == WebGpuUniformState.Strategy.DynamicOffsetBuffer,
            vertexPlan,
            uniformState);

        backend.RegisterPipeline(pipeline);
        return pipeline;
    }

    static uint DetermineUniformPayloadSize(ShaderProgramSource source)
    {
        var rewrite = WgslPushConstantRewriter.TryRewrite(source.VertexSource);
        if (!rewrite.Rewritten)
            return 0;

        var structSizes = WgslPushConstantRewriter.ScanStructSizes(source.VertexSource);
        var size = WgslPushConstantRewriter.EstimateTypeSize(rewrite.UniformTypeName, structSizes);
        return size == 0 ? 64u : size;
    }

    ShaderModule CompileWgsl(string source)
    {
        var utf8 = Encoding.UTF8.GetBytes(source);
        var descriptor = new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(utf8)
        };

        return deviceContext.Device.CreateShaderModule(in descriptor);
    }

    BindGroupLayout BuildTextureBindGroupLayout(scoped ReadOnlySpan<TextureBindingLayout> bindings)
    {
        var totalSlots = 0;
        foreach (var binding in bindings)
            totalSlots += Math.Max(1, binding.Capacity);

        using var entries = TempArray.Create<BindGroupLayoutEntry>(totalSlots * 2);
        for (var i = 0; i < totalSlots; ++i)
        {
            entries[i * 2] = BindGroupLayoutEntry.Texture((uint)(i * 2), ShaderStage.Fragment);
            entries[i * 2 + 1] = BindGroupLayoutEntry.Sampler((uint)(i * 2 + 1), ShaderStage.Fragment);
        }

        return deviceContext.Device.CreateBindGroupLayout(entries.AsReadOnlySpan());
    }

    BindGroupLayout BuildUniformBindGroupLayout(uint payloadSize)
        => deviceContext.Device.CreateBindGroupLayout([
            BindGroupLayoutEntry.Buffer(0,
                ShaderStage.Vertex | ShaderStage.Fragment,
                WGPUBufferBindingType.Uniform,
                true,
                payloadSize)
        ]);

    WgpuVertexLayoutPlan BuildVertexPlan(VertexInputLayout vertexInput)
    {
        var buffers = vertexInput.Buffers;
        using var attributeList = TempList.Create<AhjoVertexAttribute>();
        var bufferLayouts = new AhjoVertexBufferLayout[buffers.Length];

        uint location = 0;
        for (var b = 0; b < buffers.Length; ++b)
        {
            var buffer = buffers[b];
            var attrCountForBuffer = 0;

            foreach (var element in buffer.Elements)
            {
                var locationCount = element.Format.GetLocationCount();
                if (element.Format == VertexAttributeFormat.Float32Mat3x2)
                {
                    for (var loc = 0; loc < 3; ++loc)
                    {
                        attributeList.Add(new(
                            WGPUVertexFormat.Float32x2,
                            (ulong)(element.Offset + loc * 8),
                            location++));

                        ++attrCountForBuffer;
                    }
                }
                else
                {
                    attributeList.Add(new(
                        WgpuMapper.ToWgpu(element.Format),
                        (ulong)element.Offset,
                        location++));

                    ++attrCountForBuffer;
                    if (locationCount != 1)
                        throw new NotSupportedException($"Vertex format {element.Format} reports {locationCount} locations but only Float32Mat3x2 multi-location is handled");
                }
            }

            var stepMode = buffer.InputRate == VertexInputRate.Instance
                ? WGPUVertexStepMode.Instance
                : WGPUVertexStepMode.Vertex;

            bufferLayouts[b] = new((ulong)buffer.Stride, attrCountForBuffer, stepMode);
        }

        return new(bufferLayouts, attributeList.ToArray());
    }
}