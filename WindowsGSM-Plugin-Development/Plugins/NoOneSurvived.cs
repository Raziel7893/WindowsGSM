using System;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;

namespace WindowsGSM.Plugins
{
    public class NoOneSurvived : SteamCMDAgent // SteamCMDAgent is used because NoOneSurvived relies on SteamCMD for installation and update process
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.NoOneSurvived", // WindowsGSM.XXXX
            author = "GeekbeeGER",
            description = "WindowsGSM plugin for supporting No One Survived Dedicated Server",
            version = "1.0",
            url = "https://github.com/GeekbeeGER/WindowsGSM.NoOneSurvived", // Github repository link (Best practice)
            color = "#7477d6" // Color Hex
        };


        // - Standard Constructor and properties
        public NoOneSurvived(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData; // Store server start metadata, such as start ip, port, start param, etc


        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "2329680";


        // - Game server Fixed variables
        public override string StartPath => "WRSH\\\\Binaries\\\\Win64\\\\WRSHServer.exe"; // Game server start path, for NoOneSurvived, it is NoOneSurvivedserver.exe
        public string FullName = "NoOneSurvived Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 2; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "7777"; // Default port
        public string QueryPort = "28015"; // Default query port
        public string Defaultmap = "empty"; // Default map name
        public string Maxplayers = "50"; // Default maxplayers
        public string Additional = "-server -log"; // Additional server start parameter


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG() { }


        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            // Prepare start parameter
            var param = new StringBuilder();
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" -port={_serverData.ServerPort}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerName) ? string.Empty : $" -name=\"{_serverData.ServerName}\"");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerParam) ? string.Empty : $" {_serverData.ServerParam}");
 
            // Prepare Process
            Process p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = Functions.ServerPath.GetServersServerFiles(serverData.ServerID),
                    FileName = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath),
                    Arguments = param.ToString(),
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
        public async Task Stop(Process p) => await Task.Run(() => { p.Kill(); }); // I believe NoOneSurvived don't have a proper way to stop the server so just kill it
    }
}