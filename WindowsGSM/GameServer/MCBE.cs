﻿using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Collections.Generic;
using System;

namespace WindowsGSM.GameServer
{
    class MCBE
    {
        internal class MCBEWebclient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                HttpWebRequest req = (HttpWebRequest)base.GetWebRequest(address);
                // WWW server only responds to Compression
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                // Also needs a Accept header.
                req.Accept = "*/*";
                return req;
            }
        }

        private readonly Functions.ServerConfig _serverData;

        public string Error;
        public string Notice;

        public const string FullName = "Minecraft: Bedrock Edition Server";
        public string StartPath = "bedrock_server.exe";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 2;
        public dynamic QueryMethod = null;

        public string Port = "19132";
        public string QueryPort = "19132";
        public string Defaultmap = "Bedrock level";
        public string Maxplayers = "10";
        public string Additional = string.Empty;

        public MCBE(Functions.ServerConfig serverData)
        {
            _serverData = serverData;
        }

        public async void CreateServerCFG()
        {
            //Download server.properties
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "server.properties");
            if (await Functions.Github.DownloadGameServerConfig(configPath, FullName))
            {
                string configText = File.ReadAllText(configPath);
                configText = configText.Replace("{{server-name}}", _serverData.ServerName);
                configText = configText.Replace("{{max-players}}", Maxplayers);
                string tempPort = _serverData.ServerPort;
                configText = configText.Replace("{{server-port}}", tempPort);
                configText = configText.Replace("{{server-portv6}}", (int.Parse(tempPort) + 1).ToString());
                configText = configText.Replace("{{level-name}}", Defaultmap);
                File.WriteAllText(configPath, configText);
            }
        }

        public async Task<Process> Start()
        {
            string workingDir = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID);

            string exePath = Path.Combine(workingDir, StartPath);
            if (!File.Exists(exePath))
            {
                Error = $"{Path.GetFileName(exePath)} not found ({exePath})";
                return null;
            }

            string serverConfigPath = Path.Combine(workingDir, "server.properties");
            if (!File.Exists(serverConfigPath))
            {
                Error = $"{Path.GetFileName(serverConfigPath)} not found ({serverConfigPath})";
                return null;
            }

            Process p;
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = workingDir,
                        FileName = exePath,
                        WindowStyle = ProcessWindowStyle.Minimized,
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
                        WindowStyle = ProcessWindowStyle.Minimized,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                var serverConsole = new Functions.ServerConsole(_serverData.ServerID);
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
            await Task.Run(() =>
            {
                if (p.StartInfo.RedirectStandardInput)
                {
                    p.StandardInput.WriteLine("stop");
                }
                else
                {
                    Functions.ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "stop");
                }
            });
        }

        public async Task<Process> Install()
        {
            //EULA and Privacy Policy
            MessageBoxResult result = System.Windows.MessageBox.Show("By continuing you are indicating your agreement to the Minecraft End User License Agreement and Privacy Policy.\nEULA: (https://minecraft.net/terms)\nPrivacy Policy: (https://go.microsoft.com/fwlink/?LinkId=521839)", "Agreement to the EULA", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                Error = "Disagree to the EULA and Privacy Policy";
                return null;
            }

            try
            {
                using (WebClient webClient = new MCBEWebclient())
                {
                    string html = await webClient.DownloadStringTaskAsync("https://www.minecraft.net/en-us/download/server/bedrock/");
                    Regex regex = new Regex(@"https:\/\/minecraft\.azureedge\.net\/bin-win\/(bedrock-server-(.*?)\.zip)");
                    var matches = regex.Matches(html);

                    if (matches.Count <= 0)
                    {
                        return null;
                    }

                    string downloadUrl = matches[0].Value; //https://minecraft.azureedge.net/bin-win/bedrock-server-1.14.21.0.zip
                    string fileName = matches[0].Groups[1].Value; //bedrock-server-1.14.21.0.zip
                    string version = matches[0].Groups[2].Value; //1.14.21.0

                    //Download zip and extract then delete zip
                    string zipPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, fileName);
                    await webClient.DownloadFileTaskAsync(downloadUrl, zipPath);
                    await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, Functions.ServerPath.GetServersServerFiles(_serverData.ServerID)));
                    await Task.Run(() => File.Delete(zipPath));

                    //Create MCBE-version.txt and write the version
                    File.WriteAllText(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "MCBE-version.txt"), version);
                }
            }
            catch
            {
                //ignore
            }

            return null;
        }

        public async Task<Process> Update()
        {
            try
            {
                using (WebClient webClient = new MCBEWebclient())
                {
                    string remoteBuild = await GetRemoteBuild();

                    string html = await webClient.DownloadStringTaskAsync("https://www.minecraft.net/en-us/download/server/bedrock/");
                    Regex regex = new Regex(@"https:\/\/minecraft\.azureedge\.net\/bin-win\/(bedrock-server-(.*?)\.zip)");
                    var matches = regex.Matches(html);

                    if (matches.Count <= 0)
                    {
                        Error = "Fail to get latest build from https://minecraft.azureedge.net";
                        return null;
                    }

                    string downloadUrl = matches[0].Value; //https://minecraft.azureedge.net/bin-win/bedrock-server-1.14.21.0.zip
                    string fileName = matches[0].Groups[1].Value; //bedrock-server-1.14.21.0.zip
                    string version = matches[0].Groups[2].Value; //1.14.21.0

                    string tempPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "__temp");

                    //Delete old __temp folder
                    if (Directory.Exists(tempPath))
                    {
                        await Task.Run(() => Directory.Delete(tempPath, true));
                    }

                    Directory.CreateDirectory(tempPath);

                    //Download zip and extract then delete zip - install to __temp folder
                    string zipPath = Path.Combine(tempPath, fileName);
                    await webClient.DownloadFileTaskAsync(downloadUrl, zipPath);
                    await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, tempPath));
                    await Task.Run(() => File.Delete(zipPath));

                    string serverFilesPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID);

                    //Delete old folder and files
                    //List of sub folders that may or maynot exist in the various zip files
                    List<string> serverSubFolder = new List<string>() { "behavior_packs", "definitions", "resource_packs", "structures" };

                    await Task.Run(() =>
                    {
                        foreach (var folder in serverSubFolder)
                        {
                            if (Directory.Exists(Path.Combine(serverFilesPath, folder)))
                            {
                                Directory.Delete(Path.Combine(serverFilesPath, folder), true);
                            }
                        }

                        File.Delete(Path.Combine(serverFilesPath, "bedrock_server.exe"));
                        if (File.Exists(Path.Combine(serverFilesPath, "bedrock_server.pdb")))
                            File.Delete(Path.Combine(serverFilesPath, "bedrock_server.pdb"));
                        if (File.Exists(Path.Combine(serverFilesPath, "release-notes.txt")))
                            File.Delete(Path.Combine(serverFilesPath, "release-notes.txt"));
                    });

                    //Move folder and files
                    await Task.Run(() =>
                    {
                        foreach (var folder in serverSubFolder)
                        {
                            if (Directory.Exists(Path.Combine(serverFilesPath, "__temp", folder)))
                            {
                                Directory.Move(Path.Combine(serverFilesPath, "__temp", folder), Path.Combine(serverFilesPath, folder));
                            }
                        }

                        File.Move(Path.Combine(serverFilesPath, "__temp", "bedrock_server.exe"), Path.Combine(serverFilesPath, "bedrock_server.exe"));
                        if (File.Exists(Path.Combine(serverFilesPath, "__temp", "bedrock_server.pdb")))
                            File.Move(Path.Combine(serverFilesPath, "__temp", "bedrock_server.pdb"), Path.Combine(serverFilesPath, "bedrock_server.pdb"));
                        if (File.Exists(Path.Combine(serverFilesPath, "__temp", "release-notes.txt")))
                            File.Move(Path.Combine(serverFilesPath, "__temp", "release-notes.txt"), Path.Combine(serverFilesPath, "release-notes.txt"));
                    });

                    //Delete __temp folder
                    await Task.Run(() => Directory.Delete(tempPath, true));

                    //Create MCBE-version.txt and write the version
                    File.WriteAllText(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "MCBE-version.txt"), version);
                }

                return null;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }
        }

        public bool IsInstallValid()
        {
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }

        public bool IsImportValid(string path)
        {
            Error = $"Invalid Path! Fail to find {StartPath}";
            return File.Exists(Path.Combine(path, StartPath));
        }

        public string GetLocalBuild()
        {
            string versionPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "MCBE-version.txt");
            if (File.Exists(versionPath))
            {
                return File.ReadAllText(versionPath);
            }
            else
            {
                Error = $"Fail to get local build";
                return string.Empty;
            }
        }

        public async Task<string> GetRemoteBuild()
        {
            try
            {
                using (WebClient webClient = new MCBEWebclient())
                {
                    string html = await webClient.DownloadStringTaskAsync("https://www.minecraft.net/en-us/download/server/bedrock/");
                    Regex regex = new Regex(@"https:\/\/minecraft\.azureedge\.net\/bin-win\/(bedrock-server-(.*?)\.zip)");
                    var matches = regex.Matches(html);

                    if (matches.Count > 0)
                    {
                        return matches[0].Groups[2].Value; //1.14.21.0
                    }
                }
            }
            catch
            {
                //ignore
            }

            Error = $"Fail to get remote build";
            return string.Empty;
        }
    }
}
