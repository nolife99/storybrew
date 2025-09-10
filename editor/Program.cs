namespace StorybrewEditor;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BrewLib.Audio;
using BrewLib.Util;
using ManagedBass;
using OpenTK.Graphics.OpenGL;
using SDL3;
using SixLabors.ImageSharp.Diagnostics;
using SixLabors.ImageSharp.Memory;
using StorybrewEditor.Util;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public static class Program
{
    public const string Name = "storybrew editor", Repository = "Damnae/storybrew",
        DiscordUrl = "https://discord.gg/0qfFOucX93QDNVN7";

    public static readonly Version Version = typeof(Editor).Assembly.GetName().Version;

    public static readonly string FullName = $"{Name} {Version} ({Repository})";

    static readonly ConcurrentDictionary<nint, (IMemoryOwner<byte>, MemoryHandle, StackTrace)> managedAllocs = new();

    public static AudioManager AudioManager { get; private set; }
    public static Settings Settings { get; private set; }

    static void Main(string[] args)
    {
        SDL.SetMemoryFunctions(cb =>
            {
                var memory = MemoryAllocator.Default.Allocate<byte>((int)cb);
                var pinned = memory.Memory.Pin();
                var addr = memory.Memory.Span.AsPointer();

                managedAllocs.TryAdd(addr, (memory, pinned, new(true)));
                return addr;
            },
            (c, s) =>
            {
                var memory = MemoryAllocator.Default.Allocate<byte>((int)(c * s));
                var span = memory.Memory.Span;
                span.Clear();

                var pinned = memory.Memory.Pin();
                var addr = span.AsPointer();

                managedAllocs.TryAdd(addr, (memory, pinned, new(true)));
                return addr;
            },
            (addr, cb) =>
            {
                IMemoryOwner<byte> oldArr = null;
                if (managedAllocs.TryRemove(addr, out var arrayToFree))
                {
                    oldArr = arrayToFree.Item1;
                    arrayToFree.Item2.Dispose();
                }

                var memory = MemoryAllocator.Default.Allocate<byte>((int)cb);
                var span = memory.Memory.Span;

                if (oldArr is not null)
                {
                    oldArr.Memory.Span.CopyTo(span);
                    oldArr.Dispose();
                }

                var pinned = memory.Memory.Pin();
                var newAddr = span.AsPointer();

                managedAllocs.TryAdd(newAddr, (memory, pinned, new(true)));
                return newAddr;
            },
            addr =>
            {
                if (!managedAllocs.TryRemove(addr, out var arrayToFree))
                    throw new InvalidMemoryOperationException($"Attempted to free invalid memory: {addr}");

                arrayToFree.Item2.Dispose();
                arrayToFree.Item1.Dispose();
            });

        if (args.Length != 0 && handleArguments(args)) return;

        setupLogging();
        startEditor();

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();

        if (managedAllocs.IsEmpty) return;

        throw new AggregateException(managedAllocs.Values.Select(p
            => ExceptionDispatchInfo.SetRemoteStackTrace(
                new InvalidMemoryOperationException(p.Item1.Memory.Length.ToString()),
                p.Item3.ToString())));
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
        var window = createWindow(displayDeviceVal, out var context);

        using Editor editor = new(window);
        using (NetHelper.Client = new())
        {
            NetHelper.Client.DefaultRequestHeaders.Add("user-agent", Name);
            editor.Initialize(displayDeviceVal);

            var iconSetTask = Native.SetWindowIcon(editor.ResourceContainer, "icon.ico");
            using (AudioManager = audioCreateTask.Result)
                runMainLoop(window,
                    editor,
                    (ulong)(SDL.NsPerSecond / (Settings.UpdateRate > 0 ?
                        Settings.UpdateRate :
                        displayDeviceVal.RefreshRate)),
                    (ulong)(SDL.NsPerSecond / (Settings.FrameRate > 0 ?
                        Settings.FrameRate :
                        displayDeviceVal.RefreshRate)));

            iconSetTask.Wait();
        }

        Settings.Save();

        SDL.GLDestroyContext(context);
        SDL.GLUnloadLibrary();
        SDL.DestroyWindow(window);
        SDL.Quit();
    }

    static nint createWindow(DisplayMode displayDevice, out nint glContext)
    {
        if (!SDL.GLLoadLibrary(null)) throw new InvalidOperationException($"Unable to load OpenGL: {SDL.GetError()}");

        const GLContextFlag debugContext =
#if DEBUG
            GLContextFlag.Debug | GLContextFlag.ForwardCompatible;
#else
            GLContextFlag.ForwardCompatible;

        SDL.GLSetAttribute(GLAttr.ContextNoError, 1);
#endif

        SDL.GLSetAttribute(GLAttr.ContextProfileMask, (int)GLProfile.Core);
        SDL.GLSetAttribute(GLAttr.ContextFlags, (int)debugContext);
        SDL.GLSetAttribute(GLAttr.ContextMajorVersion, 3);
        SDL.GLSetAttribute(GLAttr.ContextMinorVersion, 3);

        ref var format = ref SDL.GetPixelFormatDetails(displayDevice.Format).AsRef<SDL.PixelFormatDetails>();
        SDL.GLSetAttribute(GLAttr.RedSize, format.RBits);
        SDL.GLSetAttribute(GLAttr.GreenSize, format.GBits);
        SDL.GLSetAttribute(GLAttr.BlueSize, format.BBits);
        SDL.GLSetAttribute(GLAttr.AlphaSize, format.ABits);
        SDL.GLSetAttribute(GLAttr.DepthSize, 0);

        SDL.LogInfo(SDL.LogCategory.System,
            $"Display info: R{format.RBits} G{format.GBits} B{format.BBits} A{format.ABits}");

        var window = SDL.CreateWindow(Name, 0, 0, WindowFlags.OpenGL | WindowFlags.Resizable | WindowFlags.Hidden);

        if (window == 0) throw new InvalidOperationException($"Unable to create window: {SDL.GetError()}");

        glContext = SDL.GLCreateContext(window);
        if (glContext == 0) throw new InvalidOperationException($"Unable to create OpenGL context: {SDL.GetError()}");

        if (!SDL.GLMakeCurrent(window, glContext))
            throw new InvalidOperationException($"Unable to bind OpenGL context to window: {SDL.GetError()}");

        GL.LoadBindings(new SDLBindingsContext());

        SDL.GLSetSwapInterval(0);
        SDL.GLResetAttributes();

        Native.InitializeHandle(window);
        return window;
    }

    static AudioManager createAudioManager()
    {
        AudioManager audioManager = new() { Volume = Settings.Volume };

        Settings.Volume.OnValueChanged += (_, _) => audioManager.Volume = Settings.Volume;

        return audioManager;
    }

    static void runMainLoop(nint window, Editor editor, ulong fixedRateUpdate, ulong targetFrame)
    {
        ulong prev = SDL.GetTicksNS(), fixedRate = 0, avActive = 0, longest = 0, lastStat = 0,
            statsUpdate = targetFrame * 5;

        (int X, int Y) resize = default;
        var redraw = (bool pumpEvents) =>
        {
            var cur = SDL.GetTicksNS();
            var fixedUpdates = 0;

            if (!pumpEvents && SDL.GetWindowSize(window, out var w, out var h) && (resize.X != w || resize.Y != h))
                editor.InputManager.Handler.OnResize(new() { Data1 = resize.X = w, Data2 = resize.Y = h });

            AudioManager.Update(targetFrame);

            while (cur - fixedRate >= fixedRateUpdate && fixedUpdates++ < 2)
            {
                fixedRate += fixedRateUpdate;
                editor.Update(fixedRate / (float)SDL.NsPerSecond);
            }

            var windowFocus = (SDL.GetWindowFlags(window) & WindowFlags.InputFocus) != 0;
            if (windowFocus && fixedUpdates == 0 && fixedRate < cur && cur < fixedRate + fixedRateUpdate)
                editor.Update(cur / (float)SDL.NsPerSecond, false);

            var draws = editor.Draw();
            if (!SDL.GLSwapWindow(window))
                throw new InvalidOperationException($"Unable to swap framebuffer: {SDL.GetError()}");

            using (var snapshot = TempList.Create<IDisposable>())
            {
                lock (schedulerLock)
                {
                    snapshot.AddRange(scheduledActions);
                    scheduledActions.Clear();
                }

                foreach (var action in snapshot) action.Dispose();
            }

            var active = SDL.GetTicksNS() - cur;
            var sleepTime = (windowFocus ? targetFrame : fixedRateUpdate) - active;

            if (sleepTime > 0) SDL.DelayNS(sleepTime);
            else Bass.UpdateThreads = 1;

            var frameTime = cur - prev;
            prev = cur;
            if (lastStat + statsUpdate > cur) return;

            if (sleepTime > 0) Bass.UpdateThreads = 0;

            avActive = (active + avActive) / 2;
            longest = ulong.Max(frameTime, longest);

            buildStatsMessage(editor, frameTime, avActive, longest, draws);

            lastStat = cur;
        };

        var state = (false, GCHandle.Alloc(redraw));
        EventFilter filter = (nint s, ref Event e) =>
        {
            ref var localState = ref s.AsRef<(bool, GCHandle)>();

            if (e.Type is EventType.Quit) return localState.Item1 = true;
            if (e.Type is not EventType.WindowExposed || !SDL.IsMainThread()) return false;

            ((Action<bool>)localState.Item2.Target!)(false);
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
        state.Item2.Free();
    }

    static void buildStatsMessage(Editor editor, ulong av, ulong avActive, ulong longest, int draws)
    {
        if (!editor.statsLabel.Visible) return;

        const float ticks = SDL.NsPerSecond, millis = SDL.NsPerMs;

        using var result = StringHelper.Interpolate(CultureInfo.InvariantCulture,
            $"{float.Round(ticks / av):f0}/{float.Round(ticks / avActive):f0}fps (act:{avActive / millis:f2} avg:{av / millis:f2} hi:{longest / millis:f2})\n{draws} draws\n{MemoryDiagnostics.TotalUndisposedAllocationCount} off-heap buffers");

        editor.statsLabel.Text = result.AsReadOnlySpan();
    }

    #endregion

    #region Scheduling

    static readonly Lock schedulerLock = new();
    static readonly List<IDisposable> scheduledActions = [];

    public static ValueTask Schedule<TState>(Action<TState> action, TState state = default)
    {
        if (SDL.IsMainThread())
        {
            action(state);
            return ValueTask.CompletedTask;
        }

        var tcs = ValueTaskSourceHolder<TState>.Get(action, state);
        lock (schedulerLock) scheduledActions.Add(tcs);

        return tcs.Task;
    }

    #endregion

    #region Error Handling

    const string DefaultLogPath = "logs";

    static readonly Lock errorHandlerLock = new();
    static bool insideErrorHandler;

    static void setupLogging(string logsPath = null, string commonLogFilename = null)
    {
        logsPath ??= DefaultLogPath;

        var tracePath = Path.Combine(logsPath, commonLogFilename ?? "trace.log");
        var exceptionPath = Path.Combine(logsPath, commonLogFilename ?? "exception.log");
        var crashPath = Path.Combine(logsPath, commonLogFilename ?? "crash.log");

        if (!Directory.Exists(logsPath)) Directory.CreateDirectory(logsPath);
        else if (File.Exists(exceptionPath)) File.Delete(exceptionPath);

        var domain = AppDomain.CurrentDomain;
        domain.FirstChanceException += (_, e) => logError(e.Exception, exceptionPath, false);
        domain.UnhandledException += (_, e) => logError((Exception)e.ExceptionObject, crashPath, e.IsTerminating);

        SDL.SetLogPriorities(SDL.LogPriority.Trace);

        SDL.SetAppMetadataProperty(SDL.Props.AppMetadataNameString, Name);
        SDL.SetAppMetadataProperty(SDL.Props.AppMetadataVersionString, Version.ToString());
        SDL.SetAppMetadataProperty(SDL.Props.AppMetadataURLString, Repository);
    }

    static void logError(Exception e, string filename, bool show)
    {
        lock (errorHandlerLock)
        {
            if (insideErrorHandler) return;

            insideErrorHandler = true;

            using StreamWriter w = new(Path.Combine(Environment.CurrentDirectory, filename), true);
            try
            {
                w.Write(DateTimeOffset.Now + " - ");
                w.WriteLine(e);
                w.WriteLine();

                w.Flush();

                if (!show) return;

                using MessageBoxData data = new(MessageBoxFlags.Error,
                    0,
                    FullName,
                    $"An error occurred:\n{e.Message} ({e.GetType().Name})\n\nClick Ok if you want to receive and invitation to a Discord server where you can get help with this problem.",
                    [
                        new(MessageBoxButtonFlags.EscapekeyDefault, 1, "Cancel"),
                        new(MessageBoxButtonFlags.ReturnkeyDefault, 0, "OK")
                    ]);

                if (!SDL.ShowMessageBox(in data, out var id))
                    throw new InvalidOperationException($"Cannot create message box: {SDL.GetError()}");

                if (id == 0) SDL.OpenURL(DiscordUrl);
            }
            catch (Exception e2)
            {
                w.Write(DateTimeOffset.Now + " - ");
                w.WriteLine(e2);
                w.WriteLine();
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