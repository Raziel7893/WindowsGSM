using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using System.Threading;
using System.Text;

namespace WindowsGSM.Plugins
{
    /// <summary>
    /// 
    /// Notes:
    /// 7 Days to Die Dedicated Server has a special console template which, when RedirectStandardOutput=true, the console output is still working.
    /// The console output seems have 3 channels, RedirectStandardOutput catch the first channel, RedirectStandardError catch the second channel, the third channel left on the game server console.
    /// Moreover, it has his input bar on the bottom so a normal sendkey method is not working.
    /// We need to send a {TAB} => (Send text) => {TAB} => (Send text) => {ENTER} to make the input cursor is on the input bar and send the command successfully.
    /// 
    /// RedirectStandardInput:  NOT WORKING
    /// RedirectStandardOutput: YES (Used)
    /// RedirectStandardError:  YES (Used)
    /// SendKeys Input Method:  YES (Used)
    /// 
    /// There are two methods to shutdown this special server
    /// 1. {TAB} => (Send shutdown) => {TAB} => (Send shutdown) => {ENTER}
    /// 2. p.CloseMainWindow(); => {ENTER}
    /// 
    /// The second one is used.
    /// 
    /// </summary>
    public class SDTD_ExperimentalBeta : SteamCMDAgent
    {
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.SDTD_ExperimentalBeta",
            author = "LegendSeeker",
            description = "🧩 WindowsGSM plugin for supporting 7 Days to Die Dedicated Server running Experimental Beta",
            version = "0.1",
            url = "https://github.com/LegendSeeker/WindowsGSM.SDTD_ExperimentalBeta",
            color = "#5c1504" // Color Hex
        };

        public string FullName = "7DTD Dedicated Server - Experimental Beta";
        public override string StartPath => "7DaysToDieServer.exe";
        public override bool loginAnonymous => true;
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 1;
        public dynamic QueryMethod = new A2S();

        public string Port = "26900";
        public string QueryPort = "26900";
        public string Defaultmap = "Navezgane";
        public string Maxplayers = "8";
        public string Additional = string.Empty;

        public override string AppId => "294420";

        public string OldProfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "7DaysToDie\\Saves");

        public SDTD_ExperimentalBeta(ServerConfig serverData) : base(serverData) => base.serverData = serverData;

        public async void CreateServerCFG()
        {
            await DoCreateConfig();
        }

        public async Task DoCreateConfig()
        {
            //Download serverconfig.xml
            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "serverconfig.xml");

            if (File.Exists(configPath))
            {
                File.Delete(configPath + ".bak");
                File.Move(configPath, configPath + ".bak");
            }

            if (await Functions.Github.DownloadGameServerConfig(configPath, FullName))
            {
                if (!File.Exists(configPath))
                {
                    Console.WriteLine("Download from github seems to have failed.");
                    return;
                }
                var sb = new StringBuilder();
                StreamReader sr = new StreamReader(configPath);
                var line = sr.ReadLine();
                while (line != null)
                {
                    if (line.Contains("{{hostname}}"))
                        sb.AppendLine(line.Replace("{{hostname}}", serverData.ServerName));
                    else if (line.Contains("{{rcon_password}}"))
                        sb.AppendLine(line.Replace("{{rcon_password}}", serverData.GetRCONPassword()));
                    else if (line.Contains("{{port}}"))
                        sb.AppendLine(line.Replace("{{port}}", serverData.ServerPort));
                    else if (line.Contains("{{telnetPort}}"))
                        sb.AppendLine(line.Replace("{{telnetPort}}", (int.Parse(serverData.ServerPort) - int.Parse(Port) + 8081).ToString()));
                    else if (line.Contains("{{maxplayers}}"))
                        sb.AppendLine(line.Replace("{{maxplayers}}", Maxplayers));
                    else if (line.Contains("=\"UserDataFolder\""))
                        //Move WorldData to local windowsGSM installation.
                        //it is not good to have the serverdata in %AppData% as the user will forget it, as nearly all windowsgsm servers store it inside the WindosGSM structure
                        //also the backup function would be useless without
                        sb.AppendLine($"<property name=\"UserDataFolder\"\t\t\t\tvalue=\"userdata\" />");
                    else
                        sb.AppendLine(line);

                    line = sr.ReadLine();
                }
                sr.Close();
                File.WriteAllText(configPath, sb.ToString());
            }

            //Create steam_appid.txt
            string txtPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "steam_appid.txt");
            File.WriteAllText(txtPath, "251570");
        }

        public async Task<Process> Start()
        {
            string exeName = "7DaysToDieServer.exe";
            string workingDir = Functions.ServerPath.GetServersServerFiles(serverData.ServerID);
            string exePath = Path.Combine(workingDir, exeName);

            if (!File.Exists(exePath))
            {
                Error = $"{exeName} not found ({exePath})";
                return null;
            }

            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "serverconfig.xml");
            if (!File.Exists(configPath) || new FileInfo(configPath).Length == 0)
            {
                Notice = $"serverconfig.xml not found ({configPath}), reloading it from https://github.com/WindowsGSM/Game-Server-Configs";
                await DoCreateConfig();
                if (!File.Exists(configPath))
                    Error = $"ConfigFile is still missing {configPath}";
            }

            if (Directory.Exists(OldProfile))
            {
                Notice = Notice + $"An Old Savegame found in {OldProfile}\n\n" +
                        $"If you want to have it Inside WindowsGSM to have it all in one place move it to\n" +
                        $"servers/{serverData.ServerID}/serverfiles/userdata.\n" +
                        $"and modify value \"UserDataFolder\" in serverconfig.xml to value \"userdata\"\n" +
                        $"For More info see the readme of this plugin\n" +
                        $"If this is intentional, ignore this";
            }

            string logFile = @"7DaysToDieServer_Data\output_log_dedi__" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt";
            string param = $"-logfile \"{Path.Combine(workingDir, logFile)}\" -quit -batchmode -nographics -configfile=serverconfig.xml -dedicated {serverData.ServerParam}";

            Process p;
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = workingDir,
                        FileName = exePath,
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Minimized
                    },
                    EnableRaisingEvents = true
                };
                p.Start();
            }
            else
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = workingDir,
                        FileName = exePath,
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                var serverConsole = new Functions.ServerConsole(serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }

            return p;
        }

        public async Task Stop(Process p)
        {
            await Task.Run(async () =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
                Thread.Sleep(500);
                Functions.ServerConsole.SendWaitToMainWindow("{ENTER}");

                p.CloseMainWindow();
                Thread.Sleep(1000);
                if (!p.HasExited)
                    p.Kill();
            });
        }

        public new async Task<Process> Update(bool validate = false, string custom = null)
        {
            custom = "-beta latest_experimental";
            validate = true;

            var (p, error) = await Installer.SteamCMD.UpdateEx(serverData.ServerID, AppId, validate, custom: custom);
            Error = error;
            return p;
        }
    }
}