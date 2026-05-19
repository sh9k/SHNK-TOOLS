using System;
using System.IO;
using System.Reflection;

namespace SHNK.Tools.App
{
    public static class AssetExtractor
    {
        private static readonly string TempDir =
            Path.Combine(Path.GetTempPath(), "SHNKTOOLS");

        public static string Extract(string resourceName, string outName)
        {
            Directory.CreateDirectory(TempDir);

            string outPath = Path.Combine(TempDir, outName);

            var asm = Assembly.GetExecutingAssembly();

            using Stream? s = asm.GetManifestResourceStream(resourceName);

            if (s == null)
                throw new Exception("Missing resource: " + resourceName);

            using FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write);

            s.CopyTo(fs);

            return outPath;
        }
    }
}