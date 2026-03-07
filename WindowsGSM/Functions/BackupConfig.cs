using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace WindowsGSM.Functions
{
    class BackupConfig
    {
        private const int DefaultMaximumBackups = 3;
        private const string ConfigFileName = "BackupConfig.cfg";

        static class SettingName
        {
            public const string BackupLocation = "backuplocation";
            public const string SavesLocation = "saveslocation";
            public const string MaximumBackups = "maximumbackups";
        }

        private readonly string _serverId;
        private readonly string _configPath;

        public string BackupLocation { get; private set; }
        public IReadOnlyList<string> SavesLocations { get; private set; } = new List<string>();
        public int MaximumBackups { get; private set; } = DefaultMaximumBackups;

        public BackupConfig(string serverId)
        {
            _serverId = serverId;
            _configPath = ServerPath.GetServersConfigs(_serverId, ConfigFileName);

            string defaultBackupPath = Path.Combine(MainWindow.WGSM_PATH, "Backups", serverId);
            string defaultSavesPath = Path.Combine(MainWindow.WGSM_PATH, "Servers", serverId, "serverfiles");

            try
            {
                if (!File.Exists(_configPath))
                {
                    CreateDefaultConfig(_configPath, defaultBackupPath, defaultSavesPath);
                }

                UpdateConfigWithMissingKeys(_configPath, defaultBackupPath, defaultSavesPath);
                LoadConfig();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackupConfig initialization failed: " + ex.Message);
            }
        }

        private void CreateDefaultConfig(string path, string backupPath, string savePath)
        {
            string content =
$@"// Location where backup archives will be stored
{SettingName.BackupLocation}=""{backupPath}""

// Folder(s) that contain save files to include in backups
// Multiple folders can be separated with ;
// e.g. ""C:\Games\MyServer\Saves;D:\OtherSaves""
{SettingName.SavesLocation}=""{savePath}""

// Maximum number of backup archives to keep
{SettingName.MaximumBackups}=""{DefaultMaximumBackups}""";

            File.WriteAllText(path, content);
        }

        private void UpdateConfigWithMissingKeys(string path, string backupPath, string savePath)
        {
            var lines = File.ReadAllLines(path).ToList();
            bool modified = false;

            modified |= EnsureSettingExists(lines, SettingName.BackupLocation, backupPath, new[]
            {
                "// Location where backup archives will be stored"
            });

            modified |= EnsureSettingExists(lines, SettingName.SavesLocation, savePath, new[]
            {
                "// Folder(s) that contain save files to include in backups",
                "// Multiple folders can be separated with ;",
                "// e.g. \"C:\\Games\\MyServer\\Saves;D:\\OtherSaves\""
            });

            modified |= EnsureSettingExists(lines, SettingName.MaximumBackups, DefaultMaximumBackups.ToString(), new[]
            {
                "// Maximum number of backup archives to keep"
            });

            if (modified)
            {
                File.WriteAllLines(path, lines);
            }
        }

        private static bool EnsureSettingExists(List<string> lines, string key, string value, string[] commentLines)
        {
            bool exists = lines.Any(l => l.TrimStart().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));
            if (exists) return false;

            foreach (string comment in commentLines)
            {
                lines.Add(comment);
            }

            lines.Add(string.Format("{0}=\"{1}\"", key, value));
            return true;
        }

        public void Open()
        {
            try
            {
                var psi = new ProcessStartInfo(_configPath) { UseShellExecute = true };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to open config: " + ex.Message);
            }
        }

        private void LoadConfig()
        {
            foreach (string rawLine in File.ReadLines(_configPath))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;

                int equalsIndex = line.IndexOf('=');
                if (equalsIndex <= 0) continue;

                string key = line.Substring(0, equalsIndex).Trim().ToLowerInvariant();
                string value = line.Substring(equalsIndex + 1).Trim().Trim('"');
                value = Environment.ExpandEnvironmentVariables(value);

                switch (key)
                {
                    case SettingName.BackupLocation:
                        BackupLocation = value;
                        break;

                    case SettingName.SavesLocation:
                        SavesLocations = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(x => x.Trim())
                                              .ToList();
                        break;

                    case SettingName.MaximumBackups:
                        int max;
                        MaximumBackups = int.TryParse(value, out max) ? (max <= 0 ? 1 : max) : DefaultMaximumBackups;
                        break;
                }
            }
            if (string.IsNullOrWhiteSpace(BackupLocation))
            {
                BackupLocation = Path.Combine(MainWindow.WGSM_PATH, "Backups", _serverId);
            }
            if (SavesLocations == null || SavesLocations.Count == 0)
            {
                SavesLocations = new[] { Path.Combine(MainWindow.WGSM_PATH, "Servers", _serverId, "serverfiles") }.ToList();
            }
        }
    }
}
