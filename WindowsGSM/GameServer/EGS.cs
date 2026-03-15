using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;

namespace WindowsGSM.GameServer
{
    class EGS :SteamCMDAgent
    {
        public const string FullName = "Empyrion - Galactic Survival Dedicated Server";
        public override string StartPath => "DedicatedServer\\EmpyrionDedicated.exe";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 5;
        public dynamic QueryMethod = null;

        public string Port = "30000";
        public string QueryPort = "30001";
        public string Defaultmap = "DediGame";
        public string Maxplayers = "8";
        public string Additional = "-dedicated dedicated.yaml";

        public override string AppId => "530870";

        public EGS(ServerConfig serverData) : base(serverData) => base.serverData = serverData;

        public async void CreateServerCFG()
        {
            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "dedicated.yaml");
            if (await Functions.Github.DownloadGameServerConfig(configPath, serverData.ServerGame))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{Srv_Port}}", serverData.ServerPort);
                configText = configText.Replace("{{Srv_Name}}", serverData.ServerName);
                configText = configText.Replace("{{Srv_Password}}", serverData.GetRCONPassword());
                configText = configText.Replace("{{Srv_MaxPlayers}}", serverData.ServerMaxPlayer);
                configText = configText.Replace("{{Tel_Port}}", (int.Parse(serverData.ServerPort) + 4).ToString());
                File.WriteAllText(configPath, configText);
            }
        }

        public async Task<Process> Start()
        {
            string exePath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath);
            if (!File.Exists(exePath))
            {
                Error = $"{Path.GetFileName(exePath)} not found ({exePath})";
                return null;
            }

            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "dedicated.yaml");
            if (!File.Exists(configPath))
            {
                Notice = $"default {Path.GetFileName(configPath)} not found ({configPath})";
            }

            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(serverData.ServerID),
                    FileName = exePath,
                    Arguments = "-batchmode -nographics " + serverData.ServerParam + "-logFile serverconsole.log",
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

            /*// Search UnityCrashHandler64.exe and return its commandline and get the dedicated process
            string crashHandler = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "DedicatedServer", "UnityCrashHandler64.exe");
            await Task.Delay(3000);
            for (int i = 0; i < 5; i++)
            {
                string commandLine = await Functions.ProcessManagement.GetCommandLineByApproximatePath(crashHandler);
                if (commandLine != null)
                {
                    try
                    {
                        Regex regex = new Regex(@" --attach (\d{1,})"); // Match " --attach 7144"
                        string dedicatedProcessId = regex.Match(commandLine).Groups[1].Value; // Get first group -> "7144"
                        Process dedicatedProcess = await Task.Run(() => Process.GetProcessById(int.Parse(dedicatedProcessId)));
                        dedicatedProcess.StartInfo.CreateNoWindow = true; // Just set as metadata
                        return dedicatedProcess;
                    }
                    catch
                    {
                        Error = $"Fail to find {Path.GetFileName(exePath)}";
                        return null;
                    }
                }

                await Task.Delay(5000);
            }

            Error = "Fail to find UnityCrashHandler64.exe";
            */
        }

        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                ProcessManagement.StopProcess(p);
            });
        }
    }
}
