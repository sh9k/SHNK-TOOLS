using System;
using System.IO;

namespace SHNK.Tools.App
{
    public static class FileOps
    {
        public static void CopyDirectory(string sourceDir, string targetDir, bool overwrite)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var name = Path.GetFileName(file);
                var dest = Path.Combine(targetDir, name);
                File.Copy(file, dest, overwrite);
                Logger.Log($"Copied: {name}");
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var name = Path.GetFileName(dir);
                CopyDirectory(dir, Path.Combine(targetDir, name), overwrite);
            }
        }

        public static void SafeDeleteContents(string dir)
        {
            if (!Directory.Exists(dir)) return;

            foreach (var f in Directory.GetFiles(dir))
            {
                try { File.Delete(f); } catch { }
            }

            foreach (var d in Directory.GetDirectories(dir))
            {
                try { Directory.Delete(d, recursive: true); } catch { }
            }
        }
    }
}
