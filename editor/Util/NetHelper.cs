using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace StorybrewEditor.Util;

public static class NetHelper
{
    internal static HttpClient Client;

    public static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url.Replace("&", "^&"))
    {
        UseShellExecute = true
    });

    public static void Request(string url, string cachePath, int cacheDuration, Action<string, Exception> action = null)
    {
        try
        {
            var fullPath = Path.GetFullPath(cachePath);
            var folder = Path.GetDirectoryName(fullPath);

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            else if (File.Exists(cachePath) && File.GetLastWriteTimeUtc(cachePath).AddSeconds(cacheDuration) > DateTime.UtcNow)
            {
                action?.Invoke(File.ReadAllText(cachePath), null);
                return;
            }

            Trace.WriteLine($"Requesting {url}");

            var result = Client.GetStringAsync(url).Result;
            File.WriteAllText(cachePath, result);
            action?.Invoke(result, null);
        }
        catch (Exception e)
        {
            action?.Invoke(null, e);
        }
    }
    public static void Post(string url, Dictionary<string, string> data, Action<string, Exception> action = null)
    {
        try
        {
            Trace.WriteLine($"Post {url}");

            FormUrlEncodedContent content = new(data);
            using var response = Client.PostAsync(url, content).Result;
            response.EnsureSuccessStatusCode();
            var responseContent = response.Content.ReadAsStringAsync().Result;
            action?.Invoke(responseContent, null);
        }
        catch (Exception e)
        {
            action?.Invoke(null, e);
        }
    }
    public static void BlockingPost(string url, Dictionary<string, string> data, Action<string, Exception> action = null)
    {
        try
        {
            Trace.WriteLine($"Post {url}");

            FormUrlEncodedContent content = new(data);
            var response = Client.PostAsync(url, content).Result;
            response.EnsureSuccessStatusCode();

            var responseContent = response.Content.ReadAsStringAsync().Result;
            action?.Invoke(responseContent, null);
        }
        catch (Exception e)
        {
            action?.Invoke(null, e);
        }
    }
    public static void Download(string url, string filename, Func<float, bool> progressFunc, Action<Exception> completedAction = null)
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

                using FileStream fileStream = new(filename, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
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
            completedAction?.Invoke(null);
        }
        catch (Exception e)
        {
            completedAction?.Invoke(e);
        }
    }
}