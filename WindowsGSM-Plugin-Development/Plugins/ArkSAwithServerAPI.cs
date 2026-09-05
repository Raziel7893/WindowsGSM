using System;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.ComponentModel;
using System.Threading;
using Newtonsoft.Json.Linq;

using System.Collections.Generic;
using System.IO.Compression;

namespace WindowsGSM.Plugins
{
    public class ArkSAwithServerAPI : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.ArkSAwithServerAPI", // WindowsGSM.XXXX
            author = "ohmcodes™",
            description = "WindowsGSM plugin for supporting ARK:SA™ Dedicated Server with ServerAPI from GameServerHub",
            version = "2.9.2",
            url = "https://github.com/ohmcodes/WindowsGSM.ArkSAwithServerAPI", // Github repository link (Best practice)
            color = "#008B8B" // Color Hex
        };

        // - Standard Constructor and properties
        public ArkSAwithServerAPI(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;

        private readonly string downloadUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";
        private readonly string reqFileName = "VC_redist.x64.exe";
        public readonly string registryPath = @"SOFTWARE\Microsoft\VisualStudio\14.0_Config\VC\Runtimes\X64";
        private readonly Version requiredVersion = new Version("14.38.33.130");
        private string serverAPIFileName = "AsaApi.v0.1.zip";
        // https://github.com/ArkServerApi/AsaApi/releases/latest
        public string apiUrl = "https://api.github.com/repos/ArkServerApi/AsaApi/releases/latest";

        public string currentAPIVersion = "0.1";

        private List<string> filesToDelete;

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "2430930"; /* taken via https://steamdb.info/app/2430930/info/ */

        // - Game server Fixed variables
        public string StartPath = @"ShooterGame\Binaries\Win64\ArkAscendedServer.exe"; // Game server start path
        public string FullName = "ARK:SA™ Dedicated Server with ServerAPI"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 2; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string ServerName = "wgsm_arksa_serverapi_dedicated";
        public string Defaultmap = "TheIsland_WP"; // Default map name
        public string Maxplayers = "70"; // Default maxplayers
        public string Port = "7777"; // Default port
        public string QueryPort = "27015"; // Default query port
        public string Additional = "?RCONEnabled=True?RCONPort=31001?ServerAutoForceRespawnWildDinosInterval=86400?AllowCrateSpawnsOnTopOfStructures=True -UseBattlEye -server -crossplay -nosteamclient -servergamelog -log -lowmem -nosound -ServerRCONOutputTribeLogs -culture=en -servergamelogincludetribelogs -forcerespawndinos"; // Additional server start parameter

        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            if (File.Exists(ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath)))
            {
                if (!IsRedistributableInstalled(requiredVersion))
                {
                    Notice = "Downloading VC_Distribution";
                    DownloadVCPackage();
                }
                DownloadServerAPI();
            }
        }
        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            await Task.Delay(1);
            string overrideStartPath = @"ShooterGame\Binaries\Win64\AsaApiLoader.exe";
            string shipExePath = ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            var param = new StringBuilder();

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerMap) ? _serverData.ServerMap : "TheIsland_WP");

            param.Append("?listen");

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerName) ? $"?SessionName=\"\"\"{_serverData.ServerName}\"\"\"" : "SessionName=\"wgsm_arksa_serverapi_dedicated\"");

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerIP) ? $"?MultiHome={_serverData.ServerIP}" : _serverData.GetIPAddress());

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerPort) ? $"?Port={_serverData.ServerPort}" : _serverData.GetAvailablePort(_serverData.ServerPort, PortIncrements));

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerQueryPort) ? $"?QueryPort={_serverData.ServerQueryPort}" : _serverData.GetAvailablePort(_serverData.ServerQueryPort, PortIncrements));

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer) ? $"?MaxPlayers={_serverData.ServerMaxPlayer}" : $"?MaxPlayers=70");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerParam))
            {
                if (_serverData.ServerParam.StartsWith("?"))
                {
                    param.Append($"{_serverData.ServerParam}");
                }
                else if (_serverData.ServerParam.StartsWith("-"))
                {
                    param.Append($" {_serverData.ServerParam}");
                }
            }

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer) ? $" -WinLiveMaxPlayers={_serverData.ServerMaxPlayer}" : $"?MaxPlayers=70");


            Process p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID, @"ShooterGame\Binaries\Win64\"),
                    FileName = shipExePath,
                    Arguments = param.ToString(),
                    WindowStyle = ProcessWindowStyle.Normal,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    StandardInputEncoding = Encoding.UTF8,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (_serverData.EmbedConsole)
            {
                p.StartInfo.CreateNoWindow = false;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
                p.Start();
                if (_serverData.EmbedConsole)
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
                Functions.ServerConsole.SendWaitToMainWindow("cheat DoExit");
                Task.Delay(10000);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
            });
            await Task.Delay(2000);
        }
        public async Task<Process> Install()
        {
            var steamCMD = new Installer.SteamCMD();
            Process p = await steamCMD.Install(_serverData.ServerID, string.Empty, AppId, true, loginAnonymous);
            Error = steamCMD.Error;


            return p;
        }
        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(_serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: loginAnonymous);
            Error = error;

            if (error == null)
            {
                DownloadServerAPI();
            }
            return p;
        }
        public bool IsInstallValid()
        {
            return File.Exists(ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }
        public bool IsImportValid(string path)
        {
            string importPath = Path.Combine(path, StartPath);
            Error = $"Invalid Path! Fail to find {Path.GetFileName(StartPath)}";
            return File.Exists(importPath);
        }
        public string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }
        public async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }
        private bool IsRedistributableInstalled(Version requiredVersion)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"))
            {
                if (key != null)
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        Trace.WriteLine($"Key {subKeyName}");
                        using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                        {
                            if (subKey != null)
                            {
                                Version installedVersion = new Version(subKey.GetValue("Version")?.ToString() ?? "0.0.0.0");

                                if (installedVersion > requiredVersion)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }
        private async void DownloadVCPackage()
        {
            if (File.Exists(reqFileName))
            {
                // Delete zip file
                await FileManagement.DeleteAsync(reqFileName);

            }
            WebClient webClient = new WebClient();
            webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
            webClient.DownloadFileCompleted += WebClient_DownloadFileCompletedVCPackage;

            try
            {
                webClient.DownloadFileAsync(new Uri(downloadUrl), ServerPath.GetServersServerFiles(_serverData.ServerID, reqFileName));
            }
            catch (Exception ex)
            {
                Error = $"Error downloading installer: {ex.Message}";
            }
        }
        private void InstallVCPackage()
        {
            if (File.Exists(reqFileName))
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(ServerPath.GetServersServerFiles(_serverData.ServerID, reqFileName));
                    Process.Start(startInfo);

                }
                catch (Exception ex)
                {
                    Error = $"Error installing redistributable: {ex.Message}";
                }
            }
            else
            {
                Error = "Installer not found. Please download it first.";
            }
        }
        private async void DownloadServerAPI()
        {
            string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3";

            string versionPath = ServerPath.GetServersServerFiles(_serverData.ServerID, @"version.txt");

            if (File.Exists(versionPath))
            {
                currentAPIVersion = ReadVersion();
            }

            WebClient webClient = new WebClient();
            webClient.Headers.Add(HttpRequestHeader.UserAgent, userAgent);
            try
            {
                // Download the latest release information from the GitHub API
                string responseContent = webClient.DownloadString(apiUrl);
                JObject releaseInfo = JObject.Parse(responseContent);

                string version = (releaseInfo["name"].ToString()).Trim();
                if (version == currentAPIVersion)
                {
                    return;
                }

                // Get the download URL of the first asset (assuming there is at least one asset)
                JToken asset = releaseInfo["assets"]?.FirstOrDefault();
                string downloadUrl = (asset["browser_download_url"].ToString()).Trim();
                string[] urlSegments = downloadUrl.Split('/');
                string filename = urlSegments[urlSegments.Length - 1];
                serverAPIFileName = filename;

                WriteVersion(version);
                webClient.DownloadFileAsync(new Uri(downloadUrl), ServerPath.GetServersServerFiles(_serverData.ServerID, filename));

                //await Task.Delay(5000);

                //CleanServerAPI();
            }
            catch (WebException ex)
            {
                // Handle exceptions
                Error = $"Error: {ex.Message}";
            }

            return;
        }

        //deprecated
        public async void CleanServerAPI()
        {
            string apiFilePath = ServerPath.GetServersServerFiles(_serverData.ServerID, serverAPIFileName);
            string tmpDestination = ServerPath.GetServersServerFiles(_serverData.ServerID, @"tmp\");
            string directoryPath = ServerPath.GetServersServerFiles(_serverData.ServerID, @"ShooterGame\Binaries\Win64\");

            if (Directory.Exists(tmpDestination))
            {
                try
                {
                    Directory.Delete(tmpDestination, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fail to delete {tmpDestination} {ex.Message}");
                    return;
                }
            }

            // Extract to get File List
            if (!await FileManagement.ExtractZip(apiFilePath, Directory.GetParent(tmpDestination).FullName))
            {
                Console.WriteLine($"Fail to extract {serverAPIFileName}");
                return;
            }
            else
            {
                // Get List of ServerAPI files
                GetFilesToDelete(Directory.GetParent(tmpDestination).FullName);
            }

            await Task.Run(() =>
            {
                foreach (string fileName in filesToDelete)
                {
                    string filePath = Path.Combine(directoryPath, fileName);
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            BackupConfigFiles(fileName);
                        }
                        else
                        {
                            Console.WriteLine($"File '{fileName}' does not exist.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting file '{fileName}': {ex.Message}");
                    }
                }

                filesToDelete.Clear();
                // Delete tmp
                Directory.Delete(tmpDestination, true);

                InstallServerAPI();
            });
        }
        public async void InstallServerAPI()
        {
            try
            {
                string apiFilePath = ServerPath.GetServersServerFiles(_serverData.ServerID, serverAPIFileName);
                string tmpDestination = ServerPath.GetServersServerFiles(_serverData.ServerID, @"tmp");
                string apiDestination = ServerPath.GetServersServerFiles(_serverData.ServerID, @"ShooterGame\Binaries\Win64\");

                if (!Directory.Exists(tmpDestination))
                {
                    Directory.CreateDirectory(tmpDestination);
                }

                // Extract to tmp folder
                if (!await FileManagement.ExtractZip(apiFilePath, Directory.GetParent(tmpDestination).FullName))
                {
                    Console.WriteLine($"Fail to extract {serverAPIFileName}");
                    return;
                }


                bool success = await CopyFiles2(
                    sourceDirectory: tmpDestination,
                    destinationDirectory: apiDestination,
                    folderToExclude: "Plugins"
                );

                if (success)
                {
                    Console.WriteLine("Copy succeeded.");
                }
                else
                {
                    Console.WriteLine("Copy failed.");
                }


                // Delete zip file
                await FileManagement.DeleteAsync(apiFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting package: {ex.Message}");
            }
        }
        public void GetFilesToDelete(string directoryPath)
        {
            filesToDelete = new List<string>();
            string tmpConfigFile = ServerPath.GetServersServerFiles(_serverData.ServerID, @"tmp2");
            // Get all files in the specified directory and its subdirectories
            foreach (string filePath in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                string directPath = filePath.Split(new[] { "tmp\\" }, StringSplitOptions.RemoveEmptyEntries)[1];

                // exclude files from filesToExclude
                //if (directPath.Contains(@"ArkApi\Plugins"))continue;
                Console.WriteLine($" ADDED TO DELETE PATHS {Path.Combine(directoryPath, directPath)}");
                filesToDelete.Add(directPath);
            }
        }
        private async void BackupConfigFiles(string fileName)
        {
            string tmpConfigFile = ServerPath.GetServersServerFiles(_serverData.ServerID, @"tmp2");
            string directoryPath = ServerPath.GetServersServerFiles(_serverData.ServerID, @"ShooterGame\Binaries\Win64\");
            string filePath = Path.Combine(directoryPath, fileName);
            string copySource = Path.Combine(directoryPath, fileName);
            string copyDestination = Path.Combine(tmpConfigFile, fileName);

            await Task.Run(async () =>
            {
                try
                {
                    if (filePath.Contains("config.json"))
                    {
                        // Create tmp2 folder for config files
                        if (!Directory.Exists(tmpConfigFile))
                            Directory.CreateDirectory(tmpConfigFile);

                        // Create the nested directories in the destination path
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(tmpConfigFile, fileName)));

                        // Copy the file to the destination path
                        File.Copy(copySource, copyDestination, true);
                        Console.WriteLine($"BACKUP SUCCESS! {copySource}");
                    }

                    if (File.Exists(filePath))
                    {
                        Console.WriteLine($"DELETING FILE '{filePath}'");
                        //File.Delete(filePath);
                        await FileManagement.DeleteAsync(filePath);
                    }

                    Console.WriteLine($"File '{fileName}' deleted successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"BackupConfigFiles() Error {ex.Message}");
                }
            });

            await Task.Delay(1000);
        }

        // deprecated
        private async void RestoreConfigFiles()
        {
            string tmpConfigFile = ServerPath.GetServersServerFiles(_serverData.ServerID, @"tmp2");
            string directoryPath = ServerPath.GetServersServerFiles(_serverData.ServerID, @"ShooterGame\Binaries\Win64\");

            if (!Directory.Exists(tmpConfigFile))
                return;

            await Task.Run(async () =>
            {
                try
                {
                    // Copy each file from the source to the destination
                    if (await CopyFiles(tmpConfigFile, directoryPath))
                    {
                        Console.WriteLine($"Attempting to delete tmp2");
                        // Delete tmp2 (Config files)
                        Directory.Delete(tmpConfigFile, true);
                        Console.WriteLine($"Deleted");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RestoreConfigFiles() Error {ex.Message}");
                }
            });
        }

        private async Task<bool> CopyFiles(string sourceDirectory, string destinationDirectory)
        {
            // Get all files in the source directory and its subdirectories
            string[] filesToCopy = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);

            return await Task.Run(() =>
            {
                foreach (string sourceFilePath in filesToCopy)
                {
                    // Construct the destination path by replacing the source directory with the destination directory
                    string relativePath = sourceFilePath.Substring(sourceDirectory.Length + 1);
                    string destinationFilePath = Path.Combine(destinationDirectory, relativePath);

                    if (!File.Exists(destinationFilePath))
                    {
                        Console.WriteLine($"Not Exist: {destinationFilePath}");
                        return false;
                    }

                    try
                    {
                        // Ensure the directory structure exists in the destination path
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));

                        // Copy the file to the destination path, overwriting if it already exists
                        File.Copy(sourceFilePath, destinationFilePath, overwrite: true);

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"CopyFiles() Error : {ex.Message}");
                        return false;
                    }
                }
                Console.WriteLine($"Done CopyFiles() LOOP");
                return true;
            });
        }

        private async Task<bool> CopyFiles2(string sourceDirectory, string destinationDirectory, string folderToExclude = "obj")
        {
            if (!Directory.Exists(sourceDirectory))
            {
                Console.WriteLine($"Source directory does not exist: {sourceDirectory}");
                return false;
            }

            return await Task.Run(() =>
            {
                try
                {
                    var sourceDir = new DirectoryInfo(sourceDirectory);

                    // Get all files, excluding those in the folder to exclude
                    var filesToCopy = sourceDir.GetFiles("*", SearchOption.AllDirectories)
                                               .Where(file => !IsSubDirectory(file.Directory, sourceDir, folderToExclude))
                                               .ToList();

                    foreach (var file in filesToCopy)
                    {
                        // Calculate relative path
                        string relativePath = file.FullName.Substring(sourceDirectory.Length + 1);
                        string destinationFilePath = Path.Combine(destinationDirectory, relativePath);

                        // Ensure destination directory exists
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));

                        // Copy file (overwrite)
                        File.Copy(file.FullName, destinationFilePath, overwrite: true);

                        Console.WriteLine($"Copied: {destinationFilePath}");
                    }

                    Console.WriteLine("Copy completed successfully.");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CopyFiles() Error: {ex.Message}");
                    return false;
                }
            });
        }

        // Helper: Check if a directory is a subdirectory of the excluded folder name
        private bool IsSubDirectory(DirectoryInfo currentDir, DirectoryInfo rootDir, string folderToExclude)
        {
            while (currentDir != null && currentDir.FullName.StartsWith(rootDir.FullName))
            {
                if (string.Equals(currentDir.Name, folderToExclude, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                currentDir = currentDir.Parent;
            }
            return false;
        }

        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Notice += $"Download Progress: {e.ProgressPercentage}";
        }
        private void WebClient_DownloadFileCompletedVCPackage(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Error = $"Download failed: {e.Error.Message}";
            }
            else
            {
                Notice = "Download completed successfully. Installing requirements now...";
                InstallVCPackage();
            }
        }
        private void WebClient_DownloadFileCompletedServerAPI(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Error = $"Download failed: {e.Error.Message}";
            }
            else
            {
                Notice = "Download completed successfully. Extracting server API now...";
                InstallServerAPI();
            }
        }

        private void WriteVersion(string version)
        {
            try
            {
                string versionPath = ServerPath.GetServersServerFiles(_serverData.ServerID, @"version.txt");
                StreamWriter sw = new StreamWriter(versionPath);
                sw.WriteLine(version);
                sw.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
            finally
            {
                Console.WriteLine("Executing finally block.");
            }
        }
        private string ReadVersion()
        {
            string line = "";
            try
            {
                string versionPath = ServerPath.GetServersServerFiles(_serverData.ServerID, @"version.txt");
                StreamReader sr = new StreamReader(versionPath);
                line = sr.ReadLine();
                while (line != null)
                {
                    Console.WriteLine(line);
                    line = sr.ReadLine();
                }
                sr.Close();
                //Console.ReadLine();


            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
            finally
            {
                Console.WriteLine("Executing finally block.");
            }

            return line;
        }
    }
}
