using BrewLib.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

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
            var utility = GetUtility();

            var startInfo = new ProcessStartInfo(utility)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(UtilityPath),
                RedirectStandardInput = true,
                RedirectStandardError = true
            };

            try
            {
                startInfo.Arguments = appendArgs(arg.path, useLossy, arg.lossy, arg.lossless);
                process ??= Process.Start(startInfo);

                var error = process.StandardError.ReadToEnd();
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
            var str = new StringBuilder();

            if (Environment.Is64BitOperatingSystem && useLossy)
            {
                str.AppendFormat(CultureInfo.InvariantCulture, "{0} -o {0} -f --skip-if-larger --strip", input);
                if (lossy != null)
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
                var lvl = lossless != null ? (byte)lossless.OptimizationLevel : 7;
                str.AppendFormat(CultureInfo.InvariantCulture, " -o {0} ", lvl > 6 ? "max" : lvl.ToString(CultureInfo.InvariantCulture));
                str.AppendFormat(CultureInfo.InvariantCulture, " {0} ", lossless?.CustomInputArgs);
                str.AppendFormat(CultureInfo.InvariantCulture, "−s -a {0}", input);
            }
            return str.ToString();
        }

        protected override void ensureTool()
        {
            ObjectDisposedException.ThrowIf(disposed, typeof(IntegratedCompressor));

            var utility = GetUtility();
            if (!File.Exists(utility))
            {
                File.WriteAllBytes(utility, container.GetBytes(utilName, ResourceSource.Embedded | ResourceSource.Relative));
                toCleanup.Add(utility);
            }
        }
        protected override string GetUtility() => Path.Combine(UtilityPath, utilName);

        readonly List<string> toCleanup = [];
        protected override void Dispose(bool disposing)
        {
            if (disposed) return;
            base.Dispose(disposing);

            for (var i = 0; i < toCleanup.Count; ++i) if (File.Exists(toCleanup[i])) File.Delete(toCleanup[i]);
            toCleanup.Clear();
        }
    }
}