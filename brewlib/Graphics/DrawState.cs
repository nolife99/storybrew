namespace BrewLib.Graphics;

using System;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Backend.OpenGL;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Text;
using BrewLib.Graphics.Textures;
using BrewLib.IO;
using osuTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public static class DrawState
{
    static IRenderer renderer;
    static bool flushingRenderer;
    static int drawCalls;

    public static bool UseSrgb { get; set; }
    public static bool UseTextureCompression { get; set; }

    public static IGraphicsBackend Backend { get; private set; }
    public static IGraphicsDevice Device => Backend?.Device;

    public static bool SupportsImmutable
        => Backend?.Capabilities.Has(GraphicsBackendFeatures.ImmutableBuffers) ?? false;

    public static bool CanInvalidate
        => Backend?.Capabilities.Has(GraphicsBackendFeatures.FramebufferInvalidation) ?? false;

    public static bool ColorCorrected
        => Backend?.Capabilities.Has(GraphicsBackendFeatures.SrgbFramebuffer) ?? false;

    public static int MaxTextureSize => Backend?.Capabilities.MaxTextureSize ?? 0;
    public static int MaxTextureImageUnits => Backend?.Capabilities.MaxTextureImageUnits ?? 0;

    static OpenGlGraphicsDevice OpenGlDevice => Device as OpenGlGraphicsDevice ??
        throw new InvalidOperationException("The active graphics backend is not OpenGL");

    public static int ActiveTextureUnit
    {
        get => OpenGlDevice.ActiveTextureUnit;
        set => OpenGlDevice.ActiveTextureUnit = value;
    }

    public static IRenderer Renderer
    {
        get => renderer;
        set
        {
            if (renderer == value) return;

            FlushRenderer(true);

            flushingRenderer = true;
            renderer?.EndRendering();

            renderer = value;

            renderer?.BeginRendering();
            flushingRenderer = false;
        }
    }

    public static void Initialize(ResourceContainer resourceContainer,
        TextureContainer textureContainer,
        IGraphicsBackend backend = null)
    {
        if (backend is null)
        {
            backend = new OpenGlGraphicsBackend();
            backend.Initialize(resourceContainer, textureContainer);
            return;
        }

        Backend = backend;

        var textureFactory = Backend.TextureFactory;

        WhitePixel = textureFactory.Create(Color.White,
            textureOptions: new()
            {
                TextureMagFilter = TextureFilter.Nearest, TextureMinFilter = TextureFilter.Nearest
            });

        TransparentPixel = textureFactory.Create(Color.Transparent,
            textureOptions: new()
            {
                TextureMagFilter = TextureFilter.Nearest, TextureMinFilter = TextureFilter.Nearest
            });

        TextGenerator = new(resourceContainer);
        TextFontManager = new(textureContainer);
    }

    public static void Cleanup()
    {
        WhitePixel.Dispose();
        TransparentPixel.Dispose();
        TextFontManager.Dispose();
        TextGenerator.Dispose();
    }

    public static int CompleteFrame()
    {
        Renderer = null;

        var totalDraws = drawCalls;
        drawCalls = 0;

        Device.ResetStateCache();
        RenderStates.ClearStateCache();

        return totalDraws;
    }

    public static void FlushRenderer(bool canBuffer = false)
    {
        if (renderer is null || flushingRenderer) return;

        flushingRenderer = true;
        renderer.Flush(canBuffer);
        flushingRenderer = false;
    }

    internal static void CountDrawCall() => ++drawCalls;

    public static T Prepare<T>(T nextRenderer, ICamera camera, RenderStates renderStates) where T : IRenderer
    {
        Renderer = nextRenderer;
        renderer.Camera = camera;
        renderStates.Apply();
        return nextRenderer;
    }

    public static bool SupportsShaderExtension(string extensionName)
        => Backend?.SupportsShaderExtension(extensionName) ?? false;

    #region Texture states

    public static ITextureRegion WhitePixel { get; private set; }
    public static ITextureRegion TransparentPixel { get; private set; }

    public static void BindPrimaryTexture(int textureId, TextureTarget mode = TextureTarget.Texture2D)
        => OpenGlDevice.BindPrimaryTexture(textureId, mode);

    public static int BindTexture(ITexture texture) => Device.BindTexture(texture);

    public static int BindTexture(int textureId) => OpenGlDevice.BindTexture(textureId);

    public static void UnbindTexture(ITexture texture) => Device.UnbindTexture(texture);

    public static void UnbindTexture(int textureId) => OpenGlDevice.UnbindTexture(textureId);

    #endregion

    #region Other states

    static Rectangle viewport;

    public static Rectangle Viewport
    {
        get => viewport;
        set
        {
            if (viewport == value) return;

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
            if (clipRegion == value) return;

            FlushRenderer();
            clipRegion = value;

            Device.SetScissor(clipRegion.HasValue ?
                Rectangle.Intersect(Nullable.GetValueRefOrDefaultRef(ref clipRegion), viewport) :
                null);
        }
    }

    static Rectangle? Clip(Rectangle? newRegion)
    {
        var previousClipRegion = clipRegion;
        ClipRegion = clipRegion.HasValue && newRegion.HasValue ?
            Rectangle.Intersect(Nullable.GetValueRefOrDefaultRef(ref clipRegion),
                Nullable.GetValueRefOrDefaultRef(ref newRegion)) :
            newRegion;

        return previousClipRegion;
    }

    public static Rectangle? Clip(RectangleF bounds, ICamera camera)
    {
        var screenBounds = camera.ToScreen(bounds);
        return Clip(new(float.ConvertToIntegerNative<int>(screenBounds.X),
            viewport.Height - float.ConvertToIntegerNative<int>(screenBounds.Y + screenBounds.Height),
            float.ConvertToIntegerNative<int>(screenBounds.Width),
            float.ConvertToIntegerNative<int>(screenBounds.Height)));
    }

    public static RectangleF? GetClipRegion(ICamera camera)
    {
        if (!clipRegion.HasValue) return null;

        var bounds = camera.FromScreen(Nullable.GetValueRefOrDefaultRef(ref clipRegion));
        return RectangleF.FromLTRB(bounds.X,
            camera.ExtendedViewport.Height - bounds.Bottom,
            bounds.Right,
            camera.ExtendedViewport.Height - bounds.Y);
    }

    static int programId;

    public static int ProgramId
    {
        get => programId;
        set
        {
            if (programId == value) return;

            programId = value;
            Device.UseProgram(programId);
        }
    }

    #endregion

    #region Utilities

    public static TextGenerator TextGenerator { get; private set; }
    public static TextFontManager TextFontManager { get; private set; }

    #endregion
}

public enum BlendingMode
{
    Off, AlphaBlend, Color, Additive, BlendAdd, Premultiply, Premultiplied
}
