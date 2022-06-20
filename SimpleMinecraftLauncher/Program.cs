using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.UI.Wpf;
using CmlLib.Core.Installer.FabricMC;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;

namespace SimpleMinecraftLauncher
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            InstallWebView2Async().GetAwaiter().GetResult();
            var session = SignIn();
            Launch(session).GetAwaiter().GetResult();
        }

        private static async Task InstallWebView2Async()
        {
            // Install webview2 if not already present
            RegistryKey? key64_1 = Registry.LocalMachine.OpenSubKey(@"Software\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
            RegistryKey? key64_2 = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
            RegistryKey? key32_1 = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
            RegistryKey? key32_2 = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
            if (key64_1 == null && key64_2 == null && key32_1 == null && key32_2 == null)
            {
                if (MessageBox.Show("You are missing the WebView2 runtime, this is neccesary for logging into Minecraft.\nWould you like to download and install it?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    Environment.Exit(0);
                }
                await DownloadHelper.DownloadFile(@"https://go.microsoft.com/fwlink/p/?LinkId=2124703", $"{AppDomain.CurrentDomain.BaseDirectory}{Path.DirectorySeparatorChar}MicrosoftEdgeWebview2Setup.exe", false);
                Process p = new Process();
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    p.StartInfo.UseShellExecute = true;
                    p.StartInfo.Verb = "runas";
                }
                p.StartInfo.FileName = "MicrosoftEdgeWebview2Setup.exe";
                p.StartInfo.Arguments = "/silent /install";
                p.Start();
                await p.WaitForExitAsync();

                File.Delete($"{AppDomain.CurrentDomain.BaseDirectory}{Path.DirectorySeparatorChar}MicrosoftEdgeWebview2Setup.exe");
            }
        }

        private static MSession SignIn()
        {
            var loginHandler = new LoginHandler();
            MicrosoftLoginWindow loginWindow = new MicrosoftLoginWindow();
            var session = loginWindow.ShowLoginDialog();
            if (session != null)
            {
                if (session.CheckIsValid() == false)
                {
                    MessageBox.Show("Invalid session!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(1);
                    return null;
                }

                return session;
            }
            else
            {
                MessageBox.Show("Failed to login!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
                return null;
            }
        }

        private static async Task Launch(MSession session)
        {
            ServicePointManager.DefaultConnectionLimit = 265;
            var path = new MinecraftPath(".minecraft");
            var launcher = new CMLauncher(path);

            launcher.FileChanged += (e) =>
            {
                Console.WriteLine($"[{e.FileKind}] {e.FileName} -> {(double)e.ProgressedFileCount/e.TotalFileCount:P0} ({e.TotalFileCount - e.ProgressedFileCount} remaining...)");
            };

            var versions = await launcher.GetAllVersionsAsync();
            var launchOption = new MLaunchOption()
            {
                MaximumRamMb = 1024,
                Session = session,
                GameLauncherName = "SimpleMinecraftLauncher"
            };

            // Handle Fabric missing
            var fabricVersionLoader = new FabricVersionLoader();
            var fabricVersions = await fabricVersionLoader.GetVersionMetadatasAsync();

            // Install fabric
            if (!fabricVersions.Any(x => x.Name.Contains("1.16.5")))
            {
                MessageBox.Show("Could not find minecraft version.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
            var targetVersions = fabricVersions.Where(x => x.Name.Contains("1.16.5"));
            var fabric = fabricVersions.GetVersionMetadata(targetVersions.First().Name);
            await fabric.SaveAsync(path);
            await launcher.GetAllVersionsAsync();

            // Install/Update mods
            Directory.CreateDirectory($".minecraft{Path.DirectorySeparatorChar}mods");
            await Task.Run(async () =>
            {
                try
                {
                    string modsFolder = $".minecraft{Path.DirectorySeparatorChar}mods";

                    // Mod Menu
                    await DownloadHelper.DownloadFile(@"https://media.forgecdn.net/files/3479/748/modmenu-1.16.22.jar", $"{modsFolder}{Path.DirectorySeparatorChar}modmenu-1.16.22.jar", true);

                    // Sodium
                    await DownloadHelper.DownloadFile(@"https://media.forgecdn.net/files/3488/820/sodium-fabric-mc1.16.5-0.2.0%2Bbuild.4.jar", $"{modsFolder}{Path.DirectorySeparatorChar}sodium-fabric-mc1.16.5-0.2.0%2Bbuild.4.jar", true);

                    // Iris Shaders
                    await DownloadHelper.DownloadFile(@"https://media.forgecdn.net/files/3687/468/iris-mc1.16.5-1.2.2-build.30.jar", $"{modsFolder}{Path.DirectorySeparatorChar}iris-mc1.16.5-1.2.2-build.30.jar", true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error occured while downloading mods.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(1);
                }
            });

            // Create and launch process
            var process = await launcher.CreateProcessAsync(targetVersions.First().Name, launchOption);

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += (sender, e) =>
            {
                string message = e.Data;
                Console.WriteLine(message);
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                string message = e.Data;
                Console.WriteLine(message);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Console.WriteLine("Launched!\n");
            await process.WaitForExitAsync();
        }
    }
}