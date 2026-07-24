using System;
using System.Threading;
using System.Windows;

namespace SHNK.Tools.App
{
    public partial class DownloadProgressWindow : Window
    {
        private readonly CancellationTokenSource _cts =
            new CancellationTokenSource();

        private bool _completed;
        private bool _closing;

        public CancellationToken CancellationToken =>
            _cts.Token;

        public DownloadProgressWindow(
            string title,
            string status)
        {
            InitializeComponent();

            TitleText.Text = title;
            StatusText.Text = status;

            DownloadProgressBar.Minimum = 0;
            DownloadProgressBar.Maximum = 100;
            DownloadProgressBar.Value = 0;
        }

        public void UpdateProgress(
            DownloadProgress progress)
        {
            if (_closing || _completed)
                return;

            Dispatcher.Invoke(() =>
            {
                if (_closing || _completed)
                    return;

                if (progress.Percentage.HasValue)
                {
                    DownloadProgressBar.Value =
                        Math.Clamp(
                            progress.Percentage.Value,
                            0,
                            100);

                    ProgressText.Text =
                        $"{progress.Percentage.Value:0.0}%";
                }
                else
                {
                    ProgressText.Text =
                        "Downloading...";
                }

                SpeedText.Text =
                    progress.SpeedText;

                SizeText.Text =
                    $"{progress.DownloadedText} / " +
                    $"{progress.TotalText}";

                RemainingText.Text =
                    progress.RemainingText;
            });
        }

        public void SetStatus(
            string status)
        {
            if (_closing)
                return;

            Dispatcher.Invoke(() =>
            {
                if (!_closing)
                    StatusText.Text = status;
            });
        }

        public void SetCompleted()
        {
            if (_closing)
                return;

            _completed = true;

            Dispatcher.Invoke(() =>
            {
                DownloadProgressBar.Value = 100;

                ProgressText.Text = "100%";

                StatusText.Text =
                    "Download completed successfully.";

                RemainingText.Text =
                    "Completed";
            });
        }

        private void Cancel_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_completed)
                return;

            _closing = true;

            StatusText.Text =
                "Cancelling download...";

            _cts.Cancel();

            Close();
        }

        protected override void OnClosed(
            EventArgs e)
        {
            _closing = true;

            if (!_cts.IsCancellationRequested)
                _cts.Cancel();

            _cts.Dispose();

            base.OnClosed(e);
        }
    }
}