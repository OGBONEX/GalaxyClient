using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;

namespace GalaxyClient;

public partial class MainWindow : Window
{
    private const string MinecraftManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
    private const string ModrinthSearchUrl = "https://api.modrinth.com/v2/search";
    private static readonly HttpClient Http = CreateHttpClient();
    private MinecraftManifest? _manifest;
    private bool _initializing;
    private bool _launching;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            _initializing = true;
            VersionTypePicker.SelectedIndex = 0;
            ProjectTypePicker.SelectedIndex = 0;
            _initializing = false;
            await LoadVersionsAsync();
        };
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GalaxyClient/0.1.0 (+https://github.com/OGBONEX/GalaxyClient)");
        return client;
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string index } && int.TryParse(index, out var selected))
            MainTabs.SelectedIndex = selected;
    }

    private async void RefreshVersions_Click(object sender, RoutedEventArgs e) => await LoadVersionsAsync();

    private async void VersionTypePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initializing && IsLoaded) await LoadVersionsAsync();
    }

    private async Task LoadVersionsAsync()
    {
        VersionStatus.Text = "Loading official version list…";
        try
        {
            _manifest ??= await Http.GetFromJsonAsync<MinecraftManifest>(MinecraftManifestUrl)
                ?? throw new InvalidOperationException("Mojang returned an empty version manifest.");
            var selected = VersionTypePicker.SelectedItem as ComboBoxItem;
            var type = selected?.Tag?.ToString() ?? "release";
            var versions = _manifest.Versions
                .Where(version => string.Equals(version.Type, type, StringComparison.OrdinalIgnoreCase))
                .ToList();
            VersionList.ItemsSource = versions;
            VersionStatus.Text = $"{versions.Count:N0} {type} versions • Latest release: {_manifest.Latest.Release}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            VersionStatus.Text = "Could not load the version list. Check your connection and try again.";
        }
    }

    private async void InstallAndPlay_Click(object sender, RoutedEventArgs e)
    {
        if (_launching) return;
        if (VersionList.SelectedItem is not MinecraftVersion version)
        {
            VersionStatus.Text = "Select a Minecraft version first.";
            return;
        }
        var username = OfflineNameBox.Text.Trim();
        if (username.Length is < 3 or > 16 || username.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            VersionStatus.Text = "Offline names must be 3–16 letters, numbers, or underscores.";
            OfflineNameBox.Focus();
            return;
        }

        _launching = true;
        if (sender is Button button) button.IsEnabled = false;
        VersionStatus.Text = $"Preparing Minecraft {version.Id}. The first install can take a while…";
        try
        {
            var gamePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GalaxyClient", "game");
            var launcher = new MinecraftLauncher(new MinecraftPath(gamePath));
            var process = await launcher.InstallAndBuildProcessAsync(version.Id, new MLaunchOption
            {
                Session = MSession.CreateOfflineSession(username),
                MaximumRamMb = 3072,
                GameLauncherName = "GalaxyClient",
                GameLauncherVersion = "0.1.0"
            });
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => Dispatcher.Invoke(() => VersionStatus.Text = $"Minecraft {version.Id} closed.");
            process.Start();
            VersionStatus.Text = $"Minecraft {version.Id} launched as offline profile {username}.";
        }
        catch (Exception ex)
        {
            VersionStatus.Text = "Minecraft could not be installed or launched. Check the connection and try again.";
            MessageBox.Show(this, ex.Message, "Galaxy Client", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _launching = false;
            if (sender is Button launchButton) launchButton.IsEnabled = true;
        }
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await SearchProjectsAsync();

    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SearchProjectsAsync();
        }
    }

    private async Task SearchProjectsAsync()
    {
        var selected = ProjectTypePicker.SelectedItem as ComboBoxItem;
        var projectType = selected?.Tag?.ToString() ?? "mod";
        var facetName = projectType == "datapack" ? "all_project_types" : "project_type";
        var facets = JsonSerializer.Serialize(new[] { new[] { $"{facetName}:{projectType}" } });
        var url = $"{ModrinthSearchUrl}?query={Uri.EscapeDataString(SearchBox.Text.Trim())}&facets={Uri.EscapeDataString(facets)}&limit=30";
        SearchStatus.Text = "Searching Modrinth…";
        ProjectList.ItemsSource = null;
        try
        {
            using var response = await Http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var items = document.RootElement.GetProperty("hits").EnumerateArray().Select(hit =>
            {
                var id = hit.GetProperty("project_id").GetString() ?? string.Empty;
                var slug = hit.TryGetProperty("slug", out var slugValue) && slugValue.ValueKind == JsonValueKind.String ? slugValue.GetString() : null;
                var title = hit.TryGetProperty("title", out var titleValue) ? titleValue.GetString() ?? "Untitled project" : "Untitled project";
                var description = hit.TryGetProperty("description", out var descriptionValue) ? descriptionValue.GetString() ?? string.Empty : string.Empty;
                var author = hit.TryGetProperty("author", out var authorValue) ? authorValue.GetString() ?? "Unknown author" : "Unknown author";
                var downloads = hit.TryGetProperty("downloads", out var downloadsValue) && downloadsValue.TryGetInt64(out var count) ? count : 0;
                return new ModrinthProject(title, description, $"by {author} • {downloads:N0} downloads",
                    $"https://modrinth.com/{ProjectPath(projectType)}/{Uri.EscapeDataString(string.IsNullOrWhiteSpace(slug) ? id : slug)}");
            }).ToList();
            ProjectList.ItemsSource = items;
            SearchStatus.Text = $"Showing {items.Count} results";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            SearchStatus.Text = "Search failed. Check your connection and try again.";
        }
    }

    private static string ProjectPath(string projectType) => projectType switch
    {
        "modpack" => "modpacks",
        "resourcepack" => "resourcepacks",
        "datapack" => "datapacks",
        "shader" => "shaders",
        _ => "mods"
    };

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url } && Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && uri.Host == "modrinth.com")
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private sealed class MinecraftManifest
    {
        [JsonPropertyName("latest")] public LatestVersions Latest { get; set; } = new();
        [JsonPropertyName("versions")] public List<MinecraftVersion> Versions { get; set; } = [];
    }

    private sealed class LatestVersions
    {
        [JsonPropertyName("release")] public string Release { get; set; } = "Unknown";
        [JsonPropertyName("snapshot")] public string Snapshot { get; set; } = "Unknown";
    }

    private sealed class MinecraftVersion
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("releaseTime")] public DateTimeOffset ReleaseTime { get; set; }
        [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    }

    private sealed record ModrinthProject(string Title, string Summary, string Meta, string Url);
}
