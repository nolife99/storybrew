namespace StorybrewEditor;

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Drawables;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Textures;
using BrewLib.Input;
using BrewLib.IO;
using BrewLib.ScreenLayers;
using BrewLib.Time;
using BrewLib.UserInterface;
using BrewLib.UserInterface.Skinning;
using BrewLib.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using StorybrewEditor.ScreenLayers;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public sealed class Editor(NativeWindow window) : IDisposable
{
    readonly FrameClock clock = new();

    DrawContext drawContext;
    ScreenLayerManager screenLayerManager;

    public InputManager InputManager { get; private set; }
    public bool IsFixedRateUpdate { get; private set; }
    public ResourceContainer ResourceContainer { get; private set; }
    public Skin Skin { get; private set; }
    public FrameTimeSource TimeSource => clock;

    public void Dispose()
    {
        window.Resize -= resizeToWindow;
        window.Closing -= window_Closing;

        screenLayerManager.Dispose();
        overlay.Dispose();
        overlayCamera.Dispose();
        InputManager.Dispose();
        drawContext.Dispose();
        Skin.Dispose();
        DrawState.Cleanup();
    }

    public void Initialize(MonitorInfo displayDevice)
    {
        ResourceContainer = new AssemblyResourceContainer(typeof(Editor).Assembly,
            $"{nameof(StorybrewEditor)}.Resources",
            "resources");

        drawContext = new();
        drawContext.Register(this);

        TextureContainer textureContainer = Texture2d.BindlessTexturesSupported ?
            new TextureContainerSeparate(ResourceContainer) :
            new TextureContainerAtlas(ResourceContainer);

        drawContext.Register(textureContainer, true);

        drawContext.Register<IQuadRenderer>(new QuadRendererBuffered(), true);
        drawContext.Register<ILineRenderer>(new LineRendererBuffered(), true);
        drawContext.Freeze();

        var size = window.ClientSize;
        DrawState.UseTextureCompression = Program.Settings.TextureCompression;
        DrawState.Initialize(ResourceContainer, textureContainer, size.X, size.Y);

        try
        {
            var brewLibAssembly = typeof(Drawable).Assembly;
            Skin = new(textureContainer)
            {
                ResolveDrawableType =
                    drawableTypeName => brewLibAssembly.GetType(
                        $"{nameof(BrewLib)}.{nameof(BrewLib.Graphics)}.{nameof(BrewLib.Graphics.Drawables)}.{drawableTypeName}",
                        true,
                        true),
                ResolveWidgetType =
                    widgetTypeName
                        => Type.GetType($"{nameof(StorybrewEditor)}.{nameof(UserInterface)}.{widgetTypeName}",
                            false,
                            true) ??
                        brewLibAssembly.GetType($"{nameof(BrewLib)}.{nameof(UserInterface)}.{widgetTypeName}",
                            true,
                            true),
                ResolveStyleType = styleTypeName
                    => Type.GetType(
                        $"{nameof(StorybrewEditor)}.{nameof(UserInterface)}.{nameof(UserInterface.Skinning)}.{nameof(UserInterface.Skinning.Styles)}.{styleTypeName}",
                        false,
                        true) ??
                    brewLibAssembly.GetType(
                        $"{nameof(BrewLib)}.{nameof(UserInterface)}.{nameof(UserInterface.Skinning)}.{nameof(UserInterface.Skinning.Styles)}.{styleTypeName}",
                        true,
                        true)
            };

            Skin.Load("skin.json", ResourceContainer);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Loading skin: {e}");
            Skin = new(textureContainer);
        }

        InputDispatcher inputDispatcher = new();
        InputManager = new(window, inputDispatcher);

        screenLayerManager = new(window, clock, this);
        inputDispatcher.Add(createOverlay(screenLayerManager));
        inputDispatcher.Add(screenLayerManager.InputHandler);

        window.Resize += resizeToWindow;
        window.Closing += window_Closing;

        var workArea = displayDevice.WorkArea;
        var ratio = displayDevice.HorizontalResolution / (float)displayDevice.VerticalResolution;
        var dpiScale = displayDevice.VerticalScale;

        float windowWidth = 1360 * dpiScale, windowHeight = windowWidth / ratio;
        if (windowHeight >= workArea.Max.Y)
        {
            windowWidth = 1024 * dpiScale;
            windowHeight = windowWidth / ratio;

            if (windowWidth >= workArea.Max.X)
            {
                windowWidth = 896 * dpiScale;
                windowHeight = windowWidth / ratio;
            }
        }

        window.CenterWindow(new((int)windowWidth, (int)windowHeight));
        Trace.WriteLine($"Window dpi scale: {dpiScale}");

        var location = window.Location;
        if (location.X < 0 || location.Y < 0)
        {
            window.ClientRectangle = workArea;
            window.WindowState = WindowState.Maximized;
        }

        Restart();
    }

    public void Restart(ScreenLayer initialLayer = null, ReadOnlySpan<char> message = default)
    {
        initializeOverlay();
        screenLayerManager.Set(initialLayer ?? new StartMenu());
        if (!message.IsEmpty) screenLayerManager.ShowMessage(message);
    }

    public void Update(float time, bool isFixedRateUpdate = true)
    {
        IsFixedRateUpdate = isFixedRateUpdate;
        clock.AdvanceFrameTo(time);

        updateOverlay();
        screenLayerManager.Update(IsFixedRateUpdate);
    }

    public int Draw()
    {
        GL.Clear(ClearBufferMask.ColorBufferBit);

        screenLayerManager.Draw(drawContext);
        overlay.Draw(drawContext);

        return DrawState.CompleteFrame();
    }

    #region Overlay

    WidgetManager overlay;
    CameraOrtho overlayCamera;
    LinearLayout overlayTop, altOverlayTop;
    Slider volumeSlider;
    internal Label statsLabel;

    WidgetManager createOverlay(ScreenLayerManager manager)
        => overlay = new(manager, InputManager, Skin) { Camera = overlayCamera = new() };

    void initializeOverlay()
    {
        overlay.Root.ClearWidgets();
        overlay.Root.Add(overlayTop = new(overlay)
        {
            AnchorTarget = overlay.Root,
            AnchorFrom = BoxAlignment.Top,
            AnchorTo = BoxAlignment.Top,
            Horizontal = true,
            Opacity = 0,
            Displayed = false,
            Children =
            [
                statsLabel = new(overlay)
                {
                    StyleName = "small",
                    AnchorTarget = overlay.Root,
                    AnchorTo = BoxAlignment.TopLeft,
                    Displayed = Program.Settings.ShowStats
                }
            ]
        });

        overlayTop.Pack(1024, 16);

        overlay.Root.Add(altOverlayTop = new(overlay)
        {
            AnchorTarget = overlay.Root,
            AnchorFrom = BoxAlignment.Top,
            AnchorTo = BoxAlignment.Top,
            Horizontal = true,
            Opacity = 0,
            Displayed = false,
            Children =
            [
                new Label(overlay) { StyleName = "icon", Icon = IconFont.VolumeUp },
                volumeSlider = new(overlay) { Step = .01f }
            ]
        });

        altOverlayTop.Pack(0, 0, 1024);

        Program.Settings.Volume.Bind(volumeSlider,
            () =>
            {
                using var text = StringHelper.Interpolate(CultureInfo.InvariantCulture,
                    $"Volume: {volumeSlider.Value:P0}");

                volumeSlider.Tooltip = text.AsReadOnlySpan();
            });

        overlay.Root.OnMouseWheel += (_, e) =>
        {
            if (!InputManager.AltOnly) return false;

            volumeSlider.Value += e.OffsetY * .05f;
            return true;
        };
    }

    void updateOverlay()
    {
        if (!IsFixedRateUpdate) return;

        var mousePosition = overlay.MousePosition;
        var bounds = altOverlayTop.Bounds;

        var showAltOverlayTop = InputManager.AltOnly ||
            altOverlayTop.Displayed && bounds.Y < mousePosition.Y && mousePosition.Y < bounds.Bottom;

        var altOpacity = altOverlayTop.Opacity;
        var targetOpacity = showAltOverlayTop ? 1f : 0;
        altOpacity = float.Abs(altOpacity - targetOpacity) <= .07f ?
            targetOpacity :
            float.Clamp(altOpacity + (altOpacity < targetOpacity ? .07f : -.07f), 0, 1);

        overlayTop.Opacity = 1 - altOpacity;
        overlayTop.Displayed = altOpacity < 1;

        altOverlayTop.Opacity = altOpacity;
        altOverlayTop.Displayed = altOpacity > 0;
    }

    void window_Closing(CancelEventArgs e) => screenLayerManager.Close();

    void resizeToWindow(ResizeEventArgs e)
    {
        var width = e.Width;
        var height = e.Height;

        DrawState.Viewport = new(0, 0, width, height);

        var virtualHeight = height * float.Max(1024f / width, 768f / height);
        overlayCamera.VirtualHeight = (int)virtualHeight;

        var virtualWidth = width * virtualHeight / height;
        overlayCamera.VirtualWidth = (int)virtualWidth;
        overlay.Size = new(virtualWidth, virtualHeight);
    }

    #endregion
}