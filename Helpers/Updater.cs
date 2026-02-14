using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace SHNK.Tools.App
{
    public static class Updater
    {
        // عدّلهم إذا غيرت الريبو
        private const string Owner = "sh9k";
        private const string Repo = "SHNK-TOOLS";

        // لازم اسم الملف بالريليز يحتوي win-x64.zip (حتى نعرفه)
        private const string AssetNameMustContain = "win-x64";
        private const string AssetExt = ".zip";

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public static Version CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

        public static async Task CheckAndPromptAsync(Func<string, string, bool> confirm, Action<string> info)
        {
            try
            {
                var latest = await GetLatestReleaseAsync();
                if (latest == null)
                {
                    info("Updater: No releases found.");
                    return;
                }

                if (!TryParseVersion(latest.tag_name, out var latestVer))
                {
                    info($"Updater: Can't parse tag version: {latest.tag_name}");
                    return;
                }

                if (latestVer <= CurrentVersion)
                {
                    info($"Updater: Up to date. Current={CurrentVersion} Latest={latestVer}");
                    return;
                }

                var asset = PickZipAsset(latest);
                if (asset == null)
                {
                    info("Updater: No suitable zip asset found in release.");
                    return;
                }

                var msg =
                    $"Update available!\n\n" +
                    $"Current: {CurrentVersion}\n" +
                    $"Latest:  {latestVer}\n\n" +
                    $"Download & install now?";

                if (!confirm("SHNK TOOLS Update", msg)) return;

                await DownloadAndUpdateAsync(asset.browser_download_url, latestVer, info);

            }
            catch (Exception ex)
            {
                info("Updater ERROR: " + ex.Message);
            }
        }

        private static async Task DownloadAndUpdateAsync(string url, Version latestVer, Action<string> info)
        {
            // أماكن مؤقتة
            var cache = Paths.CacheDir();
            var zipPath = Path.Combine(cache, $"update_{latestVer}.zip");
            var extractDir = Path.Combine(cache, $"update_{latestVer}_extracted");

            info("Downloading update...");
            await DownloadFileAsync(url, zipPath);

            info("Extracting update...");
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            Directory.CreateDirectory(extractDir);
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            // نتوقع الزِب يحتوي مجلد publish_out أو ملفات مباشرة
            // نحدد "root" اللي يحتوي exe
            var newRoot = FindFolderContainingExe(extractDir);
            if (newRoot == null)
                throw new InvalidOperationException("Update package doesn't contain an .exe.");

            // مسار البرنامج الحالي
            var currentExe = Process.GetCurrentProcess().MainModule!.FileName!;
            var currentDir = Path.GetDirectoryName(currentExe)!;

            info("Preparing updater script...");

            var batPath = Path.Combine(cache, "apply_update.bat");
            File.WriteAllText(batPath, BuildUpdateBat(newRoot, currentDir, Path.GetFileName(currentExe)));

            // شغّل البات واغلق التطبيق
            info("Applying update...");
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batPath}\"",
                UseShellExecute = true,
                CreateNoWindow = true
            });

            // اغلاق التطبيق حتى يسمح بالاستبدال
            Environment.Exit(0);
        }

        private static string BuildUpdateBat(string newRoot, string currentDir, string exeName)
        {
            // bat: ينتظر انتهاء البرنامج، ينسخ الملفات فوق القديمة، ثم يشغّل exe من جديد
            // robocopy أفضل من copy لأنه ينسخ مجلدات كاملة
            return $@"
@echo off
setlocal

REM Wait a bit for app to exit
timeout /t 2 /nobreak >nul

REM Copy update over current install
robocopy ""{newRoot}"" ""{currentDir}"" /E /R:2 /W:1 >nul

REM Ensure assets/config exist (optional)
REM Start app again
start """" ""{Path.Combine(currentDir, exeName)}""

endlocal
";
        }

        private static async Task DownloadFileAsync(string url, string destPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            using var res = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            res.EnsureSuccessStatusCode();

            await using var src = await res.Content.ReadAsStreamAsync();
            await using var dst = File.Create(destPath);
            await src.CopyToAsync(dst);
        }

        private static string? FindFolderContainingExe(string root)
        {
            foreach (var exe in Directory.GetFiles(root, "*.exe", SearchOption.AllDirectories))
            {
                return Path.GetDirectoryName(exe);
            }
            return null;
        }

        private static async Task<GitHubRelease?> GetLatestReleaseAsync()
        {
            // GitHub API
            var api = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";

            using var req = new HttpRequestMessage(HttpMethod.Get, api);
            req.Headers.UserAgent.ParseAdd("SHNK-TOOLS-Updater");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GitHubRelease>(json);
        }

        private static bool TryParseVersion(string tag, out Version v)
        {
            // يقبل v1.0.1 أو 1.0.1
            tag = tag.Trim();
            if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                tag = tag.Substring(1);

            return Version.TryParse(tag, out v!);
        }

        private static GitHubAsset? PickZipAsset(GitHubRelease rel)
        {
            if (rel.assets == null) return null;

            foreach (var a in rel.assets)
            {
                if (a.name == null || a.browser_download_url == null) continue;

                if (a.name.EndsWith(AssetExt, StringComparison.OrdinalIgnoreCase) &&
                    a.name.Contains(AssetNameMustContain, StringComparison.OrdinalIgnoreCase))
                    return a;
            }
            return null;
        }

        // Models
        private sealed class GitHubRelease
        {
            public string? tag_name { get; set; }
            public GitHubAsset[]? assets { get; set; }
        }

        private sealed class GitHubAsset
        {
            public string? name { get; set; }
            public string? browser_download_url { get; set; }
        }
    }
}
