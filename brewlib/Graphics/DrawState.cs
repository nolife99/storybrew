namespace BrewLib.Graphics;

using System;
using System.Numerics;
using Backend;
using Cameras;
using IO;
using Renderers;
using SixLabors.ImageSharp;
using Text;
using Textures;

public static class DrawState
{
    static IRenderer renderer;
    static bool flushingRenderer;
    static int renderPasses;

    public static bool UseSrgb { get; set; }

    public static IGraphicsBackend Backend { get; private set; }
    public static IGraphicsDevice Device => Backend?.Device;

    public static bool SupportsImmutable => Backend?.Capabilities.Has(GraphicsBackendFeatures.ImmutableBuffers) ?? false;
    public static bool CanInvalidate => Backend?.Capabilities.Has(GraphicsBackendFeatures.FramebufferInvalidation) ?? false;
    public static bool ColorCorrected => Backend?.Capabilities.Has(GraphicsBackendFeatures.SrgbFramebuffer) ?? false;
    public static int MaxTextureSize => Backend?.Capabilities.MaxTextureSize ?? 0;
    public static int MaxTextureImageUnits => Backend?.Capabilities.MaxTextureImageUnits ?? 0;

    public static IRenderer Renderer
    {
        get => renderer;
        set
        {
            if (ReferenceEquals(renderer, value))
                return;

            FlushRenderer(true);

            flushingRenderer = true;
            try
            {
                renderer?.EndRendering();
                renderer = value;
                renderer?.BeginRendering();
            }
            finally
            {
                flushingRenderer = false;
            }
        }
    }

    public static void Initialize(ResourceContainer resourceContainer, TextureContainer textureContainer, IGraphicsBackend backend = null)
    {
        if (backend is null)
            throw new ArgumentNullException(nameof(backend), "DrawState now requires an explicit graphics backend");

        Backend = backend;

        var textureFactory = Backend.TextureFactory;
        WhitePixel = textureFactory.Create(Color.White,
            textureOptions: new()
            {
                TextureMagFilter = TextureFilter.Nearest,
                TextureMinFilter = TextureFilter.Nearest
            });

        TransparentPixel = textureFactory.Create(Color.Transparent,
            textureOptions: new()
            {
                TextureMagFilter = TextureFilter.Nearest,
                TextureMinFilter = TextureFilter.Nearest
            });

        TextGenerator = new(resourceContainer);
        TextFontManager = new(textureContainer);
    }

    public static void Cleanup()
    {
        Renderer = null;

        WhitePixel?.Dispose();
        TransparentPixel?.Dispose();
        WhitePixel = null;
        TransparentPixel = null;

        Backend = null;
    }

    public static DrawFrame BeginFrame(Vector4 clearColor)
        => new(Backend?.BeginFrame(clearColor) ?? false);

    public static int DrawFrame(Vector4 clearColor, Action draw)
    {
        using var frame = BeginFrame(clearColor);
        if (!frame.IsActive)
            return -1;

        draw();
        return frame.End();
    }

    internal static int EndFrame()
    {
        Renderer = null;

        var passes = renderPasses;
        renderPasses = 0;

        Device?.ResetStateCache();
        RenderStates.ClearStateCache();
        Backend?.EndFrame(CanInvalidate);

        return passes;
    }

    public static void FlushRenderer(bool canBuffer = false)
    {
        if (renderer is null || flushingRenderer)
            return;

        flushingRenderer = true;
        try
        {
            renderer.Flush(canBuffer);
        }
        finally
        {
            flushingRenderer = false;
        }
    }

    public static void FlushRendererImmediate() => FlushRenderer();

    internal static void CountRenderPass() => ++renderPasses;

    public static T Prepare<T>(T nextRenderer, ICamera camera, RenderStates renderStates)
        where T : IRenderer
    {
        Renderer = nextRenderer;
        renderer.Camera = camera;
        renderStates.Apply();
        return nextRenderer;
    }

    public static bool SupportsShaderExtension(string extensionName) => Backend?.SupportsShaderExtension(extensionName) ?? false;

    #region Texture states

    public static ITextureRegion WhitePixel { get; private set; }
    public static ITextureRegion TransparentPixel { get; private set; }

    public static int BindTexture(ITexture texture) => Device.BindTexture(texture);
    public static void UnbindTexture(ITexture texture) => Device.UnbindTexture(texture);

    #endregion

    #region Other states

    static Rectangle viewport;

    public static Rectangle Viewport
    {
        get => viewport;
        set
        {
            if (viewport == value)
                return;

            FlushRendererImmediate();
            viewport = value;
            Device.SetViewport(viewport);
            ViewportChanged?.Invoke();
        }
    }

    public static event Action ViewportChanged;

    static Rectangle? clipRegion;

    public static Rectangle? ClipRegion
    {
        get => clipRegion;
        set
        {
            if (clipRegion == value)
                return;

            FlushRendererImmediate();
            clipRegion = value;
            Device.SetScissor(clipRegion.HasValue
                ? Rectangle.Intersect(Nullable.GetValueRefOrDefaultRef(ref clipRegion), viewport)
                : null);
        }
    }

    static Rectangle? Clip(Rectangle? newRegion)
    {
        var previousClipRegion = clipRegion;
        ClipRegion = clipRegion.HasValue && newRegion.HasValue
            ? Rectangle.Intersect(Nullable.GetValueRefOrDefaultRef(ref clipRegion), Nullable.GetValueRefOrDefaultRef(ref newRegion))
            : newRegion;

        return previousClipRegion;
    }

    public static Rectangle? Clip(RectangleF bounds, ICamera camera)
    {
        var screenBounds = camera.ToScreen(bounds);
        return Clip(new(
            float.ConvertToIntegerNative<int>(screenBounds.X),
            viewport.Height - float.ConvertToIntegerNative<int>(screenBounds.Y + screenBounds.Height),
            float.ConvertToIntegerNative<int>(screenBounds.Width),
            float.ConvertToIntegerNative<int>(screenBounds.Height)));
    }

    public static RectangleF? GetClipRegion(ICamera camera)
    {
        if (!clipRegion.HasValue)
            return null;

        var bounds = camera.FromScreen(Nullable.GetValueRefOrDefaultRef(ref clipRegion));
        return RectangleF.FromLTRB(bounds.X,
            camera.ExtendedViewport.Height - bounds.Bottom,
            bounds.Right,
            camera.ExtendedViewport.Height - bounds.Y);
    }

    #endregion

    #region Utilities

    public static TextGenerator TextGenerator { get; private set; }
    public static TextFontManager TextFontManager { get; private set; }

    #endregion
}

public ref struct DrawFrame
{
    bool ended;

    internal DrawFrame(bool isActive)
    {
        IsActive = isActive;
        ended = false;
    }

    public bool IsActive { get; }

    public int End()
    {
        if (ended)
            throw new InvalidOperationException("The draw frame has already ended");

        ended = true;
        return IsActive ? DrawState.EndFrame() : -1;
    }

    public void Dispose()
    {
        if (!ended && IsActive)
            End();
    }
}

public enum BlendingMode
{
    Off,
    AlphaBlend,
    Color,
    Additive,
    BlendAdd,
    Premultiply,
    Premultiplied
}