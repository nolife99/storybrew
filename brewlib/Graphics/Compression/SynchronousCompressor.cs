namespace BrewLib.Graphics.Compression;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data;
using Util;

public class SynchronousCompressor : ImageCompressor
{
    List<string> toCleanup = [];
    List<Argument> toCompress = [], lossyCompress = [];

    public SynchronousCompressor(string utilityPath = null) : base(utilityPath)
        => container = new AssemblyResourceContainer(typeof(SynchronousCompressor).Assembly, "BrewLib");

    protected override void InternalCompress(Argument arg, bool useLossy) => (useLossy ? lossyCompress : toCompress).Add(arg);
    async Task doCompress()
    {
        ProcessStartInfo startInfo = new("")
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(UtilityPath),
            RedirectStandardInput = true,
            RedirectStandardError = true
        };

        if (toCompress.Count != 0)
            using (Task losslessTask = new(async () =>
            {
                UtilityName = "oxipng32.exe";
                startInfo.FileName = GetUtility();
                ensureTool();

                foreach (var arg in toCompress.Where(arg => File.Exists(arg.path)))
                    try
                    {
                        startInfo.Arguments = appendArgs(arg.path, false, null, arg.lossless);
                        process ??= Process.Start(startInfo);
                        var error = await process.StandardError.ReadToEndAsync();
                        if (!string.IsNullOrEmpty(error) && process.ExitCode != 0)
                            throw new OperationCanceledException(
                                $"Image compression closed with code {process.ExitCode}: {error}");
                    }
                    finally
                    {
                        ensureStop();
                    }
            }))
            {
                losslessTask.Start();
                await losslessTask;
            }

        if (lossyCompress.Count != 0)
            using (Task lossyTask = new(async () =>
            {
                UtilityName = Environment.Is64BitOperatingSystem ? "pngquant.exe" : "oxipng32.exe";
                startInfo.FileName = GetUtility();
                ensureTool();

                foreach (var arg in lossyCompress.Where(arg => File.Exists(arg.path)))
                    try
                    {
                        startInfo.Arguments = appendArgs(arg.path, true, arg.lossy, null);
                        process ??= Process.Start(startInfo);
                        var error = await process.StandardError.ReadToEndAsync();
                        if (!string.IsNullOrEmpty(error) && process.ExitCode != 0)
                            throw new OperationCanceledException(
                                $"Image compression failed with code {process.ExitCode}: {error}");
                    }
                    finally
                    {
                        ensureStop();
                    }
            }))
            {
                lossyTask.Start();
                await lossyTask;
            }
    }
    protected override string appendArgs(string path, bool useLossy, LossyInputSettings lossy, LosslessInputSettings lossless)
    {
        var input = string.Format(CultureInfo.InvariantCulture, "\"{0}\"", path);
        StringBuilder str = new();

        if (Environment.Is64BitOperatingSystem)
        {
            if (useLossy)
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
                if (lossless is not null)
                {
                    var lvl = (byte)lossless.OptimizationLevel;
                    str.Append(CultureInfo.InvariantCulture,
                        $" -o {(lvl > 6 ? "max" : lvl.ToString(CultureInfo.InvariantCulture))} ");

                    str.Append(CultureInfo.InvariantCulture, $" {lossless.CustomInputArgs} ");
                }

                str.Append(CultureInfo.InvariantCulture, $"-s -a {input}");
            }
        }
        else
        {
            var lvl = lossless?.OptimizationLevel ?? 4;
            str.Append(CultureInfo.InvariantCulture, $" -o {(lvl > 6 ? "max" : lvl.ToString(CultureInfo.InvariantCulture))} ");
            str.Append(CultureInfo.InvariantCulture, $" {lossless?.CustomInputArgs} ");
            str.Append(CultureInfo.InvariantCulture, $"-s -a {input}");
        }

        return str.ToString();
    }

    protected override void ensureTool()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var utility = GetUtility();
        if (!File.Exists(utility))
        {
            using var src = container.GetStream(utilName, ResourceSource.Embedded | ResourceSource.Relative);
            using FileStream file = new(utility, FileMode.Create, FileAccess.Write, FileShare.Read);
            src.CopyTo(file);
        }

        toCleanup.Add(utility);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposed) return;
        try
        {
            doCompress().Wait();
        }
        finally
        {
            base.Dispose(disposing);
            foreach (var t in toCleanup.Where(File.Exists)) PathHelper.SafeDelete(t);

            if (disposing)
            {
                toCleanup.Clear();
                toCompress.Clear();
                lossyCompress.Clear();

                toCleanup = null;
                toCompress = null;
                lossyCompress = null;
            }
        }
    }
}