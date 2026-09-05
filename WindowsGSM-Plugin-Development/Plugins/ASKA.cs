using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using System.Linq;
using System.Text;

namespace WindowsGSM.Plugins
{
    public class ASKA : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.ASKA", // WindowsGSM.XXXX
            author = "Gimpy",
            description = "WindowsGSM plugin for supporting ASKA Dedicated Server",
            version = "1.4",
            url = "https://github.com/tadavispmd040507/WindowsGSM.ASKA", // Github repository link (Best practice)
            color = "#12ff12" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "3246670"; // Game server appId Steam

        // - Standard Constructor and properties
        public ASKA(ServerConfig serverData) : base(serverData) => base.serverData = serverData;


        // - Game server Fixed variables
        public override string StartPath => @"AskaServer.exe"; // Game server start path
        public string FullName = "ASKA Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        // - Game server default values
        public string Port = "27015"; // Default port
        public string QueryPort = "27016"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.

        // TODO: Unsupported option
        public string Defaultmap = "Default"; // Default map name

        // TODO: May not support
        public string Maxplayers = "4"; // Default maxplayers

        public string Additional = ""; // Additional server start parameter


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, @"server properties.txt");

            var content = File.ReadAllLines(configPath);
            StringBuilder sb = new StringBuilder();
            foreach (var line in content)
            {
                if (line.StartsWith("steam game port ="))
                {
                    sb.AppendLine($"steam game port = {serverData.ServerPort}");
                }
                else if (line.StartsWith("steam query port ="))
                {
                    sb.AppendLine($"steam query port = {serverData.ServerQueryPort}");
                }
                else if (line.StartsWith("server name = "))
                {
                    sb.AppendLine($"server name = {serverData.ServerName}");
                }
                else if (line.StartsWith("display name ="))
                {
                    sb.AppendLine($"display name = {serverData.ServerName}");
                }
                else if (line.StartsWith("authentication token ="))
                {
                    if (!string.IsNullOrWhiteSpace(serverData.ServerGSLT))
                    {
                        sb.AppendLine($"authentication token = {serverData.ServerGSLT}");
                    }
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

            if (string.IsNullOrWhiteSpace(serverData.ServerGSLT))
            {
                Error = "No Steam GSLT token found. Please set it by Clicking Edit Config in Windowsgsm";
            }
            CreateServerCFG(); // add // at the start of this line if you don't want wgsm to update your config file 


            // Prepare start parameter
            string param = "-propertiesPath \"server properties.txt\"";

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    CreateNoWindow = false,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };
            p.StartInfo.EnvironmentVariables["SteamAppId"] = "1898300";

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

        // fixes WinGSM bug, https://github.com/WindowsGSM/WindowsGSM/issues/57#issuecomment-983924499
        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: loginAnonymous);
            Error = error;
            await Task.Run(() => { p.WaitForExit(); });
            return p;
        }

    }
}
