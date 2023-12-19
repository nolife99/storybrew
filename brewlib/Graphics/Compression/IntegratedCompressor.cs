using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BrewLib.Data;
using BrewLib.Util;

namespace BrewLib.Graphics.Compression;

public class IntegratedCompressor : ImageCompressor
{
    public IntegratedCompressor(string utilityPath = null) : base(utilityPath)
        => container = new AssemblyResourceContainer(typeof(IntegratedCompressor).Assembly, "BrewLib");

    protected override void compress(Argument arg, bool useLossy)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!File.Exists(arg.path)) throw new FileNotFoundException(arg.path);

        UtilityName = Environment.Is64BitOperatingSystem && useLossy ? "pngquant.exe" : "oxipng32.exe";
        var path = GetUtility();
        ensureTool();

        tasks.Add(Task.Run(() =>
        {
            using var localProc = Process.Start(new ProcessStartInfo(path, appendArgs(arg.path, useLossy, arg.lossy, arg.lossless))
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(UtilityPath),
                RedirectStandardError = true
            });

            try
            {
                using var errorStream = localProc.StandardError;
                var error = errorStream.ReadToEnd();

                if (!string.IsNullOrEmpty(error) && localProc.ExitCode != 0) Trace.TraceError($"Image compression closed with code {localProc.ExitCode}: {error}");
            }
            finally
            {
                localProc.WaitForExit();
            }
        }));
    }
    protected override string appendArgs(string path, bool useLossy, LossyInputSettings lossy, LosslessInputSettings lossless)
    {
        var input = string.Format(CultureInfo.InvariantCulture, "\"{0}\"", path);
        StringBuilder str = new();

        if (Environment.Is64BitOperatingSystem && useLossy)
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
        if (!File.Exists(utility)) File.WriteAllBytes(GetUtility(), container.GetBytes(utilName, ResourceSource.Embedded | ResourceSource.Relative));
        toCleanup.Add(utility);
    }

    readonly HashSet<string> toCleanup = [];
    readonly List<Task> tasks = [];

    protected override async void Dispose(bool disposing)
    {
        if (!disposed) await Task.WhenAll(tasks).ContinueWith(task =>
        {
            base.Dispose(disposing);
            Parallel.ForEach(toCleanup, clean => PathHelper.SafeDelete(clean));

            if (disposing)
            {
                tasks.Clear();
                toCleanup.Clear();
            }
        }).ConfigureAwait(false);
    }
}