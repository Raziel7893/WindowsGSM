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
    public class Brink : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.BRINK", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting BRINK Dedicated Server",
            version = "1.1.0",
            url = "https://github.com/Raziel7893/WindowsGSM.brink", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => false;
        public override string AppId => "72780 "; // Game server appId Steam

        // - Standard Constructor and properties
        public Brink(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;


        // - Game server Fixed variables
        //public override string StartPath => "BRINKServer.exe"; // Game server start path
        public override string StartPath => "brink.exe";
        public string DefaultConfig = "base/autoexec.cfg"; // provided config to start quickly
        public string FullName = "BRINK Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // - Game server default values
        public string Port = "27015"; // Default port

        public string Additional = "+set exec_maxThreads 1 +set net_serverPortAuth 8766 +exec autoexec.cfg"; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "16"; // Default maxplayers        
        public string QueryPort = "27016"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.
        // TODO: Unsupported option
        public string Defaultmap = "Dedicated"; // Default map name
        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()



        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            var configFile = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, DefaultConfig);
            const string CONTENT = "/*\r\n//Brink Map Rotation Admin Control\r\n\r\n//Server admin should set these console variables as\r\n//appropriate on the server. Map rotation is only allowed\r\n//for game rules of type sdGameRulesObjective and\r\n//sdGameRulesStopwatch.\r\n\r\n//MOTD\r\n\r\nsi_adminName \"\"\r\nsi_motd_1 \"\"\r\nsi_motd_2 \"\"\r\nsi_motd_3 \"\"\r\n\r\n\r\n//Gamerules\r\nsi_rules sdGameRulesStopWatch\r\nsi_timelimit 30\r\nsi_playmode 2\r\nnet_serverAllowHijacking 0\r\n\r\n//sleep\r\nbot_sleepWhenServerEmpty 1\r\ng_emptyServerRestartMap 1\r\n\r\n//Passwords    \r\n//Password your server?\r\n    //0 = No\r\n    //1 = Yes\r\nsi_needpass 0\r\n\r\n    //Password for your server – si_needpass NEEDS to be set to 1!\r\ng_password \"\"\r\nnet_serverRemoteConsolePassword remoteconsolepassword\r\n\r\n//join requirements\r\nsi_onlineMode 3\r\nsi_rankRestricted 0\r\nsi_maxRank 4\r\nsi_privateClients “0”\r\n//g_privatePassword “”\r\n\r\n\r\n//Player + Bots\r\nbot_enable 1\r\nbot_minclients 16\r\nsi_botDifficulty 3\r\nbot_aimSkill 3\r\nsi_warmupSpawn 1\r\nsi_readyPercent \"1\"\r\nsi_disableVoting 0\r\nseta g_minAutoVotePlayers 1\r\ng_spectatorMode 1\r\ng_spectateFreeFly 1\r\n\r\n//Team settings\r\nsi_teamForceBalance 1\r\ng_teamSwitchDelay 1\r\nsi_enemyTintEnabled 0\r\nsi_teamVoipEnabled 1\r\nsi_globalVoipEnabled 1\r\n\r\n//Maps\r\n//g_mapRotationVote \"mp/aquarium,mp/ccity,mp/reactor,mp/refuel,mp/resort,mp/sectow,mp/shipyard, mp/terminal\"\r\n\r\n// This rotation has DLC maps included\r\ng_mapRotationVote \"mp/aquarium,mp/ccity,mp/reactor,mp/refuel,mp/resort,mp/sectow,mp/shipyard,mp/terminal,mp/lab,mp/founders\"\r\n\r\nspawnserver mp/ccity";
            File.WriteAllText(configFile, CONTENT);
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            //Try gather a password from the gui
            //brink.exe  +set si_name "Brink Dedicated Server 1"27015 +set net_serverPortMaster 27016

            StringBuilder sb = new StringBuilder();
            sb.Append($"+set si_name {_serverData.ServerName} ");
            sb.Append($"+set net_ip {_serverData.ServerIP} ");
            sb.Append($"+set net_serverPort {_serverData.ServerPort} ");
            sb.Append($"+set net_serverPortMaster {_serverData.ServerQueryPort} ");
            sb.Append($"+set net_serverDedicated 1 ");
            sb.Append($"+set fs_savepath ./Save ");
            sb.Append($"+set si_maxPlayers {_serverData.ServerMaxPlayer} +set si_maxPlayersHuman {_serverData.ServerMaxPlayer}");
            sb.Append($"{_serverData.ServerParam} ");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = false,
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = sb.ToString(),
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (_serverData.EmbedConsole)
            {
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.CreateNoWindow = true;
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
                Functions.ServerConsole.SendWaitToMainWindow("^c");
                p.WaitForExit(2000);
                if (!p.HasExited)
                    p.Kill();
            });
        }
    }
}