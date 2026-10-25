using System.Net.Http.Json;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GalaxyClient;

public partial class MainWindow : Window
{
    private const string MinecraftManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
    private const string ModrinthSearchUrl = "https://api.modrinth.com/v2/search";
    private static readonly HttpClient Http = CreateHttpClient();
    private MinecraftManifest? _manifest;
    private bool _initializing;

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
            var items = document.RootElement.GetProperty("hits").EnumerateArray()
                .Select(hit =>
                {
                    var id = hit.GetProperty("project_id").GetString() ?? string.Empty;
                    var slug = hit.TryGetProperty("slug", out var slugValue) && slugValue.ValueKind == JsonValueKind.String
                        ? slugValue.GetString() : null;
                    var title = hit.TryGetProperty("title", out var titleValue) ? titleValue.GetString() ?? "Untitled project" : "Untitled project";
                    var description = hit.TryGetProperty("description", out var descriptionValue) ? descriptionValue.GetString() ?? string.Empty : string.Empty;
                    var author = hit.TryGetProperty("author", out var authorValue) ? authorValue.GetString() ?? "Unknown author" : "Unknown author";
                    var downloads = hit.TryGetProperty("downloads", out var downloadsValue) && downloadsValue.TryGetInt64(out var count) ? count : 0;
                    return new ModrinthProject(title, description, $"by {author} • {downloads:N0} downloads",
                        $"https://modrinth.com/{ProjectPath(projectType)}/{Uri.EscapeDataString(string.IsNullOrWhiteSpace(slug) ? id : slug)}");
                })
                .ToList();
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
