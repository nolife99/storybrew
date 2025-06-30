namespace StorybrewEditor;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using BrewLib.Audio;
using BrewLib.Util;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Util;
using Utils = OpenTK.Core.Utils;
using Vector = System.Numerics.Vector;

public static class Program
{
    public const string Name = "storybrew editor", Repository = "Damnae/storybrew",
        DiscordUrl = "https://discord.gg/0qfFOucX93QDNVN7";

    public static readonly Version Version = typeof(Editor).Assembly.GetName().Version;

    public static readonly string FullName = $"{Name} {Version} ({Repository})";

    static Process currentProcess;

    public static AudioManager AudioManager { get; private set; }
    public static Settings Settings { get; private set; }

    static void Main(string[] args)
    {
        if (args.Length != 0 && handleArguments(args)) return;

        MainThread = Thread.CurrentThread;
        currentProcess = Process.GetCurrentProcess();

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

    #region Editor

    public static string Stats { get; private set; }

    static void startEditor()
    {
        Settings = new();
        Updater.NotifyEditorRun();
        Native.MainThreadScheduler = Schedule;

        var displayDevice = Monitors.GetPrimaryMonitor();
        using (var window = createWindow(displayDevice))
        {
            Trace.Write(Environment.OSVersion);

            using Editor editor = new(window);

            void refreshCallback()
            {
                editor.Draw();
                window.Context.SwapBuffers();
            }

            window.Refresh += refreshCallback;

            using (NetHelper.Client = new())
            {
                NetHelper.Client.DefaultRequestHeaders.Add("user-agent", Name);
                editor.Initialize(displayDevice);

                Native.SetWindowIcon(typeof(Editor), "icon.ico");

                using (AudioManager = createAudioManager())
                    runMainLoop(window,
                        editor,
                        1f / (Settings.UpdateRate > 0 ? Settings.UpdateRate : displayDevice.CurrentVideoMode.RefreshRate),
                        1f / (Settings.FrameRate > 0 ? Settings.FrameRate : displayDevice.CurrentVideoMode.RefreshRate));
            }

            window.Refresh -= refreshCallback;
        }

        Settings.Save();
    }

    static NativeWindow createWindow(MonitorInfo displayDevice)
    {
        const ContextFlags debugContext =
#if DEBUG
            ContextFlags.Debug | ContextFlags.ForwardCompatible;
#else
            ContextFlags.Debug | ContextFlags.ForwardCompatible;

        GLFW.WindowHint(WindowHintBool.ContextNoError, true);
#endif

        NativeWindow window = new(new()
        {
            Flags = debugContext,
            CurrentMonitor = displayDevice.Handle,
            Title = Name,
            StartVisible = false,
            DepthBits = 0,
            StencilBits = 0
        });

        Native.InitializeHandle(window);

        if (Vector.IsHardwareAccelerated) Trace.WriteLine($"SIMD Vector Alignment: {Vector<byte>.Count} bytes");

        return window;
    }

    static AudioManager createAudioManager()
    {
        AudioManager audioManager = new() { Volume = Settings.Volume };

        Settings.Volume.OnValueChanged += (_, _) => audioManager.Volume = Settings.Volume;

        return audioManager;
    }

    static void runMainLoop(NativeWindow window, Editor editor, float fixedRateUpdate, float targetFrame)
    {
        float prev = 0, fixedRate = 0, av = 0, avActive = 0, longest = 0, lastStat = 0, statsUpdate = targetFrame * 5;

        var windowContext = window.Context;
        while (!window.IsExiting)
        {
            var cur = (float)GLFW.GetTime();
            var fixedUpdates = 0;

            window.ProcessEvents(0);
            AudioManager.Update();

            while (cur - fixedRate >= fixedRateUpdate && fixedUpdates++ < 2)
            {
                fixedRate += fixedRateUpdate;
                editor.Update(fixedRate);
            }

            if (!window.Exists || window.IsExiting) return;

            var draws = editor.Draw();
            windowContext.SwapBuffers();

            window.IsVisible = true;
            while (scheduledActions.TryDequeue(out var action))
                try
                {
                    action.Action();
                    action.Task.TrySetResult();
                }
                catch (Exception e)
                {
                    Trace.TraceError($"Scheduled task {action.Action.Method}:\n{e}");

                    action.Task.TrySetException(e);
                }

            var active = (float)GLFW.GetTime() - cur;
            var sleepTime = (window.IsFocused ? targetFrame : fixedRateUpdate) - active;

            if (sleepTime > 0) Utils.AccurateSleep(sleepTime, 8);

            var frameTime = cur - prev;
            prev = cur;
            if (lastStat + statsUpdate > cur) continue;

            av = (frameTime + av) * .5f;
            avActive = (active + avActive) * .5f;
            longest = float.Max(frameTime, longest);

            buildStatsMessage(editor, av, avActive, longest, draws);

            longest = 0;
            lastStat = cur;
        }
    }

    static void buildStatsMessage(Editor editor, float av, float avActive, float longest, int draws)
    {
        if (!editor.statsLabel.Visible) return;

        using var result = TempList.Create<char>(128);

        result.AppendFormatted(1 / av, "f0", CultureInfo.CurrentCulture);
        result.Add('/');
        result.AppendFormatted(1 / avActive, "f0", CultureInfo.CurrentCulture);

        result.AddRange("fps (act:".AsSpan());
        result.AppendFormatted(avActive * 1000, "f2", CultureInfo.CurrentCulture);

        result.AddRange(" avg:".AsSpan());
        result.AppendFormatted(av * 1000, "f2", CultureInfo.CurrentCulture);

        result.AddRange(" hi:".AsSpan());
        result.AppendFormatted(longest * 1000, "f2", CultureInfo.CurrentCulture);

        result.AddRange(")\n".AsSpan());

        result.AppendFormatted(draws, "", CultureInfo.CurrentCulture);
        result.AddRange(" draws".AsSpan());
        result.Add('\n');

        editor.statsLabel.Text = result.AsReadOnlySpan();
    }

    #endregion

    #region Scheduling

    static readonly ConcurrentQueue<(Action Action, TaskCompletionSource Task)> scheduledActions = [];

    public static Thread MainThread { get; private set; }

    public static Task Schedule(Action action)
    {
        if (Thread.CurrentThread == MainThread)
        {
            action();
            return Task.CompletedTask;
        }

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

        Timer timer = new(s => ((TraceListener)s)!.Flush(), listener, 5000, 1000);

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

                Trace.Flush();

                if (show) Environment.FailFast(e.Message, e);
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