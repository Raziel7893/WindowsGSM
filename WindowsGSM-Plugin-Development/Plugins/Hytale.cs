using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;

namespace WindowsGSM.Plugins
{
    public class Hytale
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Hytale", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting Hytale Dedicated Server",
            version = "1.1.1",
            url = "https://github.com/Raziel7893/WindowsGSM.Hytale", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Standard Constructor and properties
        public Hytale(ServerConfig _serverData) => serverData = _serverData;

        // - Game server Fixed variables
        ServerConfig serverData;
        public string Error { get; set; }
        public string Notice { get; set; }

        public string StartPath = "Server\\HytaleServer.jar"; //TODO: check correct path
        public string Defaultmap = "Assets.zip"; // Default map name
        public string FullName = "Hytale Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // - Game server default values
        public string Port = "5520"; // Default port

        public string Additional = $""; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "16"; // Default maxplayers        
        public string QueryPort = "5520"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.

        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        //Hytale specifics
        public const string DownloaderUrl = "https://downloader.hytale.com/hytale-downloader.zip";
        public const string JreApiUrl = "https://api.github.com/repos/adoptium/temurin25-binaries/releases/latest";

        public const string FallbackJreUrl = "https://github.com/adoptium/temurin25-binaries/releases/download/jdk-25.0.1%2B8/OpenJDK25U-jre_x64_windows_hotspot_25.0.1_8.zip";
        public const string JreRootPath = "JRE25";
        public const string InstallerFolder = "installer";

        public string JreZip = Path.Combine(InstallerFolder, "jre25.zip");
        public string HytaleZip = Path.Combine(InstallerFolder, "Hytale.zip");
        public string HytaleDownloaderZip = Path.Combine(InstallerFolder, "hytale-downloader.zip");
        public string HytaleDownloader = Path.Combine(InstallerFolder, "hytale-downloader-windows-amd64.exe");
        public string HytaleDownloaderCredentialsPath = Path.Combine(InstallerFolder, ".hytale-downloader-credentials.json");

