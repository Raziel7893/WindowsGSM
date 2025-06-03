using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using Newtonsoft.Json.Linq;

namespace WindowsGSM.Plugins
{
    public class ForgeMC
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.ForgeMC",
            author = "dwhitacre and Ada",
            description = "🧩 WindowsGSM plugin for supporting Minecraft: Forge Server",
            version = "1.0.1",
            url = "https://github.com/dwhitacre/WindowsGSM.ForgeMC",
            color = "#ffffff"
        };

        // - Standard Constructor and properties
        public ForgeMC(ServerConfig serverData) => _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;

        // - Game server Fixed variables
        public string StartPath => "forge.jar"; // not used
        public string FullName = "Minecraft: Forge Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new UT3(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        // - Game server default values
        public string Port = "25565"; // Default port
        public string QueryPort = "25565"; // Default query port
        public string Defaultmap = "java.exe"; // Default map name
        public string Maxplayers = "20"; // Default maxplayers
        public string Additional = ""; // Additional server start parameter

        // - Additional configuration
        private string ForgeBuildStream = "latest"; // latest or recommended build stream
        private string ForgeFormat = "*forge*.jar"; // Filename wildcard for searching for forge jars
        private string ForgeRegex = @"forge-?([\d\.]+)-([\d\.]+)\.jar"; // Regex for finding forge version and build, version group 1 / build group 2
        private string ForgeInstallerUniqueKey = "installer"; // Filenames containing this to exclude in forge jar search
        private string ForgeHost = "http://files.minecraftforge.net"; // Host where forge files and meta exist
        private string ForgeInstallerBasePath = "/maven/net/minecraftforge/forge"; // Base path of the url for forge installers
        private string ForgePromotionsJson = "/net/minecraftforge/forge/promotions_slim.json"; // Path to promotions json in forge (contains mapping of minecraft version to forge build)
        private string ServerProperties = "server.properties"; // Filename for Minecraft server properties
        private string Eula = "eula.txt";
        private string PaperVersionApi = "https://api.papermc.io/v2/projects/paper"; // Paper minecraft versions API

        // - Constants
        private const string ERROR_JAVA_NOT_INSTALLED = "Java is not installed";
        private const string ERROR_EULA_DECLINED = "Declined the EULA";
        private const string ERROR_LOCAL_FORGE_MISSING = "Local forge jar does not exist";
        private const string EULA_HEADER = "Agreement to the EULA";
        private const string EULA_PROMPT = "By continuing you are indicating your agreement to the EULA.\n(https://account.mojang.com/documents/minecraft_eula)";
        private const string EULA_TEXT = "#By changing the setting below to TRUE you are indicating your agreement to our EULA (https://account.mojang.com/documents/minecraft_eula).\neula=true";
        private const string AGREE = "Agree";
        private const string DECLINE = "Decline";

        // - Get a prepared java process with args
        private Process GetJavaProcess(string args, string javaPath = "")
        {
            if(string.IsNullOrWhiteSpace(javaPath))
                javaPath = JavaHelper.FindJavaExecutableAbsolutePath();

            return new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = javaPath,
                    Arguments = args,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = false
            };
        }

        private Process GetBatProc(string args) {
            return new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = args,
                    Arguments = "nogui",
                    UseShellExecute = false
                },
                EnableRaisingEvents = false
            };
        }
        // - Has a local forge jar file
        private bool HasForgeFile(string dirName)
        {
            return !string.IsNullOrWhiteSpace(GetForgeFile(dirName));
        }

        // - Find the local forge jar filename
        private string GetForgeFile(string dirName)
        {
            try
            {
                string fp = GetForgeFilePath();
                // @todo(dw): handle multiple forge jars, currently we just return the first one we find
                var jarFiles = Directory.EnumerateFiles(dirName+@"\"+fp, ForgeFormat);
                foreach (string currentFile in jarFiles)
                {
                    string fileName = Path.GetFileName(currentFile);
                    if (fileName.Contains(ForgeInstallerUniqueKey)) continue;
                    return fileName;
                }
                
                // didnt find anything that looked like a forge jar
                return string.Empty;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return string.Empty;
            }
        }
        private string GetForgeFilePath() {
            Regex rx = new Regex(@"java @user_jvm_args.txt @(.*?)win_args.txt", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            try
            {
                foreach (string line in File.ReadLines(ServerPath.GetServersServerFiles(_serverData.ServerID, "run.bat")))
                {
                    MatchCollection matches = rx.Matches(line);
                    foreach (Match match in matches)
                    {   
                        return match.Groups[1].ToString();
                    }
                }
            }
            catch(Exception e){
                Error = e.ToString();
            }
            return String.Empty;
        }

        // - Get the local installer filename
        private string GetInstaller()
        {
            var localBuild = GetLocalBuild();
            return $"forge-{localBuild}-installer.jar";
        }

        // - Download the latest remote installer and return its filename
        private async Task<string> GetRemoteInstaller()
        {
            // Try getting the latest remote build
            var build = await GetRemoteBuild();

            if (string.IsNullOrWhiteSpace(build)) { return string.Empty; }

            // Download the latest forge installer to /serverfiles
            var installer = $"forge-{build}-installer.jar";
            var installerFile = ServerPath.GetServersServerFiles(_serverData.ServerID, installer);
            try
            {
                using (var webClient = new WebClient())
                {
                    await webClient.DownloadFileTaskAsync(
                        $"{ForgeHost}{ForgeInstallerBasePath}/{build}/{installer}",
                        installerFile
                    );
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
                return string.Empty;
            }

            return installer;
        }

        // - Get and run the latest remote installer
        private async Task<bool> RunInstaller()
        {
            // Try getting the latest remote installer
            var installer = await GetRemoteInstaller();
            if (string.IsNullOrWhiteSpace(installer)) { return false; }

            // Prepare process
            // @todo(dw): configure this to log to install log rather than opening a separate window
            var param = new StringBuilder($"-jar {installer} --installServer");
            var p = GetJavaProcess(param.ToString());

            // Start install process
            try
            {
                p.Start();
                p.WaitForExit();
            }
            catch (Exception e)
            {
                Error = e.Message;
                return false;
            }

            return true;
        }

        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"motd={_serverData.ServerName}");
            sb.AppendLine($"server-port={_serverData.ServerPort}");
            sb.AppendLine("enable-query=true");
            sb.AppendLine($"query.port={_serverData.ServerQueryPort}");
            sb.AppendLine($"rcon.port={int.Parse(_serverData.ServerPort) + 10}"); // @todo(dw): this seems like it should be 1 and then PortIncrements should be 2
            sb.AppendLine($"rcon.password={ _serverData.GetRCONPassword()}");
            File.WriteAllText(ServerPath.GetServersServerFiles(_serverData.ServerID, ServerProperties), sb.ToString());
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            if (!JavaHelper.IsJREInstalled())
            {
                Error = ERROR_JAVA_NOT_INSTALLED;
                return null;
            }

            // Prepare process
            string javaPath = string.Empty;
            
            if(!string.IsNullOrWhiteSpace(_serverData.ServerMap) )
                javaPath = _serverData.ServerMap;

            var param = new StringBuilder($"@user_jvm_args.txt @{GetForgeFilePath()}win_args.txt nogui");
            var p = GetJavaProcess(param.ToString(), javaPath);


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
                    return null;
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
                return null;
            }
        }

        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                if (p.StartInfo.RedirectStandardInput)
                {
                    // Send "stop" command to StandardInput stream if EmbedConsole is on
                    p.StandardInput.WriteLine("stop");
                }
                else
                {
                    // Send "stop" command to game server process MainWindow
                    ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "stop");
                }
            });
        }

        // - Install server function
        public async Task<Process> Install()
        {
            // EULA agreement
            var agreedPrompt = await UI.CreateYesNoPromptV1(EULA_HEADER, EULA_PROMPT, AGREE, DECLINE);
            if (!agreedPrompt)
            { 
                Error = ERROR_EULA_DECLINED;
                return null;
            }

            // Install Java if not installed
            if (!JavaHelper.IsJREInstalled())
            {
                var taskResult = await JavaHelper.DownloadJREToServer(_serverData.ServerID);
                if (!taskResult.installed)
                {
                    Error = taskResult.error;
                    return null;
                }
            }

            // Create eula.txt
            var eulaFile = ServerPath.GetServersServerFiles(_serverData.ServerID, Eula);
            File.WriteAllText(eulaFile, EULA_TEXT);

            // Run installer
            var task = await RunInstaller();
            return null;
        }

        // - Update server function
        public async Task<Process> Update()
        {
            if (!JavaHelper.IsJREInstalled())
            {
                Error = ERROR_JAVA_NOT_INSTALLED;
                return null;
            }

            // Try to get old jar file and installer to delete later
            var oldJar = GetForgeFile(ServerPath.GetServersServerFiles(_serverData.ServerID));
            var oldJarFile = ServerPath.GetServersServerFiles(_serverData.ServerID, oldJar);
            var oldInstaller = GetInstaller();
            var oldInstallerFile = ServerPath.GetServersServerFiles(_serverData.ServerID, oldInstaller);

            // Get and run new installer
            var success = await RunInstaller();
            if (!success) { return null; }

            // Delete old forge jar and installer if they existed
            try
            {
                if (!string.IsNullOrWhiteSpace(oldJar))
                {
                    File.Delete(oldJarFile);
                }
                if (File.Exists(oldInstallerFile))
                {
                    File.Delete(oldInstallerFile);
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
            }

            return null;
        }

        // - Check if the installation is successful
        public bool IsInstallValid()
        {
            return HasForgeFile(ServerPath.GetServersServerFiles(_serverData.ServerID));
        }

        // - Check if the directory contains forge jar for import
        public bool IsImportValid(string path)
        {
            return HasForgeFile(path);
        }

        // - Get Local server version and build
    
        public string GetLocalBuild()
        {
            var jarFile = GetForgeFile(ServerPath.GetServersServerFiles(_serverData.ServerID));
            if (string.IsNullOrWhiteSpace(jarFile))
            {
                Error = ERROR_LOCAL_FORGE_MISSING;
                return string.Empty;
            }
            
            var match = new Regex(ForgeRegex).Match(jarFile);
            var version = match.Groups[1].Value;
            var build = match.Groups[2].Value;

            return $"{version}-{build}";
        }
        

        // - Get Latest server version and build
        public async Task<string> GetRemoteBuild()
        {
            // @todo(dw): its likely we will not always want the latest
            // version of Forge AND the latest version of Minecraft.
            // however, other MC plugins make this same assumption, so
            // fine for now
            //
            // alternatively if sticking with always latest..
            // get it from here: https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml
            try
            {
                using (var webClient = new WebClient())
                {
                    // @todo(dw): dont get minecraft versions from Paper's API.
                    // this probably is the latest version of MC Paper supports
                    // which is not equal to latest version of MC Forge supports
                    var version = JObject.Parse(
                        await webClient.DownloadStringTaskAsync(PaperVersionApi)
                    )["versions"].Last.ToString();
                    
                    // @todo(dw): need to allow choice between 'latest' stream and 'recommended' stream
                    var build = JObject.Parse(
                        await webClient.DownloadStringTaskAsync($"{ForgeHost}{ForgePromotionsJson}")
                    )["promos"][$"{version}-{ForgeBuildStream}"].ToString();
                    return $"{version}-{build}";

                }
            }
            catch (Exception e)
            {
                Error = e.Message;
                return string.Empty;
            }
        }
    }
}
