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
    public class ArmaReforger : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.ArmaReforger", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting ArmaReforger Dedicated Server",
            version = "1.0.0",
            url = "https://github.com/Raziel7893/WindowsGSM.ArmaReforger", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "1874900"; // Game server appId Steam

        // - Standard Constructor and properties
        public ArmaReforger(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;


        // - Game server Fixed variables
        public override string StartPath => "ArmaReforgerServer.exe";
        const string ConfigFile = "Configs\\server.json"; // Default map name
        public string FullName = "ArmaReforger Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // - Game server default values
        public string Port = "2302"; // Default port

        public string Additional = $"-profile \".\\Saved\" -maxFPS 60 -config \".\\{ConfigFile}\""; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "16"; // Default maxplayers        
        public string QueryPort = "7777"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.
        // TODO: Unsupported option
        public string Defaultmap = "notUsed"; // Default map name
        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()



        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            Directory.CreateDirectory(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "Configs"));
            string configFile = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, ConfigFile);

            string jsonContent = $"" +
                $"{{" +
                    $"\r\n\t\"bindAddress\": \"{serverData.ServerIP}\"," +
                    $"\r\n\t\"bindPort\": {serverData.ServerPort}," +
                    $"\r\n\t\"a2s\": " +
                    $"{{" +
                        $"\r\n\t\t\"address\": \"0.0.0.0\"," +
                        $"\r\n\t\t\"port\": {serverData.ServerQueryPort}\r\n\t" +
                    $"}}," +
                    $"\r\n\t\"rcon\": " +
                    $"{{" +
                        $"\r\n\t\t\"address\": \"{serverData.ServerIP}\"," +
                        $"\r\n\t\t\"port\": 19999," +
                        $"\r\n\t\t\"password\": \"{serverData.GetRCONPassword()}\"" +
                    $"}}," +
                    $"\r\n\t\"game\": " +
                    $"{{" +
                        $"\r\n\t\t\"name\": \"{serverData.ServerName}\"," +
                        $"\r\n\t\t\"password\": \"default\"," +
                        $"\r\n\t\t\"passwordAdmin\": \"defaultAdmin\"," +
                        $"\r\n\t\t\"admins\" : []," +
                        $"\r\n\t\t\"scenarioId\": \"{{ECC61978EDCC2B5A}}Missions/23_Campaign.conf\"," +
                        $"\r\n\t\t\"maxPlayers\": {serverData.ServerMaxPlayer}," +
                        $"\r\n\t\t\"visible\": true," +
                        $"\r\n\t\t\"gameProperties\": " +
                        $"{{" +
                            $"\r\n\t\t\t\"serverMaxViewDistance\": 1600," +
                            $"\r\n\t\t\t\"networkViewDistance\": 1500," +
                            $"\r\n\t\t\t\"disableThirdPerson\": false," +
                            $"\r\n\t\t\t\"fastValidation\": true," +
                            $"\r\n\t\t\t\"battlEye\": true," +
                            $"\r\n\t\t\t\"VONDisableUI\": false," +
                            $"\r\n\t\t\t\"VONDisableDirectSpeechUI\": false," +
                            $"\r\n\t\t\t\"VONCanTransmitCrossFaction\": false\r\n\t\t" +
                        $"}}," +
                        $"\r\n\t\t\"mods\": []\r\n\t" +
                    $"}}\r\n" +
                $"}}\r\n";


            File.WriteAllText(configFile, jsonContent);
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

            StringBuilder sb = new StringBuilder();
            sb.Append($"-bindIP {_serverData.ServerIP} ");
            sb.Append($"-a2sPort {_serverData.ServerQueryPort} ");
            sb.Append($"-bindPort {_serverData.ServerPort} ");
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
