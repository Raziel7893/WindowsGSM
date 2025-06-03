using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using System.IO;
using System.Text;

namespace WindowsGSM.Plugins
{
    public class Wrapper
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Wrapper",
            author = "raziel7893",
            description = "WindowsGSM plugin to run any dedicated Server via bat file",
            version = "1.1.0",
            url = "https://github.com/Raziel7893/WindowsGSM.Wrapper", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Standard Constructor and properties
        public Wrapper(ServerConfig serverData) => _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error { get; set; }
        public string Notice { get; set; }


        // - Game server Fixed variables
        public string StartPath => "start.bat";
        public string UpdatePath => "update.bat";
        public string FullName = "Wrapper"; // Game server FullName

        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 0; // This tells WindowsGSM how many ports should skip after installation

        // - Game server default values
        public string Port = "65000"; // Default port
        public string QueryPort = "65000"; // Default query port.

        public string Additional = ""; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "1"; // Default maxplayers        
        // TODO: Unsupported option
        public string Defaultmap = ""; // Default map name
        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()
        private string Version = "1.0.0";



        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipStartScript = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipStartScript))
            {
                Error = $"{Path.GetFileName(shipStartScript)} not found ({shipStartScript})";
                return null;
            }

            //Try gather a password from the gui and pass it to the script, maybe they can work with it

            StringBuilder sb = new StringBuilder();
            sb.Append($"{_serverData.ServerIP} ");
            sb.Append($"{_serverData.ServerPort} ");
            sb.Append($"{_serverData.ServerQueryPort} ");
            sb.Append($"{_serverData.ServerParam} ");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = false,
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = shipStartScript,
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

        public async Task<Process> Install()
        {

            var content = "ECHO Started the dummy start.bat. You need to edit and call your own Server from here " +
                "or replace the start.bat with the start script of your server. You find it by clicking Browse => Server Files in wgsm" +
                "\nECHO the name start.bat IS MANDATORY (it can only be changed by opening the plugin cs file and changing the StartPath variable in line 32." +
                "\nYou can use the commandline arguments if you want to manage ports and IP from WGSM (set in the Edit Config Window): " +
                "\nFirst Arg is the IP, second is the Port, third is the query Port, and from that its the Server Start Param";
            File.WriteAllText(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath), content);

            content = "ECHO Started the dummy update.bat. You need to edit and call your own Update script from here " +
                "\nECHO Or just remove the content of the file. You find it by clicking Browse => Server Files in wgsm";
            File.WriteAllText(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, UpdatePath), content);

            return null; //not needed
        }

        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            string updatePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, UpdatePath);
            if (!File.Exists(updatePath))
            {
                Error = $"{Path.GetFileName(updatePath)} not found, maybe deactivate Update options or create the script";
                return null;
            }

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = false,
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = updatePath,
                    Arguments = "",
                    WindowStyle = ProcessWindowStyle.Normal,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

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

        public string GetLocalBuild()
        {
            return Version;
        }

        public async Task<string> GetRemoteBuild()
        {
            return Version;
        }

        public bool IsInstallValid()
        {
            string installPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            Error = $"Fail to find {installPath}";
            return File.Exists(installPath);
        }

        public bool IsImportValid(string path)
        {
            string importPath = Path.Combine(path, StartPath);
            Error = $"Invalid Path! Fail to find {Path.GetFileName(StartPath)}";
            return File.Exists(importPath);
        }

    }
}
