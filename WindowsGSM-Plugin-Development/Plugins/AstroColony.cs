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
    public class AstroColony : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.AstroColony", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting AstroColony Dedicated Server",
            version = "1.0.0",
            url = "https://github.com/Raziel7893/WindowsGSM.AstroColony", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "2662210"; // Game server appId Steam

        // - Standard Constructor and properties
        public AstroColony(ServerConfig serverData) : base(serverData) => base.serverData = serverData;

        // - Game server Fixed variables
        public override string StartPath => "AstroColonyServer.exe";
        public string FullName = "AstroColony Dedicated Server"; // Game server FullName

        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // - Game server default values
        public string Port = "7777"; // Default port

        public string Additional = "-log"; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "32"; // Default maxplayers        
        public string QueryPort = "27015"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.
        // TODO: Unsupported option
        public string Defaultmap = "Dedicated"; // Default map name
        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        public async void CreateServerCFG()
        {
            Random rnd = new Random();

            // Specify the file path
            string filePath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "AstroColony\\Saved\\Config\\WindowsServer\\ServerSettings.ini");

            // Write the INI content to the file
            File.WriteAllText(filePath, "[/Script/ACFeature.EHServerSubsystem]\r\n" +
                "ServerPassword=\r\n" +
                $"Seed={rnd.Next()}\r\n" +
                $"MapName={serverData.ServerName}\r\n" +
                $"MaxPlayers={Maxplayers}\r\n" +
                "ShouldLoadLatestSavegame=True\r\n" +
                "AdminList=76561199104220463\r\n" +
                "SharedTechnologies=True\r\n" +
                "OxygenConsumption=True\r\n" +
                "AutosaveInterval=5.0\r\n" +
                "AutosavesCount=10\r\n");
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

            StringBuilder param = new StringBuilder();
            param.Append($" -SteamServerName={serverData.ServerName}");
            param.Append($" -port={serverData.ServerPort}");
            param.Append($" -QueryPort={serverData.ServerQueryPort}");
            param.Append($" {serverData.ServerParam}");

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
                    UseShellExecute = false,
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
                Functions.ServerConsole.SendWaitToMainWindow("{ENTER}");
                p.WaitForExit(500);
                if (!p.HasExited)
                    p.Kill();
            });
        }
    }
}
