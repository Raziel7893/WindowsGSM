using System;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;
using System.IO;
using System.Linq;
using System.Net;

namespace WindowsGSM.Plugins
{
    public class Factorio : SteamCMDAgent// SteamCMDAgent is used because factorio relies on SteamCMD for installation and update process
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Factorio", // WindowsGSM.Factorio
            author = "Andy",
            description = "🧩 WindowsGSM plugin for supporting Factorio Dedicated Server",
            version = "1.0",
            url = "https://github.com/Kickbut101/WindowsGSM.Factorio", // Github repository link (Best practice)
            color = "#800080"
        };

        // - Standard Constructor and properties
        public Factorio(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData; // Store server start metadata, such as start ip, port, start param, etc

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => false;
        public override string AppId => "427520"; // Game server appId, DST is 343050


        public string FullName = "Factorio Dedicated Server";
        public override string StartPath => @"bin/x64/Factorio.exe";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 2;
        public object QueryMethod = null; // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        public string Port = "34197"; // Default factorio port - can be changed in config file - UDP only
        public string QueryPort = "27001"; // Unsure so far
        public string Defaultmap = "MyMap"; // there is no map so to speak - We'll use this value to make save file
        public string Maxplayers = "10";
        public string Additional = "";

        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG() {}

        // - Start server function, return its Process to WindowsGSM    
        public async Task<Process> Start()
        {
            string shipWorkingPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID); // c:\windowsgsm\servers\1\serverfiles\
            string shipWorkingBinx64Path = Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID, @"bin/x64/")).ToString(); // c:\windowsgsm\servers\1\serverfiles\bin\x64
            string shipWorkingDataPath = Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID, @"data/")).ToString(); // c:\windowsgsm\servers\1\serverfiles\data
            string shipWorkingEXEPathFull = Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath)); // c:\windowsgsm\servers\1\serverfiles\ + bin\x64\factorio.exe

            // Flip the backslashes for forwards slashes. Unsure if this was necessary.
            shipWorkingBinx64Path = shipWorkingBinx64Path.Replace(@"\","/");
            shipWorkingDataPath = shipWorkingDataPath.Replace(@"\","/");
            shipWorkingEXEPathFull = shipWorkingEXEPathFull.Replace(@"\","/");

            // Does \bin\ path exist?
            if(!Directory.Exists(shipWorkingBinx64Path))
            {
                Error = $"Directory not found - ({shipWorkingBinx64Path})";
                return null;
            }

            // Does .exe path exist?
            if (!File.Exists(shipWorkingEXEPathFull))
            {
                Error = $"{Path.GetFileName(shipWorkingEXEPathFull)} not found in ({shipWorkingPath})";
                return null;
            }

            // Prepare start parameters
            var param = new StringBuilder();
            param.Append($" --start-server"); // starting parameter for using the factorio.exe as a server
            param.Append($" \"{shipWorkingBinx64Path}{_serverData.ServerMap}_save.zip\""); // point to the save zip file. We're going to make and assume it's based on DefaultMap name with _save.zip appended - This is going to be in the default shipWorkingPath
            param.Append($" --server-settings \"{shipWorkingDataPath}server-settings.json\"");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" --port {_serverData.ServerPort}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerParam) ? string.Empty : $" {_serverData.ServerParam}");


            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false,
                    WorkingDirectory = shipWorkingBinx64Path,
                    FileName = shipWorkingEXEPathFull,
                    Arguments = param.ToString()
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
                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;

                // Start Process
                try // Process ID isn't being returned yet. It seems to hang on start. WindowsGSM says "Starting" forever
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
                base.Error = e.Message;
                return null; // return null if fail to start
            }
        }

        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
                p.WaitForExit(1500);
                p.CloseMainWindow();
                p.WaitForExit(500);
                if (!p.HasExited)
                    p.Kill();
            });
        }
    }
}