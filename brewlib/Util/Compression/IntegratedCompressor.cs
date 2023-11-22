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

        readonly List<Task> tasks = [];
        protected override void compress(Argument arg, bool useLossy)
        {
            if (!File.Exists(arg.path)) return;

            UtilityName = Environment.Is64BitOperatingSystem && useLossy ? "pngquant.exe" : "oxipng32.exe";
            ensureTool();
            var utility = GetUtility();

            var startInfo = new ProcessStartInfo(utility, appendArgs(arg.path, useLossy, arg.lossy, arg.lossless))
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(UtilityPath),
                RedirectStandardInput = true,
                RedirectStandardError = true
            };
            
            tasks.Add(Task.Run(() =>
            {
                using var localProcess = Process.Start(startInfo);
                using var errorStream = localProcess.StandardError;

                var error = errorStream.ReadToEnd();
                if (!string.IsNullOrEmpty(error) && localProcess.ExitCode != 0) throw new OperationCanceledException($"Image compression closed with code {localProcess.ExitCode}: {error}");
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
        protected override async void Dispose(bool disposing)
        {
            if (disposed) return;

            using (var completion = Task.WhenAll(tasks)) await completion;
            tasks.ForEach(task => task.Dispose());
            tasks.Clear();

            base.Dispose(disposing);
            for (var i = 0; i < toCleanup.Count; ++i) if (File.Exists(toCleanup[i])) File.Delete(toCleanup[i]);
            toCleanup.Clear();
        }
    }
}