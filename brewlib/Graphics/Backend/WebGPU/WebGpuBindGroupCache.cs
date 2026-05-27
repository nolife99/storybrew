namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Ahjo.Wgpu;

sealed class WebGpuBindGroupCache : IDisposable
{
    const uint MaxRetainedFrameAge = 3;

    readonly Entry[] entries;

    public WebGpuBindGroupCache(int capacity, int handleCapacity)
    {
        entries = new Entry[capacity];
        for (var i = 0; i < entries.Length; ++i)
            entries[i] = new(handleCapacity);
    }

    public void Dispose()
    {
        foreach (var entry in entries)
            entry.ClearWithoutRelease();
    }

    public bool TryGet(int signature, ReadOnlySpan<nint> handles, uint frameSerial, out BindGroup bindGroup)
    {
        foreach (var entry in entries)
        {
            if (!entry.Matches(signature, handles)) continue;

            entry.Touch(frameSerial);
            bindGroup = entry.BindGroup;
            return true;
        }

        bindGroup = default;
        return false;
    }

    public void Store(WebGpuGraphicsBackend backend,
        int signature,
        ReadOnlySpan<nint> handles,
        BindGroup bindGroup,
        uint frameSerial)
    {
        if (bindGroup.IsNull) return;

        var target = -1;
        var oldestIndex = 0;
        var oldestAge = uint.MinValue;

        for (var i = 0; i < entries.Length; ++i)
        {
            var entry = entries[i];
            if (entry.BindGroup.IsNull)
            {
                target = i;
                break;
            }

            if (entry.Matches(signature, handles))
            {
                entry.Retire(backend);
                target = i;
                break;
            }

            var age = unchecked(frameSerial - entry.LastUsedFrame);
            if (age <= oldestAge) continue;

            oldestAge = age;
            oldestIndex = i;
        }

        if (target < 0)
        {
            target = oldestIndex;
            entries[target].Retire(backend);
        }

        entries[target].Set(signature, handles, bindGroup, frameSerial);
    }

    public void Trim(WebGpuGraphicsBackend backend, uint frameSerial)
    {
        foreach (var entry in entries)
        {
            if (entry.BindGroup.IsNull) continue;

            if (unchecked(frameSerial - entry.LastUsedFrame) > MaxRetainedFrameAge)
                entry.Retire(backend);
        }
    }

    public bool PurgeReferencing(WebGpuGraphicsBackend backend, WebGpuResourceReference resource)
    {
        var purged = false;

        foreach (var entry in entries)
            purged |= entry.PurgeReferencing(backend, resource.Handle);

        return purged;
    }

    public void Clear(WebGpuGraphicsBackend backend)
    {
        foreach (var entry in entries)
            entry.Retire(backend);
    }

    sealed class Entry(int handleCapacity)
    {
        readonly nint[] handles = new nint[handleCapacity];

        public BindGroup BindGroup;
        int handleCount;
        public uint LastUsedFrame;
        int signature;

        public bool Matches(int candidateSignature, ReadOnlySpan<nint> candidateHandles)
        {
            if (BindGroup.IsNull || signature != candidateSignature || handleCount != candidateHandles.Length)
                return false;

            return candidateHandles.SequenceEqual(handles.AsSpan(0, handleCount));
        }

        public void Touch(uint frameSerial)
            => LastUsedFrame = frameSerial;

        public void Set(int newSignature, ReadOnlySpan<nint> newHandles, BindGroup bindGroup, uint frameSerial)
        {
            signature = newSignature;
            handleCount = newHandles.Length;
            newHandles.CopyTo(handles);
            BindGroup = bindGroup;
            LastUsedFrame = frameSerial;
        }

        public bool PurgeReferencing(WebGpuGraphicsBackend backend, nint resourceHandle)
        {
            if (BindGroup.IsNull) return false;

            for (var i = 0; i < handleCount; ++i)
            {
                if (handles[i] != resourceHandle) continue;

                Retire(backend);
                return true;
            }

            return false;
        }

        public void Retire(WebGpuGraphicsBackend backend)
        {
            if (!BindGroup.IsNull)
                backend.DeferredReleases.Retire(BindGroup);

            ClearWithoutRelease();
        }

        public void ClearWithoutRelease()
        {
            BindGroup = default;
            signature = 0;
            handleCount = 0;
            LastUsedFrame = 0;
        }
    }
}