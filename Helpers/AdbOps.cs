using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SHNK.Tools.App
{
    public static class AdbOps
    {
        public static string? TryFindAdbExe(string? gameLoopUiPath)
        {
            // 1) إذا عندنا مسار UI، نبحث حواليه
            if (!string.IsNullOrWhiteSpace(gameLoopUiPath))
            {
                var root = Directory.GetParent(gameLoopUiPath)?.FullName; // ...\TxGameAssistant
                if (!string.IsNullOrWhiteSpace(root))
                {
                    // شائع داخل Gameloop
                    var candidates = new[]
                    {
                        Path.Combine(root, "AppMarket", "adb.exe"),
                        Path.Combine(root, "UI", "adb.exe"),
                        Path.Combine(root, "adb.exe"),
                        Path.Combine(Directory.GetParent(root)?.FullName ?? "", "adb.exe"),
                    };

                    foreach (var c in candidates)
                        if (File.Exists(c)) return c;
                }
            }

            // 2) fallback: adb من PATH (لو منصّبه المستخدم)
            return "adb";
        }

        public static async Task<int> RunAdbAsync(string adbExe, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = adbExe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

            p.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Logger.Log("[ADB] " + e.Data); };
            p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Logger.Log("[ADB] " + e.Data); };

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            await p.WaitForExitAsync();
            return p.ExitCode;
        }

        public static async Task EnsureServerAsync(string adbExe)
        {
            Logger.Log("ADB: kill-server...");
            await RunAdbAsync(adbExe, "kill-server");

            Logger.Log("ADB: start-server...");
            await RunAdbAsync(adbExe, "start-server");
        }
    }
}
