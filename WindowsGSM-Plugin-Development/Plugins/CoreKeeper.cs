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
    public class CoreKeeper : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.CoreKeeper", // WindowsGSM.XXXX
            author = "Geekbee",
            description = "WindowsGSM plugin for supporting CoreKeeper Dedicated Server",
            version = "1.1",
            url = "https://github.com/GeekbeeGER/WindowsGSM.CoreKeeper", // Github repository link (Best practice)
            color = "#34c9ec" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "1963720"; // Game server appId

        // - Standard Constructor and properties
        public CoreKeeper(ServerConfig serverData) : base(serverData) => base.serverData = serverData;

        // - Game server Fixed variables
        public override string StartPath => @"CoreKeeperServer.exe"; // Game server start path
        public string FullName = "CoreKeeper Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = false;  // Does this server support output redirect?
        public int PortIncrements = 10; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "27016"; // Default port
        public string QueryPort = "9100"; // Default query port
        public string Defaultmap = "map"; // Default map name
        public string Maxplayers = "100"; // Default maxplayers
        public string Additional = "-datapath \"Saves\""; // Additional server start parameter


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            //No config file seems
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

            // Prepare start parameter
            string param = $"-batchmode -log" +
                $" -logfile CoreKeeperServerLog.txt" +
                $" -maxplayers {serverData.ServerMaxPlayer}" +
                // $" -port {serverData.ServerPort}" +
                $" {serverData.ServerParam}";

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            var serverConsole = new ServerConsole(serverData.ServerID);
            p.OutputDataReceived += serverConsole.AddOutput;
            p.ErrorDataReceived += serverConsole.AddOutput;


            // Start Process
            try
            {
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }

        // - Stop server function
        public async Task Stop(Process p) => await Task.Run(() => { p.Kill(); }); // I believe Core Keeper don't have a proper way to stop the server so just kill it
    }
}
