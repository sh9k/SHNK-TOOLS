using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SHNK.Tools.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // أخطاء الواجهة (UI Thread)
            this.DispatcherUnhandledException += (s, ex) =>
            {
                Logger.Log("UI ERROR: " + ex.Exception);
                MessageBox.Show(
                    "Unexpected error occurred.\nCheck logs.",
                    "SHNK TOOLS",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                ex.Handled = true; // يمنع التطبيق من السقوط
            };

            // أخطاء عامة (غير UI)
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                Logger.Log("FATAL ERROR: " + ex.ExceptionObject);
            };

            // أخطاء async
            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                Logger.Log("ASYNC ERROR: " + ex.Exception);
                ex.SetObserved();
            };
        }
    }
}
