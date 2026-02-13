using System.IO;
using System.IO.Compression;

namespace SHNK.Tools.App
{
    public static class ZipOps
    {
        public static string ExtractToCache(string zipPath, string cacheName)
        {
            var outDir = Path.Combine(Paths.CacheDir(), cacheName);

            if (Directory.Exists(outDir))
                Directory.Delete(outDir, recursive: true);

            Directory.CreateDirectory(outDir);
            ZipFile.ExtractToDirectory(zipPath, outDir);
            Logger.Log("Extracted ZIP: " + outDir);
            return outDir;
        }
    }
}
