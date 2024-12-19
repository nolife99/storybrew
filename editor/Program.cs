namespace StorybrewEditor;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BrewLib.Audio;
using BrewLib.Util;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Util;

public static class Program
{
    public const string Name = "storybrew editor", Repository = "Damnae/storybrew",
        DiscordUrl = "https://discord.gg/0qfFOucX93QDNVN7";

    public static readonly Version Version = typeof(Editor).Assembly.GetName().Version;
    public static readonly string FullName = $"{Name} {Version} ({Repository})";

    public static AudioManager AudioManager { get; set; }
    public static Settings Settings { get; set; }

    static void Main(string[] args)
    {
        if (args.Length != 0 && handleArguments(args)) return;

        setupLogging();
        startEditor();
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

    public static void ForceFullGC()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
    }

    #region Editor

    public static string Stats { get; private set; }

    static void startEditor()
    {
        Settings = new();
        Updater.NotifyEditorRun();

        var displayDevice = findDisplayDevice();
        using (var window = createWindow(displayDevice))
        {
            Trace.Write(Environment.OSVersion);
            Trace.Write(" / Handle: 0x");
            Trace.WriteLine(Native.MainWindowHandle.ToString($"X{nint.Size}", CultureInfo.InvariantCulture));

            using Editor editor = new(window);
            window.Refresh += () =>
            {
                editor.Draw();
                window.Context.SwapBuffers();
            };

            using (NetHelper.Client = new())
            {
                NetHelper.Client.DefaultRequestHeaders.Add("user-agent", Name);
                editor.Initialize(displayDevice);

                using (AudioManager = createAudioManager())
                    runMainLoop(window,
                        editor,
                        1d / (Settings.UpdateRate > 0 ? Settings.UpdateRate : displayDevice.CurrentVideoMode.RefreshRate),
                        1d / (Settings.FrameRate > 0 ? Settings.FrameRate : displayDevice.CurrentVideoMode.RefreshRate));
            }
        }

        Settings.Save();
    }

    static MonitorInfo findDisplayDevice()
    {
        try
        {
            return Monitors.GetPrimaryMonitor();
        }
        catch (Exception e)
        {
            Trace.TraceWarning($"Failed to use default display device: {e}");
            foreach (var monitor in Monitors.GetMonitors()) return monitor;
        }

        throw new InvalidOperationException("Failed to find a display device");
    }

    static NativeWindow createWindow(MonitorInfo displayDevice)
    {
        const ContextFlags debugContext =
#if DEBUG
            ContextFlags.Debug | ContextFlags.ForwardCompatible;
#else
            ContextFlags.ForwardCompatible;
#endif

        if ((debugContext & ContextFlags.Debug) == 0) GLFW.WindowHint(WindowHintBool.ContextNoError, true);

        NativeWindow window = new(new()
        {
            Flags = debugContext,
            Profile = ContextProfile.Core,
            CurrentMonitor = displayDevice.Handle,
            Title = Name,
            StartVisible = false,
            DepthBits = 0,
            StencilBits = 0
        });

        Native.InitializeHandle(window);
        Native.SetWindowIcon(typeof(Editor), "icon.ico");

        return window;
    }

    static AudioManager createAudioManager()
    {
        AudioManager audioManager = new(Native.MainWindowHandle) { Volume = Settings.Volume };

        Settings.Volume.OnValueChanged += (_, _) => audioManager.Volume = Settings.Volume;

        return audioManager;
    }

    static void runMainLoop(NativeWindow window, Editor editor, double fixedRateUpdate, double targetFrame)
    {
        double prev = 0, fixedRate = 0, av = 0, avActive = 0, longest = 0, lastStat = 0;
        while (!window.IsExiting)
        {
            var cur = GLFW.GetTime();
            var fixedUpdates = 0;

            window.NewInputFrame();
            GLFW.PollEvents();

            AudioManager.Update();

            while (cur - fixedRate >= fixedRateUpdate && fixedUpdates++ < 2)
            {
                fixedRate += fixedRateUpdate;
                editor.Update((float)fixedRate);
            }

            if (!window.Exists || window.IsExiting) return;

            editor.Draw();
            window.Context.SwapBuffers();

            window.IsVisible = true;
            while (scheduledActions.TryDequeue(out var action))
                try
                {
                    action.Item1();
                    action.Item2.SetResult();
                }
                catch (Exception e)
                {
                    Trace.TraceError($"Scheduled task {action.Item1.Method}:\n{e}");
                    action.Item2.SetException(e);
                }

            var active = GLFW.GetTime() - cur;
            var sleepTime = (window.IsFocused ? targetFrame : fixedRateUpdate) - active;
            if (sleepTime > 0) Thread.Sleep((int)(sleepTime * 1000));

            var frameTime = cur - prev;
            prev = cur;
            if (lastStat + .1 > cur) continue;

            av = (frameTime + av) * .5;
            avActive = (active + avActive) * .5;
            longest = Math.Max(frameTime, longest);

            Stats = $"fps:{1 / av:0}/{1 / avActive:0} (act:{avActive * 1000:0} avg:{av * 1000:0} hi:{longest * 1000:0})";

            longest = 0;
            lastStat = cur;
        }
    }

    #endregion

    #region Scheduling

    static readonly ConcurrentQueue<(Action, TaskCompletionSource)> scheduledActions = [];

    public static Task Schedule(Action action)
    {
        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        scheduledActions.Enqueue((action, tcs));

        return tcs.Task;
    }

    #endregion

    #region Error Handling

    const string DefaultLogPath = "logs";

    static readonly Lock errorHandlerLock = new();
    static volatile bool insideErrorHandler;

    static void setupLogging(string logsPath = null, string commonLogFilename = null)
    {
        logsPath ??= DefaultLogPath;
        var tracePath = Path.Combine(logsPath, commonLogFilename ?? "trace.log");
        var exceptionPath = Path.Combine(logsPath, commonLogFilename ?? "exception.log");
        var crashPath = Path.Combine(logsPath, commonLogFilename ?? "crash.log");

        if (!Directory.Exists(logsPath)) Directory.CreateDirectory(logsPath);
        else if (File.Exists(exceptionPath)) File.Delete(exceptionPath);

        TextWriterTraceListener listener = new(File.CreateText(tracePath), Name);
        var domain = AppDomain.CurrentDomain;

        domain.FirstChanceException += (_, e) => logError(e.Exception, exceptionPath, false);
        domain.UnhandledException += (_, e) => logError((Exception)e.ExceptionObject, crashPath, true);

        Trace.Listeners.Add(listener);
        Trace.WriteLine($"{FullName}\n");

        Timer timer = new(s => Unsafe.As<TraceListener>(s)!.Flush(), listener, 5000, 1000);
        domain.ProcessExit += (_, _) => timer.Dispose();
    }

    static void logError(Exception e, string filename, bool show)
    {
        lock (errorHandlerLock)
        {
            if (insideErrorHandler) return;
            insideErrorHandler = true;

            try
            {
                using (StreamWriter w = new(Path.Combine(Environment.CurrentDirectory, filename), true))
                {
                    w.Write(DateTimeOffset.Now + " - ");
                    w.WriteLine(e);
                    w.WriteLine();
                }

                if (show) Environment.FailFast("Unhandled exception", e);
            }
            catch (Exception e2)
            {
                Trace.WriteLine(e2.Message);
            }
            finally
            {
                insideErrorHandler = false;
            }
        }
    }

    #endregion
}