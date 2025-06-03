using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using System.Linq;
using System.Net;

namespace WindowsGSM.Plugins
{
    public class BoB : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.BoB", // WindowsGSM.XXXX
            author = "kessef",
            description = "WindowsGSM plugin for supporting Beasts of Bermuda Dedicated Server",
            version = "1.0",
            url = "https://github.com/dkdue/WindowsGSM.BoB", // Github repository link (Best practice)
            color = "#34c9eb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "882430"; // Game server appId

        // - Standard Constructor and properties
        public BoB(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;


        // - Game server Fixed variables
        public override string StartPath => @"WindowsServer\BeastsOfBermuda\Binaries\Win64\BeastsOfBermudaServer.exe"; // Game server start path
        public string FullName = "Beasts of Bermuda Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "7777"; // Default port
        public string QueryPort = "27020"; // Default query port
        public string Defaultmap = "Forest_Island"; // Default map name
        public string Maxplayers = "32"; // Default maxplayers
        public string Additional = "-GameMode Life_Cycle -MapName Forest_Island -SessionName My_awesome_server -NumPlayers 32"; // Additional server start parameter


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"WindowsServer\BeastsOfBermuda\Saved\Config\WindowsServer\Game.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));

            string name = String.Concat(FullName.Where(c => !Char.IsWhiteSpace(c)));

            //Download Game.ini
            if (await DownloadGameServerConfig(configPath, configPath))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{session_name}}", _serverData.ServerName);
                configText = configText.Replace("{{rcon_port}}", _serverData.ServerQueryPort);
                configText = configText.Replace("{{max_players}}", _serverData.ServerMaxPlayer);
                File.WriteAllText(configPath, configText);
            }
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            // Check for files in Win64
            string win64 = Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID, @"WindowsServer\BeastsOfBermuda\Binaries\Win64\"));
            string[] neededFiles = { "steamclient64.dll", "tier0_s64.dll", "vstdlib_s64.dll" };

            foreach (string file in neededFiles)
            {
                if (!File.Exists(Path.Combine(win64, file)))
                {
                    File.Copy(Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID), file), Path.Combine(win64, file));
                }
            }

            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);

            // Prepare start parameter
            string param = string.IsNullOrWhiteSpace(_serverData.ServerMap) ? string.Empty : $"{_serverData.ServerMap}?listen";
            param += _serverData.ServerParam.StartsWith("?") ? $"{_serverData.ServerParam}" : $" {_serverData.ServerParam} ";

            param += string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $"-MultiHome={_serverData.ServerIP} ";
            param += string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $"-Port={_serverData.ServerPort} ";
            param += string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $"-QueryPort={_serverData.ServerQueryPort} ";
            
            param += "-nosteamclient -game -server -log ";

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (AllowsEmbedConsole)
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;

                // Start Process
                try
                {
                    p.Start();
                }
                catch (Exception e)
                {
                    Error = e.Message;
                    return null; // return null if fail to start
                }

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                return p;
            }

            // Start Process
            try
            {
                p.Start();
                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }


        // - Stop server function
        public async Task Stop(Process p) => await Task.Run(() => { p.Kill(); });

        // Get ini files
        public static async Task<bool> DownloadGameServerConfig(string fileSource, string filePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            try
            {
                using (WebClient webClient = new WebClient())
                {
                    await webClient.DownloadFileTaskAsync($"https://raw.githubusercontent.com/dkdue/WindowsGSM-Configs/main/BoB/Game.ini", filePath);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Github.DownloadGameServerConfig {e}");
            }

            return File.Exists(filePath);
        }
    }
}