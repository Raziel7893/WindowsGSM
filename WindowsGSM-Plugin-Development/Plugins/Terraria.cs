using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;

namespace WindowsGSM.Plugins
{
    public class Terraria
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Terraria", // WindowsGSM.XXXX
            author = "Andy, Updated By: Ada",
            description = "WindowsGSM plugin for supporting Terraria",
            version = "1.1",
            url = "https://github.com/Ada64bit/WindowsGSM.Terraria", // Github repository link (Best practice)
            color = "#800080" // Color Hex
        };



        // - Standard Constructor and properties

        public Terraria(ServerConfig serverData) => _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;


        // - Game server Fixed variables
        public string StartPath => @"TerrariaServer.exe"; // Game server start path
        public string ModExeName => @"tshock\\TShock.Server.exe"; // Game server start path
        public string FullName = "Terraria Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "7777"; // Default port // Need UDP and TCP
        public string QueryPort = "7777"; // Default query port
        public string Defaultmap = "DefaultWorld"; // Default World Name
        public string Maxplayers = "8"; // Default maxplayers
        public string Additional = "-autocreate 3"; // Additional server start parameter

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        public async void CreateServerCFG()
        { }

        public async Task<Process> Start()
        {
            string shipWorkingPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID); // c:\windowsgsm\servers\1\serverfiles\
            string terrariaEXEPath = Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath)); // c:\windowsgsm\servers\1\serverfiles\terrariaserver.exe
            string modEXEPath = Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID, ModExeName)); // c:\windowsgsm\servers\1\serverfiles\terrariaserver.exe
            string terrariaServerMapPath = Path.Combine(shipWorkingPath, "Worlds", $"{_serverData.ServerMap}.wld"); // c:\windowsgsm\servers\1\serverfiles\Worlds\DefaultWorld.wld

            // Does .exe path exist?
            if (!File.Exists(terrariaEXEPath))
            {
                Error = $"{Path.GetFileName(terrariaEXEPath)} not found in ({_serverData.ServerID})";
                return null;
            }

            if (File.Exists(modEXEPath))
            {
                terrariaEXEPath = modEXEPath;
                shipWorkingPath = new FileInfo(modEXEPath).DirectoryName;
            }

            // Prepare start parameters
            var param = new StringBuilder();
            param.Append($" -config serverconfig.txt"); // The config file seems to override any/all commands dictated below. Make sure this file is either commented off if you don't want to use it, or filled out accurately
            param.Append($" -savedirectory \"{shipWorkingPath}\""); // This is the golden got-damn command needed to point all world related stuff to a directory. Thanks terraria "wiki" for not listing this. Buttholes.
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" -port {_serverData.ServerPort}"); // Currently this logic is circular. Ideally we'd be reading the serverconfig.txt file and verifying if the commands are already listed in there.
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer) ? string.Empty : $" -players {_serverData.ServerMaxPlayer}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerIP) ? string.Empty : $" -ip \"{_serverData.ServerIP}\"");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerMap) ? string.Empty : $" -world \"{terrariaServerMapPath}\"");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerMap) ? string.Empty : $" -worldname \"{_serverData.ServerMap}\"");
            param.Append($" -secure");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerParam) ? string.Empty : $" {_serverData.ServerParam}");

            // Output the startupcommands used. Helpful for troubleshooting server commands and testing them out - leaving this in because it's helpful af.
            var startupCommandsOutputTxtFile = ServerPath.GetServersServerFiles(_serverData.ServerID, "startupCommandsUsed.log");
            File.WriteAllText(startupCommandsOutputTxtFile, $"{param}");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false,
                    WorkingDirectory = shipWorkingPath,
                    FileName = terrariaEXEPath,
                    Arguments = param.ToString()
                },
                EnableRaisingEvents = true
            };


            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (AllowsEmbedConsole)
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
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
        // Stop process with commands for stopping servers (this actually right now doesn't work, server will not stop gracefully. Embedded console option doesn't seem to affect this behavior)
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                if (p.StartInfo.RedirectStandardInput)
                {
                    p.StandardInput.WriteLine("exit");
                }
                else
                {
                    Functions.ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "exit");
                }
            });
        }
        public string get_file_name()
        {
            WebClient webClient = new WebClient();

            // Base url for terraria info/webpage
            string html = webClient.DownloadString("https://terraria.org/api/get/dedicated-servers-names");

            // Regex pattern for pulling the download file location/address
            Regex regexFullString = new Regex(@"\[(.*?)\]");
            Match matches1 = regexFullString.Match(html);
            // Save group 1 as endURLString
            string endURLString = matches1.Groups[1].Value; // "terraria-server-1436.zip","Terraria-Mobile-Server-1.4.3.2.zip"

            endURLString = endURLString.Split(',')[0]; //"terraria-server-1436.zip"

            endURLString = endURLString.Substring(1, endURLString.Length - 2); //terraria-server-1436.zip

            return endURLString;
        }
        public async Task<Process> Install()
        {
            try
            {
                string endURLString = get_file_name();
                // Pull Build(?) number from string that was regexed
                Regex regexBuildNumber = new Regex(@"server-(\d+).zip"); // server-1412.zip || Capture 1412
                Match matches2 = regexBuildNumber.Match(endURLString);

                // Save build number
                string buildNumber = matches2.Groups[1].Value; // 1412

                //Set string of directory for extracted files
                string extractedDirPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "extractDir"); // c:\windowsgsm\servers\1\serverfiles\extractdir

                // Delete all files inside the extractdir (This command kills anything inside and remakes the dir)
                Directory.CreateDirectory(extractedDirPath);

                // Combined path for zip file destination based on build name
                string zipPath = Path.Combine(extractedDirPath, $"terraria-server-{buildNumber}.zip"); // c:\windowsgsm\servers\1\serverfiles\extractdir\terraria-server-1412.zip

                // Build URL String
                //throw new ArgumentException($"{zipPath}");
                string URLFull = $"https://terraria.org/api/download/pc-dedicated-server/{endURLString}";
                // Download .zip file to extracted dir
                WebClient webClient = new WebClient();

                await webClient.DownloadFileTaskAsync($"{URLFull}", zipPath);

                // Extract files to c:\windowsgsm\servers\1\serverfiles\extractDIR\1412\
                await Task.Run(async () =>
                {
                    try
                    {
                        await FileManagement.ExtractZip(zipPath, extractedDirPath);
                    }
                    catch
                    {
                        Error = "Path too long";
                    }
                });

                // Delete zip file downloaded from website
                //await Task.Run(() => File.Delete(zipPath));

                // Setup path with known buildnumber variable
                string extractedFilesPath = Path.Combine(extractedDirPath, $"{buildNumber}", "Windows"); // C:\windowsgsm\servers\1\serverfiles\extractDir\1412\Windows

                // Copy files (this is for a new install) from the extracted directory to the main file directory within windowsGSM (c:\windowsgsm\servers\1\serverfiles)
                foreach (var file in Directory.GetFiles(extractedFilesPath))
                    File.Copy(file, Path.Combine(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID), Path.GetFileName(file)), true);


                // Delete the directory for extractedfiles
                //Directory.Delete(extractedDirPath,true);

                return null;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }
        }


        // Fully update necessary server files to newest version (this typically happens after the GetRemoteBuild and GetLocalBuild tasks)
        public async Task<Process> Update()
        {
            try
            {
                string endURLString = get_file_name();
                // Pull Build(?) number from string that was regexed
                Regex regexBuildNumber = new Regex(@"server-(\d+).zip"); // server-1412.zip || Capture 1412
                Match matches2 = regexBuildNumber.Match(endURLString);

                // Save build number
                string buildNumber = matches2.Groups[1].Value; // 1412

                //Set string of directory for extracted files
                string extractedDirPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "extractDir"); // c:\windowsgsm\servers\1\serverfiles\extractdir

                // Delete all files inside the extractdir (This command kills anything inside and remakes the dir)
                Directory.CreateDirectory(extractedDirPath);

                // Combined path for zip file destination based on build name
                string zipPath = Path.Combine(extractedDirPath, $"terraria-server-{buildNumber}.zip"); // c:\windowsgsm\servers\1\serverfiles\extractdir\terraria-server-1412.zip

                // Build URL String
                string URLFull = $"https://terraria.org/api/download/pc-dedicated-server/{endURLString}";
                WebClient webClient = new WebClient();
                await webClient.DownloadFileTaskAsync($"{URLFull}", zipPath);
                // Extract files to c:\windowsgsm\servers\1\serverfiles\extractDIR\1412\
                await Task.Run(async () =>
                {
                    try
                    {
                        await FileManagement.ExtractZip(zipPath, extractedDirPath);
                    }
                    catch
                    {
                        Error = "Path too long";
                    }
                });

                // Delete zip file downloaded from website
                await Task.Run(() => File.Delete(zipPath));

                // Setup path with known buildnumber variable
                string extractedFilesPath = Path.Combine(extractedDirPath, $"{buildNumber}", "Windows"); // C:\windowsgsm\servers\1\serverfiles\extractDir\1412\Windows

                // Copy files (this is for a new install) from the extracted directory to the main file directory within windowsGSM (c:\windowsgsm\servers\1\serverfiles)
                foreach (var file in Directory.GetFiles(extractedFilesPath))
                    if (!(file.Contains("serverconfig.txt"))) // this is literally the only difference between the install process/task and the update one. Don't overwrite the config file!
                    {
                        File.Copy(file, Path.Combine(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID), Path.GetFileName(file)), true);
                    }

                // Delete the directory for extractedfiles
                Directory.Delete(extractedDirPath, true);

                return null;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }
        }


        // Verify that files needed to run server are present after gold/fresh install
        public bool IsInstallValid()
        {
            string exePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath); // c:\windowsgsm\servers\1\serverfiles\terrariaserver.exe
            return File.Exists(exePath);
        }


        // Verify that files needed to run server are present after import
        public bool IsImportValid(string path)
        {
            string exePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath); // c:\windowsgsm\servers\1\serverfiles\terrariaserver.exe
            return File.Exists(exePath);
        }


        // Read files to ascertain what version is currently installed
        public string GetLocalBuild()
        {
            var terrariaVersion = FileVersionInfo.GetVersionInfo(Path.Combine(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID), StartPath));
            return terrariaVersion.FileVersion.ToString();
        }


        // Read online sources to determine newest publically available version
        public async Task<string> GetRemoteBuild()
        {
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    string htmlstring = await webClient.DownloadStringTaskAsync("https://terraria.gamepedia.com/Server");
                    Regex regexVersionNumbers = new Regex(@"Terraria Server (\d+[\.\d+]+)");
                    MatchCollection matches1 = regexVersionNumbers.Matches(htmlstring);
                    string newestVersionString = matches1[matches1.Count - 1].Groups[1].Value; // This is verrrryyy house of cards/weak. Could easily break, but there wasn't much other option

                    return newestVersionString;
                }
            }
            catch
            {
                Error = $"Fail to get remote build";
                return string.Empty;
            }

        }
    }
}