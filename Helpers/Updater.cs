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

        private const string AssetNameMustContain = "win-x64";
        private const string AssetExt = ".zip";

        // =========================================================
        // HTTP
        // =========================================================

        private static readonly HttpClient _http =
            new HttpClient
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
        // CHECK FOR UPDATE
        // =========================================================

        public static async Task CheckAndPromptAsync(
            Func<string, string, bool> confirm,
            Action<string> info)
        {
            try
            {
                info(
                    "Updater: Checking for updates..."
                );

                GitHubRelease? latest =
                    await GetLatestReleaseAsync();

                if (latest == null)
                {
                    info(
                        "Updater: No releases found."
                    );

                    return;
                }

                if (!TryParseVersion(
                    latest.tag_name,
                    out Version latestVer))
                {
                    info(
                        "Updater: Can't parse release version: " +
                        (latest.tag_name ?? "(null)")
                    );

                    return;
                }

                info(
                    $"Updater: Current={CurrentVersion} " +
                    $"Latest={latestVer}"
                );

                // =====================================================
                // ALREADY UPDATED
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
                // FIND ZIP
                // =====================================================

                GitHubAsset? asset =
                    PickZipAsset(latest);

                if (asset == null)
                {
                    info(
                        "Updater: No suitable win-x64 ZIP asset found."
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
                // ASK USER
                // =====================================================

                string msg =
                    "A new SHNK TOOLS update is available!\n\n" +
                    $"Current version: {CurrentVersion}\n" +
                    $"New version:     {latestVer}\n\n" +
                    "Download and install now?";

                bool accepted =
                    confirm(
                        "SHNK TOOLS Update",
                        msg
                    );

                if (!accepted)
                {
                    info(
                        "Updater: User cancelled update."
                    );

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
                    "Updater: Update cancelled."
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
            string cache =
                Paths.CacheDir();

            Directory.CreateDirectory(cache);

            string zipPath =
                Path.Combine(
                    cache,
                    $"update_{latestVer}.zip"
                );

            string tempDownloadPath =
                zipPath + ".download";

            string extractDir =
                Path.Combine(
                    cache,
                    $"update_{latestVer}_extracted"
                );

            DownloadProgressWindow? progressWindow = null;

            try
            {
                // =====================================================
                // CLEAN OLD FILES
                // =====================================================

                TryDeleteFile(zipPath);
                TryDeleteFile(tempDownloadPath);
                TryDeleteDirectory(extractDir);

                // =====================================================
                // CREATE DOWNLOAD WINDOW
                // =====================================================

                progressWindow =
                    new DownloadProgressWindow(
                        "SHNK TOOLS UPDATE",
                        $"Downloading v{latestVer}..."
                    );

                Window? owner =
                    Application.Current?.MainWindow;

                if (owner != null &&
                    owner != progressWindow)
                {
                    progressWindow.Owner = owner;
                }

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
                            }
                        });

                progressWindow.Show();

                info(
                    "Updater: Downloading update..."
                );

                // =====================================================
                // DOWNLOAD
                // =====================================================

                await DownloadFileAsync(
                    url,
                    zipPath,
                    progress,
                    progressWindow.CancellationToken
                );

                // =====================================================
                // VERIFY ZIP EXISTS
                // =====================================================

                if (!File.Exists(zipPath))
                {
                    throw new InvalidOperationException(
                        "Update download completed, but the ZIP file was not created."
                    );
                }

                FileInfo downloadedFile =
                    new FileInfo(zipPath);

                if (downloadedFile.Length <= 0)
                {
                    throw new InvalidOperationException(
                        "The downloaded update ZIP is empty."
                    );
                }

                info(
                    $"Updater: Download completed. " +
                    $"Size={FormatBytes(downloadedFile.Length)}"
                );

                // =====================================================
                // SET UI COMPLETED
                // =====================================================

                progressWindow.SetCompleted();

                await Task.Delay(500);

                // =====================================================
                // EXTRACT
                // =====================================================

                progressWindow.SetStatus(
                    "Extracting update..."
                );

                info(
                    "Updater: Extracting update..."
                );

                Directory.CreateDirectory(
                    extractDir
                );

                ZipFile.ExtractToDirectory(
                    zipPath,
                    extractDir,
                    true
                );

                // =====================================================
                // FIND EXE
                // =====================================================

                string? newRoot =
                    FindFolderContainingExe(
                        extractDir
                    );

                if (string.IsNullOrWhiteSpace(newRoot))
                {
                    throw new InvalidOperationException(
                        "Update package doesn't contain an .exe file."
                    );
                }

                info(
                    "Updater: New application root: " +
                    newRoot
                );

                // =====================================================
                // CURRENT EXE
                // =====================================================

                string? currentExe =
                    Process
                        .GetCurrentProcess()
                        .MainModule?
                        .FileName;

                if (string.IsNullOrWhiteSpace(
                    currentExe))
                {
                    throw new InvalidOperationException(
                        "Unable to determine current application path."
                    );
                }

                string? currentDir =
                    Path.GetDirectoryName(
                        currentExe
                    );

                if (string.IsNullOrWhiteSpace(
                    currentDir))
                {
                    throw new InvalidOperationException(
                        "Unable to determine current application directory."
                    );
                }

                string exeName =
                    Path.GetFileName(
                        currentExe
                    );

                // =====================================================
                // PREPARE BAT
                // =====================================================

                progressWindow.SetStatus(
                    "Preparing installation..."
                );

                info(
                    "Updater: Preparing installation..."
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

                if (!File.Exists(batPath))
                {
                    throw new InvalidOperationException(
                        "Failed to create update script."
                    );
                }

                // =====================================================
                // INSTALLING
                // =====================================================

                progressWindow.SetStatus(
                    "Installing update..."
                );

                info(
                    "Updater: Applying update..."
                );

                Process? updaterProcess =
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

                if (updaterProcess == null)
                {
                    throw new InvalidOperationException(
                        "Failed to start update installer."
                    );
                }

                // =====================================================
                // GIVE BAT TIME TO START
                // =====================================================

                await Task.Delay(700);

                // =====================================================
                // CLOSE CURRENT APP
                // =====================================================

                info(
                    "Updater: Closing current application..."
                );

                if (progressWindow.IsVisible)
                {
                    progressWindow.Close();
                }

                Environment.Exit(0);
            }
            catch (OperationCanceledException)
            {
                info(
                    "Updater: Update download cancelled by user."
                );

                TryDeleteFile(zipPath);
                TryDeleteFile(tempDownloadPath);
                TryDeleteDirectory(extractDir);

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
        // REAL DOWNLOAD WITH PROGRESS
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

            if (!string.IsNullOrWhiteSpace(
                directory))
            {
                Directory.CreateDirectory(
                    directory
                );
            }

            string tempPath =
                destination + ".download";

            TryDeleteFile(tempPath);

            // =====================================================
            // REQUEST
            // =====================================================

            using HttpRequestMessage request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    url
                );

            request.Headers.UserAgent.ParseAdd(
                "SHNK-TOOLS-Updater"
            );

            request.Headers.Accept.ParseAdd(
                "application/octet-stream"
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

            long downloadedBytes = 0;

            Stopwatch stopwatch =
                Stopwatch.StartNew();

            TimeSpan lastProgressUpdate =
                TimeSpan.Zero;

            // =====================================================
            // DOWNLOAD STREAM
            // =====================================================

            await using (
                Stream source =
                    await response.Content.ReadAsStreamAsync(
                        cancellationToken
                    ))
            {
                await using (
                    FileStream destinationStream =
                        new FileStream(
                            tempPath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None,
                            64 * 1024,
                            FileOptions.Asynchronous |
                            FileOptions.SequentialScan
                        ))
                {
                    byte[] buffer =
                        new byte[64 * 1024];

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

                        if (
                            elapsed -
                            lastProgressUpdate >=
                            TimeSpan.FromMilliseconds(100)
                        )
                        {
                            lastProgressUpdate =
                                elapsed;

                            ReportProgress(
                                progress,
                                downloadedBytes,
                                totalBytes,
                                elapsed
                            );
                        }
                    }

                    await destinationStream.FlushAsync(
                        cancellationToken
                    );
                }
            }

            // =====================================================
            // IMPORTANT:
            // STREAMS ARE CLOSED NOW
            // =====================================================

            cancellationToken.ThrowIfCancellationRequested();

            ReportProgress(
                progress,
                downloadedBytes,
                totalBytes,
                stopwatch.Elapsed,
                forceComplete: true
            );

            // =====================================================
            // TEMP FILE -> FINAL ZIP
            // =====================================================

            TryDeleteFile(destination);

            File.Move(
                tempPath,
                destination
            );

            // =====================================================
            // VERIFY FINAL FILE
            // =====================================================

            if (!File.Exists(destination))
            {
                throw new IOException(
                    "Downloaded file could not be finalized."
                );
            }
        }

        // =========================================================
        // REPORT DOWNLOAD PROGRESS
        // =========================================================

        private static void ReportProgress(
            IProgress<DownloadProgress>? progress,
            long downloadedBytes,
            long? totalBytes,
            TimeSpan elapsed,
            bool forceComplete = false)
        {
            if (progress == null)
                return;

            double seconds =
                Math.Max(
                    elapsed.TotalSeconds,
                    0.001
                );

            double speed =
                downloadedBytes /
                seconds;

            double? percentage = null;

            if (
                totalBytes.HasValue &&
                totalBytes.Value > 0
            )
            {
                percentage =
                    downloadedBytes *
                    100.0 /
                    totalBytes.Value;

                percentage =
                    Math.Clamp(
                        percentage.Value,
                        0,
                        100
                    );

                if (forceComplete)
                {
                    percentage = 100;
                }
            }

            string remainingText =
                "Calculating...";

            if (
                totalBytes.HasValue &&
                totalBytes.Value > downloadedBytes &&
                speed > 0
            )
            {
                long remainingBytes =
                    totalBytes.Value -
                    downloadedBytes;

                double remainingSeconds =
                    remainingBytes /
                    speed;

                remainingText =
                    FormatTime(
                        remainingSeconds
                    );
            }
            else if (forceComplete)
            {
                remainingText =
                    "Completed";
            }

            progress.Report(
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
                            speed
                        ),

                    RemainingText =
                        remainingText
                }
            );
        }

        // =========================================================
        // UPDATE SCRIPT
        // =========================================================

        private static string BuildUpdateBat(
            string newRoot,
            string currentDir,
            string exeName)
        {
            return $"""
@echo off
setlocal EnableExtensions

REM =========================================================
REM SHNK TOOLS UPDATE
REM =========================================================

REM Wait for the old application to close
timeout /t 2 /nobreak >nul

REM =========================================================
REM COPY NEW VERSION
REM =========================================================

robocopy "{newRoot}" "{currentDir}" /E /R:5 /W:1 /NFL /NDL /NJH /NJS /NP >nul

REM =========================================================
REM CHECK COPY RESULT
REM =========================================================

if %ERRORLEVEL% GEQ 8 (
    timeout /t 2 /nobreak >nul
)

REM =========================================================
REM START UPDATED APPLICATION
REM =========================================================

start "" "{Path.Combine(currentDir, exeName)}"

REM =========================================================
REM CLEAN UPDATE FILES
REM =========================================================

timeout /t 3 /nobreak >nul

del /f /q "%~f0"

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

                if (!string.IsNullOrWhiteSpace(
                    folder))
                {
                    return folder;
                }
            }

            return null;
        }

        // =========================================================
        // GITHUB LATEST RELEASE
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
                return null;

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

            if (string.IsNullOrWhiteSpace(
                tag))
            {
                return false;
            }

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
        // FIND ZIP ASSET
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
                        asset.name)
                )
                {
                    continue;
                }

                if (
                    string.IsNullOrWhiteSpace(
                        asset.browser_download_url)
                )
                {
                    continue;
                }

                bool isZip =
                    asset.name.EndsWith(
                        AssetExt,
                        StringComparison.OrdinalIgnoreCase
                    );

                bool isWinX64 =
                    asset.name.Contains(
                        AssetNameMustContain,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (
                    isZip &&
                    isWinX64
                )
                {
                    return asset;
                }
            }

            return null;
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

            if (
                bytes <
                1024L * 1024L * 1024L)
            {
                return
                    $"{bytes / 1024.0 / 1024.0:0.00} MB";
            }

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
                1024 * 1024)
            {
                return
                    $"{bytesPerSecond / 1024.0:0.00} KB/s";
            }

            if (
                bytesPerSecond <
                1024L * 1024L * 1024L)
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
            if (
                double.IsNaN(seconds) ||
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
        // DELETE FILE
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
            }
        }

        // =========================================================
        // DELETE DIRECTORY
        // =========================================================

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