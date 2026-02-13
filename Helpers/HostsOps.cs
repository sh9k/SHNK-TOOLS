using System;
using System.IO;

namespace SHNK.Tools.App
{
    public static class HostsOps
    {
        private static string HostsPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");

        private static void BackupIfExists()
        {
            if (!File.Exists(HostsPath)) return;
            var bak = HostsPath + $".bak_{DateTime.Now:yyyyMMdd_HHmmss}";
            File.Copy(HostsPath, bak, overwrite: true);
            Logger.Log($"Hosts backup: {bak}");
        }

        public static void DeleteHostsWithBackup()
        {
            BackupIfExists();
            if (File.Exists(HostsPath))
            {
                File.Delete(HostsPath);
                Logger.Log("Hosts deleted.");
            }
            else Logger.Log("Hosts not found.");
        }

        public static void ReplaceHostsWithBackup(string hostsSourceFile)
        {
            BackupIfExists();
            Directory.CreateDirectory(Path.GetDirectoryName(HostsPath)!);
            File.Copy(hostsSourceFile, HostsPath, overwrite: true);
            Logger.Log("Hosts replaced.");
        }
    }
}
