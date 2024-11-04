using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using BrewLib.UserInterface;
using BrewLib.Util;
using StorybrewEditor.Util;

namespace StorybrewEditor.ScreenLayers;

public class UpdateMenu(string downloadUrl) : UiScreenLayer
{
    readonly string downloadUrl = downloadUrl;

    LinearLayout mainLayout;
    Label actionLabel, statusLabel;
    ProgressBar progressBar;

    public override void Load()
    {
        base.Load();

        WidgetManager.Root.Add(mainLayout = new(WidgetManager)
        {
            StyleName = "panel",
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.Centre,
            AnchorTo = BoxAlignment.Centre,
            Padding = new(16),
            FitChildren = true,
            Children =
            [
                actionLabel = new(WidgetManager)
                {
                    Text = "Updating",
                    AnchorFrom = BoxAlignment.Centre
                },
                statusLabel = new(WidgetManager)
                {
                    StyleName = "small",
                    Text = downloadUrl,
                    AnchorFrom = BoxAlignment.Centre
                },
                progressBar = new(WidgetManager)
                {
                    Value = 0,
                    AnchorFrom = BoxAlignment.Centre
                }
            ]
        });
        NetHelper.Download(downloadUrl, Updater.UpdateArchivePath, progress =>
        {
            if (IsDisposed) return false;
            progressBar.Value = progress;
            return true;
        }, exception =>
        {
            if (IsDisposed) return;
            if (exception is not null)
            {
                Trace.TraceError($"Failed to download the new version.\n\n{exception}");
                Manager.ShowMessage($"Failed to download the new version, please update manually.\n\n{exception}", () => Updater.OpenLatestReleasePage());

                Exit();
                return;
            }
            try
            {
                string executablePath = null;
                using (var zip = ZipFile.OpenRead(Updater.UpdateArchivePath))
                {
                    if (Directory.Exists(Updater.UpdateFolderPath)) Directory.Delete(Updater.UpdateFolderPath, true);

                    foreach (var entry in zip.Entries)
                    {
                        if (entry.Name.Length == 0) continue;

                        var entryPath = Path.GetFullPath(Path.Combine(Updater.UpdateFolderPath, entry.FullName));
                        var entryFolder = Path.GetDirectoryName(entryPath);

                        if (!Directory.Exists(entryFolder))
                        {
                            Trace.WriteLine($"Creating {entryFolder}");
                            Directory.CreateDirectory(entryFolder);
                        }

                        Trace.WriteLine($"Extracting {entryPath}");
                        entry.ExtractToFile(entryPath);

                        if (Path.GetExtension(entryPath) == ".exe") executablePath = entryPath;
                    }
                }

                actionLabel.Text = "Updating";

                var localPath = Path.GetDirectoryName(typeof(Editor).Assembly.Location);
                Process process = new()
                {
                    StartInfo = new(executablePath, $"update \"{localPath}\" {Program.Version}")
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Updater.UpdateFolderPath
                    }
                };
                if (process.Start()) Manager.Exit();
                else
                {
                    Manager.ShowMessage("Failed to start the update process, please update manually.", () => Updater.OpenLatestReleasePage());
                    Exit();
                }
            }
            catch (Exception e)
            {
                Trace.TraceError($"Failed to start the update process.\n\n{e}");
                Manager.ShowMessage($"Failed to start the update process, please update manually.\n\n{e}", () => Updater.OpenLatestReleasePage());
                Exit();
            }
        });
    }
    public override void Resize(int width, int height)
    {
        base.Resize(width, height);
        mainLayout.Pack(300, 0);
    }
}