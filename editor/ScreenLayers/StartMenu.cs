namespace StorybrewEditor.ScreenLayers;

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BrewLib.UserInterface;
using BrewLib.Util;
using SDL3;
using StorybrewEditor.Storyboarding;
using StorybrewEditor.Util;
using Tiny;
using Tiny.Formats.Json;
using Tiny.PooledCollections.Generic.Temporary.Internals;

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
                newProjectButton = new(WidgetManager)
                {
                    Text = "New project", AnchorFrom = BoxAlignment.Centre
                },
                openProjectButton = new(WidgetManager)
                {
                    Text = "Open project", AnchorFrom = BoxAlignment.Centre
                },
                new Button(WidgetManager)
                {
                    Text = "Preferences", AnchorFrom = BoxAlignment.Centre, Disabled = true
                },
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
                wikiButton = new(WidgetManager)
                {
                    StyleName = "small", Text = "Wiki", AnchorFrom = BoxAlignment.Centre
                }
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

        var sdkPath = Project.RuntimeRefDirectory;
        if (Directory.Exists(sdkPath))
        {
            newProjectButton.OnClick += (_, _) => Manager.Add(new NewProjectMenu());
            openProjectButton.OnClick += (_, _) => Manager.ShowOpenProject();
        }
        else
        {
            newProjectButton.Disabled = true;
            openProjectButton.Disabled = true;

            SDL.LogWarn(SDL.LogCategory.System,
                $".NET SDK {Environment.Version} not found at {sdkPath} from {RuntimeEnvironment.GetRuntimeDirectory()}");

            Manager.ShowMessage(
                $".NET SDK {Environment.Version} (or more recent) is required, do you want to install it?",
                () => SDL.OpenURL("https://dotnet.microsoft.com/en-us/download/dotnet/9.0"),
                true);
        }

        wikiButton.OnClick += (_, _) => NetHelper.OpenUrl($"https://github.com/{Program.Repository}/wiki");
        discordButton.OnClick += (_, _) => NetHelper.OpenUrl(Program.DiscordUrl);
        closeButton.OnClick += (_, _) => Exit();
        versionLabel.OnClickUp += (_, e) =>
        {
            if (e.Button == SDL.ButtonLeft) NetHelper.OpenUrl($"https://github.com/{Program.Repository}");
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

    void checkLatestVersion()
        => NetHelper.Request($"https://api.github.com/repos/{Program.Repository}/releases?per_page=10&page=1",
            async (r, e) =>
            {
                if (IsDisposed) return;

                if (e is not null)
                {
                    await handleLatestVersionException(e);
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
                                if (!downloadName.EndsWith(".zip", StringComparison.Ordinal)) continue;

                                downloadUrl = asset.Value<string>("browser_download_url");
                                break;
                            }
                        }

                        if (Program.Version < version || Program.Version >= latestVersion)
                        {
                            var publishedAt = release.Value<string>("published_at");
                            var publishDate = DateTimeOffset.ParseExact(publishedAt,
                                @"yyyy-MM-dd\THH:mm:ss\Z",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.AssumeUniversal);

                            var authorName = release.Value<string>("author", "login");

                            var body = release.Value<string>("body").AsSpan();
                            if (body.Contains("---", StringComparison.Ordinal))
                                body = body[..body.IndexOf("---", StringComparison.Ordinal)];

                            body = body.ToString().Replace("\r\n", "\n").AsSpan().Trim([' ', '\n']);
                            body = $"v{version} - {authorName}, {publishDate.ToTimeAgo()}\n{body}\n\n";

                            var newDescription = string.Concat(description, body);
                            if (description.Length > 0 && newDescription.Count(c => c == '\n') > 35) break;

                            description = newDescription;
                        }
                        else break;
                    }

                    await Program.Schedule(s =>
                        {
                            var (latestVer, desc, dlUrl, menu) = s;

                            if (Program.Version < latestVer)
                            {
                                menu.updateButton.Text = "Version " + latestVer + " available!";

                                using (var sb =
                                    StringHelper.Interpolate($"What's new:\n\n{desc.AsSpan().TrimEnd('\n')}"))
                                    menu.updateButton.Tooltip = sb.AsReadOnlySpan();

                                if (dlUrl is not null && latestVer >= new Version(1, 4))
                                    menu.updateButton.OnClick += (_, _) => menu.Manager.Add(new UpdateMenu(dlUrl));
                                else menu.updateButton.OnClick += (_, _) => Updater.OpenLatestReleasePage();

                                menu.updateButton.StyleName = "";
                                menu.updateButton.Disabled = false;
                            }
                            else
                            {
                                using var sb =
                                    StringHelper.Interpolate($"Recent changes:\n\n{desc.AsSpan().TrimEnd('\n')}");

                                menu.versionLabel.Tooltip = sb.AsReadOnlySpan();
                                menu.updateButton.Displayed = false;
                            }

                            menu.bottomLayout.Pack(600);
                        },
                        (latestVersion, description, downloadUrl, this));
                }
                catch (Exception ex)
                {
                    await handleLatestVersionException(ex);
                }
            });

    ValueTask handleLatestVersionException(Exception exception)
    {
        SDL.LogWarn(SDL.LogCategory.Application,
            $"Error while retrieving latest release information: {exception.GetType()} {exception.Message}");

        versionLabel.Text = $"Could not retrieve latest release information:\n{exception.GetType()} {exception.Message
        }\n\n{versionLabel.Text}";

        updateButton.Text = "See latest release";
        updateButton.OnClick += (_, _) => Updater.OpenLatestReleasePage();
        updateButton.Disabled = false;

        return Program.Schedule(s => s.Pack(600), bottomLayout);
    }
}