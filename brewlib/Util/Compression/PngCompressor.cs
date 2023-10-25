using BrewLib.Data;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace BrewLib.Util.Compression
{
    public class PngCompressor : ImageCompressor
    {
        public PngCompressor(string utilityPath = null) : base(utilityPath) 
            => container = new AssemblyResourceContainer(Assembly.GetAssembly(typeof(PngCompressor)), "BrewLib");

        protected override void doCompress(string path, string type, LossyInputSettings lossy, LosslessInputSettings lossless, InputFormat inputFormat = null)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The path that was specified could not be found.", path);
            if (File.Exists(path) && string.IsNullOrEmpty(Path.GetExtension(path)) && string.IsNullOrEmpty(Convert.ToString(inputFormat, CultureInfo.InvariantCulture)))
                throw new ArgumentException("Input format is required for file without extension", nameof(inputFormat));

            try
            {
                UtilityName = Environment.Is64BitOperatingSystem ? type != "lossy" ? "oxipng.exe" : "pngquant.exe" : "truepng.exe";
                ensureTool();

                var startInfo = new ProcessStartInfo(GetUtility(), appendArgs(path, type, lossy, lossless))
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(UtilityPath),
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                if (process is null) process = Process.Start(startInfo);
                var error = process.StandardError.ReadToEnd();
                if (!string.IsNullOrEmpty(error) && process.ExitCode != 0) throw new OperationCanceledException($"The image compression closed with code {process.ExitCode}: {error}");
            }
            finally
            {
                ensureStop();
            }
        }
        protected override string appendArgs(string path, string type, LossyInputSettings lossy, LosslessInputSettings lossless)
        {
            var input = string.Format(CultureInfo.InvariantCulture, "\"{0}\"", path);
            var str = new StringBuilder();

            if (Environment.Is64BitOperatingSystem)
            {
                if (type == "lossy")
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
            var path = GetUtility();
            if (File.Exists(path)) return;

            using (var stream = container.GetStream(UtilityName, ResourceSource.Embedded | ResourceSource.Relative)) 
            using (var dest = File.OpenWrite(path)) stream.CopyTo(dest);
        }
    }
}