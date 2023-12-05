using BrewLib.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace BrewLib.Util.Compression
{
    public class IntegratedCompressor : ImageCompressor
    {
        public IntegratedCompressor(string utilityPath = null) : base(utilityPath) 
            => container = new AssemblyResourceContainer(typeof(IntegratedCompressor).Assembly, "BrewLib");

        protected override void compress(Argument arg, bool useLossy)
        {
            if (!File.Exists(arg.path)) return;

            UtilityName = Environment.Is64BitOperatingSystem && useLossy ? "pngquant.exe" : "oxipng32.exe";
            ensureTool();

            try
            {
                process ??= Process.Start(new ProcessStartInfo(GetUtility(), appendArgs(arg.path, useLossy, arg.lossy, arg.lossless))
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(UtilityPath),
                    RedirectStandardError = true
                });

                var errorStream = process.StandardError;
                var error = errorStream.ReadToEnd();
                errorStream.Dispose();

                if (!string.IsNullOrEmpty(error) && process.ExitCode != 0) throw new OperationCanceledException($"Image compression closed with code {process.ExitCode}: {error}");
            }
            finally
            {
                ensureStop();
            }
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
            if (!File.Exists(utility))
            {
                var src = container.GetBytes(utilName, ResourceSource.Embedded | ResourceSource.Relative);
                File.WriteAllBytes(utility, src);
            }
            toCleanup.Add(utility);
        }
        protected override string GetUtility() => Path.Combine(UtilityPath, utilName);

        readonly HashSet<string> toCleanup = [];
        protected override void Dispose(bool disposing)
        {
            if (disposed) return;

            base.Dispose(disposing);
            foreach (var clean in toCleanup) PathHelper.SafeDelete(clean);

            toCleanup.Clear();
        }
    }
}