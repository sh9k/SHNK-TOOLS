using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SHNK.Tools.App
{
    public partial class ConsoleLogWindow : Window
    {
        public static readonly SolidColorBrush GreenBrush =
            new SolidColorBrush(Color.FromRgb(0x3B, 0xFF, 0x7A));

        public static readonly SolidColorBrush RedBrush =
            new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x3B));

        public static readonly SolidColorBrush BlueBrush =
            new SolidColorBrush(Color.FromRgb(0x2F, 0xB6, 0xFF));

        public static readonly SolidColorBrush GrayBrush =
            new SolidColorBrush(Color.FromRgb(0x5A, 0x6B, 0x80));

        public ConsoleLogWindow(string title)
        {
            InitializeComponent();

            TitleText.Text = title;
            StatusText.Text = "Working...";
            StatusText.Foreground = BlueBrush;
        }

        public void AppendLog(string line)
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

        public void SetStatus(string text, Brush color)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = text;
                StatusText.Foreground = color;
            });
        }
    }
}
