namespace BrewLib.Graphics.Textures;

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using IO;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Util;

public enum TextureUploadFormat
{
    Rgba8
}

public readonly record struct TextureUploadDescription(
    string Source,
    int Width,
    int Height,
    TextureUploadFormat Format,
    TextureOptions Options,
    int BytesPerRow,
    int ByteLength);

public abstract class PreparedTextureUpload(TextureUploadDescription description) : IDisposable, ITextureExtent
{
    public TextureUploadDescription Description { get; } = description;
    public string Source => Description.Source;
    public TextureUploadFormat Format => Description.Format;
    public TextureOptions Options => Description.Options;
    public int BytesPerRow => Description.BytesPerRow;
    public int ByteLength => Description.ByteLength;

    internal abstract Span<byte> WritableBytes { get; }

    public abstract void Dispose();
    public Size Size => new(Width, Height);
    public int Width => Description.Width;
    public int Height => Description.Height;
}

public interface IAsyncTextureUploader : IDisposable
{
    ValueTask<PreparedTextureUpload> PrepareAsync(string filename,
        ResourceContainer resourceContainer = null,
        TextureOptions textureOptions = null,
        CancellationToken cancellationToken = default);

    ITextureRegion Upload(PreparedTextureUpload upload);
}

public abstract class AsyncTextureUploaderBase(Func<int> maxTextureSizeProvider) : IAsyncTextureUploader
{
    public async ValueTask<PreparedTextureUpload> PrepareAsync(string filename,
        ResourceContainer resourceContainer = null,
        TextureOptions textureOptions = null,
        CancellationToken cancellationToken = default)
    {
        textureOptions ??= TextureLoader.LoadTextureOptions(filename, resourceContainer);

        using var bitmap = await TextureLoader.LoadBitmapAsync(filename, resourceContainer, cancellationToken)
            .ConfigureAwait(false);

        if (bitmap is null) return null;

        cancellationToken.ThrowIfCancellationRequested();
        return await PrepareAsync(filename, bitmap, textureOptions ?? TextureOptions.Default, cancellationToken)
            .ConfigureAwait(false);
    }

    public abstract ITextureRegion Upload(PreparedTextureUpload upload);

    public virtual void Dispose() { }

    protected abstract ValueTask<PreparedTextureUpload> PrepareAsync(string source,
        Image<Rgba32> bitmap,
        TextureOptions textureOptions,
        CancellationToken cancellationToken);

    protected TextureUploadDescription CreateDescription(string source,
        Image<Rgba32> bitmap,
        TextureOptions textureOptions)
    {
        var maxTextureSize = maxTextureSizeProvider();
        if (maxTextureSize <= 0) maxTextureSize = int.MaxValue;

        var width = int.Min(maxTextureSize, bitmap.Width);
        var height = int.Min(maxTextureSize, bitmap.Height);
        var bytesPerRow = checked(width * 4);
        return new(source,
            width,
            height,
            TextureUploadFormat.Rgba8,
            textureOptions,
            bytesPerRow,
            checked(bytesPerRow * height));
    }

    protected static void CopyBitmapRows(Image<Rgba32> bitmap,
        PreparedTextureUpload upload)
    {
        if (upload.Format != TextureUploadFormat.Rgba8)
            throw new NotSupportedException($"Unsupported bitmap upload format: {upload.Format}");

        var target = upload.WritableBytes;
        var source = bitmap.Frames.RootFrame.PixelBuffer;
        var rowBytes = checked(upload.Width * 4);
        for (var row = 0; row < upload.Height; ++row)
        {
            var sourceRow = MemoryMarshal.AsBytes(source.DangerousGetRowSpan(row)[..upload.Width]);
            sourceRow.CopyTo(target.Slice(row * upload.BytesPerRow, rowBytes));
        }
    }

    protected static async ValueTask RunOnMainThread(Action<object> action,
        object state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (SDL.IsMainThread())
            action(state);
        else if (Native.MainThreadScheduler is not null)
            await Native.MainThreadScheduler(action, state).ConfigureAwait(false);
        else
            throw new InvalidOperationException("Asynchronous texture upload requires a main-thread scheduler");

        cancellationToken.ThrowIfCancellationRequested();
    }
}