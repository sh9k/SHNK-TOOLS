using System;
using System.IO;
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
            Logger.Log("SHNK TOOLS started.");
        }

        private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

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

        private async void FixGl_Click(object sender, RoutedEventArgs e)
        {
            var root = await Task.Run(GameLoopFinder.DetectGameLoopRoot);
            if (root == null)
            {
                MessageBox.Show("GameLoop path not found.", "SHNK TOOLS");
                return;
            }

            var uiTarget = Path.Combine(root, "TxGameAssistant", "UI");
            var uiSource = Paths.Asset(@"Assets\fix_gl\ui");

            if (!Directory.Exists(uiSource))
            {
                MessageBox.Show(@"Missing GL fix files folder: Assets\fix_gl\ui", "SHNK TOOLS");
                return;
            }

            if (!Confirm($"Detected GameLoop:\n{root}\n\nFixGl will:\n- Backup & delete hosts\n- Copy GL UI files to:\n{uiTarget}\n\nContinue?"))
                return;

            try
            {
                Logger.Log($"FixGl root: {root}");
                HostsOps.DeleteHostsWithBackup();
                Directory.CreateDirectory(uiTarget);
                FileOps.CopyDirectory(uiSource, uiTarget, overwrite: true);
                Logger.Log("FixGl applied.");
                MessageBox.Show("FixGl done.", "SHNK TOOLS");
            }
            catch (Exception ex)
            {
                Logger.Log("FixGl ERROR: " + ex);
                MessageBox.Show("FixGl failed. Check logs.", "SHNK TOOLS");
            }
        }

        private async void FixKr_Click(object sender, RoutedEventArgs e)
        {
            var root = await Task.Run(GameLoopFinder.DetectGameLoopRoot);
            if (root == null)
            {
                MessageBox.Show("GameLoop path not found.", "SHNK TOOLS");
                return;
            }

            var uiTarget = Path.Combine(root, "TxGameAssistant", "UI");
            var krZip = Paths.Asset(@"Assets\fix_kr\KR.zip");
            var krHosts = Paths.Asset(@"Assets\fix_kr\hosts");

            if (!File.Exists(krZip))
            {
                MessageBox.Show(@"Missing: Assets\fix_kr\KR.zip", "SHNK TOOLS");
                return;
            }
            if (!File.Exists(krHosts))
            {
                MessageBox.Show(@"Missing: Assets\fix_kr\hosts", "SHNK TOOLS");
                return;
            }

            if (!Confirm($"Detected GameLoop:\n{root}\n\nFixKr will:\n- Backup & replace hosts\n- Extract KR.zip and copy files to:\n{uiTarget}\n\nContinue?"))
                return;

            try
            {
                Logger.Log($"FixKr root: {root}");
                HostsOps.ReplaceHostsWithBackup(krHosts);

                var extracted = ZipOps.ExtractToCache(krZip, "kr_fix");
                Directory.CreateDirectory(uiTarget);
                FileOps.CopyDirectory(extracted, uiTarget, overwrite: true);

                Logger.Log("FixKr applied.");
                MessageBox.Show("FixKr done.", "SHNK TOOLS");
            }
            catch (Exception ex)
            {
                Logger.Log("FixKr ERROR: " + ex);
                MessageBox.Show("FixKr failed. Check logs.", "SHNK TOOLS");
            }
        }

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
