using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.Functions;
using System.Text;
using System.Collections.Generic;
using System;

namespace WindowsGSM.Plugins
{
    //https://www.reddit.com/r/dayz/comments/afad51/automatically_update_and_sync_your_steam_workshop/
    public class DayZAuto : SteamCMDAgent
    {       
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.DayZAuto", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting DayZ Dedicated Server with automatic ModUpdates",
            version = "1.0.0",
            url = "https://github.com/raziel7893/WindowsGSM.DayZAuto", // Github repository link (Best practice)
            color = "00ff2a" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => false;
        public override string AppId => "223350"; // Game server appId Steam
        public string SteamGameId => "221100"; // Id of the Game itself. needed for workshop
//        public string SteamGameId => "221380"; // DEBUGGGG
        public override string StartPath => "DayZServer_x64.exe"; // Game server start path


        public string FullName = "DayZ Dedicated Server";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 1;
        public dynamic QueryMethod = new GameServer.Query.A2S();

        public string Port = "2302";
        public string QueryPort = "27016";
        public string Defaultmap = "DayZOffline.chernarusplus";
        public string Maxplayers = "60";
        public string Additional = "-config=serverDZ.cfg -doLogs -adminLog -netLog -profiles=profile";

        public DayZAuto(Functions.ServerConfig serverData) : base(serverData)
        {
        }

        public async void CreateServerCFG()
        {
            //Download serverDZ.cfg
            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "serverDZ.cfg");
            if (await Functions.Github.DownloadGameServerConfig(configPath, FullName))
            {
                StringBuilder configText = new StringBuilder(File.ReadAllText(configPath));
                configText = configText.Replace("{{hostname}}", serverData.ServerName);
                configText = configText.Replace("{{maxplayers}}", Maxplayers);
                configText.AppendLine("steamProtocolMaxDataSize = 4000;"); //should allow for more mods as this somehow affects how many parameters can be added via commandline 
                configText.AppendLine("enableCfgGameplayFile = 0;");
                configText.AppendLine("logFile = \"server_console.log\";");
                configText.AppendLine($"steamQueryPort = {QueryPort};            // defines Steam query port,");
                File.WriteAllText(configPath, configText.ToString());
            }
        }


        public async Task<Process> Start()
        {
            // Use DZSALModServer.exe if the exe exist, otherwise use original
            string dzsaPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "DZSALModServer.exe");
            if (File.Exists(dzsaPath))
            {
                StartPath = "DZSALModServer.exe";
            }
            else
            {
                string serverPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath);
                if (!File.Exists(serverPath))
                {
                    Error = $"{StartPath} not found ({serverPath})";
                    return null;
                }
            }

            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "serverDZ.cfg");
            if (!File.Exists(configPath))
            {
                CreateServerCFG();
            }

            string param = $" {serverData.ServerParam}";
            param += string.IsNullOrEmpty(serverData.ServerIP) ? string.Empty : $" -ip={serverData.ServerIP}";
            param += string.IsNullOrEmpty(serverData.ServerPort) ? string.Empty : $" -port={serverData.ServerPort}";

            string modPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "Modlist.txt");
            if (File.Exists(modPath))
            {
                var lines = File.ReadAllLines(modPath);
                var modParam = UpdateMods(new List<string>(lines));

                if (!string.IsNullOrWhiteSpace(modParam))
                {
                    param += $" \"-mod={modParam}\"";
                }
            }

            Process p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = Functions.ServerPath.GetServersServerFiles(serverData.ServerID),
                    FileName = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath),
                    Arguments = param,
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
        private void DebugMessageAsync(string msg) =>
            UI.CreateYesNoPromptV1("debug",msg,"yes", "yes");
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                p.Kill();
            });
        }

        private string UpdateMods(List<string> modList)
        {
            var modParam = "";
            var mods = new Dictionary<string,string>();
            int index = 0;
            foreach (string line in modList)
            {
                var splits = line.Split(',');
                if (splits.Length != 2)
                    continue;

                mods[splits[0]] = splits[1];

                modParam += $"{splits[1].Replace(",", "").Trim()};";
                index++;
            }
            DownloadMods(mods);

            return modParam;
        }

        private void DownloadMods(Dictionary<string, string> mods)
        {
            string _exeFile = "steamcmd.exe";
            string _installPath = ServerPath.GetBin("steamcmd");

            string exePath = Path.Combine(_installPath, _exeFile);

            if (!File.Exists(exePath))
            {
                Error = $"SteamCMD not available, break up";
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append($"{GetLogin()}");
            foreach (var mod in mods)
            {
                if (!string.IsNullOrEmpty(mod.Key) && !string.IsNullOrEmpty(mod.Value))
                    sb.Append($" +workshop_download_item {SteamGameId} {mod.Key}");//221100
            }

            sb.Append($" +quit");

            Process p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = _installPath,
                    FileName = exePath,
                    Arguments = sb.ToString(),
                    WindowStyle = ProcessWindowStyle.Minimized,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };
            p.Start();
            p.WaitForExit();
            CopyMods(mods, _installPath);

            return;
        }

        private void CopyMods(Dictionary<string, string> mods, string _installPath)
        {
            //go through all Plugins and create entry in keys
            var workshopPath = _installPath + $"\\steamapps\\workshop\\content\\{SteamGameId}\\";
            foreach (var mod in mods)
            {
                if (string.IsNullOrEmpty(mod.Key) || string.IsNullOrEmpty(mod.Value))
                {
                    Error = Error + $"Modlist entry invalid: key: {mod.Key}, Value:{mod.Value}";
                    continue;
                }

                var src = workshopPath + mod.Key;
                if (!Directory.Exists(src))
                {
                    Error = Error + $"Mod was not downloaded key: {mod.Key}, Value:{mod.Value}  path: {src}";
                    continue;
                }
              
                var destFolder = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, mod.Value);

                CopyDirectory(src, destFolder, true);

                //now search for *.bikey files
                var files = Directory.EnumerateFiles(destFolder, "*.bikey", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var dest = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "keys", Path.GetFileName(file));
                    File.Copy(file, dest);
                }
            }
        }

        private string GetLogin()
        {
            string _installPath = ServerPath.GetBin("steamcmd");
            string _userDataPath = Path.Combine(_installPath, "userData.txt");

            string steamUser = null, steamPass = null;

            if (File.Exists(_userDataPath))
            {
                string[] lines = File.ReadAllLines(_userDataPath);

                foreach (string line in lines)
                {
                    if (line[0] == '/' && line[1] == '/')
                    {
                        continue;
                    }

                    string[] keyvalue = line.Split(new char[] { '=' }, 2);
                    if (keyvalue[0] == "steamUser")
                    {
                        steamUser = keyvalue[1].Trim('\"');
                    }
                    else if (keyvalue[0] == "steamPass")
                    {
                        steamPass = keyvalue[1].Trim('\"');
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(steamUser) || string.IsNullOrWhiteSpace(steamPass))
            {
                Error = "Can not receive Userlogin!";
                return null;
            }

            return $" +login \"{steamUser}\" \"{steamPass}\"";
        }
        //ms implementation https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
        static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
    }
}
