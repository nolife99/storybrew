namespace BrewLib.Graphics.Compression;

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using IO;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;
using Util;

public class IntegratedCompressor : ImageCompressor
{
    readonly PooledList<char> errorData = new();
    readonly PooledList<Task> tasks = new();
    readonly PooledHashSet<string> toCleanup = new();

    public IntegratedCompressor(string utilityPath = null) : base(utilityPath)
        => container = new AssemblyResourceContainer(typeof(Argument).Assembly, "BrewLib");

    protected override void InternalCompress(Argument arg, bool useLossy)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!File.Exists(arg.path)) throw new FileNotFoundException(arg.path);

        UtilityName = Environment.Is64BitOperatingSystem && useLossy ? "pngquant.exe" : "oxipng32.exe";
        var path = GetUtility();
        ensureTool();

        var process = Process.Start(new ProcessStartInfo(path, appendArgs(arg.path, useLossy, arg.lossy, arg.lossless))
        {
            CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(UtilityPath), RedirectStandardError = true
        });

        process.BeginErrorReadLine();

        process.ErrorDataReceived += (_, e) => errorData.AddRange(e.Data.AsSpan());

        tasks.Add(process.WaitForExitAsync()
            .ContinueWith(_ =>
            {
                if (!errorData.AsReadOnlySpan().IsEmpty && process.ExitCode != 0)
                    Trace.TraceError($"Image compression - Code {process.ExitCode}: {errorData.AsReadOnlySpan()}");
            }));
    }

    protected override string appendArgs(string path,
        bool useLossy,
        LossyInputSettings lossy,
        LosslessInputSettings lossless)
    {
        var input = string.Format(CultureInfo.InvariantCulture, "\"{0}\"", path);
        var str = StringHelper.StringBuilderPool.Retrieve();

        if (Environment.Is64BitOperatingSystem && useLossy)
        {
            str.AppendFormat(CultureInfo.InvariantCulture, "{0} -o {0} -f --skip-if-larger --strip", input);
            if (lossy is null)
            {
                var temp = str.ToString();
                StringHelper.StringBuilderPool.Release(str);
                return temp;
            }

            if (lossy.MinQuality >= 0 && lossy.MaxQuality is >= 0 and <= 100)
                str.Append(CultureInfo.InvariantCulture, $" --quality {lossy.MinQuality}-{lossy.MaxQuality} ");

            if (lossy.Speed is > 0 and <= 10) str.Append(CultureInfo.InvariantCulture, $" -s{lossy.Speed} ");

            str.Append(CultureInfo.InvariantCulture, $" {lossy.CustomInputArgs} ");
        }
        else
        {
            var lvl = lossless?.OptimizationLevel ?? 4;
            str.Append(CultureInfo.InvariantCulture,
                $" -o {(lvl > 6 ? "max" : lvl.ToString(CultureInfo.InvariantCulture))} ");

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
        {
            using var source = container.GetStream(utilName, ResourceSource.Embedded);
            using var dest = File.Create(utility);
            source.CopyTo(dest);
        }

        toCleanup.Add(utility);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposed) return;

        Task.WhenAll(tasks).Wait();

        if (disposing)
        {
            tasks.Dispose();
            toCleanup.Dispose();
        }

        base.Dispose(disposing);

        // foreach (var clean in toCleanup) File.Delete(clean);
    }
}