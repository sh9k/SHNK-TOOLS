using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SHNK.Tools.App
{
    public static class Downloader
    {
        private static readonly HttpClient _http = new HttpClient();

        public static async Task DownloadFileAsync(string url, string destPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            using var res = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            res.EnsureSuccessStatusCode();

            await using var src = await res.Content.ReadAsStreamAsync();
            await using var dst = File.Create(destPath);
            await src.CopyToAsync(dst);

            Logger.Log("Downloaded: " + destPath);
        }
    }
}
