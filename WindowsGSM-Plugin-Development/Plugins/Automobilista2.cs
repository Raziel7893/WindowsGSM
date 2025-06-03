using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using System.Text;

namespace WindowsGSM.Plugins
{
    public class Automobilista2 : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Automobilista2", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting Automobilista2 Dedicated Server",
            version = "1.0.0",
            url = "https://github.com/Raziel7893/WindowsGSM.Automobilista2", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "1338040"; // Game server appId Steam

        // - Standard Constructor and properties
        public Automobilista2(ServerConfig serverData) : base(serverData) => base.serverData = serverData;


        // - Game server Fixed variables
        //public override string StartPath => "Automobilista2Server.exe"; // Game server start path
        public override string StartPath => "DedicatedServerCmd.exe";
        public string FullName = "Automobilista2 Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // - Game server default values
        public string Port = "27015"; // Default port

        public string Additional = "-LOCALLOGTIMES -log"; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "16"; // Default maxplayers        
        public string QueryPort = "27016"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.
        // TODO: Unsupported option
        public string Defaultmap = "Dedicated"; // Default map name
        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        public string SampleConfig = "config_sample\\server_with_lists.cfg";
        public string ConfigFile = "server.cfg";

        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            string sampleServerCfg = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, SampleConfig);
            string serverCfg = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, ConfigFile);

            var sb = new StringBuilder();

            StreamReader sr = new StreamReader(sampleServerCfg);
            var line = sr.ReadLine();
            while (line != null)
            {
                if (line.Contains("name :"))
                    sb.AppendLine($"name : {serverData.ServerName}");
                else if (line.Contains("hostPort :"))
                    sb.AppendLine($"hostPort : {serverData.ServerPort}");
                else if (line.Contains("queryPort :"))
                    sb.AppendLine($"queryPort : {serverData.ServerQueryPort}");
                else
                    sb.AppendLine(line);

                line = sr.ReadLine();
            }

            File.WriteAllText(serverCfg, sb.ToString());
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

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = false,
                    WorkingDirectory = ServerPath.GetServersServerFiles(serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = "",
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
                if(!p.HasExited)
                    p.Kill();
            });
        }
    }
}
