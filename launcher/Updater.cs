using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Fifawc10Launcher;

/// <summary>
/// Updates from the project's GitHub Releases: the newest release with a "-win-x64.zip" asset and a
/// higher version than this launcher. A SHA-256 in the release notes is checked against the download.
/// </summary>
public static class Updater
{
    public const string Repo = "CalinGraphics/fifa-world-cup-2010-recompiled";

    public sealed record Release(Version Version, string ZipUrl, string? Sha256);

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("fifawc10-launcher");
        return http;
    }

    static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    public static Version Current =>
        Assembly.GetExecutingAssembly().GetName().Version is { } v ? Normalize(v) : new Version(0, 0, 0);

    /// <summary>The newer release, or null when this is the newest (or GitHub can't be reached).</summary>
    public static async Task<Release?> CheckAsync(CancellationToken ct)
    {
        // FIFAWC10_UPDATE_FEED: another URL in the same format, for testing an update end to end.
        var feed = Environment.GetEnvironmentVariable("FIFAWC10_UPDATE_FEED")
                   ?? $"https://api.github.com/repos/{Repo}/releases/latest";
        var json = await Http.GetStringAsync(feed, ct);
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var tag = r.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return null;
        version = Normalize(version);
        if (version <= Current) return null;

        string? url = null;
        foreach (var asset in r.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase))
            {
                url = asset.GetProperty("browser_download_url").GetString();
                break;
            }
        }
        if (url is null) return null;

        var notes = r.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
        var sha = Regex.Match(notes, @"\b[0-9A-Fa-f]{64}\b");
        return new Release(version, url, sha.Success ? sha.Value : null);
    }

    /// <summary>
    /// Downloads and unpacks the release, then starts a small script that waits for the launcher to
    /// close, copies the new files over this folder and opens the new launcher. The caller closes.
    /// </summary>
    public static async Task InstallAsync(Release release, IProgress<double> progress, CancellationToken ct)
    {
        var work = Path.Combine(Path.GetTempPath(), "fifawc10-update");
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);

        var zip = Path.Combine(work, "update.zip");
        using (var response = await Http.GetAsync(release.ZipUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var file = File.Create(zip);
            var buffer = new byte[1 << 20];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                if (total > 0) progress.Report((double)done / total);
            }
        }

        if (release.Sha256 is not null)
        {
            await using var file = File.OpenRead(zip);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
            if (!hash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("the download doesn't match the release's checksum");
        }

        var unpacked = Path.Combine(work, "files");
        ZipFile.ExtractToDirectory(zip, unpacked);
        // The zip holds one folder; the files are wherever Launcher.exe is.
        var newFiles = Directory.GetFiles(unpacked, "Launcher.exe", SearchOption.AllDirectories)
                           .Select(Path.GetDirectoryName).FirstOrDefault()
                       ?? throw new InvalidDataException("the download has no Launcher.exe");

        var target = AppContext.BaseDirectory.TrimEnd('\\');
        // In the source repository (a developer's copy) only the program files are replaced.
        var what = Directory.Exists(Path.Combine(target, ".git")) ? "*.exe *.dll" : "/E";
        var pid = Environment.ProcessId;
        var script = Path.Combine(work, "install.cmd");
        File.WriteAllText(script, string.Join("\r\n",
            "@echo off",
            ":wait",
            $"tasklist /FI \"PID eq {pid}\" | find \"{pid}\" >nul && (timeout /t 1 /nobreak >nul & goto wait)",
            $"robocopy \"{newFiles}\" \"{target}\" {what} /R:30 /W:1 /NJH /NJS /NP >nul",
            "if %ERRORLEVEL% GEQ 8 (echo The update couldn't replace the files. Close the game and try again. & pause & exit /b 1)",
            $"start \"\" \"{target}\\Launcher.exe\"",
            ""), Encoding.ASCII);

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"\"{script}\"\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = work,
        });
    }
}
