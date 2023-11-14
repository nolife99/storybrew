using BrewLib.Data;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace BrewLib.Util.Compression
{
    public class PngCompressor : ImageCompressor
    {
        public PngCompressor(string utilityPath = null) : base(utilityPath) 
            => container = new AssemblyResourceContainer(typeof(PngCompressor).Assembly, "BrewLib");

        protected override void doCompress()
        {
            UtilityName = Environment.Is64BitOperatingSystem ? "oxipng.exe" : "truepng.exe";
            ensureTool();

            var startInfo = new ProcessStartInfo(GetUtility())
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(UtilityPath),
                RedirectStandardInput = true,
                RedirectStandardError = true,
            };

            foreach (var arg in toCompress) if (File.Exists(arg.path)) try
            {
                startInfo.Arguments = appendArgs(arg.path, false, null, arg.lossless);
                process ??= Process.Start(startInfo);
                var error = process.StandardError.ReadToEnd();
                if (!string.IsNullOrEmpty(error) && process.ExitCode != 0) throw new OperationCanceledException($"Image compression closed with code {process.ExitCode}: {error}");
            }
            finally
            {
                ensureStop();
            }

            UtilityName = Environment.Is64BitOperatingSystem ? "pngquant.exe" : "truepng.exe";
            ensureTool();
            startInfo.FileName = GetUtility();

            foreach (var arg in lossyCompress) if (File.Exists(arg.path)) try
            {
                startInfo.Arguments = appendArgs(arg.path, true, arg.lossy, null);
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

            if (Environment.Is64BitOperatingSystem)
            {
                if (useLossy)
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
                    if (lossless != null)
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
                if (lossless != null)
                {
                    var lvl = (byte)lossless.OptimizationLevel;
                    str.AppendFormat(CultureInfo.InvariantCulture, " /o{0} ", lvl > 4 ? 4 : lvl);
                    str.AppendFormat(CultureInfo.InvariantCulture, " {0} ", lossless.CustomInputArgs);
                }
                str.AppendFormat(CultureInfo.InvariantCulture, "/md remove all /a1 -Z /y /out {0} {0}", input);
            }
            return str.ToString();
        }
        protected override void ensureTool()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PngCompressor));
            var exe = container.GetBytes(utilName, ResourceSource.Embedded | ResourceSource.Relative);
            File.WriteAllBytes(GetUtility(), exe);
            toCleanup.Add(GetUtility());
        }
    }
}