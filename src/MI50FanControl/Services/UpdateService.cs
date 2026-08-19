using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MI50FanControl.Services
{
    public class UpdateInfo
    {
        public bool HasUpdate { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Changelog { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseUrl { get; set; } = string.Empty;
    }

    public class UpdateService
    {
        public static readonly Version CurrentVersion = new Version(2, 0, 0);
        public static readonly string CurrentVersionDisplay = "v2.0";
        public const string GitHubApiUrl = "https://api.github.com/repos/kingbone2006/MI50-Fan-Control/releases/latest";
        public const string GitHubRepoUrl = "https://github.com/kingbone2006/MI50-Fan-Control";

        private readonly HttpClient _http;

        public UpdateService()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("User-Agent", "MI50FanControl-App");
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                var response = await _http.GetStringAsync(GitHubApiUrl);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";
                string name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                string body = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";
                string htmlUrl = root.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() ?? "" : "";

                string downloadUrl = "";
                if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsEl.EnumerateArray())
                    {
                        string assetName = asset.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                        if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.TryGetProperty("browser_download_url", out var dl) ? dl.GetString() ?? "" : "";
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    downloadUrl = htmlUrl;
                }

                var cleanTag = tagName.Trim().TrimStart('v', 'V');
                if (Version.TryParse(cleanTag, out var remoteVer))
                {
                    if (remoteVer > CurrentVersion)
                    {
                        return new UpdateInfo
                        {
                            HasUpdate = true,
                            LatestVersion = tagName,
                            Title = string.IsNullOrWhiteSpace(name) ? tagName : name,
                            Changelog = body,
                            DownloadUrl = downloadUrl,
                            ReleaseUrl = htmlUrl
                        };
                    }
                }
                else
                {
                    // Fallback comparison for versions like "2.0"
                    string[] parts = cleanTag.Split('.');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
                    {
                        int build = parts.Length >= 3 && int.TryParse(parts[2], out int b) ? b : 0;
                        var parsedVer = new Version(major, minor, build);
                        if (parsedVer > CurrentVersion)
                        {
                            return new UpdateInfo
                            {
                                HasUpdate = true,
                                LatestVersion = tagName,
                                Title = string.IsNullOrWhiteSpace(name) ? tagName : name,
                                Changelog = body,
                                DownloadUrl = downloadUrl,
                                ReleaseUrl = htmlUrl
                            };
                        }
                    }
                }

                return new UpdateInfo { HasUpdate = false, LatestVersion = tagName, ReleaseUrl = htmlUrl };
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("UpdateService", $"Check updates error: {ex.Message}");
                return new UpdateInfo { HasUpdate = false };
            }
        }

        public async Task<string?> DownloadInstallerAsync(string downloadUrl, IProgress<int>? progress = null)
        {
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), $"MI50FanControl_Setup_{Guid.NewGuid().ToString().Substring(0, 8)}.exe");
                using var res = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                res.EnsureSuccessStatusCode();

                long? totalBytes = res.Content.Headers.ContentLength;

                using var stream = await res.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        int percent = (int)((totalRead * 100) / totalBytes.Value);
                        progress?.Report(percent);
                    }
                }

                return tempFile;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("UpdateService", $"Download installer failed: {ex.Message}");
                return null;
            }
        }
    }
}
