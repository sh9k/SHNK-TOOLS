using System;
using System.IO;

namespace SHNK.Tools.App
{
    public static class Logger
    {
        private static string _logFile = "";

        public static void Init()
        {
            Directory.CreateDirectory(Paths.AppDataDir());
            _logFile = Path.Combine(Paths.AppDataDir(), $"shnk_{DateTime.Now:yyyyMMdd}.log");
            Log("Logger initialized.");
        }

        public static void Log(string msg)
        {
            try
            {
                File.AppendAllText(_logFile, $"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            }
            catch { }
        }
    }

    public static class Paths
    {
        public static string AppDataDir() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SHNK Tools");

        public static string CacheDir()
        {
            var p = Path.Combine(AppDataDir(), "cache");
            Directory.CreateDirectory(p);
            return p;
        }

        public static string Asset(string relative) =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relative);
    }
}
