namespace StorybrewEditor;

using System;
using System.Globalization;
using System.Numerics;
using BrewLib.Graphics;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Textures;
using BrewLib.Input;
using BrewLib.IO;
using BrewLib.ScreenLayers;
using BrewLib.Time;
using BrewLib.UserInterface;
using BrewLib.UserInterface.Skinning;
using BrewLib.Util;
using ScreenLayers;
using SDL3;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public sealed class Editor(nint window, IGraphicsBackend graphicsBackend) : InputAdapter, IDisposable
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
        screenLayerManager.Dispose();
        overlay.Dispose();
        overlayCamera.Dispose();
        Skin.Dispose();
        DrawState.Cleanup();
        drawContext.Dispose();
    }

    public void Initialize(DisplayMode displayDevice)
    {
        ResourceContainer = new AssemblyResourceContainer(typeof(Editor).Assembly,
            $"{nameof(StorybrewEditor)}.Resources",
            "resources");

        drawContext = new();
        drawContext.Register(this);

        drawContext.Register(graphicsBackend, true);

        TextureContainer textureContainer = new TextureContainerAtlas(graphicsBackend.TextureFactory, ResourceContainer);

        drawContext.Register(textureContainer, true);

        graphicsBackend.Initialize(ResourceContainer, textureContainer);

        drawContext.Register(graphicsBackend.RendererFactory.CreateQuadRenderer(), true);
        drawContext.Register(graphicsBackend.RendererFactory.CreateLineRenderer(), true);
        drawContext.Freeze();

        try
        {
            var brewLibAssembly = drawContext.GetType().Assembly;
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
                        true) ?? brewLibAssembly.GetType(
                        $"{nameof(BrewLib)}.{nameof(UserInterface)}.{nameof(UserInterface.Skinning)}.{nameof(UserInterface.Skinning.Styles)}.{styleTypeName}",
                        true,
                        true)
            };

            Skin.Load("skin.json", ResourceContainer);
        }
        catch (Exception e)
        {
            SDL.LogError(LogCategory.Application, $"Loading skin: {e}");
            Skin = new(textureContainer);
        }

        InputDispatcher inputDispatcher = new();
        InputManager = new(window, inputDispatcher);

        screenLayerManager = new(window, clock, this);
        inputDispatcher.Add(createOverlay(screenLayerManager));
        inputDispatcher.Add(screenLayerManager.InputHandler);
        inputDispatcher.Add(this);

        if (!SDL.GetDisplayUsableBounds(displayDevice.DisplayID, out var workArea))
            throw new InvalidOperationException($"Unable to get display usable bounds: {SDL.GetError()}");

        var ratio = displayDevice.W / (float)displayDevice.H;
        var dpiScale = SDL.GetDisplayContentScale(displayDevice.DisplayID);

        float windowWidth = 1360 * dpiScale, windowHeight = windowWidth / ratio;
        if (windowHeight >= workArea.Y + workArea.H)
        {
            windowWidth = 1024 * dpiScale;
            windowHeight = windowWidth / ratio;

            if (windowWidth >= workArea.X + workArea.W)
            {
                windowWidth = 896 * dpiScale;
                windowHeight = windowWidth / ratio;
            }
        }

        if (!SDL.SetWindowSize(window,
            float.ConvertToIntegerNative<int>(windowWidth),
            float.ConvertToIntegerNative<int>(windowHeight)))
            throw new InvalidOperationException($"Unable to set window size: {SDL.GetError()}");

        if (!SDL.GetWindowBordersSize(window, out var top, out var left, out var bottom, out var right))
            throw new InvalidOperationException($"Unable to get window borders size: {SDL.GetError()}");

        var pos = Vector2.Round(new(workArea.X + (workArea.W + right - windowWidth + left) * .5f,
            workArea.Y + (workArea.H + bottom - windowHeight + top) * .5f));

        if (pos.X < 0 || pos.Y < 0)
        {
            if (!SDL.SetWindowSize(window, workArea.W, workArea.H))
                throw new InvalidOperationException($"Unable to set window size: {SDL.GetError()}");

            if (!SDL.MaximizeWindow(window))
                throw new InvalidOperationException($"Unable to maximize window: {SDL.GetError()}");
        }
        else if (!SDL.SetWindowPosition(window,
            float.ConvertToIntegerNative<int>(pos.X),
            float.ConvertToIntegerNative<int>(pos.Y)))
            throw new InvalidOperationException($"Unable to set window location: {SDL.GetError()}");

        if (!SDL.GetWindowSizeInPixels(window, out var pixelWidth, out var pixelHeight))
            throw new InvalidOperationException($"Unable to get window size in pixels: {SDL.GetError()}");

        inputDispatcher.OnResize(new()
        {
            Data1 = pixelWidth,
            Data2 = pixelHeight
        });

        Restart();
    }

    public void Restart(ScreenLayer initialLayer = null, ReadOnlySpan<char> message = default)
    {
        initializeOverlay();
        screenLayerManager.Set(initialLayer ?? new StartMenu());
        if (!message.IsEmpty) screenLayerManager.ShowMessage(message);
    }

    public void Update(TimeSpan time, bool isFixedRateUpdate = true)
    {
        IsFixedRateUpdate = isFixedRateUpdate;
        clock.AdvanceFrameTo(time);

        updateOverlay();
        screenLayerManager.Update(IsFixedRateUpdate);
    }

    public int Draw()
    {
        using var frame = DrawState.BeginFrame(Vector4.Zero);
        if (!frame.IsActive)
            return -1;

        screenLayerManager.Draw(drawContext);
        overlay.Draw(drawContext);

        return frame.End();
    }

    #region Overlay

    WidgetManager overlay;
    CameraOrtho overlayCamera;
    LinearLayout overlayTop, altOverlayTop;
    Slider volumeSlider;
    internal Label statsLabel;

    WidgetManager createOverlay(ScreenLayerManager manager)
        => overlay = new(manager, InputManager, Skin)
        {
            Camera = overlayCamera = new()
        };

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
                new Label(overlay)
                {
                    StyleName = "icon",
                    Icon = IconFont.VolumeUp
                },
                volumeSlider = new(overlay)
                {
                    Step = .01f
                }
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

            volumeSlider.Value += e.Y * .05f;
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

    public override void OnClose(SDL.QuitEvent e) => screenLayerManager.Close();

    public override void OnResize(WindowEvent e)
    {
        var width = e.Data1;
        var height = e.Data2;

        DrawState.Viewport = new(0, 0, width, height);

        var virtualHeight = height * float.Max(1024f / width, 768f / height);
        overlayCamera.VirtualHeight = float.ConvertToIntegerNative<int>(virtualHeight);

        var virtualWidth = width * virtualHeight / height;
        overlayCamera.VirtualWidth = float.ConvertToIntegerNative<int>(virtualWidth);
        overlay.Size = new(virtualWidth, virtualHeight);
    }

    #endregion
}