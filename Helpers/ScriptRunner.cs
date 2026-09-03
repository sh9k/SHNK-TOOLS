using System;
using System.Diagnostics;
using System.Threading.Tasks;
namespace SHNK.Tools.App
{
    public static class ScriptRunner
    {
        public static async Task RunBatWithLiveLog(string batPath, Action<string>? onLine = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = new Process { StartInfo = psi };
            p.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Logger.Log(e.Data!);
                    onLine?.Invoke(e.Data!);
                }
            };
            p.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Logger.Log("[ERR] " + e.Data!);
                    onLine?.Invoke("[ERR] " + e.Data!);
                }
            };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            await p.WaitForExitAsync();
            Logger.Log($"BAT ExitCode: {p.ExitCode}");
            onLine?.Invoke($"Exit code: {p.ExitCode}");
        }
        public static async Task RunExeWithLiveLog(string exePath, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = new Process { StartInfo = psi };
            p.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Logger.Log(e.Data!); };
            p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Logger.Log("[ERR] " + e.Data!); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            await p.WaitForExitAsync();
            Logger.Log($"EXE ExitCode: {p.ExitCode}");
        }
    }
}
