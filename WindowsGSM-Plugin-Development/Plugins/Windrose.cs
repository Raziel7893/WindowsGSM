using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;

namespace WindowsGSM.Plugins
{
    public class Windrose : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Windrose", // WindowsGSM.XXXX
            author = "MuchLive - Twitch.Tv/Much_Live",
            description = "WindowsGSM plugin for supporting Windrose Dedicated Server",
            version = "1.0.1",
            url = "", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "4129620"; // Game server appId Steam

        // - Standard Constructor and properties
        public Windrose(ServerConfig serverData) : base(serverData) => base.serverData = serverData;

        // - Game server Fixed variables
        //public override string StartPath => "WindroseServer.exe"; // Game server start path
        public override string StartPath => "R5\\Binaries\\Win64\\WindroseServer-Win64-Shipping.exe";//"WindroseServer.exe";
        public string FullName = "Windrose Dedicated Server (Windrose.cs)"; // Game server FullName

        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 2; // This tells WindowsGSM how many ports should skip after installation

        // - Game server default values
        public string Port = "7777"; // Default port
        public string Additional = "-log"; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "10"; // Default maxplayers        
        public string QueryPort = "7778"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.
        // TODO: Unsupported option
        public string Defaultmap = "Dedicated"; // Default map name
        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()



        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {

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


            StringBuilder sb = new StringBuilder();

            sb.Append($" -PORT={serverData.ServerPort}");
            sb.Append($" -QUERYPORT={serverData.ServerQueryPort}");
            sb.Append($" -MULTIHOME={serverData.ServerIP}");
            sb.Append($" {serverData.ServerParam}");

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
                if (!SendStopSignal(p))
                {
                    p.Kill();
                }
            });
        }

        #region preparation of the WindowsAPI to send process shutdown signals
        internal const int CTRL_C_EVENT = 0;
        [DllImport("kernel32.dll")]
        internal static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern bool FreeConsole();
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);
        delegate Boolean ConsoleCtrlDelegate(uint CtrlType);
        #endregion
        //sends the stop signal to the process
        public static bool SendStopSignal(Process p)
        {
            if (AttachConsole((uint)p.Id))
            {
                SetConsoleCtrlHandler(null, true);
                try
                {
                    if (!GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0))
                    {
                        return false;
                    }
                    p.WaitForExit(10000);
                }
                finally
                {
                    SetConsoleCtrlHandler(null, false);
                    FreeConsole();
                }
                return true;
            }
            return false;
        }
    }
}
