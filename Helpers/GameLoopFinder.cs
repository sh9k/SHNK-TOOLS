using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;

namespace SHNK.Tools.App
{
    public static class GameLoopFinder
    {
        public static string? DetectGameLoopRoot()
        {
            // Root is folder that contains: Root\TxGameAssistant\UI

            var p = FindFromUninstall(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
                 ?? FindFromUninstall(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");

            if (p != null)
            {
                var root = Normalize(p);
                if (HasUi(root)) return root;
            }

            var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady);
            foreach (var d in drives)
            {
                var baseDrive = d.RootDirectory.FullName;

                var candidates = new[]
                {
                    Path.Combine(baseDrive, @"Program Files\txgameassistant"),
                    Path.Combine(baseDrive, @"Program Files\TxGameAssistant"),
                    Path.Combine(baseDrive, @"Program Files (x86)\txgameassistant"),
                    Path.Combine(baseDrive, @"TxGameAssistant"),
                    Path.Combine(baseDrive, @"txgameassistant"),
                };

                foreach (var c in candidates)
                {
                    var root = Normalize(c);
                    if (HasUi(root)) return root;
                }
            }

            foreach (var d in drives)
            {
                try
                {
                    var dirs = Directory.GetDirectories(d.RootDirectory.FullName, "*txgameassistant*", SearchOption.TopDirectoryOnly);
                    foreach (var dir in dirs)
                    {
                        var root = Normalize(dir);
                        if (HasUi(root)) return root;
                    }
                }
                catch { }
            }

            return null;
        }

        private static bool HasUi(string root)
            => Directory.Exists(Path.Combine(root, "TxGameAssistant", "UI"));

        private static string Normalize(string p) => p.Trim().TrimEnd('\\');

        private static string? FindFromUninstall(RegistryKey root, string subKey)
        {
            try
            {
                using var key = root.OpenSubKey(subKey);
                if (key == null) return null;

                foreach (var name in key.GetSubKeyNames())
                {
                    using var appKey = key.OpenSubKey(name);
                    var display = appKey?.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(display)) continue;

                    if (display.Contains("GameLoop", StringComparison.OrdinalIgnoreCase) ||
                        display.Contains("MobileGamePC", StringComparison.OrdinalIgnoreCase) ||
                        display.Contains("Tencent", StringComparison.OrdinalIgnoreCase))
                    {
                        var loc = appKey?.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrWhiteSpace(loc) && Directory.Exists(loc)) return loc;

                        var icon = appKey?.GetValue("DisplayIcon") as string;
                        if (!string.IsNullOrWhiteSpace(icon))
                        {
                            var dir = Path.GetDirectoryName(icon.Trim('"'));
                            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir)) return dir;
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
