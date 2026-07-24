using System;

namespace SHNK.Tools.App
{
    public sealed class DownloadProgress
    {
        public double? Percentage { get; init; }

        public string DownloadedText { get; init; } =
            "0 B";

        public string TotalText { get; init; } =
            "Unknown";

        public string SpeedText { get; init; } =
            "0 KB/s";

        public string RemainingText { get; init; } =
            "Calculating...";
    }
}