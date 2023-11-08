using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace StorybrewEditor.Util
{
    public static class NetHelper
    {
        internal static HttpClient Client;

        public static void Request(string url, string cachePath, int cacheDuration, Action<string, Exception> action)
        {
            try
            {
                var fullPath = Path.GetFullPath(cachePath);
                var folder = Path.GetDirectoryName(fullPath);

                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                else if (File.Exists(cachePath) && File.GetLastWriteTimeUtc(cachePath).AddSeconds(cacheDuration) > DateTime.UtcNow)
                {
                    action(File.ReadAllText(cachePath), null);
                    return;
                }

                Trace.WriteLine($"Requesting {url}");

                var result = Client.GetStringAsync(url).Result;
                File.WriteAllText(cachePath, result);
                action(result, null);
            }
            catch (Exception e)
            {
                action(null, e);
            }
        }
        public static void Post(string url, NameValueCollection data, Action<string, Exception> action)
        {
            try
            {
                Trace.WriteLine($"Post {url}");

                var content = new FormUrlEncodedContent(data.AllKeys.ToDictionary(k => k, k => data[k]));
                using (var response = Client.PostAsync(url, content).Result)
                {
                    response.EnsureSuccessStatusCode();
                    var responseContent = response.Content.ReadAsStringAsync().Result;
                    action(responseContent, null);
                }
            }
            catch (Exception e)
            {
                action(null, e);
            }
        }
        public static void BlockingPost(string url, NameValueCollection data, Action<string, Exception> action)
        {
            try
            {
                Trace.WriteLine($"Post {url}");

                var content = new FormUrlEncodedContent(data.AllKeys.ToDictionary(k => k, k => data[k]));
                var response = Client.PostAsync(url, content).Result;
                response.EnsureSuccessStatusCode();

                var responseContent = response.Content.ReadAsStringAsync().Result;
                action(responseContent, null);
            }
            catch (Exception e)
            {
                action(null, e);
            }
        }
        public static void Download(string url, string filename, Func<float, bool> progressFunc, Action<Exception> completedAction)
        {
            try
            {
                var fullPath = Path.GetFullPath(filename);
                var folder = Path.GetDirectoryName(fullPath);

                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                else if (File.Exists(filename)) File.Delete(filename);

                Trace.WriteLine($"Downloading {url}");

                using (var response = Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result)
                {
                    response.EnsureSuccessStatusCode();

                    using (var fileStream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var bytesRead = 0L;
                        var isMoreToRead = true;

                        do
                        {
                            var read = response.Content.ReadAsByteArrayAsync().Result;
                            fileStream.Write(read, 0, read.Length);

                            bytesRead += read.Length;
                            isMoreToRead = read.Length != 0;

                            if (totalBytes != -1L)
                            {
                                var progressPercentage = (float)bytesRead / totalBytes * 100;
                                if (!progressFunc(progressPercentage))
                                {
                                    isMoreToRead = false;
                                    completedAction(new HttpRequestException("Download cancelled"));
                                }
                            }

                        } 
                        while (isMoreToRead);
                    }
                }
                completedAction(null);
            }
            catch (Exception e)
            {
                completedAction(e);
            }
        }
    }
}