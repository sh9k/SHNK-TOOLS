using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

        private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        // =========================
        // Cleaner Gameloop
        // =========================
        private async void Cleaner_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDanger("Cleaner Gameloop will run your BAT script (as-is). Continue?")) return;
            if (!ConfirmDanger("Second confirm: Are you sure?")) return;

            var bat = Paths.Asset(@"Assets\scripts\cleaner_gameloop.bat");
            if (!File.Exists(bat))
            {
                MessageBox.Show(@"Missing: Assets\scripts\cleaner_gameloop.bat", "SHNK TOOLS");
                return;
            }

            Logger.Log("Running Cleaner BAT...");
            await ScriptRunner.RunBatWithLiveLog(bat);
            Logger.Log("Cleaner finished.");
            MessageBox.Show("Cleaner finished. Check logs.", "SHNK TOOLS");
        }

        // =========================
        // Fix GL
        // =========================
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
                // حذف ملف hosts
                string hostsPath = @"C:\Windows\System32\drivers\etc\hosts";
                if (File.Exists(hostsPath))
                    File.Delete(hostsPath);

                // نسخ ملفات الإصلاح
                string source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "fix_gl", "ui");
                if (!Directory.Exists(source))
                {
                    MessageBox.Show("Missing folder:\n" + source, "SHNK TOOLS");
                    return;
                }

                foreach (var file in Directory.GetFiles(source))
                {
                    var dest = Path.Combine(uiPath, Path.GetFileName(file));
                    File.Copy(file, dest, true);
                }

                MessageBox.Show("Fix GL Completed.", "SHNK TOOLS");
            }
            catch (Exception ex)
            {
                Logger.Log("FixGL ERROR: " + ex);
                MessageBox.Show(ex.Message, "Error");
            }
        }

        // =========================
        // Fix KR
        // =========================
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
                // نسخ hosts من Assets
                string hostsSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "fix_kr", "hosts");
                string hostsDest = @"C:\Windows\System32\drivers\etc\hosts";

                if (File.Exists(hostsSource))
                    File.Copy(hostsSource, hostsDest, true);

                // فك KR.zip إذا موجود
                string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "fix_kr", "KR.zip");
                if (File.Exists(zipPath))
                {
                    ZipFile.ExtractToDirectory(zipPath, uiPath, true);
                }

                MessageBox.Show("Fix KR Completed.", "SHNK TOOLS");
            }
            catch (Exception ex)
            {
                Logger.Log("FixKR ERROR: " + ex);
                MessageBox.Show(ex.Message, "Error");
            }
        }

        // =========================
        // Clear Temp + Cache
        // =========================
        private async void ClearTemp_Click(object sender, RoutedEventArgs e)
        {
            if (!Confirm("Clear Temp + Cache will delete TEMP files for current user.\nContinue?")) return;

            try
            {
                await Task.Run(() =>
                {
                    var temp = Path.GetTempPath();
                    Logger.Log("Clearing temp: " + temp);
                    FileOps.SafeDeleteContents(temp);
                });

                MessageBox.Show("Temp cleared.", "SHNK TOOLS");
            }
            catch (Exception ex)
            {
                Logger.Log("ClearTemp ERROR: " + ex);
                MessageBox.Show("ClearTemp failed. Check logs.", "SHNK TOOLS");
            }
        }

        // =========================
        // Install Emu 32
        // =========================
        private async void Install32_Click(object sender, RoutedEventArgs e)
        {
            var cfgPath = Paths.Asset(@"Config\appsettings.json");
            if (!File.Exists(cfgPath))
            {
                MessageBox.Show("Missing Config/appsettings.json", "SHNK TOOLS");
                return;
            }

            AppSettings? cfg;
            try
            {
                cfg = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(cfgPath));
            }
            catch (Exception ex)
            {
                Logger.Log("Config parse ERROR: " + ex);
                MessageBox.Show("Invalid appsettings.json", "SHNK TOOLS");
                return;
            }

            if (cfg == null || string.IsNullOrWhiteSpace(cfg.Emu32InstallerUrl))
            {
                MessageBox.Show("Emu32InstallerUrl is missing in appsettings.json", "SHNK TOOLS");
                return;
            }

            if (!Confirm($"Download & run 32-bit installer?\n\nURL:\n{cfg.Emu32InstallerUrl}")) return;

            try
            {
                var fileName = string.IsNullOrWhiteSpace(cfg.Emu32InstallerFileName)
                    ? "GameLoop_32.exe"
                    : cfg.Emu32InstallerFileName;

                var dst = Path.Combine(Paths.CacheDir(), fileName);
                Logger.Log("Downloading installer to: " + dst);

                await Downloader.DownloadFileAsync(cfg.Emu32InstallerUrl, dst);

                Logger.Log("Running installer: " + dst);
                await ScriptRunner.RunExeWithLiveLog(dst, "");
                MessageBox.Show("Installer started/finished. Check logs.", "SHNK TOOLS");
            }
            catch (Exception ex)
            {
                Logger.Log("Install32 ERROR: " + ex);
                MessageBox.Show("Install failed. Check logs.", "SHNK TOOLS");
            }
        }

        // =========================
        // Fix Error Hax / AIO FIX
        // =========================
        private const string FixErrorHaxUrl = "https://aka.ms/vs/16/release/vc_redist.x64.exe";
        private const string AioFixUrl = "https://allinoneruntimes.org/files/aio-runtimes_v2.5.0.exe";

        private static string GetCacheDir()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SHNK Tools", "cache");

            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void RunAsAdmin(string exePath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            });
        }

        private async void FixErrorHax_Click(object sender, RoutedEventArgs e)
        {
            if (!Confirm("Fix Error Hax will download & run Microsoft VC++ Redistributable (x64).\nContinue?")) return;

            try
            {
                var dest = Path.Combine(GetCacheDir(), "vc_redist.x64.exe");

                Logger.Log("Fix Error Hax: Downloading...");
                await Downloader.DownloadFileAsync(FixErrorHaxUrl, dest);

                if (!File.Exists(dest)) throw new Exception("Download failed.");

                Logger.Log("Fix Error Hax: Running as admin...");
                RunAsAdmin(dest);
            }
            catch (Exception ex)
            {
                Logger.Log("Fix Error Hax ERROR: " + ex);
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private async void AioFix_Click(object sender, RoutedEventArgs e)
        {
            if (!Confirm("AIO FIX will download & run AIO Runtimes installer.\nContinue?")) return;

            try
            {
                var dest = Path.Combine(GetCacheDir(), "aio-runtimes.exe");

                Logger.Log("AIO FIX: Downloading...");
                await Downloader.DownloadFileAsync(AioFixUrl, dest);

                if (!File.Exists(dest)) throw new Exception("Download failed.");

                Logger.Log("AIO FIX: Running as admin...");
                RunAsAdmin(dest);
            }
            catch (Exception ex)
            {
                Logger.Log("AIO FIX ERROR: " + ex);
                MessageBox.Show(ex.Message, "Error");
            }
        }

        // =========================
        // Reset Guest (Runs BAT by name)
        // =========================
        private void ResetGuest_Click(object sender, RoutedEventArgs e)
        {
            var w = new ResetGuestWindow { Owner = this };
            w.OnPickAsync = async (region) => { await RunResetGuestBatAsync(region); };
            w.ShowDialog();
        }

        private async Task RunResetGuestBatAsync(string region)
        {
            try
            {
                // Assets\reset_guest\GL.bat / KR.bat / VNG.bat / TW.bat
                var bat = Paths.Asset($@"Assets\reset_guest\{region}.bat");

                if (!File.Exists(bat))
                {
                    MessageBox.Show($"Missing BAT:\n{bat}", "SHNK TOOLS");
                    return;
                }

                if (!ConfirmDanger(
     $"⚠ Reset Guest ({region}) Will Run Now?\n\n" +
     "• ADB will restart\n" +
     "• Device ID will be refreshed\n" +
     "• Game data will be cleaned\n\n" +
     "Proceed?"
 )) return;

                Logger.Log($"ResetGuest: Running {region}.bat ...");
                await ScriptRunner.RunBatWithLiveLog(bat);
                Logger.Log($"ResetGuest: Finished {region}.");

                MessageBox.Show($"{region} done. Check logs.", "SHNK TOOLS");
            }
            catch (Exception ex)
            {
                Logger.Log("ResetGuest ERROR: " + ex);
                MessageBox.Show(ex.Message, "Error");
            }
        }

        // =========================
        // Confirm dialogs
        // =========================
        private static bool Confirm(string msg) =>
            MessageBox.Show(msg, "SHNK TOOLS", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        private static bool ConfirmDanger(string msg) =>
            MessageBox.Show(msg, "WARNING - SHNK TOOLS", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public sealed class AppSettings
    {
        public string? Emu32InstallerUrl { get; set; }
        public string? Emu32InstallerFileName { get; set; }
    }
}