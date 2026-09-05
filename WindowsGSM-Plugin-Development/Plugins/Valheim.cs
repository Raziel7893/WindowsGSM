using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;


namespace WindowsGSM.Plugins
{
    public class Valheim : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Valheim", // WindowsGSM.XXXX
            author = "PsymoN",
            description = "A Fork of kessef WindowsGSM plugin version for supporting Valheim Dedicated Server",
            version = "1.6.1",
            url = "https://github.com/diegovsantos/WindowsGSM.Valheim", // Github repository link (Best practice)
            color = "#34c9eb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "896660"; // Game server appId

        // - Standard Constructor and properties
        public Valheim(ServerConfig serverData) : base(serverData) => base.serverData = serverData;


        // - Game server Fixed variables
        public override string StartPath => @"valheim_server.exe"; // Game server start path
        public string FullName = "Valheim Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 10; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "2456"; // Default port
        public string QueryPort = "2457"; // Default query port
        public string Defaultmap = "Dedicated"; // Default map name
        public string Maxplayers = "10"; // Default maxplayers
        private string BaseServerPath => Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "");
        public string Additional => $"-password \"{RandomPassword}\" -savedir \"{BaseServerPath}\\savedir\" -crossplay -public 1"; // Additional server start parameter

        //Create random password
        static string PasswordGenerator()
        {
            Random res = new Random();
            String str = "abcdefghijklmnopqrstuvwxyz0123456789";
            int size = 8;
            String randomstring = "";
            for (int i = 0; i < size; i++)
            {
                int x = res.Next(str.Length);
                randomstring = randomstring + str[x];
            }
            return randomstring;
        }

        private string RandomPassword => PasswordGenerator();
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
            var param = new StringBuilder();
            param.Append("-batchmode -nographics");
            param.Append(string.IsNullOrWhiteSpace(serverData.ServerPort) ? string.Empty : $" -port {serverData.ServerPort}");
            param.Append(string.IsNullOrWhiteSpace(serverData.ServerName) ? string.Empty : $" -name \"{serverData.ServerName}\"");
            param.Append(string.IsNullOrWhiteSpace(serverData.ServerMap) ? string.Empty : $" -world \"{serverData.ServerMap}\"");
            param.Append(string.IsNullOrWhiteSpace(serverData.ServerParam) ? string.Empty : $" {serverData.ServerParam}");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
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
                }
                catch (Exception e)
                {
                    Error = e.Message;
                    return null; // return null if fail to start
                }

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                return p;
            }

            // Start Process
            try
            {
                p.Start();
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
                ProcessManagement.StopProcess(p);
            });
        }
    }
}