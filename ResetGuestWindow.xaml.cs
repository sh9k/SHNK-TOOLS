using System;
using System.Threading.Tasks;
using System.Windows;

namespace SHNK.Tools.App
{
    public partial class ResetGuestWindow : Window
    {
        public Func<string, Task>? OnPickAsync { get; set; } // "GL"/"KR"/"VNG"/"TW"

        public ResetGuestWindow()
        {
            InitializeComponent();
        }

        private async Task RunPickAsync(string region)
        {
            try
            {
                if (OnPickAsync == null)
                {
                    MessageBox.Show("OnPickAsync is not set.", "SHNK TOOLS");
                    return;
                }

                // اختياري: تمنع ضغط زر ثاني أثناء التنفيذ
                this.IsEnabled = false;

                await OnPickAsync(region);

                Close(); // يغلق فقط إذا نفّذ بنجاح
            }
            catch (Exception ex)
            {
                // يخلي النافذة مفتوحة ويعرض الخطأ
                MessageBox.Show(ex.Message, "Reset Guest Error");
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private async void Gl_Click(object sender, RoutedEventArgs e) => await RunPickAsync("GL");
        private async void Kr_Click(object sender, RoutedEventArgs e) => await RunPickAsync("KR");
        private async void Vng_Click(object sender, RoutedEventArgs e) => await RunPickAsync("VNG");
        private async void Tw_Click(object sender, RoutedEventArgs e) => await RunPickAsync("TW");
    }
}