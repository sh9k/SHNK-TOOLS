using System;
using System.Windows;

namespace SHNK.Tools.App
{
    public partial class ResetGuestWindow : Window
    {
        public Action<string>? OnPick { get; set; } // "GL"/"KR"/"VNG"/"TW"

        public ResetGuestWindow()
        {
            InitializeComponent();
        }

        private void Gl_Click(object sender, RoutedEventArgs e) { OnPick?.Invoke("GL"); Close(); }
        private void Kr_Click(object sender, RoutedEventArgs e) { OnPick?.Invoke("KR"); Close(); }
        private void Vng_Click(object sender, RoutedEventArgs e) { OnPick?.Invoke("VNG"); Close(); }
        private void Tw_Click(object sender, RoutedEventArgs e) { OnPick?.Invoke("TW"); Close(); }
    }
}
