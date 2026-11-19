using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CopyToMd.Update;

public sealed record UpdateCheckResult(
    Version LatestVersion,
    string TagName,
    string Notes,
    string DownloadUrl,
    string AssetName,
    long AssetSize,
    bool HasUpdate);

public static class UpdateService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/RaZoR-7870/copy-to-md/releases/latest";

    public static Version CurrentVersion =>
        typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

    public static bool IsNewer(string? tagName, Version current)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        var text = tagName.Trim().TrimStart('v', 'V');
        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            text = text[..dash];
        }

        return Version.TryParse(text, out var version) && version > current;
    }

    public static HttpClient CreateClient(string? proxy, TimeSpan? timeout = null)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        if (string.IsNullOrWhiteSpace(proxy))
        {
            handler.UseProxy = false;
        }
        else
        {
            handler.UseProxy = true;
            handler.Proxy = new WebProxy(proxy.Trim());
        }

        var client = new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CopyToMd", CurrentVersion.ToString()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static async Task<UpdateCheckResult> CheckAsync(string? proxy, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(proxy);
        using var response = await client.GetAsync(LatestReleaseApi, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty;
        var latest = ParseVersion(tag);

        string? url = null;
        var name = string.Empty;
        long size = 0;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var assetName = asset.GetProperty("name").GetString() ?? string.Empty;
                if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    url = asset.GetProperty("browser_download_url").GetString();
                    name = assetName;
                    size = asset.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0;
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("最新 Release 中未找到 zip 下载附件。");
        }

        return new UpdateCheckResult(latest, tag, notes, url, name, size, IsNewer(tag, CurrentVersion));
    }

    public static async Task DownloadAsync(
        string url,
        string destinationPath,
        string? proxy,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(proxy, TimeSpan.FromMinutes(10));
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(destinationPath);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            total += read;
            progress?.Report(total);
        }
    }

    public static string StageUpdate(string zipPath, string targetExePath)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "CopyToMd-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, workDir);
            var newExe = Directory.GetFiles(workDir, "CopyToMd.exe", SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new InvalidOperationException("更新包中未找到 CopyToMd.exe。");

            var scriptPath = Path.Combine(workDir, "update.ps1");
            var script = BuildUpdaterScript(Environment.ProcessId, newExe, targetExePath, workDir);
            File.WriteAllText(scriptPath, script, new UTF8Encoding(false));
            return scriptPath;
        }
        catch
        {
            try { Directory.Delete(workDir, true); } catch (IOException) { }
            throw;
        }
    }

    public static void LaunchUpdater(string scriptPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static Version ParseVersion(string tag)
    {
        var text = tag.Trim().TrimStart('v', 'V');
        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            text = text[..dash];
        }

        return Version.TryParse(text, out var version) ? version : new Version(0, 0, 0, 0);
    }

    private static string BuildUpdaterScript(int processId, string sourceExe, string targetExe, string workDir)
    {
        static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

        return $@"$ErrorActionPreference = 'Stop'
$target = {Quote(targetExe)}
$source = {Quote(sourceExe)}
$work = {Quote(workDir)}
try {{ Wait-Process -Id {processId} -Timeout 30 -ErrorAction SilentlyContinue }} catch {{}}
Start-Sleep -Milliseconds 600
Copy-Item -LiteralPath $source -Destination $target -Force
Start-Process -FilePath $target
Start-Sleep -Milliseconds 800
Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
";
    }
}