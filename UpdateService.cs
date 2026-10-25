using System.Net.Http;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace GalaxyClient;

internal static class UpdateService
{
    private const string ReleasesUrl = "https://api.github.com/repos/OGBONEX/GalaxyClient/releases?per_page=20";
    private static readonly HttpClient Http = CreateClient();
    public static Version CurrentVersion { get; } = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 1, 0);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GalaxyClient-Updater/0.1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static async Task<UpdateRelease?> FindUpdateAsync(CancellationToken cancellationToken = default)
    {
        var releases = await Http.GetFromJsonAsync<List<GitHubRelease>>(ReleasesUrl, cancellationToken)
            ?? throw new InvalidOperationException("GitHub returned an empty release list.");
        var release = releases.Where(item => !item.Draft && Version.TryParse(item.TagName.TrimStart('v'), out _))
            .OrderByDescending(item => Version.Parse(item.TagName.TrimStart('v')))
            .FirstOrDefault(item => Version.Parse(item.TagName.TrimStart('v')) > CurrentVersion);
        if (release is null) return null;
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "win-x86-portable",
            Architecture.Arm64 => "win-arm64-portable",
            _ => "win-x64-portable"
        };
        var asset = release.Assets.FirstOrDefault(item => string.Equals(item.Name,
            $"GalaxyClient-{architecture}.exe", StringComparison.OrdinalIgnoreCase));
        if (asset is null) throw new InvalidOperationException($"Release {release.TagName} has no package for this Windows device.");
        if (!Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com")
            throw new InvalidOperationException("GitHub returned an invalid update address.");
        if (string.IsNullOrWhiteSpace(asset.Digest) || !asset.Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This release has no SHA-256 digest. The update was not installed.");
        return new UpdateRelease(release.TagName, asset.BrowserDownloadUrl, asset.Digest[7..]);
    }

    public static async Task DownloadVerifyAndStageAsync(UpdateRelease release, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GalaxyClient", "updates");
        Directory.CreateDirectory(directory);
        var staged = Path.Combine(directory, $"GalaxyClient-{Guid.NewGuid():N}.exe");
        try
        {
            using var response = await Http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using (var output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                await response.Content.CopyToAsync(output, cancellationToken);
            await using var input = File.OpenRead(staged);
            var actual = await SHA256.HashDataAsync(input, cancellationToken);
            var expected = Convert.FromHexString(release.Sha256);
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
                throw new InvalidDataException("The update did not match GitHub's SHA-256 digest.");
            StartReplacement(staged);
        }
        catch
        {
            try { File.Delete(staged); } catch (IOException) { }
            throw;
        }
    }

    private static void StartReplacement(string staged)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            throw new InvalidOperationException("Could not locate the running Galaxy Client executable.");
        var target = Path.GetFullPath(executable);
        var backup = target + ".previous";
        var script = Path.Combine(Path.GetDirectoryName(staged)!, $"replace-{Guid.NewGuid():N}.ps1");
        static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        var scriptText = $$"""
            $ErrorActionPreference = 'Stop'
            $pidToWait = {{Environment.ProcessId}}
            $target = {{Quote(target)}}
            $staged = {{Quote(staged)}}
            $backup = {{Quote(backup)}}
            try {
              Wait-Process -Id $pidToWait -ErrorAction SilentlyContinue
              if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup -Force }
              Copy-Item -LiteralPath $target -Destination $backup
              Move-Item -LiteralPath $staged -Destination $target -Force
              $app = Start-Process -FilePath $target -PassThru
              Start-Sleep -Seconds 10
              if ($app.HasExited -and $app.ExitCode -ne 0) { throw 'Updated launcher exited during startup.' }
            } catch {
              if (Test-Path -LiteralPath $backup) {
                Copy-Item -LiteralPath $backup -Destination $target -Force
                Start-Process -FilePath $target
              }
            } finally {
              Remove-Item -LiteralPath $staged -Force -ErrorAction SilentlyContinue
              Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
            }
            """;
        File.WriteAllText(script, scriptText, System.Text.Encoding.UTF8);
        var powerShell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        var startInfo = new ProcessStartInfo { FileName = powerShell, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-WindowStyle");
        startInfo.ArgumentList.Add("Hidden");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(script);
        var helper = Process.Start(startInfo);
        if (helper is null) { File.Delete(script); throw new InvalidOperationException("Could not start the update helper."); }
        Application.Current.Shutdown();
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = string.Empty;
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; } = [];
    }
    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = string.Empty;
        [JsonPropertyName("digest")] public string Digest { get; set; } = string.Empty;
    }
    public sealed record UpdateRelease(string TagName, string DownloadUrl, string Sha256);
}