        public string HytaleVersion = Path.Combine(InstallerFolder, "hytaleVersion.txt");
        public string JreVersion = Path.Combine(InstallerFolder, "jreVersion.txt");

        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            File.WriteAllText(ServerPath.GetServersServerFiles(serverData.ServerID, HytaleVersion), await GetRemoteBuild());
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string hytaleZipPath = ServerPath.GetServersServerFiles(serverData.ServerID, HytaleZip);
            string shipExePath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath);

            // Check if we need to extract the zip (either exe missing or zip is newer)
            bool shouldExtract = !File.Exists(shipExePath);
            if (!shouldExtract && File.Exists(hytaleZipPath))
            {
                var zipTime = File.GetLastWriteTime(hytaleZipPath);
                var exeTime = File.GetLastWriteTime(shipExePath);
                // Use a slightly larger epsilon for timestamp comparison to handle file system quirks
                if (zipTime >= exeTime)
                {
                    shouldExtract = true;
                }
            }

            if (shouldExtract)
            {
                if (File.Exists(hytaleZipPath))
                {
                    Notice = "Performing clean extraction of Hytale.zip... This may take a moment.";

                    string serverRoot = ServerPath.GetServersServerFiles(serverData.ServerID);
                    string assetsFile = Path.Combine(serverRoot, "Assets.zip");
                    string serverDir = Path.Combine(serverRoot, "Server");

                    try
                    {
                        if (File.Exists(assetsFile)) { File.Delete(assetsFile); }
                        if (Directory.Exists(serverDir)) { DeleteFolder(serverDir); }
                    }
                    catch (Exception e)
                    {
                        Notice = $"Warning: Could not perform clean cleanup: {e.Message}. Proceeding with merge extraction.";
                    }

                    await FileManagement.ExtractZip(hytaleZipPath, serverRoot);

                    // Update timestamp only if extraction seemingly worked
                    if (File.Exists(shipExePath))
                    {
                        File.SetLastWriteTime(shipExePath, DateTime.Now);
                    }
                }
                else if (!File.Exists(shipExePath))
                {
                    Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath}) and the hytale.zip is also not available";
                    return null;
                }
            }

            //prepare java parameters, maybe from a cfg? Lets try ServerstartParam first
            var paramSb = new StringBuilder();
            paramSb.Append(serverData.ServerGSLT);
            // paramSb.Append($" -XX:AOTCache=\"{ServerPath.GetServersServerFiles(serverData.ServerID, "Server", "HytaleServer.aot")}\"");
            paramSb.Append($" -jar \"{shipExePath}\"");
            paramSb.Append($" --assets \"{ServerPath.GetServersServerFiles(serverData.ServerID, serverData.ServerMap)}\"");
            paramSb.Append($" --bind {serverData.ServerIP}:{serverData.ServerPort}");
            paramSb.Append($" {serverData.ServerParam}");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = false,
                    WorkingDirectory = ServerPath.GetServersServerFiles(serverData.ServerID),
                    FileName = GetJavaPath(),
                    Arguments = paramSb.ToString(),
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (serverData.EmbedConsole)
            {
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.CreateNoWindow = true;
                var serverConsole = new ServerConsole(serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
                p.Start();
                if (serverData.EmbedConsole)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }
                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }

        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
                p.WaitForExit(2000);
                if (!p.HasExited)
                    p.Kill();
            });
        }

        public async Task<Process> Install()
        {
            string tmpInstallPath = ServerPath.GetServersServerFiles(serverData.ServerID, InstallerFolder);
            string hytaleInstallerZipPath = ServerPath.GetServersServerFiles(serverData.ServerID, HytaleDownloaderZip);
            string hytaleInstallerPath = ServerPath.GetServersServerFiles(serverData.ServerID, HytaleDownloader);
            string hytaleZip = ServerPath.GetServersServerFiles(serverData.ServerID, HytaleZip);
            string hytaleInstallerCredentials = ServerPath.GetServersServerFiles(serverData.ServerID, HytaleDownloaderCredentialsPath);

            Directory.CreateDirectory(tmpInstallPath);
            Directory.CreateDirectory(ServerPath.GetServersServerFiles(serverData.ServerID, JreRootPath));
            File.Create(ServerPath.GetServersServerFiles(serverData.ServerID, HytaleVersion)).Close();
            File.Create(ServerPath.GetServersServerFiles(serverData.ServerID, JreVersion)).Close();

            //Get Java
            await DownloadCurrentJre();

            //skip downloader for debugging
            if (File.Exists(".\\Hytale.zip"))
            {
                File.Copy(".\\Hytale.zip", hytaleZip);
                await Task.Delay(2000);
                return null;
            }

            //Get Hytale Downlaoder
            if (!await DownloadFileAsync(DownloaderUrl, hytaleInstallerZipPath)) return null;
            await FileManagement.ExtractZip(hytaleInstallerZipPath, ServerPath.GetServersServerFiles(serverData.ServerID, tmpInstallPath));

            return StartProcess(hytaleInstallerPath, $" -download-path \"{hytaleZip}\" -skip-update-check -credentials-path \"{hytaleInstallerCredentials}\"", true);
            //the hytale.zip will not be extracted here, this will be done in CreateServerCfg as the returning of the process is needed to pass on the output of the login page
        }

        public string GetJavaPath()
        {
            var subdirs = Directory.GetDirectories(ServerPath.GetServersServerFiles(serverData.ServerID, JreRootPath)).ToList();
            subdirs.Sort();

            string javaRoot = subdirs.Last();
            return Path.Combine(javaRoot, "bin\\java.exe");
        }

        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            string tmpInstallPath = ServerPath.GetServersServerFiles(serverData.ServerID, InstallerFolder);
            string versionPath = ServerPath.GetServersServerFiles(serverData.ServerID, HytaleVersion);
            string hytaleInstallerZipPath = ServerPath.GetServersServerFiles(serverData.ServerID, HytaleDownloaderZip);
            string hytaleInstallerPath = ServerPath.GetServersServerFiles(serverData.ServerID, HytaleDownloader);
            string hytaleZipPath = ServerPath.GetServersServerFiles(serverData.ServerID, HytaleZip);
            string hytaleInstallerCredentials = ServerPath.GetServersServerFiles(serverData.ServerID, HytaleDownloaderCredentialsPath);
            string shipExePath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath);

            //Check JRE update 
            //await DownloadCurrentJre();

            //Get Hytale Downloader
            if (!File.Exists(hytaleInstallerPath))
            {
                if (File.Exists(hytaleInstallerZipPath)) File.Delete(hytaleInstallerZipPath);
                if (!await DownloadFileAsync(DownloaderUrl, hytaleInstallerZipPath)) return null;

                File.Delete(ServerPath.GetServersServerFiles(serverData.ServerID, "Assets.zip"));
                DeleteFolder(ServerPath.GetServersServerFiles(serverData.ServerID, "Server"));
                await FileManagement.ExtractZip(hytaleInstallerZipPath, ServerPath.GetServersServerFiles(serverData.ServerID));
            }

            //update downloader if needed (synchronous check)
            Process update = StartProcess(hytaleInstallerPath, $" -check-update -skip-update-check -credentials-path \"{hytaleInstallerCredentials}\"");
            update.StandardInput.Close(); // Close input to prevent hanging
            update.WaitForExit(30000);

            string currentVersion = "none";
            if (File.Exists(versionPath))
            {
                currentVersion = File.ReadAllText(versionPath);
            }

            string remoteVersion = await GetRemoteBuild();

            // If we are already up to date, only proceed if jar is missing or user clicked Validate
            if (currentVersion == remoteVersion && !validate && File.Exists(shipExePath))
                return null;

            if (File.Exists(hytaleZipPath))
            {
                File.Delete(hytaleZipPath);
            }

            var downloaderProcess = StartProcess(hytaleInstallerPath, $" -download-path \"{hytaleZipPath}\" -skip-update-check -credentials-path \"{hytaleInstallerCredentials}\"");
            downloaderProcess.StandardInput.Close(); // Close input if not authenticated/no input needed

            // Update version file only AFTER the process has finished
            downloaderProcess.Exited += (sender, e) => {
                File.WriteAllText(versionPath, remoteVersion);
            };

            // We return the process so WindowsGSM can show the terminal output and handle authentication if needed.
            return downloaderProcess;
        }

        public Process StartProcess(string exe, string param = "", bool skipConsoleOutput = false)
        {
            Process p = null;
            try
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = ServerPath.GetServersServerFiles(serverData.ServerID, InstallerFolder),
                        FileName = exe,
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    },
                    EnableRaisingEvents = true
                };

                if (!skipConsoleOutput)
                {
                    var serverConsole = new ServerConsole(serverData.ServerID);
                    p.OutputDataReceived += serverConsole.AddOutput;
                    p.ErrorDataReceived += serverConsole.AddOutput;
                }

                p.Start();
                if (!skipConsoleOutput)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }

            }
            catch
            {
                Error = $"Could Not Execute {exe}";
            }

            return p;
        }

        public async Task<bool> DownloadFileAsync(string url, string relativePath)
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    await webClient.DownloadFileTaskAsync(url, relativePath);
                }
                return true;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return false;
            }

        }

        public string GetLocalBuild()
        {
            string versionPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, HytaleVersion);
            return File.Exists(versionPath) ? File.ReadAllText(versionPath) : "none";
        }

        public async Task<string> GetRemoteBuild()
        {
            string hytaleInstallerPath = ServerPath.GetServersServerFiles(serverData.ServerID, HytaleDownloader);
            string hytaleInstallerCredentials = ServerPath.GetServersServerFiles(serverData.ServerID, HytaleDownloaderCredentialsPath);

            string remoteVersion = "";
            if (!File.Exists(hytaleInstallerPath))
                return "offline";

            // Use -skip-update-check and ensure we don't deadlock
            Process version = StartProcess(hytaleInstallerPath, $" -print-version -skip-update-check -credentials-path \"{hytaleInstallerCredentials}\"", true);
            version.StandardInput.Close();

            // Read output and error to prevent pipe saturation
            var outputTask = version.StandardOutput.ReadLineAsync();
            var errorTask = version.StandardError.ReadToEndAsync();

            if (await Task.WhenAny(outputTask, Task.Delay(10000)) == outputTask)
            {
                remoteVersion = await outputTask;
            }

            version.WaitForExit(5000);
            Notice = $"got remote version of {remoteVersion}";
            return remoteVersion;
        }

        public bool IsInstallValid()
        {
            //need to check for the hytale.zip as we can't extract it dueto the oauth
            string installPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, HytaleZip);
            Error = $"Fail to find {installPath}";
            return File.Exists(installPath);
        }

        public bool IsImportValid(string path)
        {
            string importPath = Path.Combine(path, StartPath);
            Error = $"Invalid Path! Fail to find {Path.GetFileName(StartPath)}";
            return File.Exists(importPath);
        }

        private async Task DownloadCurrentJre()
        {
            string versionPath = ServerPath.GetServersServerFiles(serverData.ServerID, JreVersion);
            string jreZipPath = ServerPath.GetServersServerFiles(serverData.ServerID, JreZip);
            string jreDestPath = ServerPath.GetServersServerFiles(serverData.ServerID, JreRootPath);

            string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3";
            string currentAPIVersion = "";

            if (File.Exists(versionPath))
            {
                currentAPIVersion = File.ReadAllText(versionPath);
            }

            WebClient webClient = new WebClient();
            webClient.Headers.Add(HttpRequestHeader.UserAgent, userAgent);
            try
            {
                // Download the latest release information from the GitHub API
                string responseContent = webClient.DownloadString(JreApiUrl);
                JObject releaseInfo = JObject.Parse(responseContent);

                string version = (releaseInfo["name"].ToString()).Trim();
                if (version == currentAPIVersion)
                {
                    return;
                }
                //new JRE available, delete old and install new one
                ClearJre();
                // Get the download URL of the first asset (assuming there is at least one asset)
                var assets = releaseInfo["assets"].ToList();

                var winBinary = assets.Where(a => a["name"].ToString().Contains("OpenJDK25U-jre_x64_windows_hotspot_") && a["name"].ToString().EndsWith(".zip")).ToList();
                string downloadUrl = "";
                if (winBinary.Any())
                {
                    downloadUrl = (winBinary.First()["browser_download_url"].ToString()).Trim();
                    string[] urlSegments = downloadUrl.Split('/');
                    string filename = urlSegments[urlSegments.Length - 1];
                    string serverAPIFileName = filename;
                }
                else if (string.IsNullOrWhiteSpace(currentAPIVersion))
                {
                    downloadUrl = FallbackJreUrl; //no windowsbinary yet for latest, use the newest available at plugin dev time.
                    version = "jdk-25.0.1+8";
                }
                else
                {
                    return; //we already have JRE, do nothing
                }

                await webClient.DownloadFileTaskAsync(new Uri(downloadUrl), jreZipPath);

                DeleteFolder(jreDestPath);
                await FileManagement.ExtractZip(jreZipPath, jreDestPath);

                File.WriteAllText(versionPath, version);
            }
            catch (WebException ex)
            {
                // Handle exceptions
                Error = $"Error: {ex.Message}";
            }

            return;
        }

        public void ClearJre()
        {
            string jreZipPath = Path.Combine(ServerPath.GetServersServerFiles(serverData.ServerID, InstallerFolder), "jreInstall.zip");
            string javaRoot = ServerPath.GetServersServerFiles(serverData.ServerID, JreRootPath);

            File.Delete(jreZipPath);
            DeleteFolder(javaRoot);
        }

        private static void DeleteFolder(string javaRoot)
        {
            System.IO.DirectoryInfo di = new DirectoryInfo(javaRoot);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
        }
    }
}
