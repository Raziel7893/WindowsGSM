using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WindowsGSM.Functions
{
    public static class Http
    {
        private static readonly HttpClient Client = CreateClient(false);
        private static readonly HttpClient CompressedClient = CreateClient(true);

        private static HttpClient CreateClient(bool automaticDecompression)
        {
            var handler = new HttpClientHandler();
            if (automaticDecompression)
            {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }

            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WindowsGSM");
            client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            return client;
        }

        public static Task<string> DownloadStringAsync(string url, bool automaticDecompression = false)
        {
            return (automaticDecompression ? CompressedClient : Client).GetStringAsync(url);
        }

        public static string DownloadString(string url, bool automaticDecompression = false)
        {
            return DownloadStringAsync(url, automaticDecompression).GetAwaiter().GetResult();
        }

        public static async Task DownloadFileAsync(string url, string path, bool automaticDecompression = false)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var response = await (automaticDecompression ? CompressedClient : Client).GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using (var input = await response.Content.ReadAsStreamAsync())
                using (var output = File.Create(path))
                {
                    await input.CopyToAsync(output);
                }
            }
        }

        public static async Task<HttpResponseMessage> PostFormAsync(string url, string form)
        {
            using (var content = new StringContent(form, Encoding.UTF8, "application/x-www-form-urlencoded"))
            {
                return await Client.PostAsync(url, content);
            }
        }
    }
}
