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
            public const string FilesLocation = "fileslocation";
            public const string MaximumBackups = "maximumbackups";
        }

        private readonly string _serverId;
        private readonly string _configPath;

        public string BackupLocation { get; private set; }
        public IReadOnlyList<string> SavesLocations { get; private set; } = new List<string>();
        public IReadOnlyList<string> FilesLocations { get; private set; } = new List<string>();
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
// Multiple folders can be separated with ; or |
// e.g. ""C:\Games\MyServer\Saves|D:\OtherSaves""
{SettingName.SavesLocation}=""{savePath}""

// File(s) to include in backups
// Multiple files can be separated with ; or |
{SettingName.FilesLocation}=""""

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
                "// Multiple folders can be separated with ; or |",
                "// e.g. \"C:\\Games\\MyServer\\Saves|D:\\OtherSaves\""
            });

            modified |= EnsureSettingExists(lines, SettingName.FilesLocation, string.Empty, new[]
            {
                "// File(s) to include in backups",
                "// Multiple files can be separated with ; or |"
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
            bool modified = false;
            var rewrittenLines = new List<string>();
            string cfgBackupLocation = ServerConfig.GetCustomSetting(_serverId, SettingName.BackupLocation);
            string cfgSavesLocation = ServerConfig.GetCustomSetting(_serverId, SettingName.SavesLocation);
            string cfgFilesLocation = ServerConfig.GetCustomSetting(_serverId, SettingName.FilesLocation);
            string cfgMaximumBackups = ServerConfig.GetCustomSetting(_serverId, SettingName.MaximumBackups);

            foreach (string rawLine in File.ReadLines(_configPath))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                {
                    rewrittenLines.Add(rawLine);
                    continue;
                }

                int equalsIndex = line.IndexOf('=');
                if (equalsIndex <= 0)
                {
                    rewrittenLines.Add(rawLine);
                    continue;
                }

                string key = line.Substring(0, equalsIndex).Trim().ToLowerInvariant();
                string value = line.Substring(equalsIndex + 1).Trim().Trim('"');
                value = Environment.ExpandEnvironmentVariables(value);
                string rewrittenValue = value;

                switch (key)
                {
                    case SettingName.BackupLocation:
                        BackupLocation = NormalizePortablePath(OverrideValue(value, cfgBackupLocation), "Backups", _serverId);
                        rewrittenValue = BackupLocation;
                        break;

                    case SettingName.SavesLocation:
                        string savesLocation = OverrideValue(value, cfgSavesLocation);
                        SavesLocations = SplitBackupList(savesLocation)
                                              .Select(x => NormalizeBackupItemPath(x.Trim()))
                                              .ToList();
                        rewrittenValue = string.Join(";", SavesLocations);
                        break;

                    case SettingName.FilesLocation:
                        string filesLocation = OverrideValue(value, cfgFilesLocation);
                        FilesLocations = SplitBackupList(filesLocation)
                                              .Select(x => NormalizeBackupItemPath(x.Trim()))
                                              .ToList();
                        rewrittenValue = string.Join(";", FilesLocations);
                        break;

                    case SettingName.MaximumBackups:
                        int max;
                        string maximumBackups = OverrideValue(value, cfgMaximumBackups);
                        MaximumBackups = int.TryParse(maximumBackups, out max) ? (max <= 0 ? 1 : max) : DefaultMaximumBackups;
                        break;
                }

                string rewrittenLine = rawLine;
                if (key == SettingName.BackupLocation || key == SettingName.SavesLocation || key == SettingName.FilesLocation)
                {
                    rewrittenLine = $"{line.Substring(0, equalsIndex).Trim()}=\"{rewrittenValue}\"";
                    modified |= !string.Equals(value, rewrittenValue, StringComparison.OrdinalIgnoreCase);
                }

                rewrittenLines.Add(rewrittenLine);
            }
            if (string.IsNullOrWhiteSpace(BackupLocation))
            {
                BackupLocation = Path.Combine(MainWindow.WGSM_PATH, "Backups", _serverId);
            }
            if (SavesLocations == null || SavesLocations.Count == 0)
            {
                SavesLocations = new[] { Path.Combine(MainWindow.WGSM_PATH, "Servers", _serverId, "serverfiles") }.ToList();
            }
            if (FilesLocations == null)
            {
                FilesLocations = new List<string>();
            }

            if (modified)
            {
                File.WriteAllLines(_configPath, rewrittenLines);
            }
        }

        private static string OverrideValue(string value, string overrideValue)
        {
            return string.IsNullOrWhiteSpace(overrideValue) ? value : overrideValue;
        }

        private static IEnumerable<string> SplitBackupList(string value)
        {
            return (value ?? string.Empty).Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private string NormalizeBackupItemPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            string expanded = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
            if (Path.IsPathRooted(expanded))
            {
                return NormalizePortablePath(expanded, "Servers", _serverId, "serverfiles");
            }

            return Path.Combine(ServerPath.GetServersServerFiles(_serverId), expanded);
        }

        private static string NormalizePortablePath(string value, params string[] expectedTail)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            string normalized = value.Trim().Trim('"');

            try
            {
                if (!Path.IsPathRooted(normalized))
                {
                    return normalized;
                }

                string currentRoot = Path.GetFullPath(MainWindow.WGSM_PATH)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullValue = Path.GetFullPath(normalized)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (fullValue.StartsWith(currentRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return fullValue;
                }

                if (expectedTail == null || expectedTail.Length == 0)
                {
                    return fullValue;
                }

                string[] segments = fullValue.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length < expectedTail.Length)
                {
                    return fullValue;
                }

                int startIndex = segments.Length - expectedTail.Length;
                for (int i = 0; i < expectedTail.Length; i++)
                {
                    if (!string.Equals(segments[startIndex + i], expectedTail[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return fullValue;
                    }
                }

                return Path.Combine(new[] { currentRoot }.Concat(expectedTail).ToArray());
            }
            catch
            {
                return normalized;
            }
        }
    }
}
