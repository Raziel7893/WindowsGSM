using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;

namespace WindowsGSM.Plugins
{
    public class ARKSE : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.ARKSE", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting ARKSE Dedicated Server",
            version = "1.0.0",
            url = "https://github.com/Raziel7893/WindowsGSM.ARKSE", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "376030"; // Game server appId Steam
        public string FullName = "ARK: Survival Evolved Dedicated Server";
        public override string StartPath => @"ShooterGame\Binaries\Win64\ShooterGameServer.exe";

        // - Standard Constructor and properties
        public ARKSE(ServerConfig serverData) : base(serverData) => base.serverData = serverData;

        // - Game server default values
        public bool AllowsEmbedConsole = false;
        public int PortIncrements = 2;
        public dynamic QueryMethod = new A2S();

        public string Port = "7777";
        public string QueryPort = "27015";
        public string Defaultmap = "TheIsland";
        public string Maxplayers = "16";
        public string Additional = string.Empty;

        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {

        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            string param = string.IsNullOrWhiteSpace(serverData.ServerMap) ? string.Empty : serverData.ServerMap;
            param += "?listen";
            param += string.IsNullOrWhiteSpace(serverData.ServerName) ? string.Empty : $"?SessionName=\"{serverData.ServerName}\"";
            param += string.IsNullOrWhiteSpace(serverData.ServerIP) ? string.Empty : $"?MultiHome={serverData.ServerIP}";
            param += string.IsNullOrWhiteSpace(serverData.ServerPort) ? string.Empty : $"?Port={serverData.ServerPort}";
            param += string.IsNullOrWhiteSpace(serverData.ServerMaxPlayer) ? string.Empty : $"?MaxPlayers={serverData.ServerMaxPlayer}";
            param += string.IsNullOrWhiteSpace(serverData.ServerQueryPort) ? string.Empty : $"?QueryPort={serverData.ServerQueryPort}";
            param += $"{serverData.ServerParam} -server -log";

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = false,
                    WorkingDirectory = ServerPath.GetServersServerFiles(serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param.ToString(),
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
                p.WaitForExit(5000);
                if (!p.HasExited)
                    p.Kill();
            });
        }
    }
}
