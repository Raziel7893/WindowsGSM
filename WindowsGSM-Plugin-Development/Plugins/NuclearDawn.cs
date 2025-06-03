using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.Threading;

namespace WindowsGSM.Plugins
{
    public class NuclearDawn : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.NuclearDawn", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting NuclearDawn Dedicated Server",
            version = "1.0.0",
            url = "https://github.com/Raziel7893/WindowsGSM.NuclearDawn", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "111710"; // Game server appId Steam

        // - Standard Constructor and properties
        public NuclearDawn(ServerConfig serverData) : base(serverData) => base.serverData = serverData;

        // - Game server Fixed variables
        //public override string StartPath => "NuclearDawnServer.exe"; // Game server start path
        public override string StartPath => "ndsrv.exe";
        public const string ConfigFile = "nucleardawn/cfg/server.cfg";

        public string FullName = "NuclearDawn Dedicated Server";
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // - Game server default values
        public string Port = "27015";
        public string QueryPort = "27015";

        public string Additional = $"-nocrashdialog -autoupdate"; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "16"; // Default maxplayers        
        // TODO: Unsupported option
        public string Defaultmap = "downtown"; // Default map name
        public string Game = "nucleardawn"; // Default map name
        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server Fixed variables
        //public override string StartPath => "NuclearDawnServer.exe"; // Game server start path


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            //Edit WindowsGSM.cfg //set clientport dynamically on setup
            string configFile = Functions.ServerPath.GetServersConfigs(serverData.ServerID, "WindowsGSM.cfg");
            if (File.Exists(configFile))
            {
                string configText = File.ReadAllText(configFile);
                configText = configText.Replace("{{clientport}}", (int.Parse(serverData.ServerPort) - 10).ToString());
                File.WriteAllText(configFile, configText);
            }

            //create base configFile as it is not in the github config directory
            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, ConfigFile);
            if (!File.Exists(configPath))
            {
                string configText = $@"// Nuclear Dawn Configuration File, To be used with ND only!

// Server Name
hostname ""{serverData.ServerName}""
sv_tags ""nucleardawn""
    
// Rcon Cvars
rcon_password ""{serverData.GetRCONPassword()}"" // Set remote control password
sv_rcon_banpenalty 15 // Number of minutes to ban users who fail rcon authentication
sv_rcon_log 1 // Enable/disable rcon logging
sv_rcon_maxfailures 3 // Max number of times a user can fail rcon authentication before being banned
sv_rcon_minfailures 5 // Number of times a user can fail rcon authentication in sv_rcon_minfailuretime before being banned
sv_rcon_minfailuretime 10 // Number of seconds to track failed rcon authentications

sv_password """" // Password protects server

mp_roundtime 35 // Minutes in a round
mp_maxrounds 2 // Number of rounds
mp_timelimit 70 // Minutes for a map to last

// Team balance
mp_limitteams 1 // Sets how many players can a team have over the opposite team.
mp_autoteambalance 2 // 0 = No balancing, 1 = Only balance on end of round (default), 2 = Only balance during the game, 3 = Balance throughout the round and at the end of the round.
mp_unbalance_limit 2
mp_autoteambalance_delay 60 // Specify the amount of seconds into a round that balancing will occur

// Communications

// enable voice communications
sv_voiceenable 1

// Players can hear all other players, no team restrictions 0=off 1=on
sv_alltalk 0

// toggles whether the server allows spectator mode or not
mp_allowspectators 1

// Contact & Region

// Contact email for server sysop
sv_contact emailaddy@example.com

// The region of the world to report this server in.
// -1 is the world, 0 is USA east coast, 1 is USA west coast
// 2 south america, 3 europe, 4 asia, 5 australia, 6 middle east, 7 africa
sv_region 3

// Type of server 0=internet 1=lan
sv_lan 0

// Round-end Teamswap
mp_roundend_teamswap 1 // Auto swap teams at round-end? (0=no/1=yes)

//OPTIONAL:
                    
// Commander selection
nd_commander_accept_time 15 // The time (seconds) the selected commander has to accept
nd_commander_election_time 45 // How long until a commander is selected at the beginning of a round

// Team votes
nd_commander_mutiny_min_players 3 // Minimum number of players connected to start a mutiny vote
nd_commander_mutiny_surpress_time  5 // Time in minutes before a mutiny can start after a previous one, or after a new round
nd_commander_mutiny_time 30 // Time (in seconds) a mutiny vote lasts
nd_commander_mutiny_vote_threshold 51.0 // The percentage of team votes required for the current commander to be voted off
nd_commander_surrender_vote_threshold 51.0 //  The percentage of team votes required for the team to surrender the round

// Resources
nd_starting_resources 3000 // Resources that each team starts the game with

// Spawning
nd_spawn_min_time 6.0 // Min time in seconds player must be dead for before they can spawn
nd_spawn_wave_interval 12.0 // Time in seconds between spawn waves

// Bots
bot_quota 6 // How many bots should enter the game
bot_quota_mode fill // Fill the server with bots, if less than x players
bot_join_after_player 0 // Should bots join after players have joined or start playing without players (1)

// Additional
nd_xmas 0 // disable or enable Christmas hats. 0=disabled, 1=hats only for commanders 2=hats for everyone. Only active during Christmas period.

// Teams
mp_force_autoassign 0 // force players to join a team when joining the server. 0=disabled 1=enabled
";

                File.WriteAllText(configPath, configText);
            }
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

            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, ConfigFile);
            if (!File.Exists(configPath))
            {
                Notice = $"server.cfg not found ({configPath})";
            }

            //Try gather a password from the gui

            StringBuilder sb = new StringBuilder();
            sb.Append($"-console");
            sb.Append(string.IsNullOrWhiteSpace(Game) ? string.Empty : $" -game {Game}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerIP) ? string.Empty : $" -ip {serverData.ServerIP}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerPort) ? string.Empty : $" -port {serverData.ServerPort}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerGSLT) ? string.Empty : $" +sv_setsteamaccount {serverData.ServerGSLT}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerParam) ? string.Empty : $" {serverData.ServerParam}");
            sb.Append(string.IsNullOrWhiteSpace(serverData.ServerMap) ? string.Empty : $" +map {serverData.ServerMap}");

            // Prepare Process
            Process p;
            try
            {
                if (!AllowsEmbedConsole)
                {
                    p = new Process
                    {
                        StartInfo =
                        {                    
                            FileName = shipExePath,
                            Arguments = sb.ToString(),
                            WindowStyle = ProcessWindowStyle.Minimized,
                            UseShellExecute = false,
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
                            FileName = shipExePath,
                            Arguments = sb.ToString(),
                            WindowStyle = ProcessWindowStyle.Minimized,
                            UseShellExecute = false,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding = Encoding.UTF8,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        },
                        EnableRaisingEvents = true
                    };
                    var serverConsole = new Functions.ServerConsole(serverData.ServerID);
                    p.OutputDataReceived += serverConsole.AddOutput;
                    p.ErrorDataReceived += serverConsole.AddOutput;
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }

            return p;
        }

        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "quit");
                Functions.ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "^c");
                Thread.Sleep(1000);
                if (!p.HasExited)
                    p.Kill();
            });
        }
    }
}
