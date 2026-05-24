namespace BrewLib.Graphics.Backend.WebGPU;

unsafe readonly struct WebGpuHandle<T>(T* pointer) where T : unmanaged
{
    public readonly T* Pointer = pointer;
}