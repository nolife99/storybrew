﻿using BrewLib.Data;
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
        readonly static bool is64bit = Environment.Is64BitOperatingSystem;
        public PngCompressor(string utilityPath = null) : base(utilityPath) 
            => container = new AssemblyResourceContainer(Assembly.GetAssembly(typeof(PngCompressor)), "BrewLib");

        protected override void compress(string path, string type, LossyInputSettings lossy, LosslessInputSettings lossless, InputFormat inputFormat = null)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(nameof(path));
            if (File.Exists(path) && string.IsNullOrEmpty(Path.GetExtension(path)) && string.IsNullOrEmpty(Convert.ToString(inputFormat, CultureInfo.InvariantCulture)))
                throw new ArgumentException("Input format is required for file without extension");

            try
            {
                UtilityName = is64bit ? type != "lossy" ? "oxipng.exe" : "pngquant.exe" : "truepng.exe";
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
                InitStartInfo(startInfo);

                if (process is null) process = Process.Start(startInfo);
                var error = process.StandardError.ReadToEnd();
                if (!string.IsNullOrEmpty(error) && process.ExitCode != 0) throw new OperationCanceledException($"The image compression closed with code {process.ExitCode}: {error}");
            }
            finally
            {
                ensureStop();
            }
        }
        protected override string appendArgs(string path, string compressionType, 
            LossyInputSettings lossyInputSettings, LosslessInputSettings losslessInputSettings)
        {
            var input = string.Format(CultureInfo.InvariantCulture, "\"{0}\"", path);
            var str = new StringBuilder();

            if (is64bit)
            {
                if (compressionType == "lossy")
                {
                    str.AppendFormat(CultureInfo.InvariantCulture, "{0} -o {0} -f --skip-if-larger --strip", input);
                    if (lossyInputSettings != null)
                    {
                        if (lossyInputSettings.MinQuality >= 0 && lossyInputSettings.MaxQuality > 0 && lossyInputSettings.MaxQuality <= 100)
                            str.AppendFormat(CultureInfo.InvariantCulture, " --quality {0}-{1} ", lossyInputSettings.MinQuality, lossyInputSettings.MaxQuality);

                        if (lossyInputSettings.Speed > 0 && lossyInputSettings.Speed <= 10)
                            str.AppendFormat(CultureInfo.InvariantCulture, " -s{0} ", lossyInputSettings.Speed);

                        str.AppendFormat(CultureInfo.InvariantCulture, " {0} ", lossyInputSettings.CustomInputArgs);
                    }
                }
                else
                {
                    if (losslessInputSettings != null)
                    {
                        var lvl = (byte)losslessInputSettings.OptimizationLevel;
                        str.AppendFormat(CultureInfo.InvariantCulture, " -o {0} ", lvl > 6 ? "max" : lvl.ToString(CultureInfo.InvariantCulture));
                        str.AppendFormat(CultureInfo.InvariantCulture, " {0} ", losslessInputSettings.CustomInputArgs);
                    }
                    str.AppendFormat(CultureInfo.InvariantCulture, "−s -a {0}", input);
                }
            }
            else
            {
                if (losslessInputSettings != null)
                {
                    var lvl = (byte)losslessInputSettings.OptimizationLevel;
                    str.AppendFormat(CultureInfo.InvariantCulture, " /o{0} ", lvl > 4 ? 4 : lvl);
                    str.AppendFormat(CultureInfo.InvariantCulture, " {0} ", losslessInputSettings.CustomInputArgs);
                }
                str.AppendFormat(CultureInfo.InvariantCulture, "/md remove all /a1 -Z /y /out {0} {0}", input);
            }
            return str.ToString();
        }
        protected override void waitForExit()
        {
            if (process is null) return;
            if (!process.HasExited && !process.WaitForExit(ExecutionTimeout.HasValue ? (int)ExecutionTimeout.Value.TotalMilliseconds : 0))
            {
                ensureStop();
                Trace.WriteLine("Image compression process exceeded execution timeout and was aborted");
            }
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