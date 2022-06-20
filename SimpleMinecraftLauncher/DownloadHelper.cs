using Downloader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SimpleMinecraftLauncher
{
    public static class DownloadHelper
    {
        public static async Task DownloadFile(string url, string path, bool showProgress)
        {
            // Don't download if already exists
            if (File.Exists(path)) return;

            // Download options
            var downloadOpt = new DownloadConfiguration()
            {
                BufferBlockSize = 8000,
                ChunkCount = 1,
                MaximumBytesPerSecond = 0,
                MaxTryAgainOnFailover = int.MaxValue,
                OnTheFlyDownload = true,
                ParallelDownload = false,
                TempDirectory = Path.GetTempPath(),
                Timeout = 1000,
                RequestConfiguration =
                {
                    Accept = "*/*",
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    CookieContainer = new CookieContainer(),
                    Headers = new WebHeaderCollection(),
                    KeepAlive = false,
                    ProtocolVersion = HttpVersion.Version11,
                    UseDefaultCredentials = false,
                    UserAgent = $"UserAgent/SimpleMinecraftLauncher",
                    AllowAutoRedirect = true
                }
            };

            // Creates our single file download service
            var downloader = new DownloadService(downloadOpt);

            // Show progress
            if (showProgress)
            {
                downloader.DownloadStarted += (sender, e) =>
                {
                    Console.WriteLine($"Starting download for: \"{e.FileName}\"");
                };

                downloader.DownloadProgressChanged += (sender, e) =>
                {
                    Console.WriteLine($"Progress: {e.ProgressPercentage}%");
                };
            }
            downloader.DownloadFileCompleted += (sender, e) =>
            {
                if (showProgress && e.Error == null)
                {
                    Console.WriteLine("Download complete.");
                }
                if (e.Error != null)
                {
                    MessageBox.Show($"Error downloading file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(1);
                }
            };

            await downloader.DownloadFileTaskAsync(url, path);
        }
    }
}
