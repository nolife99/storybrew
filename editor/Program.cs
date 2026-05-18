namespace StorybrewEditor;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BrewLib.Audio;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Backend.OpenGL;
using BrewLib.Graphics.Backend.SDL;
using BrewLib.UserInterface;
using BrewLib.Util;
using osuTK;
using osuTK.Graphics;
using SDL3;
using StorybrewEditor.Util;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public static class Program
{
    public const string Name = "storybrew editor", Repository = "Damnae/storybrew",
        DiscordUrl = "https://discord.gg/0qfFOucX93QDNVN7";

    public static readonly Version Version = typeof(Editor).Assembly.GetName().Version;

    public static readonly string FullName = $"{Name} {Version} ({Repository})";

    static int managedAllocs;

    public static AudioManager AudioManager { get; private set; }
    public static Settings Settings { get; private set; }

    static void Main(string[] args)
    {
        SDL.SetMemoryFunctions(cb =>
            {
                Interlocked.Increment(ref managedAllocs);
                return Marshal.AllocHGlobal((nint)cb);
            },
            (c, s) =>
            {
                Interlocked.Increment(ref managedAllocs);

                var addr = Marshal.AllocHGlobal((nint)(c * s));
                addr.AsSpan<byte>((int)(c * s)).Clear();
                return addr;
            },
            (addr, cb) => Marshal.ReAllocHGlobal(addr, (nint)cb),
            addr =>
            {
                Marshal.FreeHGlobal(addr);
                Interlocked.Decrement(ref managedAllocs);
            });

        if (args.Length != 0 && handleArguments(args)) return;

        setupLogging();
        startEditor();

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
    }

    static bool handleArguments(string[] args)
    {
        switch (args[0])
        {
            case "update":
                if (args.Length < 3) return false;

                setupLogging(Path.Combine(args[1], DefaultLogPath), "update.log");

                Updater.Update(args[1], new(args[2]));
                return true;

            case "build":
                setupLogging(null, "build.log");
                Builder.Build();
                return true;
        }

        return false;
    }

    #region Editor

    static void startEditor()
    {
        if (!SDL.Init(SDL.InitFlags.Video))
            throw new InvalidOperationException($"Unable to initialize SDL video subsystem: {SDL.GetError()}");

        var audioCreateTask = Task.Run(() =>
        {
            Settings = new();
            Native.MainThreadScheduler = Schedule;
            Updater.NotifyEditorRun();

            return createAudioManager();
        });

        var primaryDisplay = SDL.GetPrimaryDisplay();
        if (primaryDisplay == 0)
            throw new InvalidOperationException($"Unable to get primary display: {SDL.GetError()}");

        var displayDevice = SDL.GetDesktopDisplayMode(primaryDisplay);
        if (!displayDevice.HasValue)
            throw new InvalidOperationException($"Unable to get display device: {SDL.GetError()}");

        var displayDeviceVal = displayDevice.Value;
        var backendKind = getGraphicsBackendKind();
        var window = createWindow(displayDeviceVal, backendKind, out var context);
        var graphicsBackend = createGraphicsBackend(backendKind, window);

        using Editor editor = new(window, graphicsBackend);
        using (NetHelper.Client = new())
        {
            NetHelper.Client.DefaultRequestHeaders.Add("user-agent", Name);
            editor.Initialize(displayDeviceVal);

            var iconSetTask = Native.SetWindowIcon(editor.ResourceContainer, "icon.ico");
            using (AudioManager = audioCreateTask.Result)
                runMainLoop(window,
                    editor,
                    backendKind == GraphicsBackendKind.OpenGl,
                    TimeSpan.FromSeconds(1) / (Settings.UpdateRate > 0 ?
                        Settings.UpdateRate :
                        displayDeviceVal.RefreshRate),
                    TimeSpan.FromSeconds(1) / (Settings.FrameRate > 0 ?
                        Settings.FrameRate :
                        displayDeviceVal.RefreshRate));

            iconSetTask.Wait();
        }

        Settings.Save();

        if (context.Handle != 0)
        {
            SDL.GLDestroyContext(context.Handle);
            SDL.GLUnloadLibrary();
        }

        SDL.DestroyWindow(window);
        SDL.Quit();
    }

    static GraphicsBackendKind getGraphicsBackendKind()
    {
        return GraphicsBackendKind.SdlGpu;
    }

    static IGraphicsBackend createGraphicsBackend(GraphicsBackendKind backendKind, nint window)
        => backendKind switch
        {
            GraphicsBackendKind.OpenGl => new OpenGlGraphicsBackend(),
            GraphicsBackendKind.SdlGpu => new SdlGraphicsBackend(window,
#if DEBUG
                debug: true
#else
                debug: false
#endif
                , "direct3d12"
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(backendKind), backendKind, null)
        };

    static nint createWindow(DisplayMode displayDevice, GraphicsBackendKind backendKind, out ContextHandle glContext)
        => backendKind switch
        {
            GraphicsBackendKind.OpenGl => createOpenGlWindow(displayDevice, out glContext),
            GraphicsBackendKind.SdlGpu => createSdlWindow(out glContext),
            _ => throw new ArgumentOutOfRangeException(nameof(backendKind), backendKind, null)
        };

    static nint createSdlWindow(out ContextHandle glContext)
    {
        glContext = default;

        var window = SDL.CreateWindow(Name, 0, 0, WindowFlags.Resizable | WindowFlags.Hidden);
        if (window == 0) throw new InvalidOperationException($"Unable to create window: {SDL.GetError()}");

        Native.InitializeHandle(window);
        return window;
    }

    static nint createOpenGlWindow(DisplayMode displayDevice, out ContextHandle glContext)
    {
        if (!SDL.GLLoadLibrary(null)) throw new InvalidOperationException($"Unable to load OpenGL: {SDL.GetError()}");

        const GLContextFlag debugContext =
#if DEBUG
            GLContextFlag.Debug | GLContextFlag.ForwardCompatible;
#else
            GLContextFlag.ForwardCompatible;

        SetAttributeSafe(GLAttr.ContextNoError, 1);
#endif

        SetAttributeSafe(GLAttr.ContextProfileMask, (int)GLProfile.Core);
        SetAttributeSafe(GLAttr.ContextFlags, (int)debugContext);
        SetAttributeSafe(GLAttr.ContextMajorVersion, 3);
        SetAttributeSafe(GLAttr.ContextMinorVersion, 2);

        ref var format = ref SDL.GetPixelFormatDetails(displayDevice.Format).AsRef<SDL.PixelFormatDetails>();
        SetAttributeSafe(GLAttr.RedSize, format.RBits);
        SetAttributeSafe(GLAttr.GreenSize, format.GBits);
        SetAttributeSafe(GLAttr.BlueSize, format.BBits);
        SetAttributeSafe(GLAttr.AlphaSize, format.ABits);
        SetAttributeSafe(GLAttr.DepthSize, 0);

        var window = SDL.CreateWindow(Name, 0, 0, WindowFlags.OpenGL | WindowFlags.Resizable | WindowFlags.Hidden);

        if (window == 0) throw new InvalidOperationException($"Unable to create window: {SDL.GetError()}");

        glContext = new(SDL.GLCreateContext(window));
        if (glContext.Handle == 0)
            throw new InvalidOperationException($"Unable to create OpenGL context: {SDL.GetError()}");

        var contextHandle = glContext;
        using (Toolkit.Init(new() { Backend = PlatformBackend.PreferNative }))
            new GraphicsContext(default,
                str =>
                {
                    var func = SDL.GLGetProcAddress(str);
                    return func is null ? 0 : Marshal.GetFunctionPointerForDelegate(func);
                },
                () => contextHandle).Dispose();

        SDL.GLSetSwapInterval(0);
        SDL.GLResetAttributes();

        Native.InitializeHandle(window);
        return window;

        static void SetAttributeSafe(GLAttr attr, int val)
        {
            if (!SDL.GLSetAttribute(attr, val))
                throw new InvalidOperationException($"Failed to set attribute {Enum.GetName(attr)}: {SDL.GetError()}");
        }
    }

    static AudioManager createAudioManager()
    {
        AudioManager audioManager = new() { Volume = Settings.Volume };

        Settings.Volume.OnValueChanged += (_, _) => audioManager.Volume = Settings.Volume;

        return audioManager;
    }

    static void runMainLoop(nint window,
        Editor editor,
        bool swapOpenGlWindow,
        TimeSpan fixedRateUpdate,
        TimeSpan targetFrame)
    {
        var startT = Stopwatch.GetTimestamp();

        TimeSpan prev = Stopwatch.GetElapsedTime(startT), fixedRate = TimeSpan.Zero, avActive = TimeSpan.Zero,
            longest = TimeSpan.Zero, lastStat = TimeSpan.Zero, statsUpdate = TimeSpan.FromSeconds(1) / 10;

        (int X, int Y) resize = default;
        var redraw = (bool pumpEvents) =>
        {
            var cur = Stopwatch.GetElapsedTime(startT);
            var fixedUpdates = 0;

            if (!pumpEvents && SDL.GetWindowSizeInPixels(window, out var w, out var h) && (resize.X != w || resize.Y != h))
                editor.InputManager.Handler.OnResize(new() { Data1 = resize.X = w, Data2 = resize.Y = h });

            AudioManager.Update(targetFrame);

            while (cur - fixedRate >= fixedRateUpdate && fixedUpdates++ < 2)
                editor.Update(fixedRate += fixedRateUpdate);

            var windowFocus = (SDL.GetWindowFlags(window) & WindowFlags.InputFocus) != 0;
            if (windowFocus && fixedUpdates == 0 && fixedRate < cur && cur < fixedRate + fixedRateUpdate)
                editor.Update(cur, false);

            var draws = editor.Draw();
            if (swapOpenGlWindow && !SDL.GLSwapWindow(window))
                throw new InvalidOperationException($"Unable to swap framebuffer: {SDL.GetError()}");

            if (Monitor.TryEnter(scheduledActions))
            {
                using var snapshot = TempList.Create(scheduledActions);
                scheduledActions.Clear();

                Monitor.Exit(scheduledActions);
                foreach (var action in snapshot) action.Dispose();
            }

            var active = Stopwatch.GetElapsedTime(startT) - cur;
            var sleepTime = (windowFocus ? targetFrame : fixedRateUpdate) - active;

            if (sleepTime > TimeSpan.Zero) SDL.DelayNS((ulong)sleepTime.Ticks * (TimeSpan.NanosecondsPerTick - 5));

            var frameTime = cur - prev;
            prev = cur;
            if (lastStat + statsUpdate > cur) return;

            avActive = (active + avActive) / 2;
            longest = new(long.Max(frameTime.Ticks, longest.Ticks));

            buildStatsMessage(editor.statsLabel, frameTime, avActive, longest, draws);

            lastStat = cur;
        };

        var state = (false, UnsafeMemory.AsPointerUnconstrained(in redraw));
        EventFilter filter = (s, ref readonly e) =>
        {
            ref var localState = ref s.AsRef<(bool, nint)>();

            if (e.Type is EventType.Quit) return localState.Item1 = true;
            if (e.Type is not EventType.WindowExposed || !SDL.IsMainThread()) return false;

            localState.Item2.AsRef<Action<bool>>()(false);
            return true;
        };

        SDL.AddEventWatch(filter, state.AsPointer());
        SDL.ShowWindow(window);

        while (true)
        {
            editor.InputManager.Update();
            if (state.Item1) break;

            redraw(true);
        }

        SDL.RemoveEventWatch(filter, state.AsPointer());
    }

    static void buildStatsMessage(Label label, TimeSpan av, TimeSpan avActive, TimeSpan longest, int draws)
    {
        if (!label.Visible) return;

        var r = TimeSpan.FromSeconds(1);
        using var result = StringHelper.Interpolate(CultureInfo.InvariantCulture,
            $"{double.Round(r / av)}/{double.Round(r / avActive)}fps (act:{avActive.TotalMilliseconds:f2} avg:{av.TotalMilliseconds:f2} hi:{longest.TotalMilliseconds:f2})\n{draws} draws\n{managedAllocs} off-heap buffers");

        label.Text = result.AsReadOnlySpan();
    }

    #endregion

    enum GraphicsBackendKind
    {
        OpenGl,
        SdlGpu
    }

    #region Scheduling

    static readonly List<IDisposable> scheduledActions = [];

    public static ValueTask Schedule<TState>(Action<TState> action, TState state = default)
    {
        if (SDL.IsMainThread())
        {
            action(state);
            return ValueTask.CompletedTask;
        }

        var tcs = ValueTaskSourceHolder<TState>.Get(action, state);
        lock (scheduledActions) scheduledActions.Add(tcs);

        return tcs.Task;
    }

    #endregion

    #region Error Handling

    const string DefaultLogPath = "logs";

    static bool insideErrorHandler;

    static void setupLogging(string logsPath = null, string commonLogFilename = null)
    {
        logsPath ??= DefaultLogPath;

        var tracePath = Path.Combine(logsPath, commonLogFilename ?? "trace.log");
        var exceptionPath = Path.Combine(logsPath, commonLogFilename ?? "exception.log");
        var crashPath = Path.Combine(logsPath, commonLogFilename ?? "crash.log");

        AppContext.SetData(nameof(tracePath), tracePath);
        AppContext.SetData(nameof(exceptionPath), exceptionPath);
        AppContext.SetData(nameof(crashPath), crashPath);

        if (!Directory.Exists(logsPath)) Directory.CreateDirectory(logsPath);
        else
        {
            if (File.Exists(tracePath)) File.Delete(tracePath);
            if (File.Exists(exceptionPath)) File.Delete(exceptionPath);
        }

        var domain = AppDomain.CurrentDomain;
        domain.FirstChanceException += (_, e) => logError(e.Exception,
            (string)AppContext.GetData(nameof(exceptionPath)),
            false);

        domain.UnhandledException += (_, e) => logError((Exception)e.ExceptionObject,
            (string)AppContext.GetData(nameof(crashPath)),
            e.IsTerminating);

        SDL.SetLogPriorities(LogPriority.Trace);
        SDL.SetLogOutputFunction((_, _, _, message) => ThreadPool.UnsafeQueueUserWorkItem(m =>
                {
                    var msg = (string)m!;
                    Span<char> text = stackalloc char[msg.Length + Environment.NewLine.Length + 20];

                    var success = DateTime.Now.TryFormat(text,
                        out var written,
                        "yyyy-MM-ddTHH:mm:ss",
                        CultureInfo.InvariantCulture);

                    text[written++] = ' ';
                    success &= msg.TryCopyTo(text[written..]) &&
                        Environment.NewLine.TryCopyTo(text[(written + msg.Length)..]);

                    if (!success) throw new InvalidOperationException("Failed to format log message");

                    var p = (string)AppContext.GetData(nameof(tracePath));
                    if (p is not null)
                        lock (p)
                            File.AppendAllText(p, text);

                    Console.Out.Write(text);
                },
                message),
            0);

        SDL.SetAppMetadataProperty(SDL.Props.AppMetadataNameString, Name);
        SDL.SetAppMetadataProperty(SDL.Props.AppMetadataVersionString, Version.ToString());
        SDL.SetAppMetadataProperty(SDL.Props.AppMetadataURLString, Repository);

        SDL.AddEventWatch((_, ref readonly e) =>
            {
                if (e.Type is not EventType.Quit) return false;

                AppContext.SetData(nameof(tracePath), null);
                AppContext.SetData(nameof(exceptionPath), null);
                AppContext.SetData(nameof(crashPath), null);

                return true;
            },
            0);
    }

    static void logError(Exception e, string filename, bool show)
    {
        if (filename is null) return;

        lock (filename)
        {
            if (insideErrorHandler) return;

            insideErrorHandler = true;

            using StreamWriter w = new(Path.Combine(Environment.CurrentDirectory, filename), true);
            try
            {
                w.Write(DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + ' ');
                w.WriteLine(e);
                w.WriteLine();

                w.Flush();

                if (!show) return;

                using var msg = StringHelper.Interpolate(
                    $"An error occurred:\n{e.Message} ({e.GetType().Name})\n\nClick Ok if you want to receive and invitation to a Discord server where you can get help with this problem.");

                using MessageBoxData data = new(MessageBoxFlags.Error,
                    0,
                    FullName,
                    msg.AsReadOnlySpan(),
                    [
                        new(MessageBoxButtonFlags.EscapekeyDefault, 1, "Cancel"),
                        new(MessageBoxButtonFlags.ReturnkeyDefault, 0, "OK")
                    ]);

                if (!SDL.ShowMessageBox(in data, out var id))
                    throw new InvalidOperationException($"Cannot create message box: {SDL.GetError()}");

                if (id == 0) NetHelper.OpenUrl(DiscordUrl);
            }
            catch (Exception e2)
            {
                w.Write(DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + " - ");
                w.WriteLine(e2);
                w.WriteLine();

                e = new AggregateException(e, e2);
            }
            finally
            {
                if (show)
                {
                    SDL.Quit();
                    Environment.FailFast(null, e);
                }

                insideErrorHandler = false;
            }
        }
    }

    #endregion
}
