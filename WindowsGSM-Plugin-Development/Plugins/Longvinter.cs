using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using Newtonsoft.Json;
using System.Text;

namespace WindowsGSM.Plugins
{
    public class Longvinter : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Longvinter", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting Longvinter Dedicated Server",
            version = "1.0.0",
            url = "https://github.com/Raziel7893/WindowsGSM.Longvinter", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "1639880"; // Game server appId Steam

        // - Standard Constructor and properties
        public Longvinter(ServerConfig serverData) : base(serverData) => base.serverData = serverData;

        // - Game server Fixed variables
        //public override string StartPath => "LongvinterServer.exe"; // Game server start path
        public override string StartPath => "Longvinter\\Binaries\\Win64\\LongvinterServer-Win64-Shipping.exe";
        public string FullName = "Longvinter Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // - Game server default values
        public string Port = "7777"; // Default port

        public string Additional = "-stdlog"; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "16"; // Default maxplayers        
        public string QueryPort = "27015"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.
        // TODO: Unsupported option
        public string Defaultmap = "default"; // Default map name
        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()



        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            string gameContent = $@"[/game/blueprints/server/gi_advancedsessions.gi_advancedsessions_c]

                ; General server settings
                ServerName={serverData.ServerName}
                ServerMOTD=Welcome to Longvinter Island!
                CommunityWebsite=www.longvinter.com
                MaxPlayers={serverData.ServerMaxPlayer}
                Password=

                ; Cooperative play settings
                CoopPlay=false
                ; Spawn at the same outpost when CoopPlay is true
                ; 0: West, 1: South, 2: East
                CoopSpawn=0

                ; VPN and server tag settings
                CheckVPN=false
                Tag=none

                ; If true, disables Wandering Traders
                DisableWanderingTraders=false

                [/game/blueprints/server/gm_longvinter.gm_longvinter_c]

                ; Admin settings
                ; AdminSteamID=AddYourSteamIdHere and remove the ; infront, seperate multiple values by a space
                ; Use EOSID not SteamID, separate multiple IDs with a single space

                ; Starting Prestige level
                DefaultPrestigeLevel=1

                ; Gameplay settings
                ChestRespawnTime=900
                ; In seconds

                ; Maximum house slots without prestige
                MaxTents=3

                ; PVP Settings
                PVP=true
                ; When false, you cannot damage other players and no raiding

                RestartTime24h=6
                ; Time in 24-hour format for daily restart

                SaveBackups=true

                ; Base upkeep settings
                TentDecay=true

                ; Hardcore mode settings
                Hardcore=false
                ; When true, houses can't be locked with code, can remove wood from other players' houses, turrets are destructible

                ; Multipliers for hardcore mode
                PriceFluctuationMultiplier=1
                MoneyDropMultiplier=0
                WeaponDamageMultiplier=1
                EnergyDrainMultiplier=1
                ";
            string configFolder = "Longvinter\\Saved\\Config\\WindowsServer\\";
            Directory.CreateDirectory(Functions.ServerPath.GetServersServerFiles(configFolder));
            string gameIniFile = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, configFolder, "game.ini");
            File.WriteAllText(gameIniFile, gameContent);
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

            //Try gather a password from the gui
            var sb = new StringBuilder();
            sb.Append($"{serverData.ServerParam}");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = false,
                    WorkingDirectory = ServerPath.GetServersServerFiles(serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = sb.ToString(),
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
    }
}
