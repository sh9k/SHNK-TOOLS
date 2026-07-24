using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SHNK.Tools.App
{
    public static class Downloader
    {
        private static readonly HttpClient Http =
            new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan
            };

        public static async Task DownloadFileAsync(
            string url,
            string destination,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException(
                    "Download URL is empty.",
                    nameof(url));

            string? directory =
                Path.GetDirectoryName(destination);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string tempFile =
                destination + ".download";

            using HttpResponseMessage response =
                await Http.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            long? totalBytes =
                response.Content.Headers.ContentLength;

            await using Stream input =
                await response.Content.ReadAsStreamAsync(
                    cancellationToken);

            await using FileStream output =
                new FileStream(
                    tempFile,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan);

            byte[] buffer =
                new byte[81920];

            long downloadedBytes = 0;

            Stopwatch stopwatch =
                Stopwatch.StartNew();

            long lastBytes = 0;
            long lastTick = stopwatch.ElapsedMilliseconds;

            while (true)
            {
                int read =
                    await input.ReadAsync(
                        buffer,
                        cancellationToken);

                if (read == 0)
                    break;

                await output.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);

                downloadedBytes += read;

                long now =
                    stopwatch.ElapsedMilliseconds;

                if (now - lastTick >= 250)
                {
                    double elapsedSeconds =
                        (now - lastTick) / 1000.0;

                    long bytesSinceLast =
                        downloadedBytes - lastBytes;

                    double speed =
                        bytesSinceLast /
                        Math.Max(
                            elapsedSeconds,
                            0.001);

                    double? percentage = null;

                    if (totalBytes.HasValue &&
                        totalBytes.Value > 0)
                    {
                        percentage =
                            downloadedBytes * 100.0 /
                            totalBytes.Value;
                    }

                    TimeSpan? remaining = null;

                    if (percentage.HasValue &&
                        speed > 0)
                    {
                        long remainingBytes =
                            totalBytes!.Value -
                            downloadedBytes;

                        remaining =
                            TimeSpan.FromSeconds(
                                remainingBytes / speed);
                    }

                    progress?.Report(
                        new DownloadProgress
                        {
                            Percentage = percentage,

                            DownloadedText =
                                FormatBytes(
                                    downloadedBytes),

                            TotalText =
                                totalBytes.HasValue
                                    ? FormatBytes(
                                        totalBytes.Value)
                                    : "Unknown",

                            SpeedText =
                                FormatBytes(speed) +
                                "/s",

                            RemainingText =
                                remaining.HasValue
                                    ? FormatTime(
                                        remaining.Value)
                                    : "Calculating..."
                        });

                    lastBytes =
                        downloadedBytes;

                    lastTick =
                        now;
                }
            }

            await output.FlushAsync(
                cancellationToken);

            output.Close();

            cancellationToken.ThrowIfCancellationRequested();

            File.Move(
                tempFile,
                destination,
                true);

            progress?.Report(
                new DownloadProgress
                {
                    Percentage = 100,

                    DownloadedText =
                        FormatBytes(
                            downloadedBytes),

                    TotalText =
                        totalBytes.HasValue
                            ? FormatBytes(
                                totalBytes.Value)
                            : FormatBytes(
                                downloadedBytes),

                    SpeedText =
                        "Completed",

                    RemainingText =
                        "Completed"
                });
        }

        private static string FormatBytes(
            double bytes)
        {
            string[] sizes =
            {
                "B",
                "KB",
                "MB",
                "GB"
            };

            int order = 0;

            while (
                bytes >= 1024 &&
                order < sizes.Length - 1)
            {
                order++;
                bytes /= 1024;
            }

            return $"{bytes:0.00} {sizes[order]}";
        }

        private static string FormatTime(
            TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return
                    $"{(int)time.TotalHours:00}:" +
                    $"{time.Minutes:00}:" +
                    $"{time.Seconds:00}";

            return
                $"{time.Minutes:00}:" +
                $"{time.Seconds:00}";
        }
    }
}