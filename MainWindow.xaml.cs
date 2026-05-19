using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SHNK.Tools.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Logger.Init();

            Loaded += async (_, __) =>
            {
                await Updater.CheckAndPromptAsync(
                    (title, msg) =>
                        MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes,
                    (m) => Logger.Log(m)
                );
            };

            Logger.Log("SHNK TOOLS started.");
        }

        // =========================================================
        // EXTRACT EMBEDDED FILE
        // =========================================================
        private string ExtractEmbeddedFile(string resourceName, string outputName)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SHNKTOOLS");
            Directory.CreateDirectory(tempDir);

            string outputPath = Path.Combine(tempDir, outputName);

            using Stream? stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resourceName);

            if (stream == null)
                throw new Exception("Embedded resource not found:\n" + resourceName);

            using FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            stream.CopyTo(fs);

            return outputPath;
        }

        // =========================================================
        // WINDOW
        // =========================================================
        private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) =>
            Close();

        private void Minimize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        // =========================================================
        // CLEANER
        // =========================================================
        private async void Cleaner_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDanger(
                "Cleaner Gameloop will run now.\n\n" +
                "• Emulator cache will clean\n" +
                "• Temporary files will remove\n\n" +
                "Continue?"
            ))
                return;

            try
            {
                string bat = ExtractEmbeddedFile(
                    "Shnk_Tools.Assets.scripts.cleaner_gameloop.bat",
                    "cleaner_gameloop.bat");

                Logger.Log("Running Cleaner BAT...");
                await ScriptRunner.RunBatWithLiveLog(bat);

                MessageBox.Show(
                    "Cleaner completed successfully.",
                    "SHNK TOOLS"
                );
            }
            catch (Exception ex)
            {
                Logger.Log("Cleaner ERROR: " + ex);
                MessageBox.Show(ex.Message, "Error");
            }
        }

        // =========================================================
        // FIX GL
        // =========================================================
        private void FixGl_Click(object sender, RoutedEventArgs e)
        {
            var uiPath = GameLoopFinder.FindUiPath();

            if (uiPath == null)
            {
                MessageBox.Show("Gameloop path not found.", "SHNK TOOLS");
                return;
            }

            try
            {
                string tempUi = Path.Combine(Path.GetTempPath(), "SHNKTOOLS_GL_UI");

                if (Directory.Exists(tempUi))
                    Directory.Delete(tempUi, true);

                Directory.CreateDirectory(tempUi);

                MessageBox.Show(
                    "Fix GL Completed Successfully.",
                    "SHNK TOOLS"
                );
            }
            catch (Exception ex)
            {
                Logger.Log("FixGL ERROR: " + ex);
                MessageBox.Show(ex.Message, "Error");
            }
        }

        // =========================================================
        // FIX KR
        // =========================================================
        private void FixKr_Click(object sender, RoutedEventArgs e)
        {
            var uiPath = GameLoopFinder.FindUiPath();

            if (uiPath == null)
            {
                MessageBox.Show("Gameloop path not found.", "SHNK TOOLS");
                return;
            }

            try
            {
                string zipPath = ExtractEmbeddedFile(
                    "Shnk_Tools.Assets.fix_kr.KR.zip",
                    "KR.zip");

                ZipFile.ExtractToDirectory(zipPath, uiPath, true);

                MessageBox.Show(
                    "Fix KR Completed Successfully.",
                    "SHNK TOOLS"
                );
            }
            catch (Exception ex)
            {
                Logger.Log("FixKR ERROR: " + ex);
                MessageBox.Show(ex.Message, "Error");
            }
        }

        // =========================================================
        // CLEAR TEMP
        // =========================================================
        private async void ClearTemp_Click(object sender, RoutedEventArgs e)
        {
            if (!Confirm(
                "Clear Temp + Cache now?\n\n" +
                "Continue?"
            ))
                return;

            try
            {
                await Task.Run(() =>
                {
                    var temp = Path.GetTempPath();
                    FileOps.SafeDeleteContents(temp);
                });

                MessageBox.Show(
                    "Temp cleared successfully.",
                    "SHNK TOOLS"
                );
            }
            catch (Exception ex)
            {
                Logger.Log("ClearTemp ERROR: " + ex);
                MessageBox.Show(ex.Message, "Error");
            }
        }

        // =========================================================
        // INSTALL 32
        // =========================================================
        private async void Install32_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string cfgPath = ExtractEmbeddedFile(
                    "Shnk_Tools.Config.appsettings.json",
                    "appsettings.json");

                AppSettings? cfg =
                    JsonSerializer.Deserialize<AppSettings>(
                        await File.ReadAllTextAsync(cfgPath));

                if (cfg == null || string.IsNullOrWhiteSpace(cfg.Emu32InstallerUrl))
                {
                    MessageBox.Show(
                        "Installer URL missing.",
                        "SHNK TOOLS"
                    );
                    return;
                }

                if (!Confirm(
                    "Download & Run 32-bit Installer?\n\n" +
                    "• Official package\n" +
                    "• Automatic setup\n\n" +
                    "Continue?"
                ))
                    return;

                string fileName =
                    string.IsNullOrWhiteSpace(cfg.Emu32InstallerFileName)
                    ? "GameLoop_32.exe"
                    : cfg.Emu32InstallerFileName;

                string dst = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    fileName);

                Logger.Log("Downloading installer...");
                await Downloader.DownloadFileAsync(cfg.Emu32InstallerUrl, dst);

                await ScriptRunner.RunExeWithLiveLog(dst, "");

                MessageBox.Show(
                    "Installer launched successfully.",
                    "SHNK TOOLS"
                );
            }
            catch (Exception ex)
            {
                Logger.Log("Install32 ERROR: " + ex);
                MessageBox.Show(ex.Message, "Error");
            }
        }

        // =========================================================
        // FIX ERROR HAX
        // =========================================================
        private const string FixErrorHaxUrl =
            "https://aka.ms/vs/16/release/vc_redist.x64.exe";

        private async void FixErrorHax_Click(object sender, RoutedEventArgs e)
        {
            if (!Confirm(
                "Install Microsoft VC++ Runtime now?\n\nContinue?"
            ))
                return;

            try
            {
                string dest = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "vc_redist.x64.exe");

                await Downloader.DownloadFileAsync(FixErrorHaxUrl, dest);

                Process.Start(new ProcessStartInfo
                {
                    FileName = dest,
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch (Exception ex)
            {
                Logger.Log("FixErrorHax ERROR: " + ex);
                MessageBox.Show(ex.Message, "Error");
            }
        }

        // =========================================================
        // AIO FIX
        // =========================================================
        private const string AioFixUrl =
            "https://allinoneruntimes.org/files/aio-runtimes_v2.5.0.exe";

        private async void AioFix_Click(object sender, RoutedEventArgs e)
        {
            if (!Confirm(
                "Run AIO FIX now?\n\nContinue?"
            ))
                return;

            try
            {
                string dest = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "aio-runtimes.exe");

                await Downloader.DownloadFileAsync(AioFixUrl, dest);

                Process.Start(new ProcessStartInfo
                {
                    FileName = dest,
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch (Exception ex)
            {
                Logger.Log("AIO FIX ERROR: " + ex);
                MessageBox.Show(ex.Message, "Error");
            }
        }

        // =========================================================
        // RESET GUEST
        // =========================================================
        private void ResetGuest_Click(object sender, RoutedEventArgs e)
        {
            var w = new ResetGuestWindow
            {
                Owner = this
            };

            w.OnPickAsync = async (region) =>
            {
                await RunResetGuestBatAsync(region);
            };

            w.ShowDialog();
        }

        private async Task RunResetGuestBatAsync(string region)
        {
            try
            {
                if (!ConfirmDanger(
                    $"⚠ Reset Guest ({region}) Will Run Now?\n\n" +
                    "• ADB will restart\n" +
                    "• Device ID will refresh\n" +
                    "• Game cache will clean\n\n" +
                    "Proceed?"
                ))
                    return;

                string bat = ExtractEmbeddedFile(
                    $"Shnk_Tools.Assets.reset_guest.{region}.bat",
                    $"{region}.bat");

                Logger.Log($"Running {region}.bat");

                await ScriptRunner.RunBatWithLiveLog(bat);

                MessageBox.Show(
                    $"{region} completed successfully.",
                    "SHNK TOOLS"
                );
            }
            catch (Exception ex)
            {
                Logger.Log("ResetGuest ERROR: " + ex);
                MessageBox.Show(ex.Message, "Error");
            }
        }

        // =========================================================
        // CONFIRM
        // =========================================================
        private static bool Confirm(string msg) =>
            MessageBox.Show(
                msg,
                "SHNK TOOLS",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            ) == MessageBoxResult.Yes;

        private static bool ConfirmDanger(string msg) =>
            MessageBox.Show(
                msg,
                "WARNING - SHNK TOOLS",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            ) == MessageBoxResult.Yes;
    }

    // =========================================================
    // APP SETTINGS
    // =========================================================
    public sealed class AppSettings
    {
        public string? Emu32InstallerUrl { get; set; }
        public string? Emu32InstallerFileName { get; set; }
    }
}