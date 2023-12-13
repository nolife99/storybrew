using BrewLib.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrewLib.Util.Compression;

public class SynchronousCompressor : ImageCompressor
{
    public IEnumerable<string> Files => toCompress.Select(s => s.path).Concat(lossyCompress.Select(s => s.path));

    public SynchronousCompressor(string utilityPath = null) : base(utilityPath) 
        => container = new AssemblyResourceContainer(typeof(SynchronousCompressor).Assembly, "BrewLib");

    List<Argument> toCompress = [], lossyCompress = [];
    List<string> toCleanup = [];

    protected override void compress(Argument arg, bool useLossy) => (useLossy ? lossyCompress : toCompress).Add(arg);
    async Task doCompress()
    {
        ProcessStartInfo startInfo = new("")
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(UtilityPath),
            RedirectStandardInput = true,
            RedirectStandardError = true,
        };

        if (toCompress.Count != 0) using (Task losslessTask = new(async () =>
        {
            UtilityName = "oxipng32.exe";
            startInfo.FileName = GetUtility();
            ensureTool();

            foreach (var arg in toCompress) if (File.Exists(arg.path)) try
            {
                startInfo.Arguments = appendArgs(arg.path, false, null, arg.lossless);
                process ??= Process.Start(startInfo);
                var error = await process.StandardError.ReadToEndAsync();
                if (!string.IsNullOrEmpty(error) && process.ExitCode != 0) throw new OperationCanceledException($"Image compression closed with code {process.ExitCode}: {error}");
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

        if (lossyCompress.Count != 0) using (Task lossyTask = new(async () =>
        {
            UtilityName = Environment.Is64BitOperatingSystem ? "pngquant.exe" : "oxipng32.exe";
            startInfo.FileName = GetUtility();
            ensureTool();

            foreach (var arg in lossyCompress) if (File.Exists(arg.path)) try
            {
                startInfo.Arguments = appendArgs(arg.path, true, arg.lossy, null);
                process ??= Process.Start(startInfo);
                var error = await process.StandardError.ReadToEndAsync();
                if (!string.IsNullOrEmpty(error) && process.ExitCode != 0) throw new OperationCanceledException($"Image compression failed with code {process.ExitCode}: {error}");
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
        var str = new StringBuilder();

        if (Environment.Is64BitOperatingSystem)
        {
            if (useLossy)
            {
                str.AppendFormat(CultureInfo.InvariantCulture, "{0} -o {0} -f --skip-if-larger --strip", input);
                if (lossy is not null)
                {
                    if (lossy.MinQuality >= 0 && lossy.MaxQuality > 0 && lossy.MaxQuality <= 100)
                        str.AppendFormat(CultureInfo.InvariantCulture, " --quality {0}-{1} ", lossy.MinQuality, lossy.MaxQuality);

                    if (lossy.Speed > 0 && lossy.Speed <= 10)
                        str.AppendFormat(CultureInfo.InvariantCulture, " -s{0} ", lossy.Speed);

                    str.AppendFormat(CultureInfo.InvariantCulture, " {0} ", lossy.CustomInputArgs);
                }
            }
            else
            {
                if (lossless is not null)
                {
                    var lvl = (byte)lossless.OptimizationLevel;
                    str.AppendFormat(CultureInfo.InvariantCulture, " -o {0} ", lvl > 6 ? "max" : lvl.ToString(CultureInfo.InvariantCulture));
                    str.AppendFormat(CultureInfo.InvariantCulture, " {0} ", lossless.CustomInputArgs);
                }
                str.AppendFormat(CultureInfo.InvariantCulture, "−s -a {0}", input);
            }
        }
        else
        {
            var lvl = lossless is not null ? lossless.OptimizationLevel : 4;
            str.AppendFormat(CultureInfo.InvariantCulture, " -o {0} ", lvl > 6 ? "max" : lvl.ToString(CultureInfo.InvariantCulture));
            str.AppendFormat(CultureInfo.InvariantCulture, " {0} ", lossless?.CustomInputArgs);
            str.AppendFormat(CultureInfo.InvariantCulture, "−s -a {0}", input);
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
        if (!disposed) try
        {
            doCompress().Wait();
        }
        finally
        {
            base.Dispose(disposing);
            for (var i = 0; i < toCleanup.Count; ++i) if (File.Exists(toCleanup[i])) PathHelper.SafeDelete(toCleanup[i]);

            toCleanup.Clear();
            toCompress.Clear();
            lossyCompress.Clear();

            toCleanup = null;
            toCompress = null;
            lossyCompress = null;
        }
    }
}