using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using System.Linq;
using System;
using System.Diagnostics;

namespace WindowsGSM.Functions
{
    class InstallAddons
    {
        private const string WindroseFullName = "Windrose Dedicated Server";
        private const string WindrosePlusDownloadUrl = "https://github.com/HumanGenome/WindrosePlus/releases/latest/download/WindrosePlus.zip";

        public static bool? IsAMXModXAndMetaModPExists(Functions.ServerTable server)
        {
            dynamic gameServer = GameServer.Data.Class.Get(server.Game);
            if (!(gameServer is GameServer.Engine.GoldSource))
            {
                // Game Type not supported
                return null;
            }

            string MMPath = Functions.ServerPath.GetServersServerFiles(server.ID, gameServer.Game, "addons\\metamod.dll");
            return Directory.Exists(MMPath);
        }

        public static async Task<bool> AMXModXAndMetaModP(Functions.ServerTable server)
        {
            try
            {
                dynamic gameServer = GameServer.Data.Class.Get(server.Game);
                return await GameServer.Addon.AMXModX.Install(server.ID, modFolder: gameServer.Game);
            }
            catch
            {
                return false;
            }
        }

        public static bool? IsSourceModAndMetaModExists(Functions.ServerTable server)
        {
            dynamic gameServer = GameServer.Data.Class.Get(server.Game);
            if (!(gameServer is GameServer.Engine.Source))
            {
                // Game Type not supported
                return null;
            }

            string SMPath = Functions.ServerPath.GetServersServerFiles(server.ID, gameServer.Game, "addons\\sourcemod");
            return Directory.Exists(SMPath);
        }

        public static async Task<bool> SourceModAndMetaMod(Functions.ServerTable server)
        {
            try
            {
                dynamic gameServer = GameServer.Data.Class.Get(server.Game);
                return await GameServer.Addon.SourceMod.Install(server.ID, modFolder: gameServer.Game);
            }
            catch
            {
                return false;
            }
        }

        public static bool? IsDayZSALModServerExists(Functions.ServerTable server)
        {
            if (server.Game != GameServer.DAYZ.FullName)
            {
                // Game Type not supported
                return null;
            }

            string exePath = Functions.ServerPath.GetServersServerFiles(server.ID, "DZSALModServer.exe");
            return File.Exists(exePath);
        }

        public static async Task<bool> DayZSALModServer(Functions.ServerTable server)
        {
            try
            {
                string zipPath = Functions.ServerPath.GetServersServerFiles(server.ID, "dzsalmodserver.zip");
                {
                    await WindowsGSM.Functions.Http.DownloadFileAsync("http://dayzsalauncher.com/releases/dzsalmodserver.zip", zipPath);
                    await Task.Run(() => { try { ZipFile.ExtractToDirectory(zipPath, Functions.ServerPath.GetServersServerFiles(server.ID)); } catch { } });
                    await Task.Run(() => { try { File.Delete(zipPath); } catch { } });
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool? IsOxideModExists(Functions.ServerTable server)
        {
            if (server.Game != GameServer.RUST.FullName)
            {
                // Game Type not supported
                return null;
            }

            return File.Exists(Functions.ServerPath.GetServersServerFiles(server.ID, "RustDedicated_Data", "Managed", "Oxide.Core.dll"));
        }

        public static async Task<bool> OxideMod(Functions.ServerTable server)
        {
            try
            {
                string basePath = Functions.ServerPath.GetServersServerFiles(server.ID);
                string zipPath = Functions.ServerPath.GetServersServerFiles(server.ID, "Oxide.Rust.zip");
                {
                    await WindowsGSM.Functions.Http.DownloadFileAsync("https://github.com/OxideMod/Oxide.Rust/releases/latest/download/Oxide.Rust.zip", zipPath);
                }

                bool success = await Task.Run(() =>
                {
                    try
                    {
                        using (var f = File.OpenRead(zipPath))
                        using (var a = new ZipArchive(f))
                        {
                            a.Entries.Where(o => o.Name == string.Empty && !Directory.Exists(Path.Combine(basePath, o.FullName))).ToList().ForEach(o => Directory.CreateDirectory(Path.Combine(basePath, o.FullName)));
                            a.Entries.Where(o => o.Name != string.Empty).ToList().ForEach(e => e.ExtractToFile(Path.Combine(basePath, e.FullName), true));
                        }
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });

                await Task.Run(() => { try { File.Delete(zipPath); } catch { } });
                return success;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<string> GetOxideModLatestVersion()
        {
            try
            {
                string json = await Http.DownloadStringAsync("https://api.github.com/repos/OxideMod/Oxide.Rust/releases/latest");
                return JObject.Parse(json)["tag_name"].ToString();
            }
            catch
            {
                return null;
            }
        }

        public static bool? IsWindrosePlusExists(Functions.ServerTable server)
        {
            if (!IsWindroseServer(server))
            {
                return null;
            }

            string basePath = Functions.ServerPath.GetServersServerFiles(server.ID);
            return Directory.Exists(Path.Combine(basePath, "windrose_plus")) ||
                   File.Exists(Path.Combine(basePath, "StartWindrosePlusServer.bat")) ||
                   File.Exists(Path.Combine(basePath, "R5", "Binaries", "Win64", "dwmapi.dll"));
        }

        public static async Task<bool> WindrosePlus(Functions.ServerTable server)
        {
            try
            {
                string basePath = Functions.ServerPath.GetServersServerFiles(server.ID);
                string zipPath = Functions.ServerPath.GetServersCache(server.ID, "WindrosePlus.zip");

                await WindowsGSM.Functions.Http.DownloadFileAsync(WindrosePlusDownloadUrl, zipPath);

                bool extracted = await Task.Run(() =>
                {
                    try
                    {
                        ExtractZipOverwrite(zipPath, basePath);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });

                if (!extracted)
                {
                    return false;
                }

                await Task.Run(() => { try { File.Delete(zipPath); } catch { } });

                string installScript = Path.Combine(basePath, "install.ps1");
                if (!File.Exists(installScript))
                {
                    return false;
                }

                return await RunPowerShellScript(basePath, installScript);
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> RunPowerShellScript(string workingDirectory, string scriptPath)
        {
            using (var process = new Process())
            {
                process.StartInfo.WorkingDirectory = workingDirectory;
                process.StartInfo.FileName = "powershell.exe";
                process.StartInfo.Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.Start();
                await Task.Run(() => process.WaitForExit());
                return process.ExitCode == 0;
            }
        }

        private static void ExtractZipOverwrite(string zipPath, string destinationDirectory)
        {
            string destinationRoot = Path.GetFullPath(destinationDirectory);
            if (!destinationRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                destinationRoot += Path.DirectorySeparatorChar;
            }

            using (var file = File.OpenRead(zipPath))
            using (var archive = new ZipArchive(file))
            {
                foreach (var entry in archive.Entries)
                {
                    string destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
                    if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                    entry.ExtractToFile(destinationPath, true);
                }
            }
        }

        private static bool IsWindroseServer(Functions.ServerTable server)
        {
            string serverGame = server?.Game ?? string.Empty;
            return string.Equals(serverGame, WindroseFullName, StringComparison.OrdinalIgnoreCase) ||
                   serverGame.StartsWith(WindroseFullName + " [", StringComparison.OrdinalIgnoreCase);
        }
    }
}
