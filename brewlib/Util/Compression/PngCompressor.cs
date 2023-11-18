using BrewLib.Data;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace BrewLib.Util.Compression
{
    public class PngCompressor : ImageCompressor
    {
        public PngCompressor(string utilityPath = null) : base(utilityPath) 
            => container = new AssemblyResourceContainer(typeof(PngCompressor).Assembly, "BrewLib");

        protected override async Task doCompress()
        {
            UtilityName = Environment.Is64BitOperatingSystem ? "oxipng.exe" : "oxipng32.exe";
            ensureTool();

            var startInfo = new ProcessStartInfo(GetUtility())
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(UtilityPath),
                RedirectStandardInput = true,
                RedirectStandardError = true,
            };

            await Task.Factory.StartNew(() =>
            { 
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
            }, TaskCreationOptions.AttachedToParent);

            UtilityName = Environment.Is64BitOperatingSystem ? "pngquant.exe" : "oxipng32.exe";
            ensureTool();
            startInfo.FileName = GetUtility();

            await Task.Factory.StartNew(() =>
            {
                foreach (var arg in lossyCompress) if (File.Exists(arg.path)) try
                {
                    startInfo.Arguments = appendArgs(arg.path, true, arg.lossy, null);
                    process ??= Process.Start(startInfo);
                    var error = process.StandardError.ReadToEnd();
                    if (!string.IsNullOrEmpty(error) && process.ExitCode != 0) throw new OperationCanceledException($"Image compression failed with code {process.ExitCode}: {error}");
                }
                finally
                {
                    ensureStop();
                }
            }, TaskCreationOptions.AttachedToParent);
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
                var lvl = lossless != null ? (byte)lossless.OptimizationLevel : 7;
                str.AppendFormat(CultureInfo.InvariantCulture, " -o {0} ", lvl > 6 ? "max" : lvl.ToString(CultureInfo.InvariantCulture));
                str.AppendFormat(CultureInfo.InvariantCulture, "−s -a {0}", input);
            }
            return str.ToString();
        }
        protected override void ensureTool()
        {
            ObjectDisposedException.ThrowIf(disposed, typeof(PngCompressor));
            var path = GetUtility();
            File.WriteAllBytes(path, container.GetBytes(utilName, ResourceSource.Embedded | ResourceSource.Relative));
            toCleanup.Add(path);
        }
    }
}