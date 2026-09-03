using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SHNK.Tools.App
{
    public partial class ResetGuestWindow : Window
    {
        // "GL"/"KR"/"VNG"/"TW" + log callback يستقبله المستدعي حتى يبعث أسطر حية للكونسل
        public Func<string, Action<string>, Task>? OnPickAsync { get; set; }

        public ResetGuestWindow()
        {
            InitializeComponent();
        }

        private void AppendLog(string line)
        {
            Dispatcher.Invoke(() =>
            {
                LogPanel.Children.Add(new TextBlock
                {
                    Text = line,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xD6, 0xFF)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 1, 0, 1)
                });

                LogScroll.ScrollToEnd();
            });
        }

        private void SetStatus(string text, Brush color)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = text;
                StatusText.Foreground = color;
            });
        }

        private async Task RunPickAsync(string region, Brush color)
        {
            try
            {
                if (OnPickAsync == null)
                {
                    MessageBox.Show("OnPickAsync is not set.", "SHNK TOOLS");
                    return;
                }

                LogPanel.Children.Clear();
                SetStatus($"Working on {region}...", color);
                IsEnabled = false;

                await OnPickAsync(region, AppendLog);

                AppendLog("DONE");
                SetStatus(
                    $"{region} reset complete",
                    new SolidColorBrush(Color.FromRgb(0x3B, 0xFF, 0x7A))
                );

                await Task.Delay(900);

                Close(); // يغلق فقط إذا نفّذ بنجاح
            }
            catch (OperationCanceledException)
            {
                AppendLog("Cancelled by user.");
                SetStatus(
                    "Cancelled",
                    new SolidColorBrush(Color.FromRgb(0x5A, 0x6B, 0x80))
                );
            }
            catch (Exception ex)
            {
                AppendLog("[ERR] " + ex.Message);
                SetStatus(
                    "Failed - see log",
                    new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x3B))
                );

                // يخلي النافذة مفتوحة ويعرض الخطأ
                MessageBox.Show(ex.Message, "Reset Guest Error");
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void Gl_Click(object sender, RoutedEventArgs e) =>
            await RunPickAsync("GL", new SolidColorBrush(Color.FromRgb(0x3B, 0xFF, 0x7A)));

        private async void Kr_Click(object sender, RoutedEventArgs e) =>
            await RunPickAsync("KR", new SolidColorBrush(Color.FromRgb(0x2F, 0xB6, 0xFF)));

        private async void Vng_Click(object sender, RoutedEventArgs e) =>
            await RunPickAsync("VNG", new SolidColorBrush(Color.FromRgb(0xB1, 0x4B, 0xFF)));

        private async void Tw_Click(object sender, RoutedEventArgs e) =>
            await RunPickAsync("TW", new SolidColorBrush(Color.FromRgb(0xFF, 0xA2, 0x3B)));
    }
}
