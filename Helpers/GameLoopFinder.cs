using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SHNK.Tools.App
{
    public static class GameLoopFinder
    {
        public static string? FindUiPath()
        {
            // 1) Registry uninstall keys
            var reg = FindFromRegistry();
            if (!string.IsNullOrWhiteSpace(reg))
            {
                var ui = NormalizeUiPath(reg!);
                if (ui != null) return ui;
            }

            // 2) Known common paths
            foreach (var p in KnownPaths())
            {
                var ui = NormalizeUiPath(p);
                if (ui != null) return ui;
            }

            // 3) Drive scan (fast)
            var scan = FindByDriveScan();
            if (!string.IsNullOrWhiteSpace(scan)) return scan;

            return null;
        }

        private static string? NormalizeUiPath(string installOrAnyPath)
        {
            if (string.IsNullOrWhiteSpace(installOrAnyPath)) return null;

            // if given install location root:
            // try TxGameAssistant\UI under it
            var candidates = new List<string>();

            var root = installOrAnyPath.Trim('"');

            // If path already ends with UI
            candidates.Add(root);

            // Common folders
            candidates.Add(Path.Combine(root, "TxGameAssistant", "UI"));
            candidates.Add(Path.Combine(root, "txgameassistant", "ui"));
            candidates.Add(Path.Combine(root, "UI"));
            candidates.Add(Path.Combine(root, "ui"));

            // Program Files patterns
            candidates.Add(Path.Combine(root, "Program Files", "txgameassistant", "ui"));
            candidates.Add(Path.Combine(root, "Program Files", "TxGameAssistant", "UI"));

            foreach (var c in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(c))
                {
                    // Verify this is really GameLoop UI folder:
                    var androidEmu = Path.Combine(c, "AndroidEmulator.exe");
                    var tsetting = Path.Combine(c, "TSettingCenter.exe");
                    if (File.Exists(androidEmu) || File.Exists(tsetting))
                        return c;
                }
            }

            return null;
        }

        private static string? FindFromRegistry()
        {
            // search both 64bit + 32bit uninstall
            var roots = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var root in roots)
            {
                using var key = Registry.LocalMachine.OpenSubKey(root);
                if (key == null) continue;

                foreach (var sub in key.GetSubKeyNames())
                {
                    using var sk = key.OpenSubKey(sub);
                    if (sk == null) continue;

                    var name = (sk.GetValue("DisplayName") as string) ?? "";
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    if (!LooksLikeGameLoop(name)) continue;

                    var install = (sk.GetValue("InstallLocation") as string)
                                  ?? (sk.GetValue("InstallSource") as string)
                                  ?? "";

                    if (!string.IsNullOrWhiteSpace(install))
                        return install;
                }
            }

            return null;
        }

        private static bool LooksLikeGameLoop(string displayName)
        {
            displayName = displayName.ToLowerInvariant();
            return displayName.Contains("gameloop")
                || displayName.Contains("tencent")
                || displayName.Contains("mobilegamepc")
                || displayName.Contains("mobile game pc")
                || displayName.Contains("txgameassistant");
        }

        private static IEnumerable<string> KnownPaths()
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            return new[]
            {
                @"C:\Program Files\txgameassistant",
                @"C:\Program Files\TxGameAssistant",
                @"C:\txgameassistant",
                @"D:\txgameassistant",
                @"E:\txgameassistant",
                @"F:\txgameassistant",

                Path.Combine(pf, "txgameassistant"),
                Path.Combine(pf, "TxGameAssistant"),
                Path.Combine(pf86, "txgameassistant"),
                Path.Combine(pf86, "TxGameAssistant"),
            };
        }

        private static string? FindByDriveScan()
        {
            // Fast scan: only root-level + Program Files folders on each drive
            var drives = DriveInfo.GetDrives()
                                  .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                                  .Select(d => d.RootDirectory.FullName)
                                  .ToArray();

            var probeFolders = new[]
            {
                "", // root
                "Program Files",
                "Program Files (x86)",
            };

            foreach (var drv in drives)
            {
                foreach (var pf in probeFolders)
                {
                    var baseDir = string.IsNullOrEmpty(pf) ? drv : Path.Combine(drv, pf);

                    // candidates
                    var candidates = new[]
                    {
                        Path.Combine(baseDir, "txgameassistant", "ui"),
                        Path.Combine(baseDir, "TxGameAssistant", "UI"),
                        Path.Combine(baseDir, "txgameassistant"),
                        Path.Combine(baseDir, "TxGameAssistant"),
                    };

                    foreach (var c in candidates)
                    {
                        var ui = NormalizeUiPath(c);
                        if (ui != null) return ui;
                    }
                }
            }

            return null;
        }
    }
}
