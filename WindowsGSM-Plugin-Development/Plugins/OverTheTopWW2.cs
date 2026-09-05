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
    public class OverTheTopWW2 : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.OverTheTopWW2", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting OverTheTopWW2 Dedicated Server",
            version = "1.0.2",
            url = "https://github.com/Raziel7893/WindowsGSM.OverTheTopWW2", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "3052620"; // Game server appId Steam

        // - Standard Constructor and properties
        public OverTheTopWW2(ServerConfig serverData) : base(serverData) => base.serverData = serverData;

        // - Game server Fixed variables
        //public override string StartPath => "OverTheTopWW2Server.exe"; // Game server start path
        public override string StartPath => "Over_The_Top_WWI.exe";
        public string FullName = "OverTheTopWW2 Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // - Game server default values
        public string Port = "7186"; // Default port

        public string Additional = ""; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "130"; // Default maxplayers        
        public string QueryPort = "7187"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.
        // TODO: Unsupported option
        public string Defaultmap = "default"; // Default map name
        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()



        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, @"ServerConfiguration.ini");

            var content = File.ReadAllLines(configPath);
            StringBuilder sb = new StringBuilder();
            foreach (var line in content)
            {
                if (line.StartsWith("PortID="))
                {
                    sb.AppendLine($"PortID={serverData.ServerPort}");
                }
                else if (line.StartsWith("SteamQueryPortID="))
                {
                    sb.AppendLine($"SteamQueryPortID={serverData.ServerQueryPort}");
                }
                else if (line.StartsWith("ServerName="))
                {
                    sb.AppendLine($"ServerName={serverData.ServerName}");
                }
                else if (line.StartsWith("MaxPlayerCount="))
                {
                    sb.AppendLine($"MaxPlayerCount={serverData.ServerMaxPlayer}");
                }
                else
                {
                    sb.AppendLine(line);
                }
            }

            File.WriteAllText(configPath, sb.ToString());
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
            //update Config with the values from WindowsGSM, put 2 // infront of next line if you don't want that
            CreateServerCFG();

            //Try gather a password from the gui
            var sb = new StringBuilder();
            sb.Append($"-batchmode -nographics -server {serverData.ServerParam}");

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
