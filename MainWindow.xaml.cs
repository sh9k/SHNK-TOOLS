using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                        MessageBox.Show(
                            msg,
                            title,
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information
                        ) == MessageBoxResult.Yes,
                    m => Logger.Log(m)
                );
            };

            Logger.Log("SHNK TOOLS started.");
        }

        // =========================================================
        // DOWNLOAD WITH PROFESSIONAL PROGRESS WINDOW
        // =========================================================
        private async Task<bool> DownloadWithProgressAsync(
            string title,
            string status,
            string url,
            string destination)
        {
            DownloadProgressWindow progressWindow =
                new DownloadProgressWindow(
                    title,
                    status)
                {
                    Owner = this
                };

            var progress =
                new Progress<DownloadProgress>(
                    p =>
                    {
                        if (progressWindow.IsVisible)
                            progressWindow.UpdateProgress(p);
                    });

            progressWindow.Show();

            try
            {
                await Downloader.DownloadFileAsync(
                    url,
                    destination,
                    progress,
                    progressWindow.CancellationToken
                );

                progressWindow.SetCompleted();

                await Task.Delay(700);

                if (progressWindow.IsVisible)
                    progressWindow.Close();

                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.Log(
                    "Download cancelled: " +
                    url
                );

                TryDeleteDownloadFiles(destination);

                if (progressWindow.IsVisible)
                    progressWindow.Close();

                return false;
            }
            catch (Exception ex)
            {
                Logger.Log(
                    "Download failed: " +
                    ex
                );

                TryDeleteDownloadFiles(destination);

                if (progressWindow.IsVisible)
                    progressWindow.Close();

                MessageBox.Show(
                    ex.Message,
                    "Download Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                return false;
            }
        }

        // =========================================================
        // CLEAN DOWNLOAD FILES
        // =========================================================
        private static void TryDeleteDownloadFiles(
            string destination)
        {
            try
            {
                if (File.Exists(destination))
                    File.Delete(destination);
            }
            catch
            {
            }

            try
            {
                string tempFile =
                    destination + ".download";

                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch
            {
            }
        }

        // =========================================================
        // EXTRACT EMBEDDED FILE
        // =========================================================
        private string ExtractEmbeddedFile(
            string resourceName,
            string outputName)
        {
            string tempDir =
                Path.Combine(
                    Path.GetTempPath(),
                    "SHNKTOOLS"
                );

            Directory.CreateDirectory(tempDir);

            string outputPath =
                Path.Combine(
                    tempDir,
                    outputName
                );

            using Stream? stream =
                Assembly
                    .GetExecutingAssembly()
                    .GetManifestResourceStream(
                        resourceName
                    );

            if (stream == null)
            {
                throw new Exception(
                    "Embedded resource not found:\n" +
                    resourceName
                );
            }

            string? dir =
                Path.GetDirectoryName(outputPath);

            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            using FileStream fs =
                new FileStream(
                    outputPath,
                    FileMode.Create,
                    FileAccess.Write
                );

            stream.CopyTo(fs);

            return outputPath;
        }

        // =========================================================
        // WINDOW
        // =========================================================
        private void DragBar_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        private void Minimize_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        // =========================================================
        // CLEANER
        // =========================================================
        private async void Cleaner_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!ConfirmDanger(
                "Cleaner Gameloop will run now.\n\n" +
                "• Emulator cache will clean\n" +
                "• Temporary files will remove\n\n" +
                "Continue?"
            ))
            {
                return;
            }

            try
            {
                string bat =
                    ExtractEmbeddedFile(
                        "Shnk_Tools.Assets.scripts.cleaner_gameloop.bat",
                        "cleaner_gameloop.bat"
                    );

                if (!File.Exists(bat))
                {
                    MessageBox.Show(
                        "BAT file was not extracted.",
                        "SHNK TOOLS"
                    );

                    return;
                }

                Logger.Log(
                    "Running Cleaner BAT..."
                );

                Logger.Log(
                    "BAT PATH: " + bat
                );

                var psi =
                    new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/k \"\"{bat}\"\"",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WorkingDirectory =
                            Path.GetDirectoryName(bat) ??
                            Environment.CurrentDirectory
                    };

                var p =
                    Process.Start(psi);

                if (p == null)
                {
                    MessageBox.Show(
                        "Failed to start BAT.",
                        "SHNK TOOLS"
                    );

                    return;
                }

                await p.WaitForExitAsync();

                Logger.Log(
                    "Cleaner BAT ExitCode: " +
                    p.ExitCode
                );

                MessageBox.Show(
                    "Cleaner finished.",
                    "SHNK TOOLS"
                );
            }
            catch (Exception ex)
            {
                Logger.Log(
                    "Cleaner ERROR: " +
                    ex
                );

                MessageBox.Show(
                    ex.ToString(),
                    "Error"
                );
            }
        }

        // =========================================================
        // FIX GL
        // =========================================================
        private void FixGl_Click(
            object sender,
            RoutedEventArgs e)
        {
            var uiPath =
                GameLoopFinder.FindUiPath();

            if (uiPath == null)
            {
                MessageBox.Show(
                    "Gameloop path not found.",
                    "SHNK TOOLS"
                );

                return;
            }

            try
            {
                string tempDir =
                    Path.Combine(
                        Path.GetTempPath(),
                        "SHNKTOOLS_FIXGL"
                    );

                Directory.CreateDirectory(tempDir);

                var asm =
                    Assembly.GetExecutingAssembly();

                int copied = 0;

                foreach (var res in
                    asm.GetManifestResourceNames())
                {
                    if (!res.Contains(
                        "Assets.fix_gl.ui",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    using Stream? s =
                        asm.GetManifestResourceStream(res);

                    if (s == null)
                        continue;

                    string fileName =
                        res.Replace(
                            "Shnk_Tools.Assets.fix_gl.ui.",
                            "",
                            StringComparison.OrdinalIgnoreCase
                        );

                    fileName =
                        fileName
                            .Replace(".dll.", ".dll")
                            .Replace(".exe.", ".exe")
                            .Replace(".pak.", ".pak")
                            .Replace(".dat.", ".dat")
                            .Replace(".ini.", ".ini");

                    string tempFile =
                        Path.Combine(
                            tempDir,
                            fileName
                        );

                    string? tempDirectory =
                        Path.GetDirectoryName(tempFile);

                    if (!string.IsNullOrWhiteSpace(
                        tempDirectory))
                    {
                        Directory.CreateDirectory(
                            tempDirectory
                        );
                    }

                    using (
                        FileStream fs =
                            new FileStream(
                                tempFile,
                                FileMode.Create,
                                FileAccess.Write,
                                FileShare.None))
                    {
                        s.CopyTo(fs);
                    }

                    string dest =
                        Path.Combine(
                            uiPath,
                            fileName
                        );

                    string? destDirectory =
                        Path.GetDirectoryName(dest);

                    if (!string.IsNullOrWhiteSpace(
                        destDirectory))
                    {
                        Directory.CreateDirectory(
                            destDirectory
                        );
                    }

                    File.Copy(
                        tempFile,
                        dest,
                        true
                    );

                    copied++;
                }

                string hostsPath =
                    @"C:\Windows\System32\drivers\etc\hosts";

                if (File.Exists(hostsPath))
                {
                    File.Delete(hostsPath);
                }

                Logger.Log(
                    $"Fix GL completed. Files copied: {copied}"
                );

                MessageBox.Show(
                    $"Fix GL Completed Successfully.\n\n" +
                    $"Files Copied: {copied}",
                    "SHNK TOOLS"
                );
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log(
                    "FixGL permission ERROR: " +
                    ex
                );

                MessageBox.Show(
                    "Access denied.\n\n" +
                    "Please run SHNK TOOLS as Administrator.",
                    "Fix GL Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            catch (Exception ex)
            {
                Logger.Log(
                    "FixGL ERROR: " +
                    ex
                );

                MessageBox.Show(
                    ex.ToString(),
                    "Error"
                );
            }
        }

        // =========================================================
        // FIX KR - DIRECT FILE INSTALL
        // =========================================================
        private async void FixKr_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!ConfirmDanger(
                "Install Fix KR now?\n\n" +
                "The following files will be replaced:\n\n" +
                "• AEngine.dll\n" +
                "• DefaultKeyMapping.xml\n" +
                "• GameSidebar.xml\n" +
                "• smk.conf\n" +
                "• translate.conf\n" +
                "• Windows hosts file\n\n" +
                "Continue?"
            ))
            {
                return;
            }

            try
            {
                Logger.Log(
                    "Starting Fix KR..."
                );

                // =====================================================
                // FIND GAMELOOP UI PATH
                // =====================================================

                string? uiPath =
                    GameLoopFinder.FindUiPath();

                if (string.IsNullOrWhiteSpace(uiPath))
                {
                    MessageBox.Show(
                        "GameLoop path was not found.\n\n" +
                        "Please make sure GameLoop is installed.",
                        "SHNK TOOLS",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    Logger.Log(
                        "Fix KR failed: GameLoop UI path not found."
                    );

                    return;
                }

                Logger.Log(
                    "GameLoop UI Path: " +
                    uiPath
                );

                Directory.CreateDirectory(uiPath);

                // =====================================================
                // FILES TO INSTALL
                // =====================================================

                string[] uiFiles =
                {
                    "AEngine.dll",
                    "DefaultKeyMapping.xml",
                    "GameSidebar.xml",
                    "smk.conf",
                    "translate.conf"
                };

                Assembly asm =
                    Assembly.GetExecutingAssembly();

                string[] resources =
                    asm.GetManifestResourceNames();

                int copied = 0;

                foreach (string fileName in uiFiles)
                {
                    // Find the resource by its ending rather than
                    // relying on the exact root namespace.
                    string? resourceName =
                        resources.FirstOrDefault(
                            r =>
                                r.EndsWith(
                                    $"Assets.fix_kr.{fileName}",
                                    StringComparison.OrdinalIgnoreCase
                                )
                        );

                    if (string.IsNullOrWhiteSpace(
                        resourceName))
                    {
                        Logger.Log(
                            $"FIX KR resource missing: {fileName}"
                        );

                        throw new Exception(
                            "Embedded resource not found:\n\n" +
                            fileName
                        );
                    }

                    Logger.Log(
                        $"FIX KR resource: {resourceName}"
                    );

                    string destination =
                        Path.Combine(
                            uiPath,
                            fileName
                        );

                    Logger.Log(
                        $"Copying: {fileName}"
                    );

                    using Stream? resourceStream =
                        asm.GetManifestResourceStream(
                            resourceName
                        );

                    if (resourceStream == null)
                    {
                        throw new Exception(
                            "Could not open embedded resource:\n\n" +
                            resourceName
                        );
                    }

                    using FileStream output =
                        new FileStream(
                            destination,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None
                        );

                    await resourceStream.CopyToAsync(
                        output
                    );

                    copied++;

                    Logger.Log(
                        $"Copied successfully: {destination}"
                    );
                }

                // =====================================================
                // HOSTS
                // =====================================================

                string hostsPath =
                    @"C:\Windows\System32\drivers\etc\hosts";

                string? hostsResource =
                    resources.FirstOrDefault(
                        r =>
                            r.EndsWith(
                                "Assets.fix_kr.hosts",
                                StringComparison.OrdinalIgnoreCase
                            )
                    );

                if (string.IsNullOrWhiteSpace(
                    hostsResource))
                {
                    throw new Exception(
                        "Embedded hosts resource not found."
                    );
                }

                Logger.Log(
                    $"Hosts resource: {hostsResource}"
                );

                using Stream? hostsStream =
                    asm.GetManifestResourceStream(
                        hostsResource
                    );

                if (hostsStream == null)
                {
                    throw new Exception(
                        "Could not open embedded hosts resource."
                    );
                }

                using FileStream hostsOutput =
                    new FileStream(
                        hostsPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read
                    );

                await hostsStream.CopyToAsync(
                    hostsOutput
                );

                Logger.Log(
                    "Hosts file installed successfully."
                );

                // =====================================================
                // CLEANUP / RESULT
                // =====================================================

                MessageBox.Show(
                    "Fix KR completed successfully!\n\n" +
                    $"Files copied: {copied}/5\n" +
                    "Hosts file installed successfully.\n\n" +
                    "Please restart GameLoop.",
                    "SHNK TOOLS",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                Logger.Log(
                    "Fix KR completed successfully."
                );
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log(
                    "Fix KR permission error: " +
                    ex
                );

                MessageBox.Show(
                    "Access denied while installing Fix KR.\n\n" +
                    "Please run SHNK TOOLS as Administrator.",
                    "SHNK TOOLS",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            catch (IOException ex)
            {
                Logger.Log(
                    "Fix KR IO error: " +
                    ex
                );

                MessageBox.Show(
                    "A file could not be replaced because it may be in use.\n\n" +
                    "Close GameLoop completely and try again.\n\n" +
                    ex.Message,
                    "Fix KR Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            catch (Exception ex)
            {
                Logger.Log(
                    "Fix KR ERROR: " +
                    ex
                );

                MessageBox.Show(
                    ex.ToString(),
                    "Fix KR Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        // =========================================================
        // CLEAR TEMP
        // =========================================================
        private async void ClearTemp_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!Confirm(
                "Clear Temp + Cache now?\n\n" +
                "Continue?"
            ))
            {
                return;
            }

            try
            {
                await Task.Run(
                    () =>
                    {
                        string temp =
                            Path.GetTempPath();

                        FileOps.SafeDeleteContents(
                            temp
                        );
                    }
                );

                MessageBox.Show(
                    "Temp cleared successfully.",
                    "SHNK TOOLS"
                );
            }
            catch (Exception ex)
            {
                Logger.Log(
                    "ClearTemp ERROR: " +
                    ex
                );

                MessageBox.Show(
                    ex.ToString(),
                    "Error"
                );
            }
        }

        // =========================================================
        // INSTALL 32
        // =========================================================
        private async void Install32_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                string cfgPath =
                    ExtractEmbeddedFile(
                        "Shnk_Tools.Config.appsettings.json",
                        "appsettings.json"
                    );

                AppSettings? cfg =
                    JsonSerializer.Deserialize<AppSettings>(
                        await File.ReadAllTextAsync(cfgPath)
                    );

                if (
                    cfg == null ||
                    string.IsNullOrWhiteSpace(
                        cfg.Emu32InstallerUrl
                    )
                )
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
                {
                    return;
                }

                string fileName =
                    string.IsNullOrWhiteSpace(
                        cfg.Emu32InstallerFileName
                    )
                    ? "GameLoop_32.exe"
                    : cfg.Emu32InstallerFileName;

                string dst =
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData
                        ),
                        fileName
                    );

                Logger.Log(
                    "Downloading installer..."
                );

                bool downloaded =
                    await DownloadWithProgressAsync(
                        "GAMELOOP 32-BIT",
                        "Downloading GameLoop installer...",
                        cfg.Emu32InstallerUrl,
                        dst
                    );

                if (!downloaded)
                    return;

                await ScriptRunner.RunExeWithLiveLog(
                    dst,
                    ""
                );

                MessageBox.Show(
                    "Installer launched successfully.",
                    "SHNK TOOLS"
                );
            }
            catch (Exception ex)
            {
                Logger.Log(
                    "Install32 ERROR: " +
                    ex
                );

                MessageBox.Show(
                    ex.ToString(),
                    "Error"
                );
            }
        }

        // =========================================================
        // FIX ERROR HAX
        // =========================================================
        private const string FixErrorHaxUrl =
            "https://aka.ms/vs/16/release/vc_redist.x64.exe";

        private async void FixErrorHax_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!Confirm(
                "Install Microsoft VC++ Runtime now?\n\n" +
                "Continue?"
            ))
            {
                return;
            }

            try
            {
                string dest =
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData
                        ),
                        "vc_redist.x64.exe"
                    );

                bool downloaded =
                    await DownloadWithProgressAsync(
                        "FIX ERROR HAX",
                        "Downloading Microsoft VC++ Runtime...",
                        FixErrorHaxUrl,
                        dest
                    );

                if (!downloaded)
                    return;

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = dest,
                        UseShellExecute = true,
                        Verb = "runas"
                    }
                );

                Logger.Log(
                    "VC++ Runtime launched."
                );
            }
            catch (Exception ex)
            {
                Logger.Log(
                    "FixErrorHax ERROR: " +
                    ex
                );

                MessageBox.Show(
                    ex.ToString(),
                    "Error"
                );
            }
        }

        // =========================================================
        // AIO FIX
        // =========================================================
        private const string AioFixUrl =
            "https://allinoneruntimes.org/files/aio-runtimes_v2.5.0.exe";

        private async void AioFix_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!Confirm(
                "Run AIO FIX now?\n\n" +
                "Continue?"
            ))
            {
                return;
            }

            try
            {
                string dest =
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData
                        ),
                        "aio-runtimes.exe"
                    );

                bool downloaded =
                    await DownloadWithProgressAsync(
                        "AIO FIX",
                        "Downloading All-in-One Runtimes...",
                        AioFixUrl,
                        dest
                    );

                if (!downloaded)
                    return;

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = dest,
                        UseShellExecute = true,
                        Verb = "runas"
                    }
                );

                Logger.Log(
                    "AIO Runtimes launched."
                );
            }
            catch (Exception ex)
            {
                Logger.Log(
                    "AIO FIX ERROR: " +
                    ex
                );

                MessageBox.Show(
                    ex.ToString(),
                    "Error"
                );
            }
        }

        // =========================================================
        // RESET GUEST
        // =========================================================
        private void ResetGuest_Click(
            object sender,
            RoutedEventArgs e)
        {
            var w =
                new ResetGuestWindow
                {
                    Owner = this
                };

            w.OnPickAsync =
                async (region, log) =>
                {
                    await RunResetGuestBatAsync(region, log);
                };

            w.ShowDialog();
        }

        private async Task RunResetGuestBatAsync(
            string region,
            Action<string> log)
        {
            if (!ConfirmDanger(
                $"⚠ Reset Guest ({region}) Will Run Now?\n\n" +
                "• ADB will restart\n" +
                "• Device ID will refresh\n" +
                "• Game cache will clean\n\n" +
                "Proceed?"
            ))
            {
                throw new OperationCanceledException();
            }

            log($"Preparing {region} script...");

            string bat =
                ExtractEmbeddedFile(
                    $"Shnk_Tools.Assets.reset_guest.{region}.bat",
                    $"{region}.bat"
                );

            Logger.Log($"Running {region}.bat");
            log($"Running {region}.bat");

            try
            {
                await ScriptRunner.RunBatWithLiveLog(bat, log);
            }
            catch (Exception ex)
            {
                Logger.Log("ResetGuest ERROR: " + ex);
                throw;
            }
        }


        // =========================================================
        // CONFIRM
        // =========================================================
        private static bool Confirm(
            string msg)
        {
            return MessageBox.Show(
                msg,
                "SHNK TOOLS",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            ) == MessageBoxResult.Yes;
        }

        private static bool ConfirmDanger(
            string msg)
        {
            return MessageBox.Show(
                msg,
                "WARNING - SHNK TOOLS",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            ) == MessageBoxResult.Yes;
        }
    }

    // =========================================================
    // APP SETTINGS
    // =========================================================
    public sealed class AppSettings
    {
        public string? Emu32InstallerUrl
        {
            get;
            set;
        }

        public string? Emu32InstallerFileName
        {
            get;
            set;
        }
    }
}