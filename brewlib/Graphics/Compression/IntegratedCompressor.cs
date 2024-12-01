namespace BrewLib.Graphics.Compression;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Data;
using Util;

public class IntegratedCompressor : ImageCompressor
{
    readonly List<Task> tasks = [];
    readonly HashSet<string> toCleanup = [];

    public IntegratedCompressor(string utilityPath = null) : base(utilityPath)
        => container = new AssemblyResourceContainer(typeof(Argument).Assembly, "BrewLib");

    protected override void InternalCompress(Argument arg, bool useLossy)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!File.Exists(arg.path)) throw new FileNotFoundException(arg.path);

        UtilityName = Environment.Is64BitOperatingSystem && useLossy ? "pngquant.exe" : "oxipng32.exe";
        var path = GetUtility();
        ensureTool();

        tasks.Add(Task.Run(() =>
        {
            using var localProc = Process.Start(
                new ProcessStartInfo(path, appendArgs(arg.path, useLossy, arg.lossy, arg.lossless))
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(UtilityPath),
                    RedirectStandardError = true
                });

            using (var errorStream = localProc.StandardError)
            {
                var error = errorStream.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(error) && localProc.ExitCode != 0)
                    Trace.TraceError($"Image compression - Code {localProc.ExitCode}: {error}");
            }

            localProc.WaitForExit();
        }));
    }
    protected override string appendArgs(string path, bool useLossy, LossyInputSettings lossy, LosslessInputSettings lossless)
    {
        var input = string.Format(CultureInfo.InvariantCulture, "\"{0}\"", path);
        var str = StringHelper.StringBuilderPool.Retrieve();

        if (Environment.Is64BitOperatingSystem && useLossy)
        {
            str.AppendFormat(CultureInfo.InvariantCulture, "{0} -o {0} -f --skip-if-larger --strip", input);
            if (lossy is null) return str.ToString();
            if (lossy.MinQuality >= 0 && lossy.MaxQuality is > 0 and <= 100)
                str.Append(CultureInfo.InvariantCulture, $" --quality {lossy.MinQuality}-{lossy.MaxQuality} ");

            if (lossy.Speed is > 0 and <= 10) str.Append(CultureInfo.InvariantCulture, $" -s{lossy.Speed} ");

            str.Append(CultureInfo.InvariantCulture, $" {lossy.CustomInputArgs} ");
        }
        else
        {
            var lvl = lossless?.OptimizationLevel ?? 4;
            str.Append(CultureInfo.InvariantCulture, $" -o {(lvl > 6 ? "max" : lvl.ToString(CultureInfo.InvariantCulture))} ");
            str.Append(CultureInfo.InvariantCulture, $" {lossless?.CustomInputArgs} ");
            str.Append(CultureInfo.InvariantCulture, $"−s -a {input}");
        }

        var output = str.ToString();
        StringHelper.StringBuilderPool.Release(str);
        return output;
    }
    protected override void ensureTool()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var utility = GetUtility();
        if (!File.Exists(utility))
            File.WriteAllBytes(GetUtility(), container.GetBytes(utilName, ResourceSource.Embedded | ResourceSource.Relative));

        toCleanup.Add(utility);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposed) return;
        using (var all = Task.WhenAll(tasks)) all.Wait();

        base.Dispose(disposing);
        foreach (var clean in toCleanup) File.Delete(clean);
    }
}