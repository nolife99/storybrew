namespace StorybrewEditor.Util;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

public static class NetHelper
{
    internal static HttpClient Client;

    public static void OpenUrl(string url)
        => Process.Start(new ProcessStartInfo(url.Replace("&", "^&")) { UseShellExecute = true })?.Dispose();

    public static async void Request(string url, Func<string, Exception, Task> action)
    {
        try
        {
            Trace.WriteLine($"Requesting {url}");

            var result = await Client.GetStringAsync(url).ConfigureAwait(false);
            action.Invoke(result, null);
        }
        catch (Exception e)
        {
            action.Invoke(null, e);
        }
    }

    public static async void Post(string url, Dictionary<string, string> data, Action<string, Exception> action = null)
    {
        try
        {
            Trace.WriteLine($"Post {url}");

            FormUrlEncodedContent content = new(data);
            using var response = await Client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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

    public static async void Download(string url,
        string filename,
        Func<float, bool> progressFunc,
        Action<Exception> completedAction = null)
    {
        try
        {
            var fullPath = Path.GetFullPath(filename);
            var folder = Path.GetDirectoryName(fullPath);

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            else if (File.Exists(filename)) File.Delete(filename);

            Trace.WriteLine($"Downloading {url}");

            using (var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                await using FileStream fileStream = new(filename, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var bytesRead = 0L;
                var isMoreToRead = true;

                do
                {
                    var read = await response.Content.ReadAsByteArrayAsync();
                    await fileStream.WriteAsync(read);

                    bytesRead += read.Length;
                    isMoreToRead = read.Length != 0;

                    if (totalBytes == -1L) continue;
                    var progressPercentage = (float)bytesRead / totalBytes * 100;

                    if (progressFunc(progressPercentage)) continue;
                    isMoreToRead = false;
                    completedAction?.Invoke(new HttpRequestException("Download cancelled"));
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