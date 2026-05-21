namespace BrewLib.Graphics.Backend.SDL;

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Shaders;

internal static class DxbcShaderCompiler
{
    const uint D3DCompileDebug = 1 << 0;
    const uint D3DCompileSkipOptimization = 1 << 2;
    const uint D3DCompileOptimizationLevel3 = 1 << 15;

    static readonly Regex VulkanAttributePattern = new(@"\[\[\s*vk::[^\]]+\]\]\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static byte[] CompileFromHlsl(string name,
        CompiledShaderStage stage,
        string source,
        string entryPoint,
        bool enableDebug)
    {
        var d3dSource = VulkanAttributePattern.Replace(source, string.Empty);
        var sourceBytes = Encoding.UTF8.GetBytes(d3dSource);
        var flags = enableDebug
            ? D3DCompileDebug | D3DCompileSkipOptimization
            : D3DCompileOptimizationLevel3;

        nint bytecode = 0, errors = 0;
        try
        {
            try
            {
                var result = D3DCompile(sourceBytes,
                    (nuint)sourceBytes.Length,
                    name,
                    0,
                    0,
                    entryPoint,
                    toProfile(stage),
                    flags,
                    0,
                    out bytecode,
                    out errors);

                if (result < 0)
                {
                    var error = blobToString(errors);
                    throw new InvalidOperationException(
                        $"Unable to compile HLSL shader {name} to DXBC: 0x{result:X8}\n{error}");
                }

                if (bytecode == 0)
                    throw new InvalidOperationException($"Unable to compile HLSL shader {name} to DXBC: compiler returned no bytecode");

                return blobToArray(bytecode);
            }
            catch (DllNotFoundException ex)
            {
                throw new InvalidOperationException(
                    $"Unable to compile HLSL shader {name} to DXBC because d3dcompiler_47.dll is unavailable",
                    ex);
            }
            catch (EntryPointNotFoundException ex)
            {
                throw new InvalidOperationException(
                    $"Unable to compile HLSL shader {name} to DXBC because D3DCompile is unavailable",
                    ex);
            }
        }
        finally
        {
            if (errors != 0) Marshal.Release(errors);
            if (bytecode != 0) Marshal.Release(bytecode);
        }
    }

    static string toProfile(CompiledShaderStage stage)
        => stage switch
        {
            CompiledShaderStage.Vertex => "vs_5_1",
            CompiledShaderStage.Fragment => "ps_5_1",
            CompiledShaderStage.Compute => "cs_5_1",
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };

    static byte[] blobToArray(nint blob)
    {
        var pointer = getBlobBufferPointer(blob);
        var size = checked((int)getBlobBufferSize(blob));
        var result = new byte[size];
        Marshal.Copy(pointer, result, 0, size);
        return result;
    }

    static string blobToString(nint blob)
    {
        if (blob == 0) return string.Empty;

        var pointer = getBlobBufferPointer(blob);
        var size = checked((int)getBlobBufferSize(blob));
        return Marshal.PtrToStringAnsi(pointer, size)?.TrimEnd('\0') ?? string.Empty;
    }

    static nint getBlobBufferPointer(nint blob)
        => Marshal.GetDelegateForFunctionPointer<GetBufferPointerDelegate>(
            Marshal.ReadIntPtr(Marshal.ReadIntPtr(blob), 3 * nint.Size))(blob);

    static nuint getBlobBufferSize(nint blob)
        => Marshal.GetDelegateForFunctionPointer<GetBufferSizeDelegate>(
            Marshal.ReadIntPtr(Marshal.ReadIntPtr(blob), 4 * nint.Size))(blob);

    [DllImport("d3dcompiler_47.dll",
        EntryPoint = "D3DCompile",
        CallingConvention = CallingConvention.Winapi,
        CharSet = CharSet.Ansi,
        ExactSpelling = true,
        BestFitMapping = false,
        ThrowOnUnmappableChar = true)]
    static extern int D3DCompile(
        byte[] sourceData,
        nuint sourceDataSize,
        [MarshalAs(UnmanagedType.LPStr)] string sourceName,
        nint defines,
        nint include,
        [MarshalAs(UnmanagedType.LPStr)] string entryPoint,
        [MarshalAs(UnmanagedType.LPStr)] string target,
        uint flags1,
        uint flags2,
        out nint bytecode,
        out nint errors);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate nint GetBufferPointerDelegate(nint self);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate nuint GetBufferSizeDelegate(nint self);
}
