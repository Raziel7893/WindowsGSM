using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using System;

namespace WindowsGSM.Plugins
{
    public class REDM
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.RedM", // WindowsGSM.XXXX
            author = "ByBlackDeath",
            description = "WindowsGSM plugin for supporting Red Dead Redemption 2 Dedicated Server",
            version = "1.0.2",
            url = "https://github.com/IOxee/WindowsGSM.RedM", // Github repository link (Best practice)
            color = "#fa010c" // Color Hex
        };

        public static string GenerateShortUUID()
        {
            Guid guid = Guid.NewGuid();
            string shortUUID = guid.ToString("N").Substring(0, 8); 
            return shortUUID;
        }

        private string GetTxAdminPort(int serverId)
        {
            /*
                30120 + 10000 + 1 (serverId) = 40121
                30120 + 10000 + 2 (serverId) = 40122
            */
            return (int.Parse(Port) + txAdminPortIncrement + serverId).ToString(); 
        }


        // - Standard Constructor and properties
        public REDM(ServerConfig serverData)
        {
            _serverData = serverData;
            Additional = $"+set serverProfile \"{GenerateShortUUID()}\" +set txAdminVerbose true "; // I DO NOT ADD ANYTHING HERE OR DELETE THIS LINE
        }
        private readonly ServerConfig _serverData;
        public string Error, Notice;

        // - Game server Fixed variables
        public string StartPath => @"server\FXServer.exe"; // Game server start path
        public string FullName = "Red Dead Redemption 2 Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new FIVEM(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "30120"; // Default port
        public int txAdminPortIncrement = 10000;
        public string QueryPort = "30120"; // Default query port
        public string Defaultmap = "redm-map-one"; // Default map name
        public string Maxplayers = "32"; // Default maxplayers
        public string Additional = "";
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        public async void CreateServerCFG() {}

        public async Task<string> GetCacheDirectory()
        {
            string serverDataPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID);
            string[] directories = Directory.GetDirectories(serverDataPath);
            foreach (string directory in directories)
            {
                if (Directory.Exists(Path.Combine(directory, "cache")))
                {
                    return Path.Combine(directory, "cache");
                }
            }
            return null;
        }
        
        public async Task<Process> Start()
        {
            string cachePath = await GetCacheDirectory();
            if (cachePath != null) 
                if (Directory.Exists(cachePath))
                    Directory.Delete(cachePath, true);

            string fxServerPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"server\FXServer.exe");
            if (!File.Exists(fxServerPath))
            {
                Error = $"FXServer.exe not found ({fxServerPath})";
                return null;
            }

            string citizenPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"server\citizen");
            if (!Directory.Exists(citizenPath))
            {
                Error = $"Directory citizen not found ({citizenPath})";
                return null;
            }

            string serverDataPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID);
            if (!Directory.Exists(serverDataPath))
            {
                Error = $"Directory not found ({serverDataPath})";
                return null;
            }

            Process p;
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = serverDataPath,
                        FileName = fxServerPath,
                        Arguments = $"+set citizen_dir \"{citizenPath}\" {_serverData.ServerParam}",
                        WindowStyle = ProcessWindowStyle.Minimized,
                        UseShellExecute = false
                    },
                    EnableRaisingEvents = true
                };
                p.Start();
            }
            else
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = serverDataPath,
                        FileName = fxServerPath,
                        Arguments = $"+set citizen_dir \"{citizenPath}\" {_serverData.ServerParam}",
                        WindowStyle = ProcessWindowStyle.Minimized,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                var serverConsole = new Functions.ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }

            return p;
        }

        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                if (p.StartInfo.RedirectStandardInput)
                {
                    p.StandardInput.WriteLine("quit");
                }
                else
                {
                    Functions.ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "quit");
                }
            });
        }

        public async Task<Process> Install()
        {
            string log = "";
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    string html = await webClient.DownloadStringTaskAsync("https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/");
                    Regex regex = new Regex(@"[0-9]{4}-[a-f0-9]{40}");
                    var matches = regex.Matches(html);

                    if (matches.Count <= 0)
                    {
                        log += "No versions found. \n";
                        return null;
                    }

                    // Find the highest version
                    string highestVersionInfo = null;
                    int highestVersion = 0;
                    foreach (Match match in matches)
                    {
                        string versionInfo = match.Value;
                        int versionNumber = int.Parse(versionInfo.Split('-')[0]);
                        if (versionNumber > highestVersion)
                        {
                            highestVersion = versionNumber;
                            highestVersionInfo = versionInfo;
                        }
                    }

                    string recommended = highestVersionInfo;
                    log += $"Highest version found: {recommended} \n";

                    //Download server.zip and extract then delete server.zip
                    string serverPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "server");
                    Directory.CreateDirectory(serverPath);
                    string zipPath = Path.Combine(serverPath, "server.zip");
                    await webClient.DownloadFileTaskAsync($"https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/{recommended}/server.zip", zipPath);
                    await Task.Run(async () =>
                    {
                        try
                        {
                            await FileManagement.ExtractZip(zipPath, serverPath);
                        }
                        catch
                        {
                            log += "Path too long \n";
                        }
                    });
                    await Task.Run(() => File.Delete(zipPath));

                    //Create FiveM-version.txt and write the downloaded version with hash
                    File.WriteAllText(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "RedM-version.txt"), recommended);

                    // zipPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "cfx-server-data-master.zip");
                    // await webClient.DownloadFileTaskAsync("https://github.com/citizenfx/cfx-server-data/archive/master.zip", zipPath);
                    // await Task.Run(() => FileManagement.ExtractZip(zipPath, Functions.ServerPath.GetServersServerFiles(_serverData.ServerID)));
                    // await Task.Run(() => File.Delete(zipPath));

                    Additional += $"+set txAdminPort {GetTxAdminPort(int.Parse(_serverData.ServerID))} +set txDataPath \"{Functions.ServerPath.GetServersServerFiles(_serverData.ServerID)}\"";
                }

                return null;
            }
            catch (Exception e)
            {
                log += e.Message;
                File.WriteAllText(Path.Combine("servers", "installLog.txt"), log);
                return null;
            }
        }

        public async Task<Process> Update()
        {
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    string remoteBuild = await GetRemoteBuild();

                    //Download server.zip and extract then delete server.zip
                    string serverPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "server");
                    await Task.Run(() =>
                    {
                        try
                        {
                            Directory.Delete(serverPath, true);
                        }
                        catch (Exception e)
                        {
                            Error = $"Unable to delete server folder. Path: {serverPath} Error: {e.Message}";
                            Console.WriteLine(Error);
                        }
                    });

                    if (Directory.Exists(serverPath))
                    {
                        Error = $"Unable to delete server folder. Path: {serverPath}";
                        return null;
                    }

                    Directory.CreateDirectory(serverPath);
                    string zipPath = Path.Combine(serverPath, "server.zip");
                    await webClient.DownloadFileTaskAsync($"https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/{remoteBuild}/server.zip", zipPath);
                    await Task.Run(async () =>
                    {
                        try
                        {
                            await FileManagement.ExtractZip(zipPath, serverPath);
                        }
                        catch (Exception e)
                        {
                            Error = "Path too long \n" + e.Message;
                            Console.WriteLine(Error);
                        }
                    });
                    await Task.Run(() => File.Delete(zipPath));
                    File.WriteAllText(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "RedM-version.txt"), remoteBuild);
                }

                return null;
            }
            catch (Exception e)
            {
                Error = e.Message;
                Console.WriteLine(Error);
                return null;
            }
        }

        public bool IsInstallValid()
        {
            string exeFile = @"server\FXServer.exe";
            string exePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, exeFile);

            return File.Exists(exePath);
        }

        public bool IsImportValid(string path)
        {
            string exeFile = @"server\FXServer.exe";
            string exePath = Path.Combine(path, exeFile);

            Error = $"Invalid Path! Fail to find {exeFile}";
            return File.Exists(exePath);
        }

        public string GetLocalBuild()
        {
            string versionPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "RedM-version.txt");
            // Error = $"Fail to get local build";
            return File.Exists(versionPath) ? File.ReadAllText(versionPath) : string.Empty;
        }

        public async Task<string> GetRemoteBuild()
        {
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    string html = await webClient.DownloadStringTaskAsync("https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/");
                    Regex regex = new Regex(@"[0-9]{4}-[a-f0-9]{40}");
                    var matches = regex.Matches(html);

                    if (matches.Count <= 0)
                    {
                        Console.WriteLine("No versions found.");
                        return null;
                    }

                    // Find the highest version
                    string highestVersionInfo = null;
                    int highestVersion = 0;
                    foreach (Match match in matches)
                    {
                        string versionInfo = match.Value;
                        int versionNumber = int.Parse(versionInfo.Split('-')[0]);
                        if (versionNumber > highestVersion)
                        {
                            highestVersion = versionNumber;
                            highestVersionInfo = versionInfo;
                        }
                    }

                    return highestVersionInfo;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            Error = $"Fail to get remote build";
            Console.WriteLine(Error);
            return string.Empty;
        }

        
    }
}
