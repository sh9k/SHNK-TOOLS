using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SHNK.Tools.App
{
    public static class Updater
    {
        // =========================================================
        // GITHUB
        // =========================================================

        private const string Owner = "sh9k";
        private const string Repo = "SHNK-TOOLS";

        // اسم ملف التحديث يجب أن يحتوي win-x64
        private const string AssetNameMustContain = "win-x64";
        private const string AssetExt = ".zip";

        // =========================================================
        // HTTP CLIENT
        // =========================================================

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        // =========================================================
        // CURRENT VERSION
        // =========================================================

        public static Version CurrentVersion =>
            Assembly
                .GetExecutingAssembly()
                .GetName()
                .Version
                ?? new Version(1, 0, 0, 0);

        // =========================================================
        // CHECK UPDATE
        // =========================================================

        public static async Task CheckAndPromptAsync(
            Func<string, string, bool> confirm,
            Action<string> info)
        {
            try
            {
                info("Updater: Checking for updates...");

                GitHubRelease? latest =
                    await GetLatestReleaseAsync();

                if (latest == null)
                {
                    info("Updater: No releases found.");
                    return;
                }

                // =====================================================
                // PARSE VERSION
                // =====================================================

                if (!TryParseVersion(
                    latest.tag_name,
                    out Version latestVer))
                {
                    info(
                        $"Updater: Can't parse tag version: " +
                        $"{latest.tag_name ?? "(null)"}"
                    );

                    return;
                }

                info(
                    $"Updater: Current={CurrentVersion} " +
                    $"Latest={latestVer}"
                );

                // =====================================================
                // ALREADY UP TO DATE
                // =====================================================

                if (latestVer <= CurrentVersion)
                {
                    info(
                        $"Updater: Up to date. " +
                        $"Current={CurrentVersion} " +
                        $"Latest={latestVer}"
                    );

                    return;
                }

                // =====================================================
                // FIND ZIP ASSET
                // =====================================================

                GitHubAsset? asset =
                    PickZipAsset(latest);

                if (asset == null)
                {
                    info(
                        "Updater: No suitable win-x64 ZIP " +
                        "asset found in release."
                    );

                    return;
                }

                if (string.IsNullOrWhiteSpace(
                    asset.browser_download_url))
                {
                    info(
                        "Updater: Asset download URL is empty."
                    );

                    return;
                }

                // =====================================================
                // CONFIRM UPDATE
                // =====================================================

                string msg =
                    "A new SHNK TOOLS update is available!\n\n" +
                    $"Current version: {CurrentVersion}\n" +
                    $"New version:     {latestVer}\n\n" +
                    "The update will be downloaded and installed.\n\n" +
                    "Continue?";

                bool accepted =
                    confirm(
                        "SHNK TOOLS Update",
                        msg
                    );

                if (!accepted)
                {
                    info("Updater: User cancelled update.");
                    return;
                }

                // =====================================================
                // DOWNLOAD + INSTALL
                // =====================================================

                await DownloadAndUpdateAsync(
                    asset.browser_download_url,
                    latestVer,
                    info
                );
            }
            catch (OperationCanceledException)
            {
                info(
                    "Updater: Update download cancelled."
                );
            }
            catch (Exception ex)
            {
                info(
                    "Updater ERROR: " +
                    ex
                );
            }
        }

        // =========================================================
        // DOWNLOAD + UPDATE
        // =========================================================

        private static async Task DownloadAndUpdateAsync(
            string url,
            Version latestVer,
            Action<string> info)
        {
            // =====================================================
            // CACHE
            // =====================================================

            string cache =
                Paths.CacheDir();

            Directory.CreateDirectory(cache);

            string zipPath =
                Path.Combine(
                    cache,
                    $"update_{latestVer}.zip"
                );

            string extractDir =
                Path.Combine(
                    cache,
                    $"update_{latestVer}_extracted"
                );

            // =====================================================
            // CREATE DOWNLOAD WINDOW
            // =====================================================

            DownloadProgressWindow? progressWindow = null;

            try
            {
                Window? owner =
                    Application.Current?.MainWindow;

                progressWindow =
                    new DownloadProgressWindow(
                        "SHNK TOOLS UPDATE",
                        $"Downloading update v{latestVer}..."
                    );

                if (owner != null &&
                    owner != progressWindow)
                {
                    progressWindow.Owner = owner;
                }

                // =================================================
                // PROGRESS
                // =================================================

                var progress =
                    new Progress<DownloadProgress>(
                        p =>
                        {
                            try
                            {
                                if (progressWindow.IsVisible)
                                {
                                    progressWindow.UpdateProgress(p);
                                }
                            }
                            catch
                            {
                                // Ignore UI update errors
                            }
                        });

                // =================================================
                // SHOW WINDOW
                // =================================================

                progressWindow.Show();

                info(
                    "Updater: Downloading update..."
                );

                // =================================================
                // DOWNLOAD
                // =================================================

                await DownloadFileAsync(
                    url,
                    zipPath,
                    progress,
                    progressWindow.CancellationToken
                );

                // =================================================
                // DOWNLOAD COMPLETED
                // =================================================

                info(
                    "Updater: Download completed."
                );

                progressWindow.SetCompleted();

                await Task.Delay(900);

                if (progressWindow.IsVisible)
                {
                    progressWindow.Close();
                }

                // =================================================
                // EXTRACT
                // =================================================

                info(
                    "Updater: Extracting update..."
                );

                if (Directory.Exists(extractDir))
                {
                    Directory.Delete(
                        extractDir,
                        true
                    );
                }

                Directory.CreateDirectory(
                    extractDir
                );

                ZipFile.ExtractToDirectory(
                    zipPath,
                    extractDir
                );

                // =================================================
                // FIND EXE
                // =================================================

                string? newRoot =
                    FindFolderContainingExe(
                        extractDir
                    );

                if (string.IsNullOrWhiteSpace(newRoot))
                {
                    throw new InvalidOperationException(
                        "Update package doesn't contain an .exe."
                    );
                }

                // =================================================
                // CURRENT EXE
                // =================================================

                Process currentProcess =
                    Process.GetCurrentProcess();

                string? currentExe =
                    currentProcess
                        .MainModule?
                        .FileName;

                if (string.IsNullOrWhiteSpace(currentExe))
                {
                    throw new InvalidOperationException(
                        "Unable to determine current application path."
                    );
                }

                string? currentDir =
                    Path.GetDirectoryName(
                        currentExe
                    );

                if (string.IsNullOrWhiteSpace(currentDir))
                {
                    throw new InvalidOperationException(
                        "Unable to determine current application directory."
                    );
                }

                string exeName =
                    Path.GetFileName(
                        currentExe
                    );

                // =================================================
                // CREATE UPDATE BAT
                // =================================================

                info(
                    "Updater: Preparing update installer..."
                );

                string batPath =
                    Path.Combine(
                        cache,
                        "apply_update.bat"
                    );

                File.WriteAllText(
                    batPath,
                    BuildUpdateBat(
                        newRoot,
                        currentDir,
                        exeName
                    )
                );

                // =================================================
                // APPLY UPDATE
                // =================================================

                info(
                    "Updater: Applying update..."
                );

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments =
                            $"/c \"{batPath}\"",
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle =
                            ProcessWindowStyle.Hidden
                    }
                );

                // =================================================
                // EXIT CURRENT APPLICATION
                // =================================================

                info(
                    "Updater: Restarting SHNK TOOLS..."
                );

                Environment.Exit(0);
            }
            catch (OperationCanceledException)
            {
                info(
                    "Updater: Download cancelled by user."
                );

                TryDeleteFile(
                    zipPath
                );

                TryDeleteDirectory(
                    extractDir
                );

                if (progressWindow != null &&
                    progressWindow.IsVisible)
                {
                    progressWindow.Close();
                }

                throw;
            }
            catch
            {
                if (progressWindow != null &&
                    progressWindow.IsVisible)
                {
                    progressWindow.Close();
                }

                throw;
            }
        }

        // =========================================================
        // DOWNLOAD WITH REAL PROGRESS
        // =========================================================

        private static async Task DownloadFileAsync(
            string url,
            string destination,
            IProgress<DownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            string? directory =
                Path.GetDirectoryName(
                    destination
                );

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(
                    directory
                );
            }

            // =====================================================
            // HTTP REQUEST
            // =====================================================

            using HttpRequestMessage request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    url
                );

            request.Headers.UserAgent.ParseAdd(
                "SHNK-TOOLS-Updater"
            );

            using HttpResponseMessage response =
                await _http.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );

            response.EnsureSuccessStatusCode();

            long? totalBytes =
                response.Content.Headers.ContentLength;

            // =====================================================
            // TEMP DOWNLOAD FILE
            // =====================================================

            string tempPath =
                destination + ".download";

            TryDeleteFile(
                tempPath
            );

            // =====================================================
            // STREAMS
            // =====================================================

            await using Stream source =
                await response.Content.ReadAsStreamAsync(
                    cancellationToken
                );

            await using FileStream destinationStream =
                new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    1024 * 64,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan
                );

            // =====================================================
            // BUFFER
            // =====================================================

            byte[] buffer =
                new byte[1024 * 64];

            long downloadedBytes = 0;

            Stopwatch stopwatch =
                Stopwatch.StartNew();

            long lastBytes = 0;

            TimeSpan lastUpdate =
                TimeSpan.Zero;

            // =====================================================
            // DOWNLOAD LOOP
            // =====================================================

            while (true)
            {
                int read =
                    await source.ReadAsync(
                        buffer.AsMemory(
                            0,
                            buffer.Length
                        ),
                        cancellationToken
                    );

                if (read == 0)
                    break;

                await destinationStream.WriteAsync(
                    buffer.AsMemory(
                        0,
                        read
                    ),
                    cancellationToken
                );

                downloadedBytes += read;

                TimeSpan elapsed =
                    stopwatch.Elapsed;

                // تحديث الواجهة تقريباً كل 100ms
                // حتى تكون الحركة ناعمة بدون ضغط على UI
                if (
                    elapsed - lastUpdate >=
                    TimeSpan.FromMilliseconds(100)
                )
                {
                    lastUpdate =
                        elapsed;

                    // =================================================
                    // SPEED
                    // =================================================

                    double seconds =
                        Math.Max(
                            elapsed.TotalSeconds,
                            0.001
                        );

                    double bytesPerSecond =
                        downloadedBytes /
                        seconds;

                    // =================================================
                    // PERCENTAGE
                    // =================================================

                    double? percentage = null;

                    if (totalBytes.HasValue &&
                        totalBytes.Value > 0)
                    {
                        percentage =
                            downloadedBytes * 100.0 /
                            totalBytes.Value;
                    }

                    // =================================================
                    // REMAINING TIME
                    // =================================================

                    string remainingText =
                        "Calculating...";

                    if (
                        totalBytes.HasValue &&
                        totalBytes.Value > downloadedBytes &&
                        bytesPerSecond > 0
                    )
                    {
                        long remainingBytes =
                            totalBytes.Value -
                            downloadedBytes;

                        double remainingSeconds =
                            remainingBytes /
                            bytesPerSecond;

                        remainingText =
                            FormatTime(
                                remainingSeconds
                            );
                    }

                    // =================================================
                    // PROGRESS OBJECT
                    // =================================================

                    DownloadProgress progressData =
                        new DownloadProgress
                        {
                            Percentage =
                                percentage,

                            DownloadedText =
                                FormatBytes(
                                    downloadedBytes
                                ),

                            TotalText =
                                totalBytes.HasValue
                                    ? FormatBytes(
                                        totalBytes.Value
                                      )
                                    : "Unknown",

                            SpeedText =
                                FormatSpeed(
                                    bytesPerSecond
                                ),

                            RemainingText =
                                remainingText
                        };

                    progress?.Report(
                        progressData
                    );

                    lastBytes =
                        downloadedBytes;
                }
            }

            // =====================================================
            // FINAL PROGRESS
            // =====================================================

            progress?.Report(
                new DownloadProgress
                {
                    Percentage = 100,

                    DownloadedText =
                        FormatBytes(
                            downloadedBytes
                        ),

                    TotalText =
                        totalBytes.HasValue
                            ? FormatBytes(
                                totalBytes.Value
                              )
                            : FormatBytes(
                                downloadedBytes
                              ),

                    SpeedText =
                        FormatSpeed(
                            downloadedBytes /
                            Math.Max(
                                stopwatch.Elapsed.TotalSeconds,
                                0.001
                            )
                        ),

                    RemainingText =
                        "Completed"
                }
            );

            await destinationStream.FlushAsync(
                cancellationToken
            );

            // =====================================================
            // REPLACE FINAL FILE
            // =====================================================

            TryDeleteFile(
                destination
            );

            File.Move(
                tempPath,
                destination
            );
        }

        // =========================================================
        // FORMAT BYTES
        // =========================================================

        private static string FormatBytes(
            long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";

            if (bytes < 1024 * 1024)
                return
                    $"{bytes / 1024.0:0.0} KB";

            if (bytes < 1024L * 1024L * 1024L)
                return
                    $"{bytes / 1024.0 / 1024.0:0.00} MB";

            return
                $"{bytes / 1024.0 / 1024.0 / 1024.0:0.00} GB";
        }

        // =========================================================
        // FORMAT SPEED
        // =========================================================

        private static string FormatSpeed(
            double bytesPerSecond)
        {
            if (bytesPerSecond < 1024)
            {
                return
                    $"{bytesPerSecond:0} B/s";
            }

            if (
                bytesPerSecond <
                1024 * 1024
            )
            {
                return
                    $"{bytesPerSecond / 1024.0:0.00} KB/s";
            }

            if (
                bytesPerSecond <
                1024L * 1024L * 1024L
            )
            {
                return
                    $"{bytesPerSecond / 1024.0 / 1024.0:0.00} MB/s";
            }

            return
                $"{bytesPerSecond / 1024.0 / 1024.0 / 1024.0:0.00} GB/s";
        }

        // =========================================================
        // FORMAT TIME
        // =========================================================

        private static string FormatTime(
            double seconds)
        {
            if (double.IsNaN(seconds) ||
                double.IsInfinity(seconds) ||
                seconds < 0)
            {
                return "Calculating...";
            }

            TimeSpan time =
                TimeSpan.FromSeconds(
                    seconds
                );

            if (time.TotalHours >= 1)
            {
                return
                    time.ToString(
                        @"hh\:mm\:ss"
                    );
            }

            return
                time.ToString(
                    @"mm\:ss"
                );
        }

        // =========================================================
        // UPDATE BAT
        // =========================================================

        private static string BuildUpdateBat(
            string newRoot,
            string currentDir,
            string exeName)
        {
            return $"""
@echo off
setlocal

REM =========================================================
REM SHNK TOOLS UPDATE SCRIPT
REM =========================================================

REM Wait for old application to close
timeout /t 2 /nobreak >nul

REM =========================================================
REM COPY NEW FILES
REM =========================================================

robocopy "{newRoot}" "{currentDir}" /E /R:3 /W:1 >nul

REM =========================================================
REM START NEW VERSION
REM =========================================================

start "" "{Path.Combine(currentDir, exeName)}"

endlocal
exit
""";
        }

        // =========================================================
        // FIND EXE
        // =========================================================

        private static string? FindFolderContainingExe(
            string root)
        {
            if (!Directory.Exists(root))
                return null;

            string[] exeFiles =
                Directory.GetFiles(
                    root,
                    "*.exe",
                    SearchOption.AllDirectories
                );

            foreach (string exe in exeFiles)
            {
                string? folder =
                    Path.GetDirectoryName(
                        exe
                    );

                if (!string.IsNullOrWhiteSpace(folder))
                    return folder;
            }

            return null;
        }

        // =========================================================
        // GITHUB API
        // =========================================================

        private static async Task<GitHubRelease?> GetLatestReleaseAsync()
        {
            string api =
                $"https://api.github.com/repos/" +
                $"{Owner}/{Repo}/releases/latest";

            using HttpRequestMessage request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    api
                );

            request.Headers.UserAgent.ParseAdd(
                "SHNK-TOOLS-Updater"
            );

            request.Headers.Accept.ParseAdd(
                "application/vnd.github+json"
            );

            using HttpResponseMessage response =
                await _http.SendAsync(
                    request
                );

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string json =
                await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<GitHubRelease>(
                json
            );
        }

        // =========================================================
        // VERSION PARSER
        // =========================================================

        private static bool TryParseVersion(
            string? tag,
            out Version version)
        {
            version =
                new Version(
                    0,
                    0,
                    0,
                    0
                );

            if (string.IsNullOrWhiteSpace(tag))
                return false;

            tag =
                tag.Trim();

            if (
                tag.StartsWith(
                    "v",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                tag =
                    tag.Substring(1);
            }

            return Version.TryParse(
                tag,
                out version
            );
        }

        // =========================================================
        // PICK ZIP ASSET
        // =========================================================

        private static GitHubAsset? PickZipAsset(
            GitHubRelease release)
        {
            if (release.assets == null)
                return null;

            foreach (
                GitHubAsset asset
                in release.assets)
            {
                if (
                    string.IsNullOrWhiteSpace(
                        asset.name
                    )
                )
                {
                    continue;
                }

                if (
                    string.IsNullOrWhiteSpace(
                        asset.browser_download_url
                    )
                )
                {
                    continue;
                }

                bool isZip =
                    asset.name.EndsWith(
                        AssetExt,
                        StringComparison.OrdinalIgnoreCase
                    );

                bool containsWinX64 =
                    asset.name.Contains(
                        AssetNameMustContain,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (
                    isZip &&
                    containsWinX64
                )
                {
                    return asset;
                }
            }

            return null;
        }

        // =========================================================
        // CLEANUP
        // =========================================================

        private static void TryDeleteFile(
            string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private static void TryDeleteDirectory(
            string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(
                        path,
                        true
                    );
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // =========================================================
        // GITHUB MODELS
        // =========================================================

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