namespace StorybrewEditor.ScreenLayers;

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using BrewLib.UserInterface;
using BrewLib.Util;
using OpenTK.Windowing.GraphicsLibraryFramework;
using StorybrewEditor.Util;
using Tiny;
using Tiny.Formats.Json;

public class StartMenu : UiScreenLayer
{
    LinearLayout mainLayout, bottomRightLayout, bottomLayout;
    Button newProjectButton, openProjectButton, closeButton, discordButton, wikiButton, updateButton;
    Label versionLabel;

    public override void Load()
    {
        base.Load();

        WidgetManager.Root.StyleName = "panel";
        WidgetManager.Root.Add(mainLayout = new(WidgetManager)
        {
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.Centre,
            AnchorTo = BoxAlignment.Centre,
            Padding = new(16),
            FitChildren = true,
            Children =
            [
                newProjectButton = new(WidgetManager) { Text = "New project", AnchorFrom = BoxAlignment.Centre },
                openProjectButton = new(WidgetManager) { Text = "Open project", AnchorFrom = BoxAlignment.Centre },
                new Button(WidgetManager) { Text = "Preferences", AnchorFrom = BoxAlignment.Centre, Disabled = true },
                closeButton = new(WidgetManager) { Text = "Close", AnchorFrom = BoxAlignment.Centre }
            ]
        });

        WidgetManager.Root.Add(bottomRightLayout = new(WidgetManager)
        {
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.BottomRight,
            AnchorTo = BoxAlignment.BottomRight,
            Padding = new(16),
            Horizontal = true,
            Fill = true,
            Children =
            [
                discordButton = new(WidgetManager)
                {
                    StyleName = "small", Text = "Join Discord", AnchorFrom = BoxAlignment.Centre
                },
                wikiButton = new(WidgetManager) { StyleName = "small", Text = "Wiki", AnchorFrom = BoxAlignment.Centre }
            ]
        });

        WidgetManager.Root.Add(bottomLayout = new(WidgetManager)
        {
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.Bottom,
            AnchorTo = BoxAlignment.Bottom,
            Padding = new(16),
            Children =
            [
                updateButton = new(WidgetManager)
                {
                    Text = "Checking for updates",
                    AnchorFrom = BoxAlignment.Centre,
                    StyleName = "small",
                    Disabled = true
                },
                versionLabel = new(WidgetManager)
                {
                    StyleName = "small", Text = Program.FullName, AnchorFrom = BoxAlignment.Centre
                }
            ]
        });

        newProjectButton.OnClick += (_, _) => Manager.Add(new NewProjectMenu());
        openProjectButton.OnClick += (_, _) => Manager.ShowOpenProject();
        wikiButton.OnClick += (_, _) => NetHelper.OpenUrl($"https://github.com/{Program.Repository}/wiki");
        discordButton.OnClick += (_, _) => NetHelper.OpenUrl(Program.DiscordUrl);
        closeButton.OnClick += (_, _) => Exit();
        versionLabel.OnClickUp += (_, e) =>
        {
            if (e.Button is MouseButton.Left) NetHelper.OpenUrl($"https://github.com/{Program.Repository}");
        };

        checkLatestVersion();
    }

    public override void Resize(int width, int height)
    {
        base.Resize(width, height);
        mainLayout.Pack(300);
        bottomLayout.Pack(600);
        bottomRightLayout.Pack((1024 - bottomLayout.Width) / 2);
    }

    void checkLatestVersion() => NetHelper.Request(
        $"https://api.github.com/repos/{Program.Repository}/releases?per_page=10&page=1", (r, e) =>
        {
            if (IsDisposed) return;
            if (e is not null)
            {
                handleLatestVersionException(e);
                return;
            }

            try
            {
                var hasLatest = false;
                var latestVersion = Program.Version;
                var description = "";
                string downloadUrl = null;

                var releases = TinyToken.ReadString<JsonFormat>(r);
                foreach (var release in releases.Values<TinyObject>())
                {
                    var isDraft = release.Value<bool>("draft");
                    var isPreRelease = release.Value<bool>("prerelease");
                    if (isDraft || isPreRelease) continue;

                    var name = release.Value<string>("name");
                    Version version = new(name);

                    if (!hasLatest)
                    {
                        hasLatest = true;
                        latestVersion = version;

                        foreach (var asset in release.Values<TinyObject>("assets"))
                        {
                            var downloadName = asset.Value<string>("name");
                            if (downloadName.EndsWith(".zip", StringComparison.Ordinal))
                            {
                                downloadUrl = asset.Value<string>("browser_download_url");
                                break;
                            }
                        }
                    }

                    if (Program.Version < version || Program.Version >= latestVersion)
                    {
                        var publishedAt = release.Value<string>("published_at");
                        var publishDate = DateTimeOffset.ParseExact(publishedAt, @"yyyy-MM-dd\THH:mm:ss\Z",
                            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

                        var authorName = release.Value<string>("author", "login");

                        var body = release.Value<string>("body");
                        if (body.Contains("---")) body = body[..body.IndexOf("---", StringComparison.Ordinal)];
                        body = body.Replace("\r\n", "\n").Trim(' ', '\n');
                        body = $"v{version} - {authorName}, {publishDate.ToTimeAgo()}\n{body}\n\n";

                        var newDescription = description + body;
                        if (description.Length > 0 && newDescription.Count(c => c == '\n') > 35) break;

                        description = newDescription;
                    }
                    else break;
                }

                if (Program.Version < latestVersion)
                {
                    updateButton.Text = "Version " + latestVersion + " available!";
                    updateButton.Tooltip = $"What's new:\n\n{description.AsSpan().TrimEnd('\n')}";
                    updateButton.OnClick += (_, _) =>
                    {
                        if (downloadUrl is not null && latestVersion >= new Version(1, 4))
                            Manager.Add(new UpdateMenu(downloadUrl));
                        else Updater.OpenLatestReleasePage();
                    };

                    updateButton.StyleName = "";
                    updateButton.Disabled = false;
                }
                else
                {
                    versionLabel.Tooltip = $"Recent changes:\n\n{description.AsSpan().TrimEnd('\n')}";
                    updateButton.Displayed = false;
                }

                bottomLayout.Pack(600);
            }
            catch (Exception ex)
            {
                handleLatestVersionException(ex);
            }
        });

    void handleLatestVersionException(Exception exception)
    {
        Trace.TraceError($"Error while retrieving latest release information: {exception.GetType()} {exception.Message}");
        versionLabel.Text = $"Could not retrieve latest release information:\n{exception.GetType()} {exception.Message
        }\n\n{versionLabel.Text}";

        updateButton.Text = "See latest release";
        updateButton.OnClick += (_, _) => Updater.OpenLatestReleasePage();
        updateButton.Disabled = false;
        bottomLayout.Pack(600);
    }
}