using ControlzEx.Theming;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using NCrontab;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsGSM.DiscordBot;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using Label = System.Windows.Controls.Label;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;

namespace WindowsGSM
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, WindowShowStyle nCmdShow);

        [DllImport("user32.dll")]
        private static extern int SetWindowText(IntPtr hWnd, string windowName);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static class RegistryKeyName
        {
            public const string HardWareAcceleration = "HardWareAcceleration";
            public const string UIAnimation = "UIAnimation";
            public const string DarkTheme = "DarkTheme";
            public const string StartOnBoot = "StartOnBoot";
            public const string MinimizedOnStart = "MinimizeOnStart";
            public const string RestartOnCrash = "RestartOnCrash";
            public const string DonorTheme = "DonorTheme";
            public const string DonorColor = "DonorColor";
            public const string DonorAuthKey = "DonorAuthKey";
            public const string SendStatistics = "SendStatistics";
            public const string Height = "Height";
            public const string Width = "Width";
            public const string DiscordBotAutoStart = "DiscordBotAutoStart";
            public const string ReadinessCheckShown = "ReadinessCheckShown";
        }

        public class ServerMetadata
        {
            public ServerStatus ServerStatus = ServerStatus.Stopped;
            public Process Process;
            public IntPtr MainWindow;
            public ServerConsole ServerConsole;

            // Basic Game Server Settings
            public bool AutoRestart;
            public bool AutoStart;
            public bool AutoUpdate;
            public bool UpdateOnStart;
            public bool BackupOnStart;

            // Discord Alert Settings
            public bool DiscordAlert;
            public string DiscordMessage;
            public string DiscordWebhook;
            public bool AutoRestartAlert;
            public bool AutoStartAlert;
            public bool AutoUpdateAlert;
            public bool RestartCrontabAlert;
            public bool CrashAlert;
            public bool AutoIpUpdateAlert;
            public bool SkipUserSetup;

            // Restart Crontab Settings
            public bool RestartCrontab;
            public string CrontabFormat;

            // Game Server Start Priority and Affinity
            public string CPUPriority;
            public string CPUAffinity;

            public bool EmbedConsole;
            public bool ShowConsole;
            public bool AutoScroll;

            //runntime member to determine if public Ip changed
            public string CurrentPublicIp;
        }

        private enum WindowShowStyle : uint
        {
            Hide = 0,
            ShowNormal = 1,
            Show = 5,
            Minimize = 6,
            ShowMinNoActivate = 7
        }

        public enum ServerStatus
        {
            Started = 0,
            Starting = 1,
            Stopped = 2,
            Stopping = 3,
            Restarted = 4,
            Restarting = 5,
            Updated = 6,
            Updating = 7,
            Backuped = 8,
            Backup = 9,
            Restored = 10,
            Restoring = 11,
            Deleting = 12,
            Crashed = 13
        }

        private enum DashboardResourceMetric
        {
            CPU,
            Memory
        }

        private sealed class ServerResourceSample
        {
            public DateTime Timestamp { get; set; }
            public double CpuPercent { get; set; }
            public double MemoryMb { get; set; }
        }

        private sealed class ProcessUsageSample
        {
            public TimeSpan TotalProcessorTime { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private enum ReadinessStatus
        {
            Pass,
            Warning,
            Fail,
            Info
        }

        private sealed class ReadinessCheckResult
        {
            public ReadinessStatus Status { get; set; }
            public string Scope { get; set; }
            public string Name { get; set; }
            public string Message { get; set; }
        }

        public static readonly string WGSM_VERSION = "v" + string.Concat(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
        public static readonly int MAX_SERVER = 256;
        public static readonly string WGSM_PATH = GetApplicationPath();
        public static readonly string DEFAULT_THEME = "Cyan";

        private readonly NotifyIcon notifyIcon;
        private Process Installer;

        public static readonly Dictionary<int, ServerMetadata> _serverMetadata = new Dictionary<int, ServerMetadata>();
        public ServerMetadata GetServerMetadata(object serverId) => _serverMetadata.TryGetValue(int.Parse(serverId.ToString()), out var s) ? s : null;

        public List<PluginMetadata> PluginsList = new List<PluginMetadata>();

        private readonly List<System.Windows.Controls.CheckBox> _checkBoxes = new List<System.Windows.Controls.CheckBox>();
        private readonly Dictionary<string, System.Windows.Controls.Control> _editConfigCustomSettingControls = new Dictionary<string, System.Windows.Controls.Control>(StringComparer.OrdinalIgnoreCase);
        private bool _editConfigUsesCustomServerSettingSchema;
        private readonly Dictionary<string, List<ServerResourceSample>> _serverResourceSamples = new Dictionary<string, List<ServerResourceSample>>();
        private readonly Dictionary<string, ProcessUsageSample> _lastProcessUsageSamples = new Dictionary<string, ProcessUsageSample>();
        private readonly Brush[] _dashboardResourceBrushes =
        {
            Brushes.RoyalBlue,
            Brushes.ForestGreen,
            Brushes.Goldenrod,
            Brushes.MediumVioletRed,
            Brushes.DarkOrange,
            Brushes.DeepSkyBlue,
            Brushes.MediumSeaGreen,
            Brushes.IndianRed,
            Brushes.SlateBlue,
            Brushes.Teal
        };
        private static readonly TimeSpan DashboardResourceHistoryDuration = TimeSpan.FromHours(1);
        private DashboardResourceMetric _dashboardResourceMetric = DashboardResourceMetric.CPU;

        private static string GetApplicationPath()
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName;
            string exeDirectory = string.IsNullOrWhiteSpace(exePath) ? null : Path.GetDirectoryName(exePath);

            return string.IsNullOrWhiteSpace(exeDirectory) ? AppContext.BaseDirectory : exeDirectory;
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                {
                    return parent;
                }

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        public string g_DonorType = string.Empty;

        private readonly DiscordBot.Bot g_DiscordBot = new DiscordBot.Bot();

        private long _lastAutoRestartTime = 0;
        private long _lastCrashTime = 0;
        private const long _webhookThresholdTimeInMs = 6000 * 5;
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE = 0xF020;
        private const int HTCAPTION = 2;
        public ServerStatus _latestWebhookSend = ServerStatus.Stopped;

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            source.AddHook(new HwndSourceHook(HandleMessages));
        }

        private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1 || e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            if (FindVisualParent<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            Point mousePosition = e.GetPosition(this);
            if (!IsInDraggableTitleArea(mousePosition))
            {
                return;
            }

            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source?.Handle != IntPtr.Zero)
            {
                ReleaseCapture();
                SendMessage(source.Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
                e.Handled = true;
            }
        }

        private bool IsInDraggableTitleArea(Point point)
        {
            double titleBarHeight = TitleBarHeight > 0 ? TitleBarHeight : 32;
            if (point.Y < 0 || point.Y > titleBarHeight || point.X < 0 || point.X > ActualWidth)
            {
                return false;
            }

            var source = InputHitTest(point) as DependencyObject;
            return FindVisualParent<System.Windows.Controls.Button>(source) == null;
        }

        protected override async void OnClosing(CancelEventArgs e)
        {
            if (e.Cancel)
            {
                return;
            }

            var dialog = await this.GetCurrentDialogAsync<BaseMetroDialog>();
            if (dialog != null && (ShowDialogsOverTitleBar || !dialog.DialogSettings.OwnerCanCloseWithDialog))
            {
                e.Cancel = true;
                return;
            }

            var runningServers = ServerGrid.Items.Cast<ServerTable>()
                                .Count(server => GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started);

            if (runningServers > 0)
            {
                string message = $"{runningServers} server(s) are currently running and will be stopped.\nAre you sure you want to continue?";
                const string caption = "Servers Running";
                var result = MessageBox.Show(this, message, caption, MessageBoxButton.OKCancel, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            e.Cancel = true;
            // Hide();
            await StoppAllServers();
            System.Windows.Application.Current.Shutdown();
        }

        public async Task StoppAllServers()
        {
            var stopTasks = new List<Task>();
            foreach (var server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started)
                {
                    stopTasks.Add(GameServer_Stop(server));
                }
            }

            if (stopTasks.Any())
            {
                await Task.WhenAll(stopTasks);
            }
        }

        private IntPtr HandleMessages(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST)
            {
                int x = unchecked((short)((long)lParam & 0xFFFF));
                int y = unchecked((short)(((long)lParam >> 16) & 0xFFFF));
                Point point = PointFromScreen(new Point(x, y));

                if (IsInDraggableTitleArea(point))
                {
                    handled = true;
                    return new IntPtr(HTCAPTION);
                }
            }

            if (msg == WM_SYSCOMMAND && ((int)wParam & 0xFFF0) == SC_MINIMIZE)
            {
                NotifyIcon_MouseClick(null, null);
                handled = true;
            }

            return IntPtr.Zero;
        }

        public MainWindow(bool showCrashHint)
        {
            //Add SplashScreen
            var splashScreen = new SplashScreen("Images/SplashScreen.png");
            splashScreen.Show(false, true);
            try
            {
                DiscordWebhook.SendErrorLog();
            }
            catch
            {
                // Ignore errors sending error logs
            }

            InitializeComponent();
            AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(MainWindow_MouseLeftButtonDown), true);
            this.SourceInitialized += new EventHandler(OnSourceInitialized);

            Title = $"WindowsGSM {WGSM_VERSION}";

            //Close SplashScreen
            splashScreen.Close(new TimeSpan(0, 0, 1));

            // Add all themes to comboBox_Themes
            ThemeManager.Current.Themes.Select(t => Path.GetExtension(t.Name).Trim('.')).Distinct().OrderBy(x => x).ToList().ForEach(delegate (string name) { comboBox_Themes.Items.Add(name); });

            // Set up _serverMetadata
            for (int i = 0; i < MAX_SERVER; i++)
            {
                _serverMetadata[i] = new ServerMetadata
                {
                    ServerStatus = ServerStatus.Stopped,
                    ServerConsole = new ServerConsole(i)
                };
            }

            var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM");
            if (key == null)
            {
                key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\WindowsGSM");
                key.SetValue(RegistryKeyName.HardWareAcceleration, "True");
                key.SetValue(RegistryKeyName.UIAnimation, "True");
                key.SetValue(RegistryKeyName.DarkTheme, "False");
                key.SetValue(RegistryKeyName.StartOnBoot, "False");
                key.SetValue(RegistryKeyName.MinimizedOnStart, "False");
                key.SetValue(RegistryKeyName.RestartOnCrash, "False");
                key.SetValue(RegistryKeyName.DonorTheme, "False");
                key.SetValue(RegistryKeyName.DonorColor, DEFAULT_THEME);
                key.SetValue(RegistryKeyName.DonorAuthKey, "");
                key.SetValue(RegistryKeyName.SendStatistics, "True");
                key.SetValue(RegistryKeyName.Height, Height);
                key.SetValue(RegistryKeyName.Width, Width);
                key.SetValue(RegistryKeyName.DiscordBotAutoStart, "False");
                key.SetValue(RegistryKeyName.ReadinessCheckShown, "False");
            }

            bool shouldShowReadinessCheck = (key.GetValue(RegistryKeyName.ReadinessCheckShown) ?? false).ToString() != "True";

            MahAppSwitch_HardWareAcceleration.IsOn = (key.GetValue(RegistryKeyName.HardWareAcceleration) ?? true).ToString() == "True";
            MahAppSwitch_UIAnimation.IsOn = (key.GetValue(RegistryKeyName.UIAnimation) ?? true).ToString() == "True";
            MahAppSwitch_DarkTheme.IsOn = (key.GetValue(RegistryKeyName.DarkTheme) ?? false).ToString() == "True";
            MahAppSwitch_StartOnBoot.IsOn = (key.GetValue(RegistryKeyName.StartOnBoot) ?? false).ToString() == "True";
            MahAppSwitch_MinimizeOnStart.IsOn = (key.GetValue(RegistryKeyName.MinimizedOnStart) ?? false).ToString() == "True";
            MahAppSwitch_RestartOnCrash.IsOn = (key.GetValue(RegistryKeyName.RestartOnCrash) ?? false).ToString() == "True";
            MahAppSwitch_DonorConnect.Toggled -= DonorConnect_IsCheckedChanged;
            MahAppSwitch_DonorConnect.IsOn = (key.GetValue(RegistryKeyName.DonorTheme) ?? false).ToString() == "True";
            MahAppSwitch_DonorConnect.Toggled += DonorConnect_IsCheckedChanged;
            MahAppSwitch_SendStatistics.IsOn = (key.GetValue(RegistryKeyName.SendStatistics) ?? true).ToString() == "True";
            MahAppSwitch_DiscordBotAutoStart.IsOn = (key.GetValue(RegistryKeyName.DiscordBotAutoStart) ?? false).ToString() == "True";
            string color = (key.GetValue(RegistryKeyName.DonorColor) ?? string.Empty).ToString();
            comboBox_Themes.SelectionChanged -= ComboBox_Themes_SelectionChanged;
            comboBox_Themes.SelectedItem = comboBox_Themes.Items.Contains(color) ? color : DEFAULT_THEME;
            comboBox_Themes.SelectionChanged += ComboBox_Themes_SelectionChanged;

            if (MahAppSwitch_DonorConnect.IsOn)
            {
                string authKey = (key.GetValue(RegistryKeyName.DonorAuthKey) == null) ? string.Empty : key.GetValue(RegistryKeyName.DonorAuthKey).ToString();
                if (!string.IsNullOrWhiteSpace(authKey))
                {
#pragma warning disable 4014
                    AuthenticateDonor(authKey);
#pragma warning restore
                }
            }

            //double.parse can throw when the user changes locale after WindowsGsm setup
            //Height = (key.GetValue(RegistryKeyName.Height) == null) ? Height : double.Parse(key.GetValue(RegistryKeyName.Height).ToString());
            //Width = (key.GetValue(RegistryKeyName.Width) == null) ? Width : double.Parse(key.GetValue(RegistryKeyName.Width).ToString());
            var regValue = 0.0;
            if (InvariantTryParse(key.GetValue(RegistryKeyName.Height).ToString(), out regValue))
                Height = regValue;
            if (InvariantTryParse(key.GetValue(RegistryKeyName.Width).ToString(), out regValue))
                Width = regValue;
            key.Close();

            RenderOptions.ProcessRenderMode = MahAppSwitch_HardWareAcceleration.IsOn ? System.Windows.Interop.RenderMode.SoftwareOnly : System.Windows.Interop.RenderMode.Default;
            WindowTransitionsEnabled = MahAppSwitch_UIAnimation.IsOn;
            ThemeManager.Current.ChangeTheme(this, $"{(MahAppSwitch_DarkTheme.IsOn ? "Dark" : "Light")}.{comboBox_Themes.SelectedItem}");
            //Not required - it is set in windows settings
            //SetStartOnBoot(MahAppSwitch_StartOnBoot.IsChecked ?? false);
            if (MahAppSwitch_DiscordBotAutoStart.IsOn)
            {
                switch_DiscordBot.IsOn = true;
            }

            // Add items to Set Affinity Flyout
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                StackPanel stackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(15, 0, 0, 0)
                };

                _checkBoxes.Add(new System.Windows.Controls.CheckBox());
                _checkBoxes[i].Focusable = false;
                var label = new Label
                {
                    Content = $"CPU {i}",
                    Padding = new Thickness(0, 5, 0, 5)
                };

                stackPanel.Children.Add(_checkBoxes[i]);
                stackPanel.Children.Add(label);
                StackPanel_SetAffinity.Children.Add(stackPanel);
            }

            // Add click listener on each checkBox
            foreach (var checkBox in _checkBoxes)
            {
                checkBox.Click += (sender, e) =>
                {
                    var server = (ServerTable)ServerGrid.SelectedItem;
                    if (server == null) { return; }

                CheckPrioity:
                    string priority = string.Empty;
                    for (int i = _checkBoxes.Count - 1; i >= 0; i--)
                    {
                        priority += (_checkBoxes[i].IsChecked ?? false) ? "1" : "0";
                    }

                    if (!priority.Contains("1"))
                    {
                        checkBox.IsChecked = true;
                        goto CheckPrioity;
                    }

                    textBox_SetAffinity.Text = Functions.CPU.Affinity.GetAffinityValidatedString(priority);

                    _serverMetadata[int.Parse(server.ID)].CPUAffinity = priority;
                    ServerConfig.SetSetting(server.ID, "cpuaffinity", priority);

                    if (GetServerMetadata(server.ID).Process != null && !GetServerMetadata(server.ID).Process.HasExited)
                    {
                        _serverMetadata[int.Parse(server.ID)].Process.ProcessorAffinity = Functions.CPU.Affinity.GetAffinityIntPtr(priority);
                    }
                };
            }

            notifyIcon = new NotifyIcon
            {
                BalloonTipTitle = "WindowsGSM",
                BalloonTipText = "WindowsGSM is running in the background",
                Text = "WindowsGSM",
                BalloonTipIcon = ToolTipIcon.Info,
                Visible = true
            };

            notifyIcon.Icon = new System.Drawing.Icon(System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Images/WindowsGSM-Icon.ico")).Stream);
            notifyIcon.BalloonTipClicked += OnBalloonTipClick;
            notifyIcon.MouseClick += NotifyIcon_MouseClick;


            ServerPath.CreateAndFixDirectories();

            LoadPlugins(shouldAwait: false);
            AddGamesToComboBox();

            LoadServerTable();

            if (ServerGrid.Items.Count > 0)
            {
                ServerGrid.SelectedItem = ServerGrid.Items[0];
            }

            foreach (var server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                int pid = ServerCache.GetPID(server.ID);
                if (pid != -1)
                {
                    Process p = null;
                    try
                    {
                        p = Process.GetProcessById(pid);
                    }
                    catch
                    {
                        continue;
                    }

                    string pName = ServerCache.GetProcessName(server.ID);
                    if (!string.IsNullOrWhiteSpace(pName) && p.ProcessName == pName)
                    {
                        _serverMetadata[int.Parse(server.ID)].Process = p;
                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Started;
                        SetServerStatus(server, "Started");

                        /*// Get Console process - untested
                        Process console = GetConsoleProcess(pid);
                        if (console != null)
                        {
                            ReadConsoleOutput(server.ID, console);
                        }
                        */

                        _serverMetadata[int.Parse(server.ID)].MainWindow = ServerCache.GetWindowsIntPtr(server.ID);
                        p.Exited += (sender, e) => OnGameServerExited(server);

                        StartAutoUpdateCheck(server);
                        StartRestartCrontabCheck(server);
                        StartSendHeartBeat(server);
                        StartQuery(server);
                    }
                }
            }

            if (showCrashHint)
            {
                string logFile = $"CRASH_{DateTime.Now:yyyyMMdd}.log";
                Log("System", $"WindowsGSM crashed unexpectedly, please view the crash log {logFile}");
            }

            AutoStartServer();

            if (MahAppSwitch_SendStatistics.IsOn)
            {
                SendGoogleAnalytics();
            }

            StartConsoleRefresh();

            StartServerTableRefresh();

            StartDashBoardRefresh();

            StartAutpIpUpdate();

            if (shouldShowReadinessCheck)
            {
                key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true);
                key?.SetValue(RegistryKeyName.ReadinessCheckShown, "True");
                key?.Close();

                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    await RunReadinessChecks();
                    MahAppFlyout_ReadinessCheck.IsOpen = true;
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        private Process GetConsoleProcess(int processId)
        {
            try
            {
                ManagementObjectSearcher mos = new ManagementObjectSearcher($"Select * From Win32_Process Where ParentProcessID={processId}");
                foreach (ManagementObject mo in mos.Get())
                {
                    Process p = Process.GetProcessById(Convert.ToInt32(mo["ProcessID"]));
                    if (Equals(p, "conhost"))
                    {
                        return p;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        // Read console redirect output - not tested
        private async void ReadConsoleOutput(string serverId, Process p)
        {
            await Task.Run(() =>
            {
                var reader = p.StandardOutput;
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        GetServerMetadata(serverId).ServerConsole.Add(line);
                    });
                }
            });
        }

        public void AddGamesToComboBox()
        {
            comboBox_InstallGameServer.Items.Clear();
            comboBox_ImportGameServer.Items.Clear();

            //Add games to ComboBox
            SortedList sortedList = new SortedList();
            List<DictionaryEntry> gameName = GameServer.Data.Icon.ResourceManager.GetResourceSet(System.Globalization.CultureInfo.CurrentUICulture, true, true).Cast<DictionaryEntry>().ToList();
            gameName.ForEach(delegate (DictionaryEntry entry) { sortedList.Add(entry.Key, $"/WindowsGSM;component/{entry.Value}"); });
            int pluginLoaded = 0;
            PluginsList.ForEach(delegate (PluginMetadata plugin)
            {
                if (plugin.IsLoaded)
                {
                    pluginLoaded++;
                    sortedList.Add(plugin.FullName, plugin.GameImage == PluginManagement.DefaultPluginImage ? plugin.GameImage.Replace("pack://application:,,,", "/WindowsGSM;component") : plugin.GameImage);
                }
            });

            label_GameServerCount.Content = $"{gameName.Count + pluginLoaded} game servers supported";

            for (int i = 0; i < sortedList.Count; i++)
            {
                var row = new Images.Row
                {
                    Image = sortedList.GetByIndex(i).ToString(),
                    Name = sortedList.GetKey(i).ToString()
                };

                comboBox_InstallGameServer.Items.Add(row);
                comboBox_ImportGameServer.Items.Add(row);
            }
        }

        public async void LoadPlugins(bool shouldAwait = true)
        {
            var pm = new PluginManagement();
            PluginsList = await pm.LoadPlugins(shouldAwait);

            int loadedCount = 0;
            PluginsList.ForEach(delegate (PluginMetadata plugin)
            {
                if (!plugin.IsLoaded)
                {
                    Directory.CreateDirectory(ServerPath.GetLogs(ServerPath.FolderName.Plugins));
                    string logFile = ServerPath.GetLogs(ServerPath.FolderName.Plugins, $"{plugin.FileName}.log");
                    File.WriteAllText(ServerPath.GetLogs(logFile), plugin.Error);
                    Log("Plugins", $"{plugin.FileName} fail to load. Please view the log: {logFile.Replace(WGSM_PATH, string.Empty)}");
                }
                else
                {
                    loadedCount++;
                    var converter = new BrushConverter();
                    Brush brush;
                    try
                    {
                        brush = (Brush)converter.ConvertFromString(plugin.Plugin.color);
                    }
                    catch
                    {
                        brush = Brushes.DimGray;
                    }

                    var borderBase = new Border
                    {
                        BorderBrush = brush,
                        Background = Brushes.SlateGray,
                        BorderThickness = new Thickness(1.5),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(6),
                        Margin = new Thickness(10, 0, 10, 10)
                    };
                    DockPanel.SetDock(borderBase, Dock.Top);
                    var dockPanelBase = new DockPanel();
                    var gameImage = new Border
                    {
                        BorderBrush = Brushes.White,
                        Background = new ImageBrush
                        {
                            Stretch = Stretch.Fill,
                            ImageSource = plugin.GameImage == PluginManagement.DefaultPluginImage ? PluginManagement.GetDefaultPluginBitmapSource() : new BitmapImage(new Uri(plugin.GameImage))
                        },
                        BorderThickness = new Thickness(0),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(10),
                        Width = 63,
                        Height = 63,
                        MinWidth = 63,
                        MinHeight = 63,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    dockPanelBase.Children.Add(gameImage);

                    var dockPanel = new DockPanel { Margin = new Thickness(0, 0, 3, 0) };
                    DockPanel.SetDock(dockPanel, Dock.Top);
                    var label = new Label { Content = $"v{plugin.Plugin.version}", Padding = new Thickness(0) };
                    DockPanel.SetDock(label, Dock.Right);
                    dockPanel.Children.Add(label);
                    label = new Label { Content = plugin.Plugin.name.Split('.')[1], Padding = new Thickness(0), FontSize = 14, FontWeight = FontWeights.Bold };
                    DockPanel.SetDock(label, Dock.Left);
                    dockPanel.Children.Add(label);
                    dockPanelBase.Children.Add(dockPanel);

                    var textBlock = new TextBlock { Text = plugin.Plugin.description };
                    DockPanel.SetDock(textBlock, Dock.Top);
                    dockPanelBase.Children.Add(textBlock);

                    var stackPanelBase = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
                    var authorImage = new Border
                    {
                        Background = new ImageBrush
                        {
                            Stretch = Stretch.Fill,
                            ImageSource = plugin.AuthorImage == PluginManagement.DefaultUserImage ? PluginManagement.GetDefaultUserBitmapSource() : new BitmapImage(new Uri(plugin.AuthorImage))
                        },
                        BorderThickness = new Thickness(0),
                        CornerRadius = new CornerRadius(30),
                        Padding = new Thickness(10),
                        Width = 25,
                        Height = 25,
                        Margin = new Thickness(0, 0, 5, 0)
                    };
                    stackPanelBase.Children.Add(authorImage);
                    var stackPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    label = new Label { Content = plugin.Plugin.author, Padding = new Thickness(0) };
                    DockPanel.SetDock(label, Dock.Top);
                    stackPanel.Children.Add(label);
                    label = new Label { Content = "•", Padding = new Thickness(0), Margin = new Thickness(5, 0, 5, 0) };
                    DockPanel.SetDock(label, Dock.Top);
                    stackPanel.Children.Add(label);
                    textBlock = new TextBlock();
                    var hyperlink = new Hyperlink(new Run(plugin.Plugin.url)) { Foreground = brush };
                    try
                    {
                        hyperlink.NavigateUri = new Uri(plugin.Plugin.url);
                        hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
                    }
                    catch { }

                    textBlock.Inlines.Add(hyperlink);
                    stackPanel.Children.Add(textBlock);
                    stackPanelBase.Children.Add(stackPanel);
                    dockPanelBase.Children.Add(stackPanelBase);

                    borderBase.Child = dockPanelBase;
                    StackPanel_PluginList.Children.Add(borderBase);
                }
            });

            AddGamesToComboBox();

            Label_PluginInstalled.Content = PluginsList.Count.ToString();
            Label_PluginLoaded.Content = loadedCount.ToString();
            Label_PluginFailed.Content = (PluginsList.Count - loadedCount).ToString();

            Log("Plugins", $"Installed: {PluginsList.Count}, Loaded: {loadedCount}, Failed: {PluginsList.Count - loadedCount}");
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ImportPlugin_Click(object sender, RoutedEventArgs e)
        {
            // If a server is installing or import => return
            if (progressbar_InstallProgress.IsIndeterminate || progressbar_ImportProgress.IsIndeterminate)
            {
                MessageBox.Show("WindowsGSM is currently installing/importing server!", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string pluginsDir = ServerPath.FolderName.Plugins;

            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "zip files (*.zip)|*.zip";
            ofd.InitialDirectory = pluginsDir;

            DialogResult dr = ofd.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK)
            {
                Button_ImportPlugin.IsEnabled = false;
                ProgressRing_LoadPlugins.Visibility = Visibility.Visible;
                Label_PluginInstalled.Content = "-";
                Label_PluginLoaded.Content = "-";
                Label_PluginFailed.Content = "-";
                StackPanel_PluginList.Children.Clear();

                /// This is relying on it keeps the naming shceme of the ZIP files that're downloaed from GitHub releases. Like WindowsGSM.Spigot-1.0.zip,
                /// Just by following WindowsGSM naming of plugins, and this will be fine!
                string zipPath = ofd.FileName;
                string dirName = ofd.SafeFileName.Split('.')[1].Split('-')[0] + ".cs";
                string knownPattern = ".cs";
                // Unziping plugin
                using (ZipArchive zip = System.IO.Compression.ZipFile.OpenRead(zipPath))
                {
                    var result = from entry in zip.Entries
                                 where Path.GetDirectoryName(entry.FullName).Contains(knownPattern)
                                 where !String.IsNullOrEmpty(entry.Name)
                                 select entry;

                    Directory.CreateDirectory(Path.Combine(pluginsDir, dirName));
                    foreach (ZipArchiveEntry entryFile in result)
                    {
                        entryFile.ExtractToFile(Path.Combine(pluginsDir, dirName, entryFile.Name), true);
                    }
                }

                await Task.Delay(500);
                LoadPlugins();
                LoadServerTable();

                Button_ImportPlugin.IsEnabled = true;
                ProgressRing_LoadPlugins.Visibility = Visibility.Collapsed;
            }
        }

        private async void RefreshPlugins_Click(object sender, RoutedEventArgs e)
        {
            // If a server is installing or import => return
            if (progressbar_InstallProgress.IsIndeterminate || progressbar_ImportProgress.IsIndeterminate)
            {
                MessageBox.Show("WindowsGSM is currently installing/importing server!", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Button_RefreshPlugins.IsEnabled = false;
            ProgressRing_LoadPlugins.Visibility = Visibility.Visible;
            Label_PluginInstalled.Content = "-";
            Label_PluginLoaded.Content = "-";
            Label_PluginFailed.Content = "-";
            StackPanel_PluginList.Children.Clear();

            await Task.Delay(500);
            LoadPlugins();
            LoadServerTable();

            Button_RefreshPlugins.IsEnabled = true;
            ProgressRing_LoadPlugins.Visibility = Visibility.Collapsed;
        }

        public void LoadServerTable()
        {
            string[] livePlayerData = new string[MAX_SERVER + 1];
            string[] liveUptimeData = new string[MAX_SERVER + 1];
            foreach (ServerTable item in ServerGrid.Items)
            {
                livePlayerData[int.Parse(item.ID)] = item.Maxplayers;
                liveUptimeData[int.Parse(item.ID)] = item.Uptime;
            }

            var selectedrow = (ServerTable)ServerGrid.SelectedItem;
            ServerGrid.Items.Clear();

            //Add server to datagrid
            for (int i = 1; i <= MAX_SERVER; i++)
            {
                string serverid_path = Path.Combine(WGSM_PATH, "servers", i.ToString());
                if (!Directory.Exists(serverid_path)) { continue; }

                string configpath = ServerPath.GetServersConfigs(i.ToString(), "WindowsGSM.cfg");
                if (!File.Exists(configpath)) { continue; }

                var serverConfig = new ServerConfig(i.ToString());

                //If Game server not exist return
                if (GameServer.Data.Class.Get(serverConfig.ServerGame, pluginList: PluginsList) == null) { continue; }

                string status;
                switch (GetServerMetadata(i).ServerStatus)
                {
                    case ServerStatus.Started: status = "Started"; break;
                    case ServerStatus.Starting: status = "Starting"; break;
                    case ServerStatus.Stopped: status = "Stopped"; break;
                    case ServerStatus.Stopping: status = "Stopping"; break;
                    case ServerStatus.Restarted: status = "Restarted"; break;
                    case ServerStatus.Restarting: status = "Restarting"; break;
                    case ServerStatus.Updated: status = "Updated"; break;
                    case ServerStatus.Updating: status = "Updating"; break;
                    case ServerStatus.Backuped: status = "Backuped"; break;
                    case ServerStatus.Backup: status = "Backup"; break;
                    case ServerStatus.Restored: status = "Restored"; break;
                    case ServerStatus.Restoring: status = "Restoring"; break;
                    case ServerStatus.Deleting: status = "Deleting"; break;
                    default:
                        {
                            _serverMetadata[i].ServerStatus = ServerStatus.Stopped;
                            status = "Stopped";
                            break;
                        }
                }

                try
                {
                    string icon = GameServer.Data.Icon.ResourceManager.GetString(serverConfig.ServerGame);
                    if (icon == null)
                    {
                        PluginsList.ForEach(delegate (PluginMetadata plugin)
                        {
                            if (plugin.FullName == serverConfig.ServerGame && plugin.IsLoaded)
                            {
                                icon = plugin.GameImage == PluginManagement.DefaultPluginImage
                                    ? plugin.GameImage.Replace("pack://application:,,,", "/WindowsGSM;component")
                                    : plugin.GameImage;
                            }
                        });
                    }
                    if (icon == null)
                    {
                        icon = PluginManagement.DefaultPluginImage.Replace("pack://application:,,,", "/WindowsGSM;component");
                    }

                    string serverId = i.ToString();
                    string pidString = string.Empty;

                    try
                    {
                        pidString = Process.GetProcessById(ServerCache.GetPID(serverId)).Id.ToString();
                    }
                    catch { }

                    var server = new ServerTable
                    {
                        ID = i.ToString(),
                        PID = pidString,
                        Game = serverConfig.ServerGame,
                        Icon = icon,
                        Status = status,
                        Name = serverConfig.ServerName,
                        IP = serverConfig.ServerIP,
                        Port = serverConfig.ServerPort,
                        QueryPort = serverConfig.ServerQueryPort,
                        Defaultmap = serverConfig.ServerMap,
                        Maxplayers = (GetServerMetadata(i).ServerStatus != ServerStatus.Started) ? serverConfig.ServerMaxPlayer : livePlayerData[i],
                        Uptime = liveUptimeData[i]
                    };

                    SaveServerConfigToServerMetadata(i, serverConfig);
                    ServerGrid.Items.Add(server);

                    if (selectedrow != null)
                    {
                        if (selectedrow.ID == server.ID)
                        {
                            ServerGrid.SelectedItem = server;
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            grid_action.Visibility = (ServerGrid.Items.Count != 0) ? Visibility.Visible : Visibility.Hidden;
            label_select.Visibility = grid_action.Visibility == Visibility.Hidden ? Visibility.Visible : Visibility.Hidden;
        }

        private void SaveServerConfigToServerMetadata(object serverId, ServerConfig serverConfig)
        {
            int i = int.Parse(serverId.ToString());

            // Basic Game Server Settings
            _serverMetadata[i].AutoRestart = serverConfig.AutoRestart;
            _serverMetadata[i].AutoStart = serverConfig.AutoStart;
            _serverMetadata[i].AutoUpdate = serverConfig.AutoUpdate;
            _serverMetadata[i].UpdateOnStart = serverConfig.UpdateOnStart;
            _serverMetadata[i].BackupOnStart = serverConfig.BackupOnStart;

            // Discord Alert Settings
            _serverMetadata[i].DiscordAlert = serverConfig.DiscordAlert;
            _serverMetadata[i].DiscordMessage = serverConfig.DiscordMessage;
            _serverMetadata[i].DiscordWebhook = serverConfig.DiscordWebhook;
            _serverMetadata[i].AutoRestartAlert = serverConfig.AutoRestartAlert;
            _serverMetadata[i].AutoStartAlert = serverConfig.AutoStartAlert;
            _serverMetadata[i].AutoUpdateAlert = serverConfig.AutoUpdateAlert;
            _serverMetadata[i].AutoIpUpdateAlert = serverConfig.AutoIpUpdate;
            _serverMetadata[i].SkipUserSetup = serverConfig.SkipUserSetup;
            _serverMetadata[i].RestartCrontabAlert = serverConfig.RestartCrontabAlert;
            _serverMetadata[i].CrashAlert = serverConfig.CrashAlert;

            // Restart Crontab Settings
            _serverMetadata[i].RestartCrontab = serverConfig.RestartCrontab;
            _serverMetadata[i].CrontabFormat = serverConfig.CrontabFormat;

            // Game Server Start Priority and Affinity
            _serverMetadata[i].CPUPriority = serverConfig.CPUPriority;
            _serverMetadata[i].CPUAffinity = serverConfig.CPUAffinity;

            _serverMetadata[i].EmbedConsole = serverConfig.EmbedConsole;
            _serverMetadata[i].ShowConsole = serverConfig.ShowConsole;
            _serverMetadata[i].AutoScroll = serverConfig.AutoScroll;
        }

        private async void AutoStartServer()
        {
            foreach (ServerTable server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                int serverId = int.Parse(server.ID);

                if (GetServerMetadata(serverId).AutoStart && GetServerMetadata(server.ID).ServerStatus == ServerStatus.Stopped)
                {
                    await GameServer_Start(server, " | Auto Start");

                    if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started)
                    {
                        if (GetServerMetadata(serverId).DiscordAlert && GetServerMetadata(serverId).AutoStartAlert)
                        {
                            var webhook = new DiscordWebhook(GetServerMetadata(serverId).DiscordWebhook, GetServerMetadata(serverId).DiscordMessage, g_DonorType, GetServerMetadata(serverId).SkipUserSetup);
                            await webhook.Send(server.ID, server.Game, "Started | Auto Start", server.Name, server.IP, server.Port);
                            _latestWebhookSend = GetServerMetadata(serverId).ServerStatus;
                        }
                    }
                }
            }
        }


        private async Task SendCurrentPublicIPs()
        {
            string currentPublicIp = GetPublicIP();
            foreach (ServerTable server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                try
                {
                    int serverId = int.Parse(server.ID);
                    Process p = GetServerMetadata(server.ID).Process;
                    if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started || GetServerMetadata(server.ID).ServerStatus == ServerStatus.Starting)
                    {

                        if (GetServerMetadata(serverId).DiscordAlert && GetServerMetadata(server.ID).AutoIpUpdateAlert)
                        {
                            if (_serverMetadata[serverId].CurrentPublicIp == string.Empty || _serverMetadata[serverId].CurrentPublicIp != currentPublicIp)
                            {
                                var webhook = new DiscordWebhook(GetServerMetadata(serverId).DiscordWebhook, GetServerMetadata(serverId).DiscordMessage, g_DonorType, GetServerMetadata(serverId).SkipUserSetup);
                                await webhook.Send(server.ID, server.Game, "Current Public IP", server.Name, currentPublicIp, server.Port);
                                _serverMetadata[serverId].CurrentPublicIp = currentPublicIp;
                            }
                        }
                    }
                }
                catch (Exception) { }
            }
        }

        private async void StartServerTableRefresh()
        {
            while (true)
            {
                await Task.Delay(60 * 1000);
                ServerGrid.Items.Refresh();
            }
        }

        private async void StartConsoleRefresh()
        {
            while (true)
            {
                await Task.Delay(100);
                var row = (ServerTable)ServerGrid.SelectedItem;
                if (row != null)
                {
                    string text = GetServerMetadata(int.Parse(row.ID)).ServerConsole.Get();
                    if (text.Length != console.Text.Length && text != console.Text)
                    {
                        console.Text = text;

                        if (GetServerMetadata(row.ID).AutoScroll)
                        {
                            console.ScrollToEnd();
                        }
                    }
                }
            }
        }

        private async void StartDashBoardRefresh()
        {
            var system = new SystemMetrics();

            // Get CPU info and Set
            await Task.Run(() => system.GetCPUStaticInfo());
            dashboard_cpu_type.Content = system.CPUType;

            // Get RAM info and Set
            await Task.Run(() => system.GetRAMStaticInfo());
            dashboard_ram_type.Content = system.RAMType;

            // Get Disk info and Set
            await Task.Run(() => system.GetDiskStaticInfo());
            dashboard_disk_name.Content = $"({system.DiskName})";
            dashboard_disk_type.Content = system.DiskType;

            while (true)
            {
                dashboard_cpu_bar.Value = await Task.Run(() => system.GetCPUUsage());
                dashboard_cpu_bar.Value = (dashboard_cpu_bar.Value > 100.0) ? 100.0 : dashboard_cpu_bar.Value;
                dashboard_cpu_usage.Content = $"{dashboard_cpu_bar.Value}%";

                dashboard_ram_bar.Value = await Task.Run(() => system.GetRAMUsage());
                dashboard_ram_bar.Value = (dashboard_ram_bar.Value > 100.0) ? 100.0 : dashboard_ram_bar.Value;
                dashboard_ram_usage.Content = $"{string.Format("{0:0.00}", dashboard_ram_bar.Value)}%";
                dashboard_ram_ratio.Content = SystemMetrics.GetMemoryRatioString(dashboard_ram_bar.Value, system.RAMTotalSize);

                dashboard_disk_bar.Value = await Task.Run(() => system.GetDiskUsage());
                dashboard_disk_bar.Value = (dashboard_disk_bar.Value > 100.0) ? 100.0 : dashboard_disk_bar.Value;
                dashboard_disk_usage.Content = $"{string.Format("{0:0.00}", dashboard_disk_bar.Value)}%";
                dashboard_disk_ratio.Content = SystemMetrics.GetDiskRatioString(dashboard_disk_bar.Value, system.DiskTotalSize);

                dashboard_servers_bar.Value = ServerGrid.Items.Count * 100.0 / MAX_SERVER;
                dashboard_servers_bar.Value = (dashboard_servers_bar.Value > 100.0) ? 100.0 : dashboard_servers_bar.Value;
                dashboard_servers_usage.Content = $"{string.Format("{0:0.00}", dashboard_servers_bar.Value)}%";
                dashboard_servers_ratio.Content = $"{ServerGrid.Items.Count}/{MAX_SERVER}";

                int startedCount = GetStartedServerCount();
                dashboard_started_bar.Value = ServerGrid.Items.Count == 0 ? 0 : startedCount * 100.0 / ServerGrid.Items.Count;
                dashboard_started_bar.Value = (dashboard_started_bar.Value > 100.0) ? 100.0 : dashboard_started_bar.Value;
                dashboard_started_usage.Content = $"{string.Format("{0:0.00}", dashboard_started_bar.Value)}%";
                dashboard_started_ratio.Content = $"{startedCount}/{ServerGrid.Items.Count}";

                dashboard_players_count.Content = GetActivePlayers().ToString();

                foreach (ServerTable server in ServerGrid.Items)
                {
                    var serverMetadata = GetServerMetadata(server.ID);
                    if (serverMetadata.ServerStatus == ServerStatus.Started && serverMetadata.Process != null && !serverMetadata.Process.HasExited)
                    {
                        try
                        {
                            var uptime = DateTime.Now - serverMetadata.Process.StartTime;
                            server.Uptime = $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
                        }
                        catch
                        {
                            server.Uptime = "N/A";
                        }
                    }
                    else
                    {
                        server.Uptime = string.Empty;
                    }
                }
                ServerGrid.Items.Refresh();

                CaptureServerResourceSamples();
                Refresh_DashboardResourceChart();
                Refresh_DashBoard_LiveChart();

                await Task.Delay(1000);
            }
        }

        public int GetStartedServerCount()
        {
            return ServerGrid.Items.Cast<ServerTable>().Where(s => s.Status == "Started").Count();
        }

        public int GetActivePlayers()
        {
            return ServerGrid.Items.Cast<ServerTable>().Where(s => s.Maxplayers != null && s.Maxplayers.Contains('/')).Sum(s => int.TryParse(s.Maxplayers.Split('/')[0], out int count) ? count : 0);
        }

        private void Refresh_DashBoard_LiveChart()
        {
            // List<(ServerType, PlayerCount)> Example: ("Ricochet Dedicated Server", 0)
            List<(string, int)> typePlayers = ServerGrid.Items.Cast<ServerTable>()
                .Where(s => s.Status == "Started" && s.Maxplayers != null && s.Maxplayers.Contains("/"))
                .Select(s => (type: s.Game, players: int.Parse(s.Maxplayers.Split('/')[0])))
                .GroupBy(s => s.type)
                .Select(s => (type: s.Key, players: s.Sum(p => p.players)))
                .ToList();

            livechart_players.Children.Clear();
            livechart_players.ColumnDefinitions.Clear();

            if (typePlayers.Count == 0) { return; }

            int maxValue = Math.Max(typePlayers.Select(s => s.Item2).Max() + 5, 10);
            for (int i = 0; i < typePlayers.Count; i++)
            {
                var item = typePlayers[i];
                livechart_players.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var column = new Grid { Margin = new Thickness(6, 0, 6, 0), ToolTip = $"{item.Item1}: {item.Item2}" };
                column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var barHost = new Grid { MinHeight = 130 };
                barHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Max(maxValue - item.Item2, 0), GridUnitType.Star) });
                barHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Max(item.Item2, 0.1), GridUnitType.Star) });

                var bar = new Border
                {
                    Background = Brushes.Goldenrod,
                    MinHeight = item.Item2 > 0 ? 2 : 0,
                    Margin = new Thickness(2, 0, 2, 0)
                };
                Grid.SetRow(bar, 1);
                barHost.Children.Add(bar);

                var label = new TextBlock
                {
                    Text = item.Item1,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxHeight = 36,
                    Margin = new Thickness(0, 6, 0, 0)
                };
                Grid.SetRow(label, 1);

                column.Children.Add(barHost);
                column.Children.Add(label);
                Grid.SetColumn(column, i);
                livechart_players.Children.Add(column);
            }
        }

        private void CaptureServerResourceSamples()
        {
            DateTime now = DateTime.Now;
            DateTime cutoff = now - DashboardResourceHistoryDuration;
            HashSet<string> visibleServerIds = new HashSet<string>();

            foreach (ServerTable server in ServerGrid.Items.Cast<ServerTable>())
            {
                visibleServerIds.Add(server.ID);
                var serverMetadata = GetServerMetadata(server.ID);

                if (serverMetadata?.ServerStatus != ServerStatus.Started || serverMetadata.Process == null)
                {
                    _lastProcessUsageSamples.Remove(server.ID);
                    PruneServerResourceSamples(server.ID, cutoff);
                    continue;
                }

                try
                {
                    Process process = serverMetadata.Process;
                    if (process.HasExited)
                    {
                        _lastProcessUsageSamples.Remove(server.ID);
                        PruneServerResourceSamples(server.ID, cutoff);
                        continue;
                    }

                    process.Refresh();
                    TimeSpan totalProcessorTime = process.TotalProcessorTime;
                    double cpuPercent = 0;

                    if (_lastProcessUsageSamples.TryGetValue(server.ID, out var lastSample))
                    {
                        double elapsedMs = (now - lastSample.Timestamp).TotalMilliseconds;
                        if (elapsedMs > 0)
                        {
                            double processorMs = (totalProcessorTime - lastSample.TotalProcessorTime).TotalMilliseconds;
                            cpuPercent = processorMs / elapsedMs / Environment.ProcessorCount * 100.0;
                            cpuPercent = Math.Max(0, Math.Min(100, cpuPercent));
                        }
                    }

                    _lastProcessUsageSamples[server.ID] = new ProcessUsageSample
                    {
                        TotalProcessorTime = totalProcessorTime,
                        Timestamp = now
                    };

                    if (!_serverResourceSamples.TryGetValue(server.ID, out var samples))
                    {
                        samples = new List<ServerResourceSample>();
                        _serverResourceSamples[server.ID] = samples;
                    }

                    samples.Add(new ServerResourceSample
                    {
                        Timestamp = now,
                        CpuPercent = cpuPercent,
                        MemoryMb = process.WorkingSet64 / 1024.0 / 1024.0
                    });

                    samples.RemoveAll(sample => sample.Timestamp < cutoff);
                }
                catch
                {
                    _lastProcessUsageSamples.Remove(server.ID);
                    PruneServerResourceSamples(server.ID, cutoff);
                }
            }

            foreach (string serverId in _serverResourceSamples.Keys.Except(visibleServerIds).ToList())
            {
                PruneServerResourceSamples(serverId, cutoff);
            }
        }

        private void PruneServerResourceSamples(string serverId, DateTime cutoff)
        {
            if (!_serverResourceSamples.TryGetValue(serverId, out var samples))
            {
                return;
            }

            samples.RemoveAll(sample => sample.Timestamp < cutoff);
            if (samples.Count == 0)
            {
                _serverResourceSamples.Remove(serverId);
            }
        }

        private void Refresh_DashboardResourceChart()
        {
            if (dashboard_resource_chart.ActualWidth <= 0 || dashboard_resource_chart.ActualHeight <= 0)
            {
                return;
            }

            dashboard_resource_chart.Children.Clear();
            dashboard_resource_legend.Children.Clear();
            UpdateDashboardResourceButtons();

            DateTime now = DateTime.Now;
            DateTime cutoff = now - DashboardResourceHistoryDuration;
            var servers = ServerGrid.Items.Cast<ServerTable>()
                .Where(server => _serverResourceSamples.TryGetValue(server.ID, out var samples) && samples.Any(sample => sample.Timestamp >= cutoff))
                .OrderBy(server => int.TryParse(server.ID, out int id) ? id : int.MaxValue)
                .ToList();

            dashboard_resource_empty.Visibility = servers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (servers.Count == 0)
            {
                return;
            }

            double width = dashboard_resource_chart.ActualWidth;
            double height = dashboard_resource_chart.ActualHeight;
            double left = 46;
            double top = 18;
            double right = 18;
            double bottom = 28;
            double plotWidth = Math.Max(1, width - left - right);
            double plotHeight = Math.Max(1, height - top - bottom);

            double maxValue = servers
                .SelectMany(server => _serverResourceSamples[server.ID].Where(sample => sample.Timestamp >= cutoff))
                .Select(GetDashboardResourceSampleValue)
                .DefaultIfEmpty(0)
                .Max();
            maxValue = GetDashboardResourceAxisMaximum(maxValue);

            DrawDashboardResourceGrid(left, top, plotWidth, plotHeight, maxValue);

            for (int index = 0; index < servers.Count; index++)
            {
                ServerTable server = servers[index];
                Brush brush = GetDashboardResourceBrush(index);
                var points = _serverResourceSamples[server.ID]
                    .Where(sample => sample.Timestamp >= cutoff)
                    .OrderBy(sample => sample.Timestamp)
                    .Select(sample =>
                    {
                        double x = left + ((sample.Timestamp - cutoff).TotalSeconds / DashboardResourceHistoryDuration.TotalSeconds) * plotWidth;
                        double y = top + plotHeight - (GetDashboardResourceSampleValue(sample) / maxValue) * plotHeight;
                        return new Point(x, y);
                    })
                    .ToList();

                if (points.Count == 1)
                {
                    points.Add(new Point(points[0].X + 1, points[0].Y));
                }

                var line = new System.Windows.Shapes.Polyline
                {
                    Stroke = brush,
                    StrokeThickness = 2,
                    Points = new PointCollection(points),
                    ToolTip = $"{GetDashboardServerLabel(server)} {GetDashboardResourceMetricLabel()}"
                };
                dashboard_resource_chart.Children.Add(line);

                AddDashboardResourceLegendItem(server, brush);
            }
        }

        private void DrawDashboardResourceGrid(double left, double top, double plotWidth, double plotHeight, double maxValue)
        {
            for (int i = 0; i <= 4; i++)
            {
                double y = top + plotHeight - plotHeight * i / 4.0;
                var line = new System.Windows.Shapes.Line
                {
                    X1 = left,
                    X2 = left + plotWidth,
                    Y1 = y,
                    Y2 = y,
                    Stroke = Brushes.Gray,
                    StrokeThickness = i == 0 ? 1.2 : 0.5,
                    Opacity = i == 0 ? 0.7 : 0.35
                };
                dashboard_resource_chart.Children.Add(line);

                var label = new TextBlock
                {
                    Text = FormatDashboardResourceValue(maxValue * i / 4.0),
                    Foreground = Brushes.Gray,
                    FontSize = 11,
                    Width = left - 8,
                    TextAlignment = TextAlignment.Right
                };
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, y - 8);
                dashboard_resource_chart.Children.Add(label);
            }

            AddDashboardResourceTimeLabel("60m ago", left, top + plotHeight + 6, TextAlignment.Left);
            AddDashboardResourceTimeLabel("now", left + plotWidth - 50, top + plotHeight + 6, TextAlignment.Right);
        }

        private void AddDashboardResourceTimeLabel(string text, double x, double y, TextAlignment alignment)
        {
            var label = new TextBlock
            {
                Text = text,
                Foreground = Brushes.Gray,
                FontSize = 11,
                Width = 50,
                TextAlignment = alignment
            };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);
            dashboard_resource_chart.Children.Add(label);
        }

        private void AddDashboardResourceLegendItem(ServerTable server, Brush brush)
        {
            var item = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6),
                ToolTip = GetDashboardServerLabel(server)
            };

            item.Children.Add(new Border
            {
                Background = brush,
                Width = 12,
                Height = 12,
                Margin = new Thickness(0, 3, 6, 0)
            });

            item.Children.Add(new TextBlock
            {
                Text = GetDashboardServerLabel(server),
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 36
            });

            dashboard_resource_legend.Children.Add(item);
        }

        private double GetDashboardResourceSampleValue(ServerResourceSample sample)
        {
            return _dashboardResourceMetric == DashboardResourceMetric.CPU ? sample.CpuPercent : sample.MemoryMb;
        }

        private double GetDashboardResourceAxisMaximum(double maxValue)
        {
            if (_dashboardResourceMetric == DashboardResourceMetric.CPU)
            {
                return Math.Max(10, Math.Min(100, Math.Ceiling(maxValue / 10.0) * 10.0));
            }

            if (maxValue <= 0)
            {
                return 128;
            }

            double step = maxValue < 1024 ? 128 : 512;
            return Math.Ceiling(maxValue / step) * step;
        }

        private string FormatDashboardResourceValue(double value)
        {
            if (_dashboardResourceMetric == DashboardResourceMetric.CPU)
            {
                return $"{value:0}%";
            }

            return value >= 1024 ? $"{value / 1024.0:0.0} GB" : $"{value:0} MB";
        }

        private string GetDashboardResourceMetricLabel()
        {
            return _dashboardResourceMetric == DashboardResourceMetric.CPU ? "CPU" : "Memory";
        }

        private Brush GetDashboardResourceBrush(int index)
        {
            return _dashboardResourceBrushes[index % _dashboardResourceBrushes.Length];
        }

        private string GetDashboardServerLabel(ServerTable server)
        {
            string name = string.IsNullOrWhiteSpace(server.Name) ? server.Game : server.Name;
            return $"{server.ID} - {name}";
        }

        private void UpdateDashboardResourceButtons()
        {
            bool isCpu = _dashboardResourceMetric == DashboardResourceMetric.CPU;
            button_DashboardResourceCpu.FontWeight = isCpu ? FontWeights.SemiBold : FontWeights.Normal;
            button_DashboardResourceMemory.FontWeight = isCpu ? FontWeights.Normal : FontWeights.SemiBold;
            button_DashboardResourceCpu.Background = isCpu ? Brushes.RoyalBlue : Brushes.Transparent;
            button_DashboardResourceMemory.Background = isCpu ? Brushes.Transparent : Brushes.Purple;
            button_DashboardResourceCpu.Foreground = isCpu ? Brushes.White : Foreground;
            button_DashboardResourceMemory.Foreground = isCpu ? Foreground : Brushes.White;
        }

        private void Button_DashboardResourceCpu_Click(object sender, RoutedEventArgs e)
        {
            _dashboardResourceMetric = DashboardResourceMetric.CPU;
            Refresh_DashboardResourceChart();
        }

        private void Button_DashboardResourceMemory_Click(object sender, RoutedEventArgs e)
        {
            _dashboardResourceMetric = DashboardResourceMetric.Memory;
            Refresh_DashboardResourceChart();
        }

        private void DashboardResourceChart_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Refresh_DashboardResourceChart();
        }

        private async void SendGoogleAnalytics()
        {
            try
            {
                var analytics = new GoogleAnalytics();
                analytics.SendWindowsOS();
                analytics.SendWindowsGSMVersion();
                analytics.SendProcessorName();
                analytics.SendRAM();
                analytics.SendDisk();
            }
            catch
            {
                // i basically just don't care when google analytics fail
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save height and width
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue("Height", Height.ToString());
                key?.SetValue("Width", Width.ToString());
            }

            // Get rid of system tray icon
            notifyIcon.Visible = false;
            notifyIcon.Dispose();

            // Stop Discord Bot
            g_DiscordBot.Stop().ConfigureAwait(false);
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServerGrid.SelectedIndex != -1)
            {
                DataGrid_RefreshElements();
            }
        }

        private void DataGrid_RefreshElements()
        {
            var row = (ServerTable)ServerGrid.SelectedItem;

            if (row != null)
            {
#if DEBUG
                Console.WriteLine("Datagrid Changed");
#endif
                if (GetServerMetadata(row.ID).ServerStatus == ServerStatus.Stopped)
                {
                    button_Start.IsEnabled = true;
                    button_Stop.IsEnabled = false;
                    button_Restart.IsEnabled = false;
                    button_Console.IsEnabled = false;
                    button_Update.IsEnabled = true;
                    button_Backup.IsEnabled = true;

                    textbox_servercommand.IsEnabled = false;
                    button_servercommand.IsEnabled = false;
                }
                else if (GetServerMetadata(row.ID).ServerStatus == ServerStatus.Started)
                {
                    button_Start.IsEnabled = false;
                    button_Stop.IsEnabled = true;
                    button_Restart.IsEnabled = true;
                    Process p = GetServerMetadata(row.ID).Process;
                    try
                    {
                        button_Console.IsEnabled = (p == null || p.HasExited) ? false : !(p.StartInfo.CreateNoWindow || p.StartInfo.RedirectStandardOutput);
                    }
                    catch (Exception)
                    {
                        button_Console.IsEnabled = false;
                    }
                    button_Update.IsEnabled = false;
                    button_Backup.IsEnabled = false;

                    textbox_servercommand.IsEnabled = true;
                    button_servercommand.IsEnabled = true;
                }
                else
                {
                    button_Start.IsEnabled = false;
                    button_Stop.IsEnabled = false;
                    button_Restart.IsEnabled = false;
                    button_Console.IsEnabled = false;
                    button_Update.IsEnabled = false;
                    button_Backup.IsEnabled = false;

                    textbox_servercommand.IsEnabled = false;
                    button_servercommand.IsEnabled = false;
                }

                switch (GetServerMetadata(row.ID).ServerStatus)
                {
                    case ServerStatus.Restarting:
                    case ServerStatus.Restarted:
                    case ServerStatus.Started:
                    case ServerStatus.Starting:
                    case ServerStatus.Stopping:
                        button_Kill.IsEnabled = true;
                        break;
                    default: button_Kill.IsEnabled = false; break;
                }

                button_ManageAddons.IsEnabled = ServerAddon.IsGameSupportManageAddons(row.Game);
                if (GetServerMetadata(row.ID).ServerStatus == ServerStatus.Deleting || GetServerMetadata(row.ID).ServerStatus == ServerStatus.Restoring)
                {
                    button_ManageAddons.IsEnabled = false;
                }

                slider_ProcessPriority.Value = Functions.CPU.Priority.GetPriorityInteger(GetServerMetadata(row.ID).CPUPriority);
                textBox_ProcessPriority.Text = Functions.CPU.Priority.GetPriorityByInteger((int)slider_ProcessPriority.Value);

                textBox_SetAffinity.Text = Functions.CPU.Affinity.GetAffinityValidatedString(GetServerMetadata(row.ID).CPUAffinity);
                string affinity = new string(textBox_SetAffinity.Text.Reverse().ToArray());
                for (int i = 0; i < _checkBoxes.Count; i++)
                {
                    _checkBoxes[i].IsChecked = affinity[i] == '1';
                }

                button_Status.Content = row.Status.ToUpper();
                button_Status.Background = (GetServerMetadata(row.ID).ServerStatus == ServerStatus.Started) ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Orange;

                var gameServer = GameServer.Data.Class.Get(row.Game, pluginList: PluginsList);
                switch_embedconsole.IsEnabled = gameServer.AllowsEmbedConsole;
                switch_embedconsole.IsOn = gameServer.AllowsEmbedConsole ? GetServerMetadata(row.ID).EmbedConsole : false;
                Button_AutoScroll.Content = GetServerMetadata(row.ID).AutoScroll ? "✔️ AUTO SCROLL" : "❌ AUTO SCROLL";

                switch_autorestart.IsOn = GetServerMetadata(row.ID).AutoRestart;
                switch_restartcrontab.IsOn = GetServerMetadata(row.ID).RestartCrontab;
                switch_autostart.IsOn = GetServerMetadata(row.ID).AutoStart;
                switch_autoupdate.IsOn = GetServerMetadata(row.ID).AutoUpdate;
                switch_updateonstart.IsOn = GetServerMetadata(row.ID).UpdateOnStart;
                switch_backuponstart.IsOn = GetServerMetadata(row.ID).BackupOnStart;
                switch_discordalert.IsOn = GetServerMetadata(row.ID).DiscordAlert;
                button_discordtest.IsEnabled = switch_discordalert.IsOn;

                textBox_restartcrontab.Text = GetServerMetadata(row.ID).CrontabFormat;
                textBox_nextcrontab.Text = CrontabSchedule.TryParse(textBox_restartcrontab.Text)?.GetNextOccurrence(DateTime.Now).ToString("ddd, MM/dd/yyyy HH:mm:ss");

                MahAppSwitch_AutoStartAlert.IsOn = GetServerMetadata(row.ID).AutoStartAlert;
                MahAppSwitch_AutoRestartAlert.IsOn = GetServerMetadata(row.ID).AutoRestartAlert;
                MahAppSwitch_AutoUpdateAlert.IsOn = GetServerMetadata(row.ID).AutoUpdateAlert;
                MahAppSwitch_RestartCrontabAlert.IsOn = GetServerMetadata(row.ID).RestartCrontabAlert;
                MahAppSwitch_CrashAlert.IsOn = GetServerMetadata(row.ID).CrashAlert;
            }
        }

        private void Install_Click(object sender, RoutedEventArgs e)
        {
            if (ServerGrid.Items.Count >= MAX_SERVER)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            MahAppFlyout_InstallGameServer.IsOpen = true;

            if (!progressbar_InstallProgress.IsIndeterminate)
            {
                textbox_InstallServerName.IsEnabled = true;
                comboBox_InstallGameServer.IsEnabled = true;
                stackPanel_InstallSteamBranch.IsEnabled = true;
                progressbar_InstallProgress.IsIndeterminate = false;
                textblock_InstallProgress.Text = string.Empty;
                button_Install.IsEnabled = true;

                ComboBox_InstallGameServer_SelectionChanged(sender, null);

                var newServerConfig = new ServerConfig(null);
                textbox_InstallServerName.Text = $"WindowsGSM - Server #{newServerConfig.ServerID}";
            }
        }

        private async void Button_Install_Click(object sender, RoutedEventArgs e)
        {
            if (Installer != null)
            {
                if (!Installer.HasExited) { Installer.Kill(); }
                Installer = null;
            }

            var selectedgame = (Images.Row)comboBox_InstallGameServer.SelectedItem;
            if (string.IsNullOrWhiteSpace(textbox_InstallServerName.Text) || selectedgame == null) { return; }

            var newServerConfig = new ServerConfig(null);
            string installPath = ServerPath.GetServersServerFiles(newServerConfig.ServerID);

            if (Directory.Exists(installPath))
            {
                Log(newServerConfig.ServerID, "[ERROR] Server folder already exists, but does not seem to be loaded in WGSM. " +
                    "You either have messed up the Windowsgsm.cfg, or changed the plugin of the server." +
                    " Please Back it up manually and maybe remove it and reinstall the server.");
                for (int i = int.Parse(newServerConfig.ServerID); i < 100; i++)
                {
                    newServerConfig = new ServerConfig(i.ToString());
                    installPath = ServerPath.GetServersServerFiles(newServerConfig.ServerID);
                    if (!Directory.Exists(installPath))
                    {
                        break;
                    }
                }

                Log(newServerConfig.ServerID, "[Notice] Found a free ServerID");
            }

            Directory.CreateDirectory(installPath);

            //Installation start
            textbox_InstallServerName.IsEnabled = false;
            comboBox_InstallGameServer.IsEnabled = false;
            stackPanel_InstallSteamBranch.IsEnabled = false;
            progressbar_InstallProgress.IsIndeterminate = true;
            textblock_InstallProgress.Text = "Installing";
            button_Install.IsEnabled = false;
            textbox_InstallLog.Text = string.Empty;

            string servername = textbox_InstallServerName.Text;
            string servergame = selectedgame.Name;

            newServerConfig.CreateServerDirectory();

            dynamic gameServer = GameServer.Data.Class.Get(servergame, newServerConfig, PluginsList);
            string steamBranch = GetInstallSteamBranch();
            string steamBranchPassword = textbox_InstallSteamBranchPassword.Text.Trim();
            WindowsGSM.Installer.SteamCMD.SetPendingSteamBranch(newServerConfig.ServerID, steamBranch, steamBranchPassword);
            Installer = await gameServer.Install();

            if (Installer != null)
            {
                string installOutput = await WaitForInstallerOutput(Installer);

                if (!gameServer.IsInstallValid() && installOutput.Contains("Missing configuration", StringComparison.OrdinalIgnoreCase))
                {
                    AppendInstallLogLine("SteamCMD was still completing first-run configuration. Retrying install once...");
                    Log(newServerConfig.ServerID, "[NOTICE] SteamCMD missing configuration on first install attempt; retrying once.");

                    Installer = await gameServer.Install();
                    if (Installer != null)
                    {
                        await WaitForInstallerOutput(Installer);
                    }
                }
            }

            if (gameServer.IsInstallValid())
            {
                newServerConfig.ServerIP = newServerConfig.GetIPAddress();
                newServerConfig.ServerPort = newServerConfig.GetAvailablePort(gameServer.Port, gameServer.PortIncrements);

                // Create WindowsGSM.cfg
                newServerConfig.SetData(servergame, servername, gameServer);
                newServerConfig.SteamBranch = steamBranch;
                newServerConfig.SteamBranchPassword = steamBranchPassword;
                newServerConfig.SteamBranchLastInstalled = steamBranch;
                newServerConfig.CreateWindowsGSMConfig();
                WindowsGSM.Installer.SteamCMD.SetPendingSteamBranch(newServerConfig.ServerID, string.Empty, string.Empty);

                // Create WindowsGSM.cfg and game server config
                try
                {
                    gameServer = GameServer.Data.Class.Get(servergame, newServerConfig, PluginsList);
                    gameServer.CreateServerCFG();
                }
                catch
                {
                    // ignore
                }

                LoadServerTable();
                Log(newServerConfig.ServerID, "Install: Success");

                MahAppFlyout_InstallGameServer.IsOpen = false;
                textbox_InstallServerName.IsEnabled = true;
                comboBox_InstallGameServer.IsEnabled = true;
                stackPanel_InstallSteamBranch.IsEnabled = true;
                progressbar_InstallProgress.IsIndeterminate = false;

                if (MahAppSwitch_SendStatistics.IsOn)
                {
                    try
                    {
                        var analytics = new GoogleAnalytics();
                        analytics.SendGameServerInstall(newServerConfig.ServerID, servergame);
                    }
                    catch
                    {
                        // i basically just don't care when google analytics fail
                    }
                }
            }
            else
            {
                textbox_InstallServerName.IsEnabled = true;
                comboBox_InstallGameServer.IsEnabled = true;
                stackPanel_InstallSteamBranch.IsEnabled = true;
                progressbar_InstallProgress.IsIndeterminate = false;
                textblock_InstallProgress.Text = "Install";
                button_Install.IsEnabled = true;
                WindowsGSM.Installer.SteamCMD.SetPendingSteamBranch(newServerConfig.ServerID, string.Empty, string.Empty);

                if (Installer != null && Installer.ExitCode != 0)
                {
                    textblock_InstallProgress.Text = "Fail to install [ERROR] Exit code: " + Installer.ExitCode;
                }
                else
                {
                    string installError = string.IsNullOrWhiteSpace(gameServer.Error)
                        ? "SteamCMD finished, but WindowsGSM could not validate the installed server files."
                        : gameServer.Error;
                    textblock_InstallProgress.Text = "Fail to install [ERROR] Validation failed. See Install Log.";
                    AppendInstallLogLine($"Install validation failed: {installError}");
                    WriteInstallFailureReport(newServerConfig, servergame, servername, steamBranch, installError, Installer?.ExitCode);
                }
            }
        }

        private void WriteInstallFailureReport(ServerConfig serverConfig, string serverGame, string serverName, string steamBranch, string error, int? exitCode)
        {
            try
            {
                Directory.CreateDirectory(ServerPath.GetServersConfigs(serverConfig.ServerID));
                var report = new StringBuilder();
                report.AppendLine("WindowsGSM install did not complete.");
                report.AppendLine($"Server ID: {serverConfig.ServerID}");
                report.AppendLine($"Server Game: {serverGame}");
                report.AppendLine($"Server Name: {serverName}");
                report.AppendLine($"Steam Branch: {(string.IsNullOrWhiteSpace(steamBranch) ? "public" : steamBranch)}");
                report.AppendLine($"SteamCMD Exit Code: {(exitCode.HasValue ? exitCode.Value.ToString() : "not available")}");
                report.AppendLine($"Reason: {error}");
                report.AppendLine();
                report.AppendLine("SteamCMD may have downloaded files successfully, but WindowsGSM did not create WindowsGSM.cfg because the plugin's install validation failed.");
                report.AppendLine("Check that the selected Steam branch contains the expected dedicated server executable for this plugin.");

                File.WriteAllText(ServerPath.GetServersConfigs(serverConfig.ServerID, "InstallFailure.txt"), report.ToString());
            }
            catch (Exception ex)
            {
                AppendInstallLogLine($"Could not write install failure report: {ex.Message}");
            }
        }

        private void ComboBox_InstallGameServer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Set the elements visibility of Install Server Flyout
            var selectedgame = (Images.Row)comboBox_InstallGameServer.SelectedItem;
            button_InstallSetAccount.IsEnabled = false;
            textBox_InstallToken.Visibility = Visibility.Hidden;
            button_InstallSendToken.Visibility = Visibility.Hidden;
            comboBox_InstallSteamBranch.Items.Clear();
            comboBox_InstallSteamBranch.Text = string.Empty;
            PopulateSteamBranchComboBox(comboBox_InstallSteamBranch, string.Empty);
            textbox_InstallSteamBranchPassword.Text = string.Empty;
            stackPanel_InstallSteamBranch.Visibility = Visibility.Collapsed;
            if (selectedgame == null) { return; }

            try
            {
                dynamic gameServer = GameServer.Data.Class.Get(selectedgame.Name, pluginList: PluginsList);
                stackPanel_InstallSteamBranch.Visibility = string.IsNullOrWhiteSpace(GetSteamAppId(gameServer)) ? Visibility.Collapsed : Visibility.Visible;
                if (!gameServer.loginAnonymous)
                {
                    button_InstallSetAccount.IsEnabled = true;
                    textBox_InstallToken.Visibility = Visibility.Visible;
                    button_InstallSendToken.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                // ignore
            }
        }

        private async void Button_InstallRefreshSteamBranches_Click(object sender, RoutedEventArgs e)
        {
            var selectedgame = (Images.Row)comboBox_InstallGameServer.SelectedItem;
            if (selectedgame == null) { return; }

            try
            {
                dynamic gameServer = GameServer.Data.Class.Get(selectedgame.Name, pluginList: PluginsList);
                string appId = GetSteamAppId(gameServer);
                if (string.IsNullOrWhiteSpace(appId)) { return; }

                button_InstallRefreshSteamBranches.IsEnabled = false;
                string previousBranch = GetInstallSteamBranch();
                AppendInstallLogLine($"Refreshing Steam branches for app {appId}...");

                var steamCMD = new Installer.SteamCMD();
                bool loginAnonymous = GetSteamLoginAnonymous(gameServer);
                List<string> branches = await steamCMD.GetBranches(appId, loginAnonymous);

                PopulateSteamBranchComboBox(comboBox_InstallSteamBranch, previousBranch, branches);
                if (branches.Count > 0)
                {
                    AppendInstallLogLine($"Found Steam branches: {string.Join(", ", branches)}");
                }
                else
                {
                    AppendInstallLogLine($"Could not read Steam branches. {steamCMD.Error} Enter a branch manually if needed.");
                }
            }
            catch (Exception ex)
            {
                AppendInstallLogLine($"Could not refresh Steam branches. {ex.Message}");
            }
            finally
            {
                button_InstallRefreshSteamBranches.IsEnabled = true;
            }
        }

        private async void Button_EC_RefreshSteamBranches_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            try
            {
                var serverConfig = new ServerConfig(server.ID);
                dynamic gameServer = GameServer.Data.Class.Get(serverConfig.ServerGame, pluginList: PluginsList);
                string appId = GetSteamAppId(gameServer);
                if (string.IsNullOrWhiteSpace(appId)) { return; }

                button_EC_RefreshSteamBranches.IsEnabled = false;
                string previousBranch = GetComboBoxSteamBranch(comboBox_EC_SteamBranch);

                var steamCMD = new Installer.SteamCMD();
                bool loginAnonymous = GetSteamLoginAnonymous(gameServer);
                List<string> branches = await steamCMD.GetBranches(appId, loginAnonymous);

                PopulateSteamBranchComboBox(comboBox_EC_SteamBranch, previousBranch, branches);
                if (branches.Count == 0)
                {
                    await this.ShowMessageAsync("Steam Branches", $"Could not read Steam branches. {steamCMD.Error}\n\nYou can still enter a branch manually.");
                }
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Steam Branches", $"Could not refresh Steam branches.\n\n{ex.Message}");
            }
            finally
            {
                button_EC_RefreshSteamBranches.IsEnabled = true;
            }
        }

        private string GetInstallSteamBranch()
        {
            return GetComboBoxSteamBranch(comboBox_InstallSteamBranch);
        }

        private static string GetComboBoxSteamBranch(System.Windows.Controls.ComboBox comboBox)
        {
            string branch = comboBox.Text?.Trim() ?? string.Empty;
            return string.Equals(branch, "public", StringComparison.OrdinalIgnoreCase) ? string.Empty : branch;
        }

        private static void PopulateSteamBranchComboBox(System.Windows.Controls.ComboBox comboBox, string selectedBranch, IEnumerable<string> branches = null)
        {
            string selected = string.IsNullOrWhiteSpace(selectedBranch) ? "public" : selectedBranch.Trim();
            comboBox.Items.Clear();
            comboBox.Items.Add("public");

            foreach (string branch in branches ?? Enumerable.Empty<string>())
            {
                if (!comboBox.Items.Cast<object>().Any(item => string.Equals(item.ToString(), branch, StringComparison.OrdinalIgnoreCase)))
                {
                    comboBox.Items.Add(branch);
                }
            }

            comboBox.Text = selected;
        }

        private static string GetSteamAppId(dynamic gameServer)
        {
            return GetMemberValue(gameServer, "AppId")?.ToString() ?? string.Empty;
        }

        private static bool GetSteamLoginAnonymous(dynamic gameServer)
        {
            object value = GetMemberValue(gameServer, "loginAnonymous");
            return value == null || bool.TryParse(value.ToString(), out bool loginAnonymous) && loginAnonymous;
        }

        private void Button_SetAccount_Click(object sender, RoutedEventArgs e)
        {
            var steamCMD = new Installer.SteamCMD();
            steamCMD.CreateUserDataTxtIfNotExist();

            string userDataPath = ServerPath.GetBin("steamcmd", "userData.txt");
            if (File.Exists(userDataPath))
            {
                Process.Start("notepad", userDataPath);
            }
        }

        private void Button_SendToken_Click(object sender, RoutedEventArgs e)
        {
            if (Installer != null)
            {
                Installer.StandardInput.WriteLine(textBox_InstallToken.Text);
            }

            textBox_InstallToken.Text = string.Empty;
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            if (ServerGrid.Items.Count >= MAX_SERVER)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            MahAppFlyout_ImportGameServer.IsOpen = true;

            if (!progressbar_ImportProgress.IsIndeterminate)
            {
                textbox_ImportServerName.IsEnabled = true;
                comboBox_ImportGameServer.IsEnabled = true;
                progressbar_ImportProgress.IsIndeterminate = false;
                textblock_ImportProgress.Text = string.Empty;
                button_Import.Content = "Import";

                var newServerConfig = new ServerConfig(null);
                textbox_ImportServerName.Text = $"WindowsGSM - Server #{newServerConfig.ServerID}";
            }
        }

        private async void Button_Import_Click(object sender, RoutedEventArgs e)
        {
            var selectedgame = (Images.Row)comboBox_ImportGameServer.SelectedItem;
            label_ServerDirWarn.Content = Directory.Exists(textbox_ServerDir.Text) ? string.Empty : "Server Dir is invalid";
            if (string.IsNullOrWhiteSpace(textbox_ImportServerName.Text) || selectedgame == null) { return; }

            string servername = textbox_ImportServerName.Text;
            string servergame = selectedgame.Name;

            var newServerConfig = new ServerConfig(null);
            dynamic gameServer = GameServer.Data.Class.Get(servergame, newServerConfig, PluginsList);

            if (!gameServer.IsImportValid(textbox_ServerDir.Text))
            {
                label_ServerDirWarn.Content = gameServer.Error;
                return;
            }

            string importPath = ServerPath.GetServersServerFiles(newServerConfig.ServerID);
            if (Directory.Exists(importPath))
            {
                try
                {
                    Directory.Delete(importPath, true);
                }
                catch
                {
                    System.Windows.Forms.MessageBox.Show(importPath + " is not accessible!", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            //Import start
            textbox_ImportServerName.IsEnabled = false;
            comboBox_ImportGameServer.IsEnabled = false;
            textbox_ServerDir.IsEnabled = false;
            button_Browse.IsEnabled = false;
            progressbar_ImportProgress.IsIndeterminate = true;
            textblock_ImportProgress.Text = "Importing";

            string sourcePath = textbox_ServerDir.Text;
            string importLog = await Task.Run(() =>
            {
                try
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(sourcePath, importPath);

                    // Scary error while moving the directory, some files may lost - Risky
                    //Microsoft.VisualBasic.FileIO.FileSystem.MoveDirectory(sourcePath, importPath);

                    // This doesn't work on cross drive - Not working on cross drive
                    //Directory.Move(sourcePath, importPath);

                    return null;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            });

            if (importLog != null)
            {
                textbox_ImportServerName.IsEnabled = true;
                comboBox_ImportGameServer.IsEnabled = true;
                textbox_ServerDir.IsEnabled = true;
                button_Browse.IsEnabled = true;
                progressbar_ImportProgress.IsIndeterminate = false;
                textblock_ImportProgress.Text = "[ERROR] Fail to import";
                MessageBox.Show($"Fail to copy the directory.\n{textbox_ServerDir.Text}\nto\n{importPath}\n\nYou may install a new server and copy the old servers file to the new server.\n\nException: {importLog}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Create WindowsGSM.cfg
            newServerConfig.SetData(servergame, servername, gameServer);
            newServerConfig.CreateWindowsGSMConfig();

            LoadServerTable();
            Log(newServerConfig.ServerID, "Import: Success");

            MahAppFlyout_ImportGameServer.IsOpen = false;
            textbox_ImportServerName.IsEnabled = true;
            comboBox_ImportGameServer.IsEnabled = true;
            textbox_ServerDir.IsEnabled = true;
            button_Browse.IsEnabled = true;
            progressbar_ImportProgress.IsIndeterminate = false;
            textblock_ImportProgress.Text = string.Empty;
        }

        private void Button_Browse_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.ShowDialog();

            if (!string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
            {
                textbox_ServerDir.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped) { return; }

            MessageBoxResult result = MessageBox.Show("Do you want to delete this server?\n(There is no comeback)", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) { return; }

            await GameServer_Delete(server);
        }

        private async void Button_DiscordEdit_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string webhookUrl = ServerConfig.GetSetting(server.ID, Functions.ServerConfig.SettingName.DiscordWebhook);

            var settings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Save",
                DefaultText = webhookUrl
            };

            webhookUrl = await this.ShowInputAsync("Discord Webhook URL", "Please enter the discord webhook url.", settings);
            if (webhookUrl == null) { return; } //If pressed cancel

            _serverMetadata[int.Parse(server.ID)].DiscordWebhook = webhookUrl;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.DiscordWebhook, webhookUrl);
        }

        private async void Button_DiscordSetMessage_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            var message = ServerConfig.GetSetting(server.ID, ServerConfig.SettingName.DiscordMessage);

            var settings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Save",
                DefaultText = message
            };

            message = await this.ShowInputAsync("Discord Custom Message", "Please enter the custom message.\n\nExample ping message <@discorduserid>:\n<@348921660361146380>", settings);
            if (message == null) { return; } //If pressed cancel

            _serverMetadata[int.Parse(server.ID)].DiscordMessage = message;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.DiscordMessage, message);
        }

        private async void Button_DiscordWebhookTest_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            int serverId = int.Parse(server.ID);
            if (!GetServerMetadata(serverId).DiscordAlert) { return; }

            var webhook = new DiscordWebhook(GetServerMetadata(serverId).DiscordWebhook, GetServerMetadata(serverId).DiscordMessage, g_DonorType, GetServerMetadata(serverId).SkipUserSetup);
            await webhook.Send(server.ID, server.Game, "Webhook Test Alert", server.Name, server.IP, server.Port);
        }

        private void Button_ServerCommand_Click(object sender, RoutedEventArgs e)
        {
            string command = textbox_servercommand.Text;
            textbox_servercommand.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(command)) { return; }

            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            _ = SendCommandAsync(server, command);
        }

        private void Textbox_ServerCommand_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (textbox_servercommand.Text.Length != 0)
                {
                    GetServerMetadata(0).ServerConsole.Add(textbox_servercommand.Text);
                }

                Button_ServerCommand_Click(this, new RoutedEventArgs());
            }
        }

        private void Textbox_ServerCommand_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.IsDown && e.Key == Key.Up)
            {
                e.Handled = true;
                textbox_servercommand.Text = GetServerMetadata(0).ServerConsole.GetPreviousCommand();
            }
            else if (e.IsDown && e.Key == Key.Down)
            {
                e.Handled = true;
                textbox_servercommand.Text = GetServerMetadata(0).ServerConsole.GetNextCommand();
            }
        }

        #region Actions - Button Click
        private void Actions_Crash_Click(object sender, RoutedEventArgs e)
        {
            int test = 0;
            _ = 0 / test; // Crash
        }

        private async void Actions_Start_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            // Reload WindowsGSM.cfg on start
            SaveServerConfigToServerMetadata(server.ID, new ServerConfig(server.ID));

            await GameServer_Start(server);
        }

        private async void Actions_Stop_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            await GameServer_Stop(server);
        }

        private async void Actions_Restart_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            await GameServer_Restart(server);
        }

        private async void Actions_Kill_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            switch (GetServerMetadata(server.ID).ServerStatus)
            {
                case ServerStatus.Restarting:
                case ServerStatus.Restarted:
                case ServerStatus.Started:
                case ServerStatus.Starting:
                case ServerStatus.Stopping:
                    Process p = GetServerMetadata(server.ID).Process;
                    if (p != null && !p.HasExited)
                    {
                        Log(server.ID, "Actions: Kill");
                        p.Kill();

                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                        Log(server.ID, "Server: Killed");
                        SetServerStatus(server, "Stopped");
                        _serverMetadata[int.Parse(server.ID)].ServerConsole.Clear();
                        _serverMetadata[int.Parse(server.ID)].Process = null;
                    }

                    break;
            }
        }

        private void Actions_ToggleConsole_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            Process p = GetServerMetadata(server.ID).Process;
            if (p == null) { return; }

            //If console is useless, return
            //if (p.StartInfo.RedirectStandardOutput) { return; }
            _serverMetadata[int.Parse(server.ID)].ShowConsole = !_serverMetadata[int.Parse(server.ID)].ShowConsole;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ShowConsole, GetServerMetadata(server.ID).ShowConsole ? "1" : "0");

            WindowShowStyle style = _serverMetadata[int.Parse(server.ID)].ShowConsole ? WindowShowStyle.ShowNormal : WindowShowStyle.Hide;
            IntPtr hWnd = GetServerMetadata(server.ID).MainWindow;

            //ShowWindow(hWnd, ShowWindow(hWnd, WindowShowStyle.Hide) ? WindowShowStyle.Hide : WindowShowStyle.ShowNormal);
            ShowWindow(hWnd, WindowShowStyle.Hide);
            Thread.Sleep(500);
            ShowWindow(hWnd, style);
        }

        private async void Actions_StartAllServers_Click(object sender, RoutedEventArgs e)
        {
            foreach (var server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Stopped)
                {
                    await GameServer_Start(server);
                }
            }
        }

        private async void Actions_StartServersWithAutoStartEnabled_Click(object sender, RoutedEventArgs e)
        {
            foreach (var server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Stopped && GetServerMetadata(server.ID).AutoStart)
                {
                    await GameServer_Start(server);
                }
            }
        }

        private async void Actions_StopAllServers_Click(object sender, RoutedEventArgs e)
        {
            foreach (var server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started)
                {
                    await GameServer_Stop(server);
                }
            }
        }

        private async void Actions_RestartAllServers_Click(object sender, RoutedEventArgs e)
        {
            foreach (var server in ServerGrid.Items.Cast<ServerTable>().ToList())
            {
                if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started)
                {
                    await GameServer_Restart(server);
                }
            }
        }

        private async void Actions_Update_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped) { return; }

            MessageBoxResult result = System.Windows.MessageBox.Show("Do you want to update this server?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) { return; }

            await GameServer_Update(server);
        }

        private async void Actions_UpdateValidate_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped) { return; }

            MessageBoxResult result = System.Windows.MessageBox.Show("Do you want to validate this server?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) { return; }

            await GameServer_Update(server, notes: " | Validate", validate: true);
        }

        private async void Actions_Backup_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped) { return; }

            MessageBoxResult result = System.Windows.MessageBox.Show("Do you want to backup on this server?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) { return; }

            await GameServer_Backup(server);
        }

        private async void Actions_RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped) { return; }

            listbox_RestoreBackup.Items.Clear();
            var backupConfig = new BackupConfig(server.ID);
            if (Directory.Exists(backupConfig.BackupLocation))
            {
                string zipFileName = $"WGSM-Backup-Server-{server.ID}-";
                foreach (var fi in new DirectoryInfo(backupConfig.BackupLocation).GetFiles("*.zip").Where(x => x.Name.Contains(zipFileName)).OrderByDescending(x => x.LastWriteTime))
                {
                    listbox_RestoreBackup.Items.Add(fi.Name);
                }
            }

            if (listbox_RestoreBackup.Items.Count > 0)
            {
                listbox_RestoreBackup.SelectedIndex = 0;
            }

            label_RestoreBackupServerName.Content = server.Name;
            MahAppFlyout_RestoreBackup.IsOpen = true;
        }

        private async void Button_RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped) { return; }

            if (listbox_RestoreBackup.SelectedIndex >= 0)
            {
                MahAppFlyout_RestoreBackup.IsOpen = false;
                await GameServer_RestoreBackup(server, listbox_RestoreBackup.SelectedItem.ToString());
            }
        }

        private void Actions_ManageAddons_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            ListBox_ManageAddons_Refresh();
            ToggleMahappFlyout(MahAppFlyout_ManageAddons);
        }
        #endregion

        private void ListBox_ManageAddonsLeft_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (listBox_ManageAddonsLeft.SelectedItem != null)
            {
                var server = (ServerTable)ServerGrid.SelectedItem;
                if (server == null) { return; }

                string item = listBox_ManageAddonsLeft.SelectedItem.ToString();
                listBox_ManageAddonsLeft.Items.Remove(listBox_ManageAddonsLeft.Items[listBox_ManageAddonsLeft.SelectedIndex]);
                listBox_ManageAddonsRight.Items.Add(item);
                var serverAddon = new ServerAddon(server.ID, server.Game);
                serverAddon.AddToRight(listBox_ManageAddonsRight.Items.OfType<string>().ToList(), item);

                ListBox_ManageAddons_Refresh();

                foreach (var selected in listBox_ManageAddonsRight.Items)
                {
                    if (selected.ToString() == item)
                    {
                        listBox_ManageAddonsRight.SelectedItem = selected;
                    }
                }
            }
        }

        private void ListBox_ManageAddonsRight_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (listBox_ManageAddonsRight.SelectedItem != null)
            {
                var server = (ServerTable)ServerGrid.SelectedItem;
                if (server == null) { return; }

                string item = listBox_ManageAddonsRight.SelectedItem.ToString();
                listBox_ManageAddonsRight.Items.Remove(listBox_ManageAddonsRight.Items[listBox_ManageAddonsRight.SelectedIndex]);
                listBox_ManageAddonsLeft.Items.Add(item);
                var serverAddon = new ServerAddon(server.ID, server.Game);
                serverAddon.AddToLeft(listBox_ManageAddonsRight.Items.OfType<string>().ToList(), item);

                ListBox_ManageAddons_Refresh();

                foreach (var selected in listBox_ManageAddonsLeft.Items)
                {
                    if (selected.ToString() == item)
                    {
                        listBox_ManageAddonsLeft.SelectedItem = selected;
                    }
                }
            }
        }

        private void ListBox_ManageAddons_Refresh()
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            var serverAddon = new ServerAddon(server.ID, server.Game);
            label_ManageAddonsName.Content = server.Name;
            label_ManageAddonsGame.Content = server.Game;
            label_ManageAddonsType.Content = serverAddon.GetModsName();

            listBox_ManageAddonsLeft.Items.Clear();
            foreach (string item in serverAddon.GetLeftListBox())
            {
                listBox_ManageAddonsLeft.Items.Add(item);
            }

            listBox_ManageAddonsRight.Items.Clear();
            foreach (string item in serverAddon.GetRightListBox())
            {
                listBox_ManageAddonsRight.Items.Add(item);
            }
        }

        public async Task<dynamic> Server_BeginStart(ServerTable server)
        {
            dynamic gameServer = GameServer.Data.Class.Get(server.Game, new ServerConfig(server.ID), PluginsList);
            if (gameServer == null) { return null; }

            //End All Running Process
            await EndAllRunningProcess(server.ID);
            await Task.Delay(500);

            //Add Start File to WindowsFirewall before start
            string startPath = ServerPath.GetServersServerFiles(server.ID, gameServer.StartPath);
            if (!string.IsNullOrWhiteSpace(gameServer.StartPath))
            {
                WindowsFirewall firewall = new WindowsFirewall(Path.GetFileName(startPath), startPath);
                if (!await firewall.IsRuleExist())
                {
                    await firewall.AddRule();
                }
            }

            gameServer.AllowsEmbedConsole = GetServerMetadata(server.ID).EmbedConsole;
            Process p = await gameServer.Start();

            //Fail to start
            if (p == null)
            {
                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                Log(server.ID, "Server: Fail to start");
                Log(server.ID, "[ERROR] " + gameServer.Error);
                SetServerStatus(server, "Stopped");

                return null;
            }

            _serverMetadata[int.Parse(server.ID)].Process = p;
            p.Exited += (sender, e) => OnGameServerExited(server);

            var task = Task.Run(() =>
            {
                try
                {
                    if (!p.StartInfo.CreateNoWindow)
                    {
                        while (!p.HasExited && !ShowWindow(p.MainWindowHandle, WindowShowStyle.Minimize))
                        {
                            Thread.Sleep(500);
                            //Debug.WriteLine("Try Setting ShowMinNoActivate Console Window");
                        }
                        Debug.WriteLine("Set ShowMinNoActivate Console Window");

                        //Save MainWindow
                        _serverMetadata[int.Parse(server.ID)].MainWindow = p.MainWindowHandle;
                    }

                    p.WaitForInputIdle();

                    ShowWindow(p.MainWindowHandle, WindowShowStyle.Hide);
                    Thread.Sleep(500);
                    ShowWindow(p.MainWindowHandle, _serverMetadata[int.Parse(server.ID)].ShowConsole ? WindowShowStyle.ShowNormal : WindowShowStyle.Hide);
                }
                catch
                {
                    Debug.WriteLine("No Window require to hide");
                }
            });

            //An error may occur on ShowWindow if not adding this
            if (p == null || p.HasExited)
            {
                _serverMetadata[int.Parse(server.ID)].Process = null;

                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                Log(server.ID, "Server: Fail to start");
                Log(server.ID, "[ERROR] Exit Code: " + p.ExitCode.ToString());
                SetServerStatus(server, "Stopped");

                return null;
            }

            // Set Priority
            p = Functions.CPU.Priority.SetProcessWithPriority(p, Functions.CPU.Priority.GetPriorityInteger(GetServerMetadata(server.ID).CPUPriority));

            // Set Affinity
            try
            {
                p.ProcessorAffinity = Functions.CPU.Affinity.GetAffinityIntPtr(GetServerMetadata(server.ID).CPUAffinity);
            }
            catch (Exception e)
            {
                Log(server.ID, $"[NOTICE] Fail to set affinity. ({e.Message})");
            }

            // Save Cache
            ServerCache.SavePID(server.ID, p.Id);
            ServerCache.SaveProcessName(server.ID, p.ProcessName);
            ServerCache.SaveWindowsIntPtr(server.ID, GetServerMetadata(server.ID).MainWindow);

            SetWindowText(p.MainWindowHandle, server.Name);

            ShowWindow(p.MainWindowHandle, _serverMetadata[int.Parse(server.ID)].ShowConsole ? WindowShowStyle.ShowNormal : WindowShowStyle.Hide);

            StartAutoUpdateCheck(server);

            StartRestartCrontabCheck(server);

            StartSendHeartBeat(server);

            StartQuery(server);

            if (MahAppSwitch_SendStatistics.IsOn)
            {
                try
                {
                    var analytics = new GoogleAnalytics();
                    analytics.SendGameServerStart(server.ID, server.Game);
                }
                catch
                {
                    // i basically just don't care when google analytics fail
                }
            }

            return gameServer;
        }

        private bool InvariantTryParse(string input, out double value)
        {
            return double.TryParse(input.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        public async Task<bool> Server_BeginStop(ServerTable server, Process p)
        {
            _serverMetadata[int.Parse(server.ID)].Process = null;

            dynamic gameServer = GameServer.Data.Class.Get(server.Game, pluginList: PluginsList);
            try
            {
                await gameServer.Stop(p);
                for (int i = 0; i < 10; i++)
                {
                    if (p == null || p.HasExited) { break; }
                    await Task.Delay(1000);
                }
            }
            catch (Exception)
            {
                ProcessManagement.StopProcess(p);//no stop function most likely, try windows graceful shutdown
            }

            _serverMetadata[int.Parse(server.ID)].ServerConsole.Clear();

            // Save Cache
            ServerCache.SavePID(server.ID, -1);
            ServerCache.SaveProcessName(server.ID, string.Empty);
            ServerCache.SaveWindowsIntPtr(server.ID, (IntPtr)0);

            if (p != null && !p.HasExited)
            {
                p.Kill();
                return false;
            }

            return true;
        }

        private async Task<(Process, string, dynamic)> Server_BeginUpdate(ServerTable server, bool silenceCheck, bool forceUpdate, bool validate = false, string custum = null)
        {
            dynamic gameServer = GameServer.Data.Class.Get(server.Game, new ServerConfig(server.ID), PluginsList);
            string steamAppId = GetSteamAppId(gameServer);
            bool steamBranchChanged = !string.IsNullOrWhiteSpace(steamAppId) && WindowsGSM.Installer.SteamCMD.IsSteamBranchChangePending(server.ID);

            string localVersion = gameServer.GetLocalBuild();
            if (string.IsNullOrWhiteSpace(localVersion) && !silenceCheck)
            {
                Log(server.ID, $"[NOTICE] {gameServer.Error}");
            }

            string remoteVersion = string.IsNullOrWhiteSpace(steamAppId)
                ? await gameServer.GetRemoteBuild()
                : await new Installer.SteamCMD().GetRemoteBuild(steamAppId, ServerConfig.GetSetting(server.ID, ServerConfig.SettingName.SteamBranch));
            if (string.IsNullOrWhiteSpace(remoteVersion) && !silenceCheck)
            {
                Log(server.ID, $"[NOTICE] {gameServer.Error}");
            }

            if (!silenceCheck)
            {
                Log(server.ID, $"Checking: {GetSteamBranchLogPrefix(steamAppId, server.ID)}build ({localVersion}) => ({remoteVersion})");
                if (steamBranchChanged)
                {
                    Log(server.ID, $"Checking: Steam branch change required ({GetSteamBranchNameForLog(ServerConfig.GetSetting(server.ID, ServerConfig.SettingName.SteamBranchLastInstalled))}) => ({GetSteamBranchNameForLog(ServerConfig.GetSetting(server.ID, ServerConfig.SettingName.SteamBranch))})");
                }
            }

            if ((!string.IsNullOrWhiteSpace(localVersion) && !string.IsNullOrWhiteSpace(remoteVersion) && localVersion != remoteVersion) || forceUpdate || steamBranchChanged)
            {
                try
                {
                    return (await gameServer.Update(validate, custum), remoteVersion, gameServer);
                }
                catch
                {
                    return (await gameServer.Update(), remoteVersion, gameServer);
                }
            }

            return (null, remoteVersion, gameServer);
        }

        #region Actions - Game Server
        private async Task GameServer_Start(ServerTable server, string notes = "")
        {
            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped) { return; }

            string error = string.Empty;
            if (!string.IsNullOrWhiteSpace(server.IP) && !IsValidIPAddress(server.IP))
            {
                error += " IP address is not valid.";
            }

            if (!string.IsNullOrWhiteSpace(server.Port) && !IsValidPort(server.Port))
            {
                error += " Port number is not valid.";
            }

            if (error != string.Empty)
            {
                Log(server.ID, "Server: Fail to start");
                Log(server.ID, "[ERROR]" + error);

                return;
            }

            Process p = GetServerMetadata(server.ID).Process;
            if (p != null) { return; }

            if (GetServerMetadata(server.ID).BackupOnStart)
            {
                await GameServer_Backup(server, " | Backup on Start");
            }

            if (GetServerMetadata(server.ID).UpdateOnStart)
            {
                await GameServer_Update(server, " | Update on Start");
            }

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Starting;
            Log(server.ID, "Action: Start" + notes);
            SetServerStatus(server, "Starting");

            var gameServer = await Server_BeginStart(server);
            if (gameServer == null)
            {
                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                Log(server.ID, "Server: Fail to start");
                SetServerStatus(server, "Stopped");
                return;
            }

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Started;
            Log(server.ID, "Server: Started");
            if (!string.IsNullOrWhiteSpace(gameServer.Notice))
            {
                Log(server.ID, "[Notice] " + gameServer.Notice);
            }
            SetServerStatus(server, "Started", ServerCache.GetPID(server.ID).ToString());
        }

        private async Task GameServer_Stop(ServerTable server)
        {
            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Started) { return; }

            Process p = GetServerMetadata(server.ID).Process;
            if (p == null) { return; }

            //Begin stop
            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopping;
            Log(server.ID, "Action: Stop");
            SetServerStatus(server, "Stopping");

            bool stopGracefully = await Server_BeginStop(server, p);

            Log(server.ID, "Server: Stopped");
            if (!stopGracefully)
            {
                Log(server.ID, "[NOTICE] Server fail to stop gracefully");
            }
            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
            SetServerStatus(server, "Stopped");
        }

        private async Task GameServer_Restart(ServerTable server)
        {
            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Started) { return; }

            Process p = GetServerMetadata(server.ID).Process;
            if (p == null) { return; }

            _serverMetadata[int.Parse(server.ID)].Process = null;

            //Begin Restart
            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Restarting;
            Log(server.ID, "Action: Restart");
            SetServerStatus(server, "Restarting");

            await Server_BeginStop(server, p);

            await Task.Delay(500);

            if (GetServerMetadata(server.ID).UpdateOnStart)
            {
                await GameServer_Update(server, " | Update on Start");
            }

            await Task.Delay(500);

            var gameServer = await Server_BeginStart(server);
            if (gameServer == null)
            {
                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                SetServerStatus(server, "Stopped");
                return;
            }

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Started;
            Log(server.ID, "Server: Restarted");
            if (!string.IsNullOrWhiteSpace(gameServer.Notice))
            {
                Log(server.ID, "[Notice] " + gameServer.Notice);
            }
            SetServerStatus(server, "Started", ServerCache.GetPID(server.ID).ToString());
        }

        public async Task<bool> GameServer_Update(ServerTable server, string notes = "", bool validate = false)
        {
            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped)
            {
                return false;
            }

            //Begin Update
            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Updating;
            Log(server.ID, "Action: Update" + GetSteamBranchActionSuffix(server) + notes);
            SetServerStatus(server, "Updating");

            var (p, remoteVersion, gameServer) = await Server_BeginUpdate(server, silenceCheck: validate, forceUpdate: true, validate: validate);

            if (p == null && string.IsNullOrEmpty(gameServer.Error)) // Update success (non-steamcmd server)
            {
                MarkSteamBranchInstalledIfNeeded(server.ID, gameServer);
                Log(server.ID, $"Server: Updated {(validate ? "Validate " : string.Empty)}{GetSteamBranchLogSuffix(gameServer, server.ID)}to build {remoteVersion}");
            }
            else if (p != null) // p stores process of steamcmd
            {
                await Task.Run(() => { p.WaitForExit(); });
                if (p.ExitCode == 0)
                {
                    MarkSteamBranchInstalledIfNeeded(server.ID, gameServer);
                    Log(server.ID, $"Server: Updated {(validate ? "Validate " : string.Empty)}{GetSteamBranchLogSuffix(gameServer, server.ID)}to build {remoteVersion}");
                }
                else
                {
                    Log(server.ID, "Server: Fail to update");
                    Log(server.ID, $"[ERROR] SteamCMD exited with code {p.ExitCode}");
                }
            }
            else
            {
                Log(server.ID, "Server: Fail to update");
                Log(server.ID, "[ERROR] " + gameServer.Error);
            }

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
            SetServerStatus(server, "Stopped");

            return true;
        }

        private static void MarkSteamBranchInstalledIfNeeded(string serverId, dynamic gameServer)
        {
            if (string.IsNullOrWhiteSpace(GetSteamAppId(gameServer))) { return; }

            WindowsGSM.Installer.SteamCMD.MarkSteamBranchInstalled(serverId);
        }

        private static string GetSteamBranchLogPrefix(string steamAppId, string serverId)
        {
            return string.IsNullOrWhiteSpace(steamAppId)
                ? string.Empty
                : $"Steam branch {GetSteamBranchNameForLog(ServerConfig.GetSetting(serverId, ServerConfig.SettingName.SteamBranch))} ";
        }

        private static string GetSteamBranchLogSuffix(dynamic gameServer, string serverId)
        {
            return string.IsNullOrWhiteSpace(GetSteamAppId(gameServer))
                ? string.Empty
                : $"Steam branch {GetSteamBranchNameForLog(ServerConfig.GetSetting(serverId, ServerConfig.SettingName.SteamBranch))} ";
        }

        private static string GetSteamBranchActionSuffix(ServerTable server)
        {
            if (server == null) { return string.Empty; }

            try
            {
                dynamic gameServer = GameServer.Data.Class.Get(server.Game);
                if (string.IsNullOrWhiteSpace(GetSteamAppId(gameServer))) { return string.Empty; }

                return $" | Steam branch {GetSteamBranchNameForLog(ServerConfig.GetSetting(server.ID, ServerConfig.SettingName.SteamBranch))}";
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetSteamBranchNameForLog(string branch)
        {
            return string.IsNullOrWhiteSpace(branch) ? "public" : branch;
        }

        private async Task<bool> GameServer_Backup(ServerTable server, string notes = "")
        {
            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped)
            {
                return false;
            }

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Backup;
            Log(server.ID, "Action: Backup" + notes);
            SetServerStatus(server, "Backup");

            await EndAllRunningProcess(server.ID);
            await Task.Delay(1000);

            var backupConfig = new BackupConfig(server.ID);
            string backupLocation = string.IsNullOrWhiteSpace(backupConfig.BackupLocation) ? ServerPath.GetBackups(server.ID) : backupConfig.BackupLocation;
            try
            {
                Directory.CreateDirectory(backupLocation);
            }
            catch (Exception ex)
            {
                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                Log(server.ID, "Server: Fail to backup");
                Log(server.ID, "[ERROR] Backup location not accessible: " + ex.Message);
                SetServerStatus(server, "Stopped");
                return false;
            }

            string zipFileNamePrefix = $"WGSM-Backup-Server-{server.ID}-";
            string zipFile = Path.Combine(backupLocation, $"{zipFileNamePrefix}{DateTime.Now.ToString("yyyyMMddHHmmss")}.zip");

            try
            {
                int maxKeep = backupConfig.MaximumBackups <= 0 ? 1 : backupConfig.MaximumBackups;
                var toRemove = new DirectoryInfo(backupLocation)
                    .GetFiles("*.zip")
                    .Where(x => x.Name.Contains(zipFileNamePrefix))
                    .OrderByDescending(x => x.LastWriteTime)
                    .Skip(maxKeep - 1)
                    .ToList();

                foreach (var f in toRemove)
                {
                    try { f.Delete(); } catch { }
                }
            }
            catch { }
            var backupFolders = (backupConfig.SavesLocations ?? Enumerable.Empty<string>()).ToList();
            var backupFiles = (backupConfig.FilesLocations ?? Enumerable.Empty<string>()).ToList();
            if (backupFolders.Count == 0 && backupFiles.Count == 0)
            {
                string startPath = ServerPath.GetServers(server.ID);
                string error = string.Empty;
                await Task.Run(() =>
                {
                    try
                    {
                        if (File.Exists(zipFile))
                        {
                            File.Delete(zipFile);
                        }

                        using (FileStream zipStream = new FileStream(zipFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                        using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                        {
                            AddDirectoryToArchive(archive, startPath, string.Empty);
                        }
                    }
                    catch (Exception e)
                    {
                        error = e.Message;
                    }
                });

                if (!string.IsNullOrEmpty(error))
                {
                    _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                    Log(server.ID, "Server: Fail to backup");
                    Log(server.ID, $"[ERROR] {error}");
                    SetServerStatus(server, "Stopped");
                    return false;
                }

                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                Log(server.ID, "Server: Backuped");
                SetServerStatus(server, "Stopped");
                return true;
            }

            try
            {
                var manifest = new List<string>();
                string error = string.Empty;
                await Task.Run(() =>
                {
                    try
                    {
                        if (File.Exists(zipFile))
                        {
                            File.Delete(zipFile);
                        }

                        using (FileStream zipStream = new FileStream(zipFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                        using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                        {
                            int manifestIndex = 0;
                            for (int i = 0; i < backupFolders.Count; i++)
                            {
                                string original = backupFolders[i] ?? string.Empty;
                                original = Environment.ExpandEnvironmentVariables(original).Trim();
                                manifest.Add($"{manifestIndex}|D|{original}");

                                string entryRoot = $"save_{manifestIndex}";
                                if (string.IsNullOrWhiteSpace(original))
                                {
                                    archive.CreateEntry(entryRoot + "/");
                                    manifestIndex++;
                                    continue;
                                }

                                if (Directory.Exists(original))
                                {
                                    AddDirectoryToArchive(archive, original, entryRoot);
                                }
                                else if (File.Exists(original))
                                {
                                    AddFileToArchive(archive, original, CombineArchivePath(entryRoot, Path.GetFileName(original)));
                                }
                                else
                                {
                                    archive.CreateEntry(entryRoot + "/");
                                }

                                manifestIndex++;
                            }

                            for (int i = 0; i < backupFiles.Count; i++)
                            {
                                string original = backupFiles[i] ?? string.Empty;
                                original = Environment.ExpandEnvironmentVariables(original).Trim();
                                manifest.Add($"{manifestIndex}|F|{original}");

                                string entryRoot = $"save_{manifestIndex}";
                                if (string.IsNullOrWhiteSpace(original))
                                {
                                    archive.CreateEntry(entryRoot + "/");
                                    manifestIndex++;
                                    continue;
                                }

                                if (File.Exists(original))
                                {
                                    AddFileToArchive(archive, original, CombineArchivePath(entryRoot, Path.GetFileName(original)));
                                }
                                else
                                {
                                    archive.CreateEntry(entryRoot + "/");
                                }

                                manifestIndex++;
                            }

                            ZipArchiveEntry manifestEntry = archive.CreateEntry("backup_manifest.txt");
                            using (StreamWriter writer = new StreamWriter(manifestEntry.Open()))
                            {
                                foreach (string line in manifest)
                                {
                                    writer.WriteLine(line);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        error = e.Message;
                    }
                });
                if (!string.IsNullOrEmpty(error))
                {
                    _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                    Log(server.ID, "Server: Fail to backup");
                    Log(server.ID, $"[ERROR] {error}");
                    SetServerStatus(server, "Stopped");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                Log(server.ID, "Server: Fail to backup");
                Log(server.ID, $"[ERROR] {ex.Message}");
                SetServerStatus(server, "Stopped");
                return false;
            }

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
            Log(server.ID, "Server: Backuped");
            SetServerStatus(server, "Stopped");
            return true;
        }

        private static void AddDirectoryToArchive(ZipArchive archive, string sourceDirectory, string entryRoot)
        {
            string normalizedRoot = NormalizeArchivePath(entryRoot).TrimEnd('/');
            DirectoryInfo directory = new DirectoryInfo(sourceDirectory);

            if (!directory.Exists)
            {
                return;
            }

            FileInfo[] files = directory.GetFiles("*", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                archive.CreateEntry(string.IsNullOrEmpty(normalizedRoot) ? directory.Name + "/" : normalizedRoot + "/");
                return;
            }

            foreach (FileInfo file in files)
            {
                string relativePath = Path.GetRelativePath(sourceDirectory, file.FullName);
                string entryName = string.IsNullOrEmpty(normalizedRoot)
                    ? NormalizeArchivePath(relativePath)
                    : CombineArchivePath(normalizedRoot, relativePath);
                AddFileToArchive(archive, file.FullName, entryName);
            }
        }

        private static void AddFileToArchive(ZipArchive archive, string sourceFile, string entryName)
        {
            ZipArchiveEntry entry = archive.CreateEntry(NormalizeArchivePath(entryName));
            using Stream input = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using Stream output = entry.Open();
            input.CopyTo(output);
        }

        private static string CombineArchivePath(string left, string right)
        {
            string normalizedLeft = NormalizeArchivePath(left).TrimEnd('/');
            string normalizedRight = NormalizeArchivePath(right).TrimStart('/');

            if (string.IsNullOrEmpty(normalizedLeft))
            {
                return normalizedRight;
            }

            if (string.IsNullOrEmpty(normalizedRight))
            {
                return normalizedLeft;
            }

            return normalizedLeft + "/" + normalizedRight;
        }

        private static string NormalizeArchivePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private async Task<bool> GameServer_RestoreBackup(ServerTable server, string backupFile)
        {
            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped)
            {
                return false;
            }

            var backupConfig = new BackupConfig(server.ID);
            string backupLocation = string.IsNullOrWhiteSpace(backupConfig.BackupLocation) ? ServerPath.GetBackups(server.ID) : backupConfig.BackupLocation;
            string backupPath = Path.Combine(backupLocation, backupFile);

            if (!File.Exists(backupPath))
            {
                Log(server.ID, "Server: Fail to restore backup");
                Log(server.ID, "[ERROR] Backup not found");
                return false;
            }

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Restoring;
            Log(server.ID, "Action: Restore Backup");
            SetServerStatus(server, "Restoring");

            string tempRoot = Path.Combine(Path.GetTempPath(), "WindowsGSM_Backup_Restore", server.ID, DateTime.Now.ToString("yyyyMMddHHmmss"));
            string error = string.Empty;

            void DirectoryCopy(string src, string dst)
            {
                var dir = new DirectoryInfo(src);
                if (!dir.Exists) return;
                Directory.CreateDirectory(dst);

                foreach (var file in dir.GetFiles())
                {
                    file.CopyTo(Path.Combine(dst, file.Name), true);
                }

                foreach (var sub in dir.GetDirectories())
                {
                    DirectoryCopy(sub.FullName, Path.Combine(dst, sub.Name));
                }
            }
            try
            {
                Directory.CreateDirectory(tempRoot);
                await Task.Run(() =>
                {
                    try
                    {
                        ZipFile.ExtractToDirectory(backupPath, tempRoot);
                    }
                    catch (Exception e)
                    {
                        error = e.Message;
                    }
                });

                if (!string.IsNullOrEmpty(error))
                {
                    _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                    Log(server.ID, "Server: Fail to restore backup");
                    Log(server.ID, $"[ERROR] {error}");
                    SetServerStatus(server, "Stopped");
                    try { Directory.Delete(tempRoot, true); } catch { }
                    return false;
                }

                string manifestPath = Path.Combine(tempRoot, "backup_manifest.txt");
                if (!File.Exists(manifestPath))
                {
                    string extractPath = ServerPath.GetServers(server.ID);

                    if (Directory.Exists(extractPath))
                    {
                        string ex = string.Empty;
                        await Task.Run(() =>
                        {
                            try
                            {
                                Directory.Delete(extractPath, true);
                            }
                            catch (Exception e)
                            {
                                ex = e.Message;
                            }
                        });

                        if (!string.IsNullOrEmpty(ex))
                        {
                            error = ex;
                        }
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                        Log(server.ID, "Server: Fail to restore backup");
                        Log(server.ID, $"[ERROR] {error}");
                        SetServerStatus(server, "Stopped");
                        try { Directory.Delete(tempRoot, true); } catch { }
                        return false;
                    }

                    error = string.Empty;
                    await Task.Run(() =>
                    {
                        try
                        {
                            ZipFile.ExtractToDirectory(backupPath, ServerPath.GetServers(server.ID));
                        }
                        catch (Exception e)
                        {
                            error = e.Message;
                        }
                    });

                    try { Directory.Delete(tempRoot, true); } catch { }

                    if (!string.IsNullOrEmpty(error))
                    {
                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                        Log(server.ID, "Server: Fail to restore backup");
                        Log(server.ID, $"[ERROR] {error}");
                        SetServerStatus(server, "Stopped");
                        return false;
                    }

                    _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                    Log(server.ID, "Server: Restored");
                    SetServerStatus(server, "Stopped");
                    return true;
                }

                var manifestLines = File.ReadAllLines(manifestPath);
                foreach (var line in manifestLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(new[] { '|' }, 3);
                    if (parts.Length < 2) continue;
                    if (!int.TryParse(parts[0], out int idx)) continue;
                    string entryType = parts.Length == 3 ? parts[1] : "D";
                    string originalPath = parts.Length == 3 ? parts[2] : parts[1];
                    if (string.IsNullOrWhiteSpace(originalPath)) continue;

                    originalPath = Environment.ExpandEnvironmentVariables(originalPath).Trim();
                    string extractedSub = Path.Combine(tempRoot, $"save_{idx}");

                    try
                    {
                        if (string.Equals(entryType, "F", StringComparison.OrdinalIgnoreCase))
                        {
                            string extractedFile = Directory.Exists(extractedSub)
                                ? Directory.GetFiles(extractedSub).FirstOrDefault()
                                : null;

                            if (!string.IsNullOrWhiteSpace(extractedFile))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(originalPath) ?? originalPath);
                                File.Copy(extractedFile, originalPath, true);
                            }
                        }
                        else
                        {
                            if (Directory.Exists(originalPath))
                            {
                                Directory.Delete(originalPath, true);
                            }

                            if (Directory.Exists(extractedSub))
                            {
                                DirectoryCopy(extractedSub, originalPath);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        error = e.Message;
                        break;
                    }
                }
                try { Directory.Delete(tempRoot, true); } catch { }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            if (!string.IsNullOrEmpty(error))
            {
                _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                Log(server.ID, "Server: Fail to restore backup");
                Log(server.ID, $"[ERROR] {error}");
                SetServerStatus(server, "Stopped");
                return false;
            }
            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
            Log(server.ID, "Server: Restored");
            SetServerStatus(server, "Stopped");
            return true;
        }

        private async Task<bool> GameServer_Delete(ServerTable server)
        {
            if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Stopped)
            {
                return false;
            }

            //Begin delete
            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Deleting;
            Log(server.ID, "Action: Delete");
            SetServerStatus(server, "Deleting");

            //Remove firewall rule
            var firewall = new WindowsFirewall(null, ServerPath.GetServers(server.ID));
            firewall.RemoveRuleEx();

            //End All Running Process
            await EndAllRunningProcess(server.ID);
            await Task.Delay(1000);

            string serverPath = ServerPath.GetServers(server.ID);

            await Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(serverPath))
                    {
                        Directory.Delete(serverPath, true);
                    }
                }
                catch
                {

                }
            });

            await Task.Delay(1000);

            if (Directory.Exists(serverPath))
            {
                string wgsmCfgPath = ServerPath.GetServersConfigs(server.ID, "WindowsGSM.cfg");
                if (File.Exists(wgsmCfgPath))
                {
                    Log(server.ID, "Server: Fail to delete server");
                    Log(server.ID, "[ERROR] Directory is not accessible");

                    _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                    SetServerStatus(server, "Stopped");

                    return false;
                }
            }

            Log(server.ID, "Server: Deleted server");

            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
            SetServerStatus(server, "Stopped");

            LoadServerTable();

            return true;
        }
        #endregion

        private async void OnGameServerExited(ServerTable server)
        {
            if (System.Windows.Application.Current == null) { return; }

            await System.Windows.Application.Current.Dispatcher.Invoke(async () =>
            {
                int serverId = int.Parse(server.ID);

                if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started)
                {
                    Process crashedProcess = GetServerMetadata(server.ID).Process;
                    string crashLog = WriteGameServerCrashLog(server, crashedProcess, out string exitCode);
                    bool autoRestart = GetServerMetadata(serverId).AutoRestart;
                    _serverMetadata[int.Parse(server.ID)].ServerStatus = autoRestart ? ServerStatus.Restarting : ServerStatus.Stopped;
                    Log(server.ID, string.IsNullOrWhiteSpace(exitCode) ? "Server: Crashed" : $"Server: Crashed (Exit Code: {exitCode})");
                    if (!string.IsNullOrWhiteSpace(crashLog))
                    {
                        Log(server.ID, $"[ERROR] Crash details: {crashLog}");
                    }
                    SetServerStatus(server, autoRestart ? "Restarting" : "Stopped");

                    if (GetServerMetadata(serverId).DiscordAlert && GetServerMetadata(serverId).CrashAlert)
                    {
                        if (CheckWebhookThreshold(ref _lastCrashTime) || _latestWebhookSend != ServerStatus.Crashed)
                        {
                            var webhook = new DiscordWebhook(GetServerMetadata(serverId).DiscordWebhook, GetServerMetadata(serverId).DiscordMessage, g_DonorType, GetServerMetadata(serverId).SkipUserSetup);
                            await webhook.Send(server.ID, server.Game, "Crashed", server.Name, server.IP, server.Port);
                            _latestWebhookSend = ServerStatus.Crashed;
                        }
                    }

                    _serverMetadata[int.Parse(server.ID)].Process = null;

                    if (autoRestart)
                    {
                        if (GetServerMetadata(server.ID).BackupOnStart)
                        {
                            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                            await GameServer_Backup(server, " | Backup on Start");
                        }

                        if (GetServerMetadata(server.ID).UpdateOnStart)
                        {
                            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                            await GameServer_Update(server, " | Update on Start");
                        }

                        var gameServer = await Server_BeginStart(server);
                        if (gameServer == null)
                        {
                            _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopped;
                            return;
                        }

                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Started;
                        Log(server.ID, "Server: Started | Auto Restart");
                        if (!string.IsNullOrWhiteSpace(gameServer.Notice))
                        {
                            Log(server.ID, "[Notice] " + gameServer.Notice);
                        }
                        SetServerStatus(server, "Started", ServerCache.GetPID(server.ID).ToString());

                        if (GetServerMetadata(serverId).DiscordAlert && GetServerMetadata(serverId).AutoRestartAlert)
                        {
                            //Only send Webhook_Start if there wasn't a retry in the last X min
                            if (CheckWebhookThreshold(ref _lastAutoRestartTime))
                            {
                                var webhook = new DiscordWebhook(GetServerMetadata(serverId).DiscordWebhook, GetServerMetadata(serverId).DiscordMessage, g_DonorType, GetServerMetadata(serverId).SkipUserSetup);
                                await webhook.Send(server.ID, server.Game, "Restarted | Auto Restart", server.Name, server.IP, server.Port);
                                _latestWebhookSend = GetServerMetadata(serverId).ServerStatus;
                            }
                        }
                    }
                }
            });
        }

        const int UPDATE_INTERVAL_MINUTE = 30;
        const int IP_UPDATE_INTERVAL_MINUTE = 2;
        private async void StartAutpIpUpdate()
        {
            await Task.Run(async () =>
            {
                await Task.Delay(30000); //delay initial check so the servers can start
                while (true)
                {
                    await SendCurrentPublicIPs();
                    await Task.Delay(60000 * IP_UPDATE_INTERVAL_MINUTE); //check every minute
                }
            });
        }
        private async void StartAutoUpdateCheck(ServerTable server)
        {
            int serverId = int.Parse(server.ID);

            //Save the process of game server
            Process p = GetServerMetadata(server.ID).Process;

            dynamic gameServer = GameServer.Data.Class.Get(server.Game, new ServerConfig(server.ID), PluginsList);

            string localVersion = gameServer.GetLocalBuild();

            while (p != null && !p.HasExited)
            {
                await Task.Delay(60000 * UPDATE_INTERVAL_MINUTE);

                if (!GetServerMetadata(server.ID).AutoUpdate || GetServerMetadata(server.ID).ServerStatus == ServerStatus.Updating)
                {
                    continue;
                }

                if (p == null || p.HasExited) { break; }

                //Try to get local build again if not found just now
                if (string.IsNullOrWhiteSpace(localVersion))
                {
                    localVersion = gameServer.GetLocalBuild();
                }

                //Get remote build
                string steamAppId = GetSteamAppId(gameServer);
                string remoteVersion = string.IsNullOrWhiteSpace(steamAppId)
                    ? await gameServer.GetRemoteBuild()
                    : await new Installer.SteamCMD().GetRemoteBuild(steamAppId, ServerConfig.GetSetting(server.ID, ServerConfig.SettingName.SteamBranch));
                bool steamBranchChanged = !string.IsNullOrWhiteSpace(steamAppId) && WindowsGSM.Installer.SteamCMD.IsSteamBranchChangePending(server.ID);

                //Continue if success to get localVersion and remoteVersion, or if changing branch requires a forced update.
                if ((!string.IsNullOrWhiteSpace(localVersion) && !string.IsNullOrWhiteSpace(remoteVersion)) || steamBranchChanged)
                {
                    if (GetServerMetadata(server.ID).ServerStatus != ServerStatus.Started)
                    {
                        break;
                    }

                    Log(server.ID, $"Checking: {GetSteamBranchLogPrefix(steamAppId, server.ID)}build ({localVersion}) => ({remoteVersion})");
                    if (steamBranchChanged)
                    {
                        Log(server.ID, $"Checking: Steam branch change required ({GetSteamBranchNameForLog(ServerConfig.GetSetting(server.ID, ServerConfig.SettingName.SteamBranchLastInstalled))}) => ({GetSteamBranchNameForLog(ServerConfig.GetSetting(server.ID, ServerConfig.SettingName.SteamBranch))})");
                    }

                    if (steamBranchChanged || localVersion != remoteVersion)
                    {
                        _serverMetadata[int.Parse(server.ID)].Process = null;

                        //Begin stop
                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Stopping;
                        SetServerStatus(server, "Stopping");

                        //Stop the server
                        await Server_BeginStop(server, p);

                        if (p != null && !p.HasExited)
                        {
                            p.Kill();
                        }

                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Updating;
                        SetServerStatus(server, "Updating");

                        //Update the server
                        Process updateProcess = await gameServer.Update();
                        int? updateExitCode = null;
                        if (updateProcess != null)
                        {
                            await Task.Run(() => updateProcess.WaitForExit());
                            updateExitCode = updateProcess.ExitCode;
                        }

                        if (string.IsNullOrWhiteSpace(gameServer.Error) && (!updateExitCode.HasValue || updateExitCode.Value == 0))
                        {
                            MarkSteamBranchInstalledIfNeeded(server.ID, gameServer);
                            Log(server.ID, $"Server: Updated {GetSteamBranchLogSuffix(gameServer, server.ID)}to build {remoteVersion}");

                            if (GetServerMetadata(serverId).DiscordAlert && GetServerMetadata(serverId).AutoUpdateAlert)
                            {
                                var webhook = new DiscordWebhook(GetServerMetadata(serverId).DiscordWebhook, GetServerMetadata(serverId).DiscordMessage, g_DonorType, GetServerMetadata(serverId).SkipUserSetup);
                                await webhook.Send(server.ID, server.Game, "Updated | Auto Update", server.Name, server.IP, server.Port);
                                _latestWebhookSend = GetServerMetadata(serverId).ServerStatus;
                            }
                        }
                        else
                        {
                            Log(server.ID, "Server: Fail to update");
                            Log(server.ID, "[ERROR] " + (!string.IsNullOrWhiteSpace(gameServer.Error) ? gameServer.Error : $"SteamCMD exited with code {updateExitCode}"));
                        }

                        //Start the server
                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Starting;
                        SetServerStatus(server, "Starting");

                        var gameServerStart = await Server_BeginStart(server);
                        if (gameServerStart == null) { return; }

                        _serverMetadata[int.Parse(server.ID)].ServerStatus = ServerStatus.Started;
                        SetServerStatus(server, "Started", ServerCache.GetPID(server.ID).ToString());

                        break;
                    }
                }
                else if (string.IsNullOrWhiteSpace(localVersion))
                {
                    Log(server.ID, $"[NOTICE] Fail to get local build.");
                }
                else if (string.IsNullOrWhiteSpace(remoteVersion))
                {
                    Log(server.ID, $"[NOTICE] Fail to get remote build.");
                }
            }
        }

        private async void StartRestartCrontabCheck(ServerTable server)
        {
            var crontabManager = new CrontabManager(this, server, GetServerMetadata(server.ID).Process);

            await crontabManager.MainLoop();
        }

        private async void StartSendHeartBeat(ServerTable server)
        {
            //Save the process of game server
            Process p = GetServerMetadata(server.ID).Process;

            while (p != null && !p.HasExited)
            {
                if (MahAppSwitch_SendStatistics.IsOn)
                {
                    try
                    {
                        var analytics = new GoogleAnalytics();
                        analytics.SendGameServerHeartBeat(server.Game, server.Name);
                    }
                    catch
                    {
                        // i basically just don't care when google analytics fail
                    }
                }

                await Task.Delay(300000);
            }
        }

        private async void StartQuery(ServerTable server)
        {
            if (string.IsNullOrWhiteSpace(server.IP)) { return; }

            // Check the server support Query Method
            dynamic gameServer = GameServer.Data.Class.Get(server.Game, pluginList: PluginsList);
            if (gameServer == null) { return; }
            if (gameServer.QueryMethod == null) { return; }

            // Save the process of game server
            Process p = GetServerMetadata(server.ID).Process;

            // Query server every 5 seconds
            while (p != null && !p.HasExited)
            {
                if (GetServerMetadata(server.ID).ServerStatus == ServerStatus.Stopped)
                {
                    break;
                }

                dynamic query = gameServer.QueryMethod;
                string portString = (query is EOS) ? server.Port : server.QueryPort;

                if (!IsValidIPAddress(server.IP) || !IsValidPort(portString))
                {
                    await Task.Delay(5000);
                    continue;
                }

                try
                {
                    string ip = server.IP;
                    //map "bind to all" ip to localhost
                    if (ip.Contains("0.0.0.0"))
                    {
                        ip = "127.0.0.1";
                    }

                    query.SetAddressPort(server.IP, int.Parse(portString));

                    string players = await query.GetPlayersAndMaxPlayers();

                    if (players != null)
                    {
                        server.Maxplayers = players;

                        for (int i = 0; i < ServerGrid.Items.Count; i++)
                        {
                            if (server.ID == ((ServerTable)ServerGrid.Items[i]).ID)
                            {
                                int selectedIndex = ServerGrid.SelectedIndex;
                                ServerGrid.Items[i] = server;
                                ServerGrid.SelectedIndex = selectedIndex;
                                ServerGrid.Items.Refresh();
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // keep existing behavior (swallow); optionally log
                }

                try
                {
                    if (query is QueryTemplate templateQuery)
                    {
                        var playerData = await templateQuery.GetPlayersData();
                        if (playerData != null && playerData.Count != 0)
                        {
                            server.PlayerList = playerData;
                        }
                    }
                }
                catch
                {
                    // keep existing behavior (swallow); optionally log
                }

                await Task.Delay(5000);
            }
        }

        private async Task EndAllRunningProcess(string serverId)
        {
            await Task.Run(() =>
            {
                //LINQ query for windowsgsm old processes
                var processes = (from p in Process.GetProcesses()
                                 where ((Predicate<Process>)(p_ =>
                                 {
                                     try
                                     {
                                         return p_.MainModule.FileName.Contains(Path.Combine(WGSM_PATH, "servers", serverId) + "\\");
                                     }
                                     catch
                                     {
                                         return false;
                                     }
                                 }))(p)
                                 select p).ToList();

                // Kill all processes
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        //ignore
                    }
                }
            });
        }

        public void SetServerStatus(ServerTable server, string status, string pid = null)
        {
            server.Status = status;
            if (pid != null)
            {
                server.PID = pid;
            }
            if (status == "Stopped")
            {
                server.PID = string.Empty;
            }

            if (server.Status != "Started" && server.Maxplayers.Contains('/'))
            {
                var serverConfig = new ServerConfig(server.ID);
                server.Maxplayers = serverConfig.ServerMaxPlayer;
            }

            for (int i = 0; i < ServerGrid.Items.Count; i++)
            {
                if (server.ID == ((ServerTable)ServerGrid.Items[i]).ID)
                {
                    int selectedIndex = ServerGrid.SelectedIndex;
                    ServerGrid.Items[i] = server;
                    ServerGrid.SelectedIndex = selectedIndex;
                    ServerGrid.Items.Refresh();
                    break;
                }
            }

            DataGrid_RefreshElements();
        }

        public void Log(string serverId, string logText, bool debug = false)
        {
            string title = int.TryParse(serverId, out int i) ? $"#{i.ToString()}" : serverId;
            string log = $"[{DateTime.Now.ToString("MM/dd/yyyy-HH:mm:ss")}][{title}] {logText}" + Environment.NewLine;
            string logPath = ServerPath.GetLogs();
            Directory.CreateDirectory(logPath);
            string logFile = Path.Combine(logPath, $"L{DateTime.Now.ToString("yyyyMMdd")}.log");
            File.AppendAllText(logFile, log);

            textBox_wgsmlog.AppendText(log);
            textBox_wgsmlog.Text = RemovedOldLog(textBox_wgsmlog.Text);
            textBox_wgsmlog.ScrollToEnd();
        }

        private async Task<string> WaitForInstallerOutput(Process installer)
        {
            if (installer == null) { return string.Empty; }

            var output = new StringBuilder();
            var tasks = new List<Task>();

            if (installer.StartInfo.RedirectStandardOutput)
            {
                tasks.Add(ReadInstallerStream(installer.StandardOutput, output));
            }

            if (installer.StartInfo.RedirectStandardError)
            {
                tasks.Add(ReadInstallerStream(installer.StandardError, output));
            }

            tasks.Add(Task.Run(() => installer.WaitForExit()));
            await Task.WhenAll(tasks);

            return output.ToString();
        }

        private async Task ReadInstallerStream(StreamReader reader, StringBuilder output)
        {
            while (true)
            {
                string nextLine = await reader.ReadLineAsync();
                if (nextLine == null) { break; }

                if (nextLine.Contains("Logging in user "))
                {
                    nextLine += Environment.NewLine + "Please send the Login Token:";
                }

                lock (output)
                {
                    output.AppendLine(nextLine);
                }

                AppendInstallLogLine(nextLine);
            }
        }

        private void AppendInstallLogLine(string text)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                textbox_InstallLog.AppendText(text + Environment.NewLine);
                textbox_InstallLog.ScrollToEnd();
            });
        }

        public void DiscordBotLog(string logText)
        {
            string log = $"[{DateTime.Now.ToString("MM/dd/yyyy-HH:mm:ss")}] {logText}" + Environment.NewLine;
            string logPath = ServerPath.GetLogs();
            Directory.CreateDirectory(logPath);

            string logFile = Path.Combine(logPath, $"L{DateTime.Now.ToString("yyyyMMdd")}-DiscordBot.log");
            File.AppendAllText(logFile, log);

            textBox_DiscordBotLog.AppendText(log);
            textBox_DiscordBotLog.Text = RemovedOldLog(textBox_DiscordBotLog.Text);
            textBox_DiscordBotLog.ScrollToEnd();
        }

        private string WriteGameServerCrashLog(ServerTable server, Process process, out string exitCode)
        {
            exitCode = string.Empty;

            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string logDirectory = ServerPath.GetLogs("servers", server.ID);
                Directory.CreateDirectory(logDirectory);

                string crashLogPath = Path.Combine(logDirectory, $"crash_{timestamp}.log");
                var log = new StringBuilder();
                log.AppendLine($"WindowsGSM crash details");
                log.AppendLine($"Time: {DateTime.Now:MM/dd/yyyy-HH:mm:ss}");
                log.AppendLine($"Server ID: {server.ID}");
                log.AppendLine($"Server Name: {server.Name}");
                log.AppendLine($"Game: {server.Game}");
                log.AppendLine($"PID: {server.PID}");

                if (process != null)
                {
                    try
                    {
                        exitCode = process.ExitCode.ToString();
                        log.AppendLine($"Exit Code: {exitCode}");
                    }
                    catch (Exception e)
                    {
                        log.AppendLine($"Exit Code: unavailable ({e.Message})");
                    }

                    try
                    {
                        log.AppendLine($"Executable: {process.StartInfo.FileName}");
                        log.AppendLine($"Arguments: {process.StartInfo.Arguments}");
                        log.AppendLine($"Working Directory: {process.StartInfo.WorkingDirectory}");
                    }
                    catch (Exception e)
                    {
                        log.AppendLine($"Process start info unavailable: {e.Message}");
                    }
                }

                log.AppendLine();
                log.AppendLine("Captured console output:");
                string consoleOutput = GetServerMetadata(server.ID)?.ServerConsole?.Get();
                AppendLines(log, consoleOutput, 200);

                log.AppendLine();
                log.AppendLine("Recent server log files:");
                AppendRecentServerLogs(log, server.ID, 3, 200);

                File.WriteAllText(crashLogPath, log.ToString());
                return crashLogPath.Replace(WGSM_PATH, string.Empty).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception e)
            {
                return $"failed to write crash details ({e.Message})";
            }
        }

        private static void AppendLines(StringBuilder builder, string text, int maxLines)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine("(none captured)");
                return;
            }

            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (string line in lines.Skip(Math.Max(lines.Length - maxLines, 0)))
            {
                builder.AppendLine(line);
            }
        }

        private static void AppendRecentServerLogs(StringBuilder builder, string serverId, int maxFiles, int maxLinesPerFile)
        {
            string serverFilesPath = ServerPath.GetServersServerFiles(serverId);
            if (!Directory.Exists(serverFilesPath))
            {
                builder.AppendLine("(serverfiles directory not found)");
                return;
            }

            var files = Directory.EnumerateFiles(serverFilesPath, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                .Select(path => new FileInfo(path))
                .Where(file => file.Exists)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(maxFiles)
                .ToList();

            if (files.Count == 0)
            {
                builder.AppendLine("(no .log or .txt files found under serverfiles)");
                return;
            }

            foreach (var file in files)
            {
                builder.AppendLine();
                builder.AppendLine($"--- {file.FullName.Replace(serverFilesPath, string.Empty).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)} ({file.LastWriteTime:MM/dd/yyyy-HH:mm:ss}) ---");
                try
                {
                    string text = ReadSharedText(file.FullName);
                    AppendDiagnosticLines(builder, text, 50);
                    AppendLines(builder, text, maxLinesPerFile);
                }
                catch (Exception e)
                {
                    builder.AppendLine($"Failed to read log file: {e.Message}");
                }
            }
        }

        private static void AppendDiagnosticLines(StringBuilder builder, string text, int maxMatches)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string[] markers = { " ERR ", "ERROR", "EXC", "Exception", "Failed", "Invalid", "Could not", "denied", "shutting down" };
            var matches = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(line => markers.Any(marker => line.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0))
                .Take(maxMatches)
                .ToList();

            if (matches.Count == 0)
            {
                return;
            }

            builder.AppendLine("Diagnostic lines:");
            foreach (string line in matches)
            {
                builder.AppendLine(line);
            }

            builder.AppendLine();
            builder.AppendLine("Log tail:");
        }

        private static string ReadSharedText(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private string RemovedOldLog(string logText)
        {
            const int MAX_LOG_LINE = 50;
            int lineCount = logText.Count(f => f == '\n');
            return (lineCount > MAX_LOG_LINE) ? string.Join("\n", logText.Split('\n').Skip(lineCount - MAX_LOG_LINE).ToArray()) : logText;
        }

        private void Button_ClearServerConsole_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            _serverMetadata[int.Parse(server.ID)].ServerConsole.Clear();
            console.Clear();
        }

        private void Button_ClearWGSMLog_Click(object sender, RoutedEventArgs e)
        {
            textBox_wgsmlog.Clear();
        }

        public async Task<string> SendCommandAsync(ServerTable server, string command, int waitForDataInMs = 0)
        {
            Process p = GetServerMetadata(server.ID).Process;
            int.TryParse(server.ID, out var id);
            if (p == null) { return ""; }

            textbox_servercommand.Focusable = false;
            _serverMetadata[id].ServerConsole.StartRecorder();
            _serverMetadata[id].ServerConsole.Input(p, command, GetServerMetadata(server.ID).MainWindow);
            textbox_servercommand.Focusable = true;

            if (waitForDataInMs != 0)
            {
                await Task.Delay(waitForDataInMs);
                return _serverMetadata[id].ServerConsole.StopRecorder();
            }
            else
                return "Sent!";
        }

        private static bool IsValidIPAddress(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return false;
            }

            string[] splitValues = ip.Split('.');
            if (splitValues.Length != 4)
            {
                return false;
            }

            return splitValues.All(r => byte.TryParse(r, out byte tempForParsing));
        }

        private static bool IsValidPort(string port)
        {
            if (!int.TryParse(port, out int portnum))
            {
                return false;
            }

            return portnum > 1 && portnum < 65535;
        }

        #region Menu - Browse
        private void Browse_ServerBackups_Click(object sender, RoutedEventArgs e)
        {
            var server = (Functions.ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            Process.Start("explorer", Functions.ServerPath.GetBackups(server.ID));
        }

        private void Browse_BackupFiles_Click(object sender, RoutedEventArgs e)
        {
            var server = (Functions.ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            var backupConfig = new Functions.BackupConfig(server.ID);
            backupConfig.Open();
        }

        private void Browse_ServerConfigs_Click(object sender, RoutedEventArgs e)
        {
            var server = (Functions.ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string path = Functions.ServerPath.GetServersConfigs(server.ID);
            if (Directory.Exists(path))
            {
                Process.Start("explorer", path);
            }
        }

        private void Browse_ServerFiles_Click(object sender, RoutedEventArgs e)
        {
            var server = (Functions.ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string path = Functions.ServerPath.GetServersServerFiles(server.ID);
            if (Directory.Exists(path))
            {
                Process.Start("explorer", path);
            }
        }
        #endregion

        #region Top Bar Button
        private void Button_Website_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://windowsgsm.com/") { UseShellExecute = true });
        }

        private void Button_Discord_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://discord.gg/bGc7t2R") { UseShellExecute = true });
        }

        private void Button_Patreon_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.patreon.com/WindowsGSM/") { UseShellExecute = true });
        }

        private void Button_Settings_Click(object sender, RoutedEventArgs e)
        {
            ToggleMahappFlyout(MahAppFlyout_Settings);
        }

        private void Button_Hide_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(0);
            notifyIcon.Visible = false;
            notifyIcon.Visible = true;
        }
        #endregion

        #region Settings Flyout
        private void HardWareAcceleration_IsCheckedChanged(object sender, EventArgs e)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue(RegistryKeyName.HardWareAcceleration, MahAppSwitch_HardWareAcceleration.IsOn.ToString());
            }

            RenderOptions.ProcessRenderMode = MahAppSwitch_HardWareAcceleration.IsOn ? System.Windows.Interop.RenderMode.SoftwareOnly : System.Windows.Interop.RenderMode.Default;
        }

        private void UIAnimation_IsCheckedChanged(object sender, EventArgs e)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue(RegistryKeyName.UIAnimation, MahAppSwitch_UIAnimation.IsOn.ToString());
            }

            WindowTransitionsEnabled = MahAppSwitch_UIAnimation.IsOn;
        }

        private void DarkTheme_IsCheckedChanged(object sender, EventArgs e)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue(RegistryKeyName.DarkTheme, MahAppSwitch_DarkTheme.IsOn.ToString());
            }

            ThemeManager.Current.ChangeTheme(this, $"{(MahAppSwitch_DarkTheme.IsOn ? "Dark" : "Light")}.{comboBox_Themes.SelectedItem ?? DEFAULT_THEME}");
        }

        private void StartOnLogin_IsCheckedChanged(object sender, EventArgs e)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue(RegistryKeyName.StartOnBoot, MahAppSwitch_StartOnBoot.IsOn.ToString());
            }

            SetStartOnBoot(MahAppSwitch_StartOnBoot.IsOn);
        }
        private void MinimizeOnStart_IsCheckedChanged(object sender, EventArgs e)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue(RegistryKeyName.MinimizedOnStart, MahAppSwitch_MinimizeOnStart.IsOn.ToString());
            }
        }

        private void RestartOnCrash_IsCheckedChanged(object sender, EventArgs e)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue(RegistryKeyName.RestartOnCrash, MahAppSwitch_RestartOnCrash.IsOn.ToString());
            }
        }

        private void SendStatistics_IsCheckedChanged(object sender, EventArgs e)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue(RegistryKeyName.SendStatistics, MahAppSwitch_SendStatistics.IsOn.ToString());
            }
        }

        private void SetStartOnBoot(bool enable)
        {
            string taskName = "WindowsGSM";
            string wgsmPath = Process.GetCurrentProcess().MainModule.FileName;

            Process schtasks = new Process
            {
                StartInfo =
                {
                    FileName = "schtasks",
                    Arguments = enable ? $"/create /tn {taskName} /tr \"{wgsmPath}\" /sc onlogon /delay 0000:10 /rl HIGHEST /f" : $"/delete /tn {taskName} /f",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };
            schtasks.Start();
        }
        #endregion

        #region Donor Connect
        private async void DonorConnect_IsCheckedChanged(object sender, EventArgs e)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true);

            //If switch is checked
            if (!MahAppSwitch_DonorConnect.IsOn)
            {
                g_DonorType = string.Empty;
                comboBox_Themes.SelectedItem = DEFAULT_THEME;
                comboBox_Themes.IsEnabled = false;

                //Set theme
                ThemeManager.Current.ChangeTheme(this, $"{(MahAppSwitch_DarkTheme.IsOn ? "Dark" : "Light")}.{comboBox_Themes.SelectedItem}");

                key.SetValue(RegistryKeyName.DonorTheme, MahAppSwitch_DonorConnect.IsOn.ToString());
                key.SetValue(RegistryKeyName.DonorColor, DEFAULT_THEME);
                key.Close();
                return;
            }

            //If switch is not checked
            key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true);
            string authKey = (key.GetValue(RegistryKeyName.DonorAuthKey) == null) ? string.Empty : key.GetValue(RegistryKeyName.DonorAuthKey).ToString();

            var settings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Activate",
                DefaultText = authKey
            };

            authKey = await this.ShowInputAsync("Donor Connect (Patreon)", "Please enter the activation key.", settings);

            //If pressed cancel or key is null or whitespace
            if (string.IsNullOrWhiteSpace(authKey))
            {
                MahAppSwitch_DonorConnect.IsOn = false;
                key.Close();
                return;
            }

            ProgressDialogController controller = await this.ShowProgressAsync("Authenticating...", "Please wait...");
            controller.SetIndeterminate();
            (bool success, string name) = await AuthenticateDonor(authKey);
            await controller.CloseAsync();

            if (success)
            {
                key.SetValue(RegistryKeyName.DonorTheme, "True");
                key.SetValue(RegistryKeyName.DonorAuthKey, authKey);
                await this.ShowMessageAsync("Success!", $"Thanks for your donation {name}, your support help us a lot!\nYou can choose any theme you like on the Settings!");
            }
            else
            {
                key.SetValue(RegistryKeyName.DonorTheme, "False");
                key.SetValue(RegistryKeyName.DonorAuthKey, "");
                await this.ShowMessageAsync("Fail to activate.", "Please visit https://windowsgsm.com/patreon/ to get the key.");

                MahAppSwitch_DonorConnect.IsOn = false;
            }
            key.Close();
        }

        private async Task<(bool, string)> AuthenticateDonor(string authKey)
        {
            try
            {
                string json = await WindowsGSM.Functions.Http.DownloadStringAsync($"https://windowsgsm.com/patreon/patreonAuth.php?auth={authKey}");
                bool success = JObject.Parse(json)["success"].ToString() == "True";

                if (success)
                {
                    string name = JObject.Parse(json)["name"].ToString();
                    string type = JObject.Parse(json)["type"].ToString();

                    g_DonorType = type;

                    g_DiscordBot.SetDonorType(g_DonorType);
                    comboBox_Themes.IsEnabled = true;

                    ThemeManager.Current.ChangeTheme(this, $"{(MahAppSwitch_DarkTheme.IsOn ? "Dark" : "Light")}.{comboBox_Themes.SelectedItem}");

                    return (true, name);
                }

                MahAppSwitch_DonorConnect.IsOn = false;

                //Set theme
                ThemeManager.Current.ChangeTheme(this, $"{(MahAppSwitch_DarkTheme.IsOn ? "Dark" : "Light")}.{comboBox_Themes.SelectedItem}");
            }
            catch
            {
                // ignore
            }

            //Set theme
            ThemeManager.Current.ChangeTheme(this, $"{(MahAppSwitch_DarkTheme.IsOn ? "Dark" : "Light")}.{comboBox_Themes.SelectedItem}");

            return (false, string.Empty);
        }

        private void ComboBox_Themes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue(RegistryKeyName.DonorColor, comboBox_Themes.SelectedItem.ToString());
            }

            //Set theme
            ThemeManager.Current.ChangeTheme(this, $"{(MahAppSwitch_DarkTheme.IsOn ? "Dark" : "Light")}.{comboBox_Themes.SelectedItem}");
        }
        #endregion

        #region Menu - Help
        private void Help_OnlineDocumentation_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://docs.windowsgsm.com") { UseShellExecute = true });
        }

        private void Help_ReportIssue_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/raziel7893/WindowsGSM/issues") { UseShellExecute = true });
        }

        private async void Help_SoftwareUpdates_Click(object sender, RoutedEventArgs e)
        {
            ProgressDialogController controller = await this.ShowProgressAsync("Checking updates...", "Please wait...");
            controller.SetIndeterminate();
            string latestVersion = await GetLatestVersion();
            await controller.CloseAsync();

            if (string.IsNullOrEmpty(latestVersion))
            {
                await this.ShowMessageAsync("Software Updates", "Fail to get latest version, please try again later.");
                return;
            }

            if (latestVersion == WGSM_VERSION)
            {
                await this.ShowMessageAsync("Software Updates", "WindowsGSM is up to date.");
                return;
            }

            await this.ShowMessageAsync("Software Updates", $"Version {latestVersion} is available. Please update manually by replacing the exe found in https://github.com/Raziel7893/WindowsGSM/releases");
        }

        private async Task<string> GetLatestVersion()
        {
            try
            {
                string json = await WindowsGSM.Functions.Http.DownloadStringAsync("https://api.github.com/repos/Raziel7893/WindowsGSM/releases/latest");
                return JObject.Parse(json)["tag_name"].ToString();
            }
            catch
            {
                return null;
            }
        }

        private async void Help_AboutWindowsGSM_Click(object sender, RoutedEventArgs e)
        {
            var settings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Patreon",
                NegativeButtonText = "Ok",
                DefaultButtonFocus = MessageDialogResult.Negative
            };

            var result = await this.ShowMessageAsync("About WindowsGSM", $"Product:\t\tWindowsGSM\nVersion:\t\t{WGSM_VERSION.Substring(1)}\nCreator:\t\tTatLead\n\nIf you like WindowsGSM, consider becoming a Patron!", MessageDialogStyle.AffirmativeAndNegative, settings);

            if (result == MessageDialogResult.Affirmative)
            {
                Process.Start(new ProcessStartInfo("https://www.patreon.com/WindowsGSM/") { UseShellExecute = true });
            }
        }
        #endregion

        #region Menu - Tools
        private async void Tools_ReadinessCheck_Click(object sender, RoutedEventArgs e)
        {
            await RunReadinessChecks();
            MahAppFlyout_ReadinessCheck.IsOpen = true;
        }

        private async void Button_ReadinessCheck_Run_Click(object sender, RoutedEventArgs e)
        {
            await RunReadinessChecks();
        }

        private void Button_ReadinessCheck_Close_Click(object sender, RoutedEventArgs e)
        {
            MahAppFlyout_ReadinessCheck.IsOpen = false;
        }

        private async Task RunReadinessChecks()
        {
            stackPanel_ReadinessCheckResults.Children.Clear();
            textBlock_ReadinessCheckSummary.Text = "Running checks...";

            List<ReadinessCheckResult> results = await Task.Run(() =>
            {
                var checks = new List<ReadinessCheckResult>();
                AddAppReadinessChecks(checks);

                ServerTable selectedServer = null;
                List<ServerTable> servers = null;
                Dispatcher.Invoke(() =>
                {
                    selectedServer = ServerGrid.SelectedItem as ServerTable;
                    servers = ServerGrid.Items.Cast<ServerTable>().ToList();
                });
                if (selectedServer != null)
                {
                    AddServerReadinessChecks(checks, selectedServer, servers);
                }
                else
                {
                    checks.Add(new ReadinessCheckResult
                    {
                        Status = ReadinessStatus.Info,
                        Scope = "Server",
                        Name = "Selected server",
                        Message = "Select a server before running checks to include server-specific checks."
                    });
                }

                return checks;
            });

            foreach (ReadinessCheckResult result in results)
            {
                AddReadinessCheckRow(result);
            }

            int pass = results.Count(x => x.Status == ReadinessStatus.Pass);
            int warn = results.Count(x => x.Status == ReadinessStatus.Warning);
            int fail = results.Count(x => x.Status == ReadinessStatus.Fail);
            textBlock_ReadinessCheckSummary.Text = $"{pass} passed, {warn} warnings, {fail} failed";
        }

        private void AddAppReadinessChecks(List<ReadinessCheckResult> checks)
        {
            checks.Add(CreateWritablePathCheck("App", "WindowsGSM folder writable", WGSM_PATH));
            checks.Add(CreateWritablePathCheck("App", "Logs folder writable", ServerPath.GetLogs()));
            checks.Add(CreateWritablePathCheck("App", "Backups folder writable", ServerPath.Get(ServerPath.FolderName.Backups)));
            checks.Add(CreateAdminCheck());
            checks.Add(CreateSteamCmdCheck());
            checks.Add(CreateJavaCheck());
            checks.Add(CreateFirewallCheck());
            checks.Add(CreateDiskSpaceCheck());
            checks.Add(CreatePluginCheck());
            checks.Add(CreatePublicIpCheck());
        }

        private void AddServerReadinessChecks(List<ReadinessCheckResult> checks, ServerTable server, List<ServerTable> servers)
        {
            checks.Add(CreateDirectoryExistsCheck("Server", "Server folder", ServerPath.GetServers(server.ID)));
            checks.Add(CreateDirectoryExistsCheck("Server", "Server files folder", ServerPath.GetServersServerFiles(server.ID)));
            checks.Add(CreateDirectoryExistsCheck("Server", "Server configs folder", ServerPath.GetServersConfigs(server.ID)));
            checks.Add(CreateServerExecutableCheck(server));
            checks.Add(CreatePortCheck("Server", "Server port", server.Port));
            checks.Add(CreatePortCheck("Server", "Query port", server.QueryPort));
            checks.Add(CreatePortCollisionCheck(server, servers));

            var backupConfig = new BackupConfig(server.ID);
            checks.Add(CreateWritablePathCheck("Backup", "Backup location writable", string.IsNullOrWhiteSpace(backupConfig.BackupLocation) ? ServerPath.GetBackups(server.ID) : backupConfig.BackupLocation));

            foreach (string folder in backupConfig.SavesLocations ?? Enumerable.Empty<string>())
            {
                checks.Add(CreateBackupSourceCheck("Backup", "Backup folder", folder, expectedFile: false));
            }

            foreach (string file in backupConfig.FilesLocations ?? Enumerable.Empty<string>())
            {
                checks.Add(CreateBackupSourceCheck("Backup", "Backup file", file, expectedFile: true));
            }
        }

        private ReadinessCheckResult CreateAdminCheck()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    bool isAdmin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                    return new ReadinessCheckResult
                    {
                        Status = isAdmin ? ReadinessStatus.Pass : ReadinessStatus.Warning,
                        Scope = "App",
                        Name = "Administrator permissions",
                        Message = isAdmin ? "WindowsGSM is running as administrator." : "WindowsGSM is not running as administrator. Some install/firewall operations may fail."
                    };
                }
            }
            catch (Exception ex)
            {
                return FailCheck("App", "Administrator permissions", ex.Message);
            }
        }

        private ReadinessCheckResult CreateSteamCmdCheck()
        {
            string steamCmdPath = ServerPath.GetBin("steamcmd", "steamcmd.exe");
            bool exists = File.Exists(steamCmdPath);
            return new ReadinessCheckResult
            {
                Status = exists ? ReadinessStatus.Pass : ReadinessStatus.Warning,
                Scope = "App",
                Name = "SteamCMD",
                Message = exists ? $"Found {steamCmdPath}" : "SteamCMD is not installed yet. It will be downloaded during SteamCMD server install/update."
            };
        }

        private ReadinessCheckResult CreateJavaCheck()
        {
            try
            {
                string javaPath = JavaHelper.FindJavaExecutableAbsolutePath();
                return new ReadinessCheckResult
                {
                    Status = string.IsNullOrWhiteSpace(javaPath) ? ReadinessStatus.Warning : ReadinessStatus.Pass,
                    Scope = "App",
                    Name = "Java runtime",
                    Message = string.IsNullOrWhiteSpace(javaPath) ? "Java was not detected. Minecraft Java servers may need Java installed." : $"Found {javaPath}"
                };
            }
            catch (Exception ex)
            {
                return new ReadinessCheckResult
                {
                    Status = ReadinessStatus.Warning,
                    Scope = "App",
                    Name = "Java runtime",
                    Message = "Java detection failed: " + ex.Message
                };
            }
        }

        private ReadinessCheckResult CreateFirewallCheck()
        {
            try
            {
                Type managerType = Type.GetTypeFromProgID("HNetCfg.FwMgr");
                if (managerType == null)
                {
                    return FailCheck("App", "Windows Firewall API", "Firewall COM API is unavailable.");
                }

                dynamic manager = Activator.CreateInstance(managerType);
                bool enabled = manager.LocalPolicy.CurrentProfile.FirewallEnabled;
                return new ReadinessCheckResult
                {
                    Status = ReadinessStatus.Pass,
                    Scope = "App",
                    Name = "Windows Firewall API",
                    Message = enabled ? "Firewall API is accessible and firewall is enabled." : "Firewall API is accessible and firewall is currently disabled."
                };
            }
            catch (Exception ex)
            {
                return new ReadinessCheckResult
                {
                    Status = ReadinessStatus.Warning,
                    Scope = "App",
                    Name = "Windows Firewall API",
                    Message = "Firewall API check failed. Firewall rule changes may fail: " + ex.Message
                };
            }
        }

        private ReadinessCheckResult CreateDiskSpaceCheck()
        {
            try
            {
                string root = Path.GetPathRoot(WGSM_PATH);
                DriveInfo drive = new DriveInfo(root);
                long freeGb = drive.AvailableFreeSpace / 1024 / 1024 / 1024;
                return new ReadinessCheckResult
                {
                    Status = freeGb < 5 ? ReadinessStatus.Warning : ReadinessStatus.Pass,
                    Scope = "App",
                    Name = "Disk space",
                    Message = $"{freeGb} GB available on {root}"
                };
            }
            catch (Exception ex)
            {
                return new ReadinessCheckResult
                {
                    Status = ReadinessStatus.Warning,
                    Scope = "App",
                    Name = "Disk space",
                    Message = "Disk space check failed: " + ex.Message
                };
            }
        }

        private ReadinessCheckResult CreatePluginCheck()
        {
            int failed = PluginsList.Count(plugin => !plugin.IsLoaded);
            return new ReadinessCheckResult
            {
                Status = failed == 0 ? ReadinessStatus.Pass : ReadinessStatus.Warning,
                Scope = "App",
                Name = "Plugins",
                Message = failed == 0 ? $"{PluginsList.Count} plugin(s) loaded." : $"{failed} plugin(s) failed to load. Check logs\\plugins."
            };
        }

        private ReadinessCheckResult CreatePublicIpCheck()
        {
            try
            {
                string publicIp = WindowsGSM.Functions.Http.DownloadStringAsync("https://ipinfo.io/ip").GetAwaiter().GetResult().Trim();
                return new ReadinessCheckResult
                {
                    Status = string.IsNullOrWhiteSpace(publicIp) ? ReadinessStatus.Warning : ReadinessStatus.Pass,
                    Scope = "Network",
                    Name = "Public IP lookup",
                    Message = string.IsNullOrWhiteSpace(publicIp) ? "Public IP lookup returned no value." : $"Public IP lookup works: {publicIp}"
                };
            }
            catch (Exception ex)
            {
                return new ReadinessCheckResult
                {
                    Status = ReadinessStatus.Warning,
                    Scope = "Network",
                    Name = "Public IP lookup",
                    Message = "Public IP lookup failed: " + ex.Message
                };
            }
        }

        private ReadinessCheckResult CreateServerExecutableCheck(ServerTable server)
        {
            try
            {
                dynamic gameServer = GameServer.Data.Class.Get(server.Game, new ServerConfig(server.ID), PluginsList);
                if (gameServer == null)
                {
                    return FailCheck("Server", "Server executable", "Game/plugin definition could not be loaded.");
                }

                string startPath = ServerPath.GetServersServerFiles(server.ID, gameServer.StartPath);
                return new ReadinessCheckResult
                {
                    Status = File.Exists(startPath) ? ReadinessStatus.Pass : ReadinessStatus.Fail,
                    Scope = "Server",
                    Name = "Server executable",
                    Message = File.Exists(startPath) ? $"Found {startPath}" : $"Missing {startPath}"
                };
            }
            catch (Exception ex)
            {
                return FailCheck("Server", "Server executable", ex.Message);
            }
        }

        private ReadinessCheckResult CreatePortCollisionCheck(ServerTable server, List<ServerTable> servers)
        {
            var collisions = (servers ?? new List<ServerTable>())
                .Where(other => other.ID != server.ID)
                .Where(other =>
                    (!string.IsNullOrWhiteSpace(server.Port) && (server.Port == other.Port || server.Port == other.QueryPort)) ||
                    (!string.IsNullOrWhiteSpace(server.QueryPort) && (server.QueryPort == other.Port || server.QueryPort == other.QueryPort)))
                .Select(other => $"{other.ID}: {other.Name}")
                .ToList();

            return new ReadinessCheckResult
            {
                Status = collisions.Count == 0 ? ReadinessStatus.Pass : ReadinessStatus.Warning,
                Scope = "Server",
                Name = "Port collisions",
                Message = collisions.Count == 0 ? "No game/query port collisions found." : "Possible collision with " + string.Join(", ", collisions)
            };
        }

        private ReadinessCheckResult CreatePortCheck(string scope, string name, string value)
        {
            bool valid = int.TryParse(value, out int port) && port >= 1 && port <= 65535;
            return new ReadinessCheckResult
            {
                Status = valid ? ReadinessStatus.Pass : ReadinessStatus.Fail,
                Scope = scope,
                Name = name,
                Message = valid ? $"{value} is valid." : $"{value} is not a valid TCP/UDP port."
            };
        }

        private ReadinessCheckResult CreateDirectoryExistsCheck(string scope, string name, string path)
        {
            return new ReadinessCheckResult
            {
                Status = Directory.Exists(path) ? ReadinessStatus.Pass : ReadinessStatus.Fail,
                Scope = scope,
                Name = name,
                Message = Directory.Exists(path) ? $"Found {path}" : $"Missing {path}"
            };
        }

        private ReadinessCheckResult CreateBackupSourceCheck(string scope, string name, string path, bool expectedFile)
        {
            bool exists = expectedFile ? File.Exists(path) : Directory.Exists(path);
            return new ReadinessCheckResult
            {
                Status = exists ? ReadinessStatus.Pass : ReadinessStatus.Warning,
                Scope = scope,
                Name = name,
                Message = exists ? $"Found {path}" : $"Missing {path}. It may be generated after the first server start."
            };
        }

        private ReadinessCheckResult CreateWritablePathCheck(string scope, string name, string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                string probe = Path.Combine(path, $".wgsm_write_test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probe, "test");
                File.Delete(probe);
                return new ReadinessCheckResult
                {
                    Status = ReadinessStatus.Pass,
                    Scope = scope,
                    Name = name,
                    Message = $"Writable: {path}"
                };
            }
            catch (Exception ex)
            {
                return FailCheck(scope, name, $"{path} is not writable: {ex.Message}");
            }
        }

        private static ReadinessCheckResult FailCheck(string scope, string name, string message)
        {
            return new ReadinessCheckResult
            {
                Status = ReadinessStatus.Fail,
                Scope = scope,
                Name = name,
                Message = message
            };
        }

        private void AddReadinessCheckRow(ReadinessCheckResult result)
        {
            var border = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = Brushes.LightGray,
                Padding = new Thickness(0, 8, 0, 8)
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            AddReadinessCell(row, GetReadinessStatusText(result.Status), 0, GetReadinessBrush(result.Status), FontWeights.Bold);
            AddReadinessCell(row, result.Scope, 1, Brushes.DimGray, FontWeights.Normal);
            AddReadinessCell(row, result.Name, 2, Brushes.Black, FontWeights.SemiBold);
            AddReadinessCell(row, result.Message, 3, Brushes.Black, FontWeights.Normal, true);

            border.Child = row;
            stackPanel_ReadinessCheckResults.Children.Add(border);
        }

        private static void AddReadinessCell(Grid row, string text, int column, Brush foreground, FontWeight weight, bool wrap = false)
        {
            var block = new TextBlock
            {
                Text = text ?? string.Empty,
                Foreground = foreground,
                FontWeight = weight,
                Margin = new Thickness(0, 0, 10, 0),
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap
            };

            Grid.SetColumn(block, column);
            row.Children.Add(block);
        }

        private static string GetReadinessStatusText(ReadinessStatus status)
        {
            switch (status)
            {
                case ReadinessStatus.Pass: return "PASS";
                case ReadinessStatus.Warning: return "WARN";
                case ReadinessStatus.Fail: return "FAIL";
                default: return "INFO";
            }
        }

        private static Brush GetReadinessBrush(ReadinessStatus status)
        {
            switch (status)
            {
                case ReadinessStatus.Pass: return Brushes.ForestGreen;
                case ReadinessStatus.Warning: return Brushes.DarkOrange;
                case ReadinessStatus.Fail: return Brushes.Firebrick;
                default: return Brushes.SteelBlue;
            }
        }

        private void Tools_GlobalServerListCheck_Click(object sender, RoutedEventArgs e)
        {
            var row = (ServerTable)ServerGrid.SelectedItem;
            if (row == null) { return; }

            if (row.Game == GameServer.MCPE.FullName || row.Game == GameServer.MC.FullName)
            {
                Log(row.ID, $"This feature is not applicable on {row.Game}");
                return;
            }

            string publicIP = GetPublicIP();
            if (publicIP == null)
            {
                Log(row.ID, "Fail to check. Reason: Fail to get the public ip.");
                return;
            }

            string messageText = $"Server Name: {row.Name}\nPublic IP: {publicIP}\nQuery Port: {row.QueryPort}";
            if (GlobalServerList.IsServerOnSteamServerList(publicIP, row.QueryPort))
            {
                MessageBox.Show(messageText + "\n\nResult: Online\n\nYour server is on the global server list!", "Global Server List Check", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(messageText + "\n\nResult: Offline\n\nYour server is not on the global server list.", "Global Server List Check", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Tool_InstallAMXModXMetamodP_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            bool? existed = InstallAddons.IsAMXModXAndMetaModPExists(server);
            if (existed == null)
            {
                await this.ShowMessageAsync("Tools - Install AMX Mod X & MetaMod-P", $"Doesn't support on {server.Game} (ID: {server.ID})");
                return;
            }

            if (existed == true)
            {
                await this.ShowMessageAsync("Tools - Install AMX Mod X & MetaMod-P", $"Already Installed (ID: {server.ID})");
                return;
            }

            var result = await this.ShowMessageAsync("Tools - Install AMX Mod X & MetaMod-P", $"Are you sure to install? (ID: {server.ID})", MessageDialogStyle.AffirmativeAndNegative);
            if (result == MessageDialogResult.Affirmative)
            {
                ProgressDialogController controller = await this.ShowProgressAsync("Installing...", "Please wait...");
                controller.SetIndeterminate();
                bool installed = await InstallAddons.AMXModXAndMetaModP(server);
                await controller.CloseAsync();

                string message = installed ? $"Installed successfully" : $"Fail to install";
                await this.ShowMessageAsync("Tools - Install AMX Mod X & MetaMod-P", $"{message} (ID: {server.ID})");
            }
        }

        private async void Tools_InstallSourcemodMetamod_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            bool? existed = InstallAddons.IsSourceModAndMetaModExists(server);
            if (existed == null)
            {
                await this.ShowMessageAsync("Tools - Install SourceMod & MetaMod", $"Doesn't support on {server.Game} (ID: {server.ID})");
                return;
            }

            if (existed == true)
            {
                await this.ShowMessageAsync("Tools - Install SourceMod & MetaMod", $"Already Installed (ID: {server.ID})");
                return;
            }

            var result = await this.ShowMessageAsync("Tools - Install SourceMod & MetaMod", $"Are you sure to install? (ID: {server.ID})", MessageDialogStyle.AffirmativeAndNegative);
            if (result == MessageDialogResult.Affirmative)
            {
                var controller = await this.ShowProgressAsync("Installing...", "Please wait...");
                controller.SetIndeterminate();
                bool installed = await InstallAddons.SourceModAndMetaMod(server);
                await controller.CloseAsync();

                var message = installed ? $"Installed successfully" : $"Fail to install";
                await this.ShowMessageAsync("Tools - Install SourceMod & MetaMod", $"{message} (ID: {server.ID})");
            }
        }

        private async void Tools_InstallDayZSALModServer_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            bool? existed = InstallAddons.IsDayZSALModServerExists(server);
            if (existed == null)
            {
                await this.ShowMessageAsync("Tools - Install DayZSAL Mod Server", $"Doesn't support on {server.Game} (ID: {server.ID})");
                return;
            }

            if (existed == true)
            {
                await this.ShowMessageAsync("Tools - Install DayZSAL Mod Server", $"Already Installed (ID: {server.ID})");
                return;
            }

            var result = await this.ShowMessageAsync("Tools - Install DayZSAL Mod Server", $"Are you sure to install? (ID: {server.ID})", MessageDialogStyle.AffirmativeAndNegative);
            if (result == MessageDialogResult.Affirmative)
            {
                ProgressDialogController controller = await this.ShowProgressAsync("Installing...", "Please wait...");
                controller.SetIndeterminate();
                bool installed = await InstallAddons.DayZSALModServer(server);
                await controller.CloseAsync();

                string message = installed ? $"Installed successfully" : $"Fail to install";
                await this.ShowMessageAsync("Tools - Install DayZSAL Mod Server", $"{message} (ID: {server.ID})");
            }
        }

        private async void Tools_InstallOxideMod_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string messageTitle = "Tools - Install OxideMod";

            bool? existed = InstallAddons.IsOxideModExists(server);
            if (existed == null)
            {
                await this.ShowMessageAsync(messageTitle, $"Doesn't support on {server.Game} (ID: {server.ID})");
                return;
            }

            if (existed == true)
            {
                await this.ShowMessageAsync(messageTitle, $"Already Installed (ID: {server.ID})");
                return;
            }

            var result = await this.ShowMessageAsync(messageTitle, $"Are you sure to install? (ID: {server.ID})", MessageDialogStyle.AffirmativeAndNegative);
            if (result == MessageDialogResult.Affirmative)
            {
                ProgressDialogController controller = await this.ShowProgressAsync("Installing...", "Please wait...");
                controller.SetIndeterminate();
                bool installed = await InstallAddons.OxideMod(server);
                await controller.CloseAsync();

                string message = installed ? $"Installed successfully" : $"Fail to install";
                await this.ShowMessageAsync(messageTitle, $"{message} (ID: {server.ID})");
            }
        }

        private async void Tools_InstallWindrosePlus_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string messageTitle = "Tools - Install Windrose+";

            bool? existed = InstallAddons.IsWindrosePlusExists(server);
            if (existed == null)
            {
                await this.ShowMessageAsync(messageTitle, $"Doesn't support on {server.Game} (ID: {server.ID})");
                return;
            }

            if (existed == true)
            {
                var reinstallResult = await this.ShowMessageAsync(messageTitle, $"Windrose+ is already installed. Reinstall/upgrade it? (ID: {server.ID})", MessageDialogStyle.AffirmativeAndNegative);
                if (reinstallResult != MessageDialogResult.Affirmative)
                {
                    return;
                }
            }
            else
            {
                var result = await this.ShowMessageAsync(messageTitle, $"Are you sure to install? (ID: {server.ID})", MessageDialogStyle.AffirmativeAndNegative);
                if (result != MessageDialogResult.Affirmative)
                {
                    return;
                }
            }

            ProgressDialogController controller = await this.ShowProgressAsync("Installing...", "Downloading Windrose+ and running install.ps1...");
            controller.SetIndeterminate();
            bool installed = await InstallAddons.WindrosePlus(server);
            await controller.CloseAsync();

            string message = installed
                ? "Installed successfully. Windrose+ will be prepared automatically before the next Windrose server start."
                : "Fail to install";
            await this.ShowMessageAsync(messageTitle, $"{message} (ID: {server.ID})");
        }
        #endregion

        public static string GetPublicIP()
        {
            try
            {
                return WindowsGSM.Functions.Http.DownloadString("https://ipinfo.io/ip").Replace("\n", string.Empty);
            }
            catch
            {
                return null;
            }
        }

        private void OnBalloonTipClick(object sender, EventArgs e)
        {
        }

        private void NotifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
                Focus();
            }
        }

        #region Left Buttom Grid
        private void Slider_ProcessPriority_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            _serverMetadata[int.Parse(server.ID)].CPUPriority = ((int)slider_ProcessPriority.Value).ToString();
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.CPUPriority, GetServerMetadata(server.ID).CPUPriority);
            textBox_ProcessPriority.Text = Functions.CPU.Priority.GetPriorityByInteger((int)slider_ProcessPriority.Value);

            if (GetServerMetadata(server.ID).Process != null && !GetServerMetadata(server.ID).Process.HasExited)
            {
                _serverMetadata[int.Parse(server.ID)].Process = Functions.CPU.Priority.SetProcessWithPriority(GetServerMetadata(server.ID).Process, (int)slider_ProcessPriority.Value);
            }
        }

        private void Button_SetAffinity_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            ToggleMahappFlyout(MahAppFlyout_SetAffinity);
        }

        private void Button_EditConfig_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            if (Refresh_EditConfig_Data(server.ID))
            {
                ToggleMahappFlyout(MahAppFlyout_EditConfig);
            }
            else
            {
                MahAppFlyout_EditConfig.IsOpen = false;
            }
        }

        private void ServerGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var server = ServerGrid.SelectedItem as ServerTable;
            if (server == null) { return; }

            if (Refresh_EditConfig_Data(server.ID))
            {
                MahAppFlyout_EditConfig.IsOpen = true;
            }
        }

        private bool Refresh_EditConfig_Data(string serverId)
        {
            var serverConfig = new ServerConfig(serverId);
            if (string.IsNullOrWhiteSpace(serverConfig.ServerGame)) { return false; }
            var gameServer = GameServer.Data.Class.Get(serverConfig.ServerGame, pluginList: PluginsList);
            if (gameServer == null) { return false; }

            textbox_EC_ServerID.Text = serverConfig.ServerID;
            textbox_EC_ServerGame.Text = serverConfig.ServerGame;
            textbox_EC_ServerName.Text = serverConfig.ServerName;
            textbox_EC_ServerIP.Text = serverConfig.ServerIP;
            numericUpDown_EC_ServerMaxplayer.Value = int.TryParse(serverConfig.ServerMaxPlayer, out var maxplayer) ? maxplayer : int.Parse(gameServer.Maxplayers);
            numericUpDown_EC_ServerPort.Value = int.TryParse(serverConfig.ServerPort, out var port) ? port : int.Parse(gameServer.Port);
            numericUpDown_EC_ServerQueryPort.Value = int.TryParse(serverConfig.ServerQueryPort, out var queryPort) ? queryPort : int.Parse(gameServer.QueryPort);
            textbox_EC_ServerMap.Text = serverConfig.ServerMap;
            textbox_EC_ServerGSLT.Text = serverConfig.ServerGSLT;
            bool isSteamServer = !string.IsNullOrWhiteSpace(GetSteamAppId(gameServer));
            stackPanel_EC_SteamBranch.Visibility = isSteamServer ? Visibility.Visible : Visibility.Collapsed;
            PopulateSteamBranchComboBox(comboBox_EC_SteamBranch, serverConfig.SteamBranch);
            textbox_EC_SteamBranchPassword.Text = serverConfig.SteamBranchPassword ?? string.Empty;
            textbox_EC_SteamBranchLastInstalled.Text = serverConfig.SteamBranchLastInstalled ?? string.Empty;
            Refresh_EditConfig_CustomSettings(serverConfig, gameServer);
            textbox_EC_ServerParam.Text = serverConfig.ServerParam;
            return true;
        }

        private void Refresh_EditConfig_CustomSettings(ServerConfig serverConfig, dynamic gameServer)
        {
            _editConfigCustomSettingControls.Clear();
            stackPanel_EC_CustomSettings.Children.Clear();

            var customSettings = GetCustomServerSettings(gameServer);
            _editConfigUsesCustomServerSettingSchema = UsesCustomServerSettingSchema(gameServer);

            stackPanel_EC_ServerName.Visibility = _editConfigUsesCustomServerSettingSchema ? Visibility.Collapsed : Visibility.Visible;
            stackPanel_EC_ServerNetwork.Visibility = _editConfigUsesCustomServerSettingSchema ? Visibility.Collapsed : Visibility.Visible;
            stackPanel_EC_ServerMap.Visibility = _editConfigUsesCustomServerSettingSchema ? Visibility.Collapsed : Visibility.Visible;
            stackPanel_EC_ServerGSLT.Visibility = _editConfigUsesCustomServerSettingSchema ? Visibility.Collapsed : Visibility.Visible;

            MahAppFlyout_EditConfig.Width = _editConfigUsesCustomServerSettingSchema ? 1060 : double.NaN;
            scrollViewer_EC_CustomSettings.Width = _editConfigUsesCustomServerSettingSchema ? 1020 : 500;
            scrollViewer_EC_CustomSettings.MaxHeight = _editConfigUsesCustomServerSettingSchema ? 520 : 220;
            textbox_EC_ServerParam.Width = _editConfigUsesCustomServerSettingSchema ? 1020 : 500;
            textbox_EC_ServerParam.Height = _editConfigUsesCustomServerSettingSchema ? 120 : double.NaN;
            button_EC_SaveConfig.Width = _editConfigUsesCustomServerSettingSchema ? 1020 : 500;
            double editFieldWidth = _editConfigUsesCustomServerSettingSchema ? 1020 : 500;
            comboBox_EC_SteamBranch.Width = editFieldWidth - 85;
            textbox_EC_SteamBranchPassword.Width = editFieldWidth;
            textbox_EC_SteamBranchLastInstalled.Width = editFieldWidth;

            stackPanel_EC_CustomSettingsContainer.Visibility = customSettings.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (customSettings.Count == 0) { return; }

            System.Windows.Controls.Panel settingsPanel = stackPanel_EC_CustomSettings;
            if (_editConfigUsesCustomServerSettingSchema)
            {
                var wrapPanel = new System.Windows.Controls.WrapPanel
                {
                    Orientation = Orientation.Horizontal,
                    ItemWidth = 335
                };
                stackPanel_EC_CustomSettings.Children.Add(wrapPanel);
                settingsPanel = wrapPanel;
            }

            foreach (var setting in customSettings)
            {
                string value = ServerConfig.GetSetting(serverConfig.ServerID, setting.Key);
                if (string.IsNullOrWhiteSpace(value))
                {
                    value = setting.DefaultValue ?? string.Empty;
                }

                var row = new StackPanel
                {
                    Margin = _editConfigUsesCustomServerSettingSchema
                        ? new Thickness(0, 0, 10, 8)
                        : new Thickness(0, 0, 0, 8)
                };
                row.Children.Add(new Label { Content = string.IsNullOrWhiteSpace(setting.Label) ? setting.Key : setting.Label, Padding = new Thickness(0, 0, 0, 3) });

                System.Windows.Controls.Control editor = CreateCustomSettingEditor(setting, value, _editConfigUsesCustomServerSettingSchema ? 310 : 480);

                row.Children.Add(editor);
                settingsPanel.Children.Add(row);
                _editConfigCustomSettingControls[setting.Key] = editor;
            }
        }

        private static System.Windows.Controls.Control CreateCustomSettingEditor(CustomServerSetting setting, string value, double width)
        {
            var options = setting.Options?.Where(option => option != null).ToList() ?? new List<string>();
            if (options.Count > 0)
            {
                var comboBox = new System.Windows.Controls.ComboBox
                {
                    Height = 25,
                    Width = width,
                    FontFamily = new FontFamily("Consolas"),
                    VerticalAlignment = VerticalAlignment.Top
                };

                foreach (string option in options)
                {
                    comboBox.Items.Add(option);
                }

                comboBox.SelectedItem = options.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? options.FirstOrDefault();
                return comboBox;
            }

            bool isBackupList =
                string.Equals(setting.Key, "saveslocation", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(setting.Key, "fileslocation", StringComparison.OrdinalIgnoreCase);

            return new System.Windows.Controls.TextBox
            {
                Height = isBackupList ? 58 : 23,
                Width = width,
                TextWrapping = TextWrapping.Wrap,
                Text = value,
                FontFamily = new FontFamily("Consolas"),
                AcceptsReturn = false,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalAlignment = VerticalAlignment.Top
            };
        }

        private static string GetCustomSettingEditorValue(System.Windows.Controls.Control editor)
        {
            if (editor is System.Windows.Controls.ComboBox comboBox)
            {
                return comboBox.SelectedItem?.ToString()?.Trim() ?? string.Empty;
            }

            if (editor is System.Windows.Controls.TextBox textBox)
            {
                return textBox.Text.Trim();
            }

            return string.Empty;
        }

        private static bool UsesCustomServerSettingSchema(dynamic gameServer)
        {
            if (gameServer == null) { return false; }

            object rawSettings = GetMemberValue(gameServer, "CustomSettings");
            if (rawSettings == null || rawSettings is string) { return false; }

            if (rawSettings is CustomServerSetting) { return true; }

            if (rawSettings is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    if (item is CustomServerSetting) { return true; }
                }
            }

            return false;
        }

        private static List<CustomServerSetting> GetCustomServerSettings(dynamic gameServer)
        {
            var settings = new List<CustomServerSetting>();
            if (gameServer == null) { return settings; }

            bool usesCustomServerSettingSchema = UsesCustomServerSettingSchema(gameServer);
            object rawSettings = GetMemberValue(gameServer, "CustomSettings");
            if (rawSettings == null) { return settings; }

            if (rawSettings is IEnumerable enumerable && !(rawSettings is string))
            {
                foreach (object item in enumerable)
                {
                    AddCustomServerSetting(settings, item);
                }
            }
            else
            {
                AddCustomServerSetting(settings, rawSettings);
            }

            return settings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.Key)
                    && !string.Equals(setting.Key, ServerConfig.SettingName.ServerParam, StringComparison.OrdinalIgnoreCase)
                    && (usesCustomServerSettingSchema || !ServerConfig.IsBuiltInSetting(setting.Key)))
                .GroupBy(setting => setting.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static void AddCustomServerSetting(List<CustomServerSetting> settings, object item)
        {
            if (item == null) { return; }

            if (item is CustomServerSetting customServerSetting)
            {
                settings.Add(NormalizeCustomServerSetting(customServerSetting));
                return;
            }

            if (item is string key)
            {
                settings.Add(new CustomServerSetting(key));
                return;
            }

            string reflectedKey = GetMemberValue(item, "Key")?.ToString()
                ?? GetMemberValue(item, "Name")?.ToString()
                ?? GetMemberValue(item, "SettingName")?.ToString();

            if (string.IsNullOrWhiteSpace(reflectedKey)) { return; }

            settings.Add(new CustomServerSetting
            {
                Key = reflectedKey,
                Label = GetMemberValue(item, "Label")?.ToString()
                    ?? GetMemberValue(item, "DisplayName")?.ToString()
                    ?? reflectedKey,
                DefaultValue = GetMemberValue(item, "DefaultValue")?.ToString()
                    ?? GetMemberValue(item, "Default")?.ToString()
                    ?? string.Empty,
                Options = GetCustomServerSettingOptions(item)
            });
        }

        private static CustomServerSetting NormalizeCustomServerSetting(CustomServerSetting setting)
        {
            setting.Label = string.IsNullOrWhiteSpace(setting.Label) ? setting.Key : setting.Label;
            setting.DefaultValue = setting.DefaultValue ?? string.Empty;
            setting.Options = setting.Options ?? new string[0];
            return setting;
        }

        private static string[] GetCustomServerSettingOptions(object item)
        {
            object rawOptions = GetMemberValue(item, "Options")
                ?? GetMemberValue(item, "Values")
                ?? GetMemberValue(item, "AllowedValues");

            if (rawOptions == null || rawOptions is string)
            {
                return new string[0];
            }

            if (rawOptions is IEnumerable enumerable)
            {
                return enumerable
                    .Cast<object>()
                    .Where(option => option != null)
                    .Select(option => option.ToString())
                    .ToArray();
            }

            return new string[0];
        }

        private static object GetMemberValue(object source, string memberName)
        {
            if (source == null) { return null; }

            var type = source.GetType();
            var property = type.GetProperty(memberName);
            if (property != null) { return property.GetValue(source); }

            var field = type.GetField(memberName);
            return field?.GetValue(source);
        }

        private void Button_EditConfig_Save_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerGame, textbox_EC_ServerGame.Text.Trim());

            if (!_editConfigUsesCustomServerSettingSchema)
            {
                ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerName, textbox_EC_ServerName.Text.Trim());
                ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerIP, textbox_EC_ServerIP.Text.Trim());
                ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerMaxPlayer, numericUpDown_EC_ServerMaxplayer.Value.ToString());
                ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerPort, numericUpDown_EC_ServerPort.Value.ToString());
                ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerQueryPort, numericUpDown_EC_ServerQueryPort.Value.ToString());
                ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerMap, textbox_EC_ServerMap.Text.Trim());
                ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerGSLT, textbox_EC_ServerGSLT.Text.Trim());
            }

            foreach (var customSetting in _editConfigCustomSettingControls)
            {
                ServerConfig.SetSetting(server.ID, customSetting.Key, GetCustomSettingEditorValue(customSetting.Value));
            }

            if (stackPanel_EC_SteamBranch.Visibility == Visibility.Visible)
            {
                ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.SteamBranch, GetComboBoxSteamBranch(comboBox_EC_SteamBranch));
                ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.SteamBranchPassword, textbox_EC_SteamBranchPassword.Text.Trim());
            }

            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.ServerParam, textbox_EC_ServerParam.Text.Trim());

            LoadServerTable();
            MahAppFlyout_EditConfig.IsOpen = false;
        }

        private void Button_RestartCrontab_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].RestartCrontab = switch_restartcrontab.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.RestartCrontab, GetServerMetadata(server.ID).RestartCrontab ? "1" : "0");
        }

        private void Button_EmbedConsole_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].EmbedConsole = switch_embedconsole.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.EmbedConsole, GetServerMetadata(server.ID).EmbedConsole ? "1" : "0");
        }

        private void Button_AutoRestart_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].AutoRestart = switch_autorestart.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoRestart, GetServerMetadata(server.ID).AutoRestart ? "1" : "0");
        }

        private void Button_AutoStart_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].AutoStart = switch_autostart.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoStart, GetServerMetadata(server.ID).AutoStart ? "1" : "0");
        }

        private void Button_AutoUpdate_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].AutoUpdate = switch_autoupdate.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoUpdate, GetServerMetadata(server.ID).AutoUpdate ? "1" : "0");
        }

        private async void Button_DiscordAlertSettings_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            ToggleMahappFlyout(MahAppFlyout_DiscordAlert);
        }

        private void Button_UpdateOnStart_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].UpdateOnStart = switch_updateonstart.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.UpdateOnStart, GetServerMetadata(server.ID).UpdateOnStart ? "1" : "0");
        }

        private void Button_BackupOnStart_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].BackupOnStart = switch_backuponstart.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.BackupOnStart, GetServerMetadata(server.ID).BackupOnStart ? "1" : "0");
        }

        private void Button_DiscordAlert_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].DiscordAlert = switch_discordalert.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.DiscordAlert, GetServerMetadata(server.ID).DiscordAlert ? "1" : "0");
            button_discordtest.IsEnabled = GetServerMetadata(server.ID).DiscordAlert;
        }

        private async void Button_CrontabEdit_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            string crontabFormat = ServerConfig.GetSetting(server.ID, ServerConfig.SettingName.CrontabFormat);

            var settings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Save",
                DefaultText = crontabFormat
            };

            crontabFormat = await this.ShowInputAsync("Crontab Format", "Please enter the crontab expressions", settings);
            if (crontabFormat == null) { return; } //If pressed cancel

            _serverMetadata[int.Parse(server.ID)].CrontabFormat = crontabFormat;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.CrontabFormat, crontabFormat);

            textBox_restartcrontab.Text = crontabFormat;
            textBox_nextcrontab.Text = CrontabSchedule.TryParse(crontabFormat)?.GetNextOccurrence(DateTime.Now).ToString("ddd, MM/dd/yyyy HH:mm:ss") ?? string.Empty;
        }
        #endregion

        #region Switches
        private void Switch_AutoStartAlert_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].AutoStartAlert = MahAppSwitch_AutoStartAlert.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoStartAlert, GetServerMetadata(server.ID).AutoStartAlert ? "1" : "0");
        }

        private void Switch_AutoRestartAlert_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].AutoRestartAlert = MahAppSwitch_AutoRestartAlert.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoRestartAlert, GetServerMetadata(server.ID).AutoRestartAlert ? "1" : "0");
        }

        private void Switch_AutoUpdateAlert_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].AutoUpdateAlert = MahAppSwitch_AutoUpdateAlert.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoUpdateAlert, GetServerMetadata(server.ID).AutoUpdateAlert ? "1" : "0");
        }

        private void Switch_RestartCrontabAlert_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].RestartCrontabAlert = MahAppSwitch_RestartCrontabAlert.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.RestartCrontabAlert, GetServerMetadata(server.ID).RestartCrontabAlert ? "1" : "0");
        }

        private void Switch_CrashAlert_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].CrashAlert = MahAppSwitch_CrashAlert.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.CrashAlert, GetServerMetadata(server.ID).CrashAlert ? "1" : "0");
        }

        private void Switch_AutoIpUpdate_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].AutoIpUpdateAlert = MahAppSwitch_AutoIpUpdate.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoIpUpdateAlert, GetServerMetadata(server.ID).AutoIpUpdateAlert ? "1" : "0");
        }

        private void Switch_SkipUserSetup_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }
            _serverMetadata[int.Parse(server.ID)].SkipUserSetup = MahAppSwitch_SkipUserSetup.IsOn;
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.SkipUserSetup, GetServerMetadata(server.ID).SkipUserSetup ? "1" : "0");
        }
        #endregion

        private async void Window_Activated(object sender, EventArgs e)
        {
            if (MahAppFlyout_ManageAddons.IsOpen)
            {
                ListBox_ManageAddons_Refresh();
            }

            // Fix the windows cannot toggle issue because of LoadServerTable
            await Task.Delay(1);

            if (ShowActivated)
            {
                LoadServerTable();
            }
        }

        #region Discord Bot
        private async void Switch_DiscordBot_Toggled(object sender, RoutedEventArgs e)
        {
            if (!switch_DiscordBot.IsEnabled) { return; }

            if (switch_DiscordBot.IsOn)
            {
                switch_DiscordBot.IsEnabled = false;
                button_DiscordBotInvite.IsEnabled = switch_DiscordBot.IsOn = await g_DiscordBot.Start();
                DiscordBotLog("Discord Bot " + (switch_DiscordBot.IsOn ? "started." : "fail to start. Reason: Bot Token is invalid."));
                switch_DiscordBot.IsEnabled = true;
            }
            else
            {
                button_DiscordBotInvite.IsEnabled = switch_DiscordBot.IsEnabled = false;
                await g_DiscordBot.Stop();
                DiscordBotLog("Discord Bot stopped.");
                switch_DiscordBot.IsEnabled = true;
            }
        }

        private void Button_DiscordBotPrefixEdit_Click(object sender, RoutedEventArgs e)
        {
            if (button_DiscordBotPrefixEdit.Content.ToString() == "Edit")
            {
                button_DiscordBotPrefixEdit.Content = "Save";
                textBox_DiscordBotPrefix.IsEnabled = true;
                textBox_DiscordBotName.IsEnabled = true;
                textBox_DiscordBotPrefix.Focus();
                textBox_DiscordBotPrefix.SelectAll();
            }
            else
            {
                button_DiscordBotPrefixEdit.Content = "Edit";
                textBox_DiscordBotPrefix.IsEnabled = false;
                textBox_DiscordBotName.IsEnabled = false;

                DiscordBot.Configs.SetBotPrefix(textBox_DiscordBotPrefix.Text);
                DiscordBot.Configs.SetBotName(textBox_DiscordBotName.Text);

                label_DiscordBotCommands.Content = DiscordBot.Configs.GetCommandsList();
            }
        }

        private void Button_DiscordBotTokenEdit_Click(object sender, RoutedEventArgs e)
        {
            if (button_DiscordBotTokenEdit.Content.ToString() == "Edit")
            {
                rectangle_DiscordBotTokenSpoiler.Visibility = Visibility.Hidden;
                button_DiscordBotTokenEdit.Content = "Save";
                textBox_DiscordBotToken.IsEnabled = true;
                textBox_DiscordBotToken.Focus();
                textBox_DiscordBotToken.SelectAll();
            }
            else
            {
                rectangle_DiscordBotTokenSpoiler.Visibility = Visibility.Visible;
                button_DiscordBotTokenEdit.Content = "Edit";
                textBox_DiscordBotToken.IsEnabled = false;
                DiscordBot.Configs.SetBotToken(textBox_DiscordBotToken.Text);
            }
        }

        /*
        private void Button_DiscordBotDashboardEdit_Click(object sender, RoutedEventArgs e)
        {
            if (button_DiscordBotDashboardEdit.Content.ToString() == "Edit")
            {
                button_DiscordBotDashboardEdit.Content = "Save";
                textBox_DiscordBotDashboard.IsEnabled = true;
                textBox_DiscordBotDashboard.Focus();
                textBox_DiscordBotDashboard.SelectAll();
            }
            else
            {
                button_DiscordBotDashboardEdit.Content = "Edit";
                textBox_DiscordBotDashboard.IsEnabled = false;
                DiscordBot.Configs.SetDashboardChannel(textBox_DiscordBotDashboard.Text);
            }
        }
       

        private void NumericUpDown_DiscordRefreshRate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            double rate = numericUpDown_DiscordRefreshRate.Value ?? 5;
            DiscordBot.Configs.SetDashboardRefreshRate((int)rate);
        }
        */

        private async void Button_DiscordBotAddID_Click(object sender, RoutedEventArgs e)
        {
            var settings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Add"
            };

            string newAdminID = await this.ShowInputAsync("Add Admin ID", "Please enter the discord user ID.", settings);
            if (newAdminID == null) { return; } //If pressed cancel

            var adminList = DiscordBot.Configs.GetBotAdminList();
            adminList.Add((newAdminID, "0"));
            DiscordBot.Configs.SetBotAdminList(adminList);
            Refresh_DiscordBotAdminList(listBox_DiscordBotAdminList.SelectedIndex);
        }

        private async void Button_DiscordBotEditServerID_Click(object sender, RoutedEventArgs e)
        {
            var adminListItem = (DiscordBot.AdminListItem)listBox_DiscordBotAdminList.SelectedItem;
            if (adminListItem == null) { return; }

            var settings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Save",
                DefaultText = adminListItem.ServerIds
            };

            string example = "0 - Grant All servers Permission.\n\nExamples:\n0\n1,2,3,4,5\n";
            string newServerIds = await this.ShowInputAsync($"Edit Server IDs ({adminListItem.AdminId})", $"Please enter the server Ids where admin has access to the server.\n{example}", settings);
            if (newServerIds == null) { return; } //If pressed cancel

            var adminList = DiscordBot.Configs.GetBotAdminList();
            for (int i = 0; i < adminList.Count; i++)
            {
                if (adminList[i].Item1 == adminListItem.AdminId)
                {
                    adminList.RemoveAt(i);
                    adminList.Insert(i, (adminListItem.AdminId, newServerIds.Trim()));
                    break;
                }
            }
            DiscordBot.Configs.SetBotAdminList(adminList);
            Refresh_DiscordBotAdminList(listBox_DiscordBotAdminList.SelectedIndex);
        }

        private void Button_DiscordBotRemoveID_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_DiscordBotAdminList.SelectedIndex >= 0)
            {
                var adminList = DiscordBot.Configs.GetBotAdminList();
                try
                {
                    adminList.RemoveAt(listBox_DiscordBotAdminList.SelectedIndex);
                }
                catch
                {
                    Console.WriteLine($"Fail to delete item {listBox_DiscordBotAdminList.SelectedIndex} in adminIDs.txt");
                }
                DiscordBot.Configs.SetBotAdminList(adminList);

                listBox_DiscordBotAdminList.Items.Remove(listBox_DiscordBotAdminList.Items[listBox_DiscordBotAdminList.SelectedIndex]);
            }
        }

        public void Refresh_DiscordBotAdminList(int selectIndex = 0)
        {
            listBox_DiscordBotAdminList.Items.Clear();
            foreach (var (adminID, serverIDs) in DiscordBot.Configs.GetBotAdminList())
            {
                listBox_DiscordBotAdminList.Items.Add(new DiscordBot.AdminListItem { AdminId = adminID, ServerIds = serverIDs });
            }
            listBox_DiscordBotAdminList.SelectedIndex = listBox_DiscordBotAdminList.Items.Count >= 0 ? selectIndex : -1;
        }

        private bool CheckWebhookThreshold(ref long lastWebhookTimeInMs)
        {
            bool ret = false;
            //init counter
            if (lastWebhookTimeInMs == 0)
            {
                lastWebhookTimeInMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                return true;
            }
            if ((DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastWebhookTimeInMs) > _webhookThresholdTimeInMs)
                ret = true;

            //reset counter
            lastWebhookTimeInMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            return ret;
        }

        public int GetServerCount()
        {
            return ServerGrid.Items.Count;
        }

        public List<(string, string, string)> GetServerList()
        {
            var list = new List<(string, string, string)>();

            for (int i = 0; i < ServerGrid.Items.Count; i++)
            {
                var server = (ServerTable)ServerGrid.Items[i];
                list.Add((server.ID, server.Status, server.Name));
            }

            return list;
        }

        public List<(string, string, string)> GetServerList(string userId)
        {
            var serverIds = Configs.GetServerIdsByAdminId(userId);
            var serverList = ServerGrid.Items.Cast<ServerTable>().ToList();

            if (serverIds.Contains("0"))
            {
                return serverList
                    .Select(server => (server.ID, server.Status, server.Name))
                    .ToList();
            }

            return serverList
                .Where(server => serverIds.Contains(server.ID))
                .Select(server => (server.ID, server.Status, server.Name)).ToList();
        }

        public List<(string, string, string)> GetServerListByUserId(string userId)
        {
            var serverIds = Configs.GetServerIdsByAdminId(userId);
            var serverList = ServerGrid.Items.Cast<ServerTable>().ToList();

            if (serverIds.Contains("0"))
            {
                return serverList
                    .Select(server => (server.ID, server.Status, server.Name))
                    .ToList();
            }

            return serverList
                .Where(server => serverIds.Contains(server.ID))
                .Select(server => (server.ID, server.Status, server.Name)).ToList();
        }

        public bool IsServerExist(string serverId)
        {
            for (int i = 0; i < ServerGrid.Items.Count; i++)
            {
                var server = (ServerTable)ServerGrid.Items[i];
                if (server.ID == serverId) { return true; }
            }

            return false;
        }

        public ServerStatus GetServerStatus(string serverId)
        {
            return GetServerMetadata(serverId).ServerStatus;
        }

        public string GetServerName(string serverId)
        {
            var server = GetServerTableById(serverId);
            return server?.Name ?? string.Empty;
        }

        public ServerTable GetServerTableById(string serverId)
        {
            for (int i = 0; i < ServerGrid.Items.Count; i++)
            {
                var server = (ServerTable)ServerGrid.Items[i];
                if (server.ID == serverId) { return server; }
            }

            return null;
        }

        public async Task<bool> StartServerById(string serverId, string adminID, string adminName)
        {
            var server = GetServerTableById(serverId);
            if (server == null) { return false; }

            DiscordBotLog($"Discord: Receive START action | {adminName} ({adminID})");
            await GameServer_Start(server);
            return GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started;
        }

        public async Task<bool> StopServerById(string serverId, string adminID, string adminName)
        {
            var server = GetServerTableById(serverId);
            if (server == null) { return false; }

            DiscordBotLog($"Discord: Receive STOP action | {adminName} ({adminID})");
            await GameServer_Stop(server);
            return GetServerMetadata(server.ID).ServerStatus == ServerStatus.Stopped;
        }

        public async Task<bool> RestartServerById(string serverId, string adminID, string adminName)
        {
            var server = GetServerTableById(serverId);
            if (server == null) { return false; }

            DiscordBotLog($"Discord: Receive RESTART action | {adminName} ({adminID})");
            await GameServer_Restart(server);
            return GetServerMetadata(server.ID).ServerStatus == ServerStatus.Started;
        }

        public Task<string> SendCommandById(string serverId, string command, string adminID, string adminName, int waitForDataInMs = 0)
        {
            var server = GetServerTableById(serverId);
            if (server == null) { return Task.FromResult(""); }

            DiscordBotLog($"Discord: Receive SEND action | {adminName} ({adminID}) | {command}");
            return SendCommandAsync(server, command, waitForDataInMs);
        }

        public async Task<bool> BackupServerById(string serverId, string adminID, string adminName)
        {
            var server = GetServerTableById(serverId);
            if (server == null) { return false; }

            DiscordBotLog($"Discord: Receive BACKUP action | {adminName} ({adminID})");
            await GameServer_Backup(server);
            return GetServerMetadata(server.ID).ServerStatus == ServerStatus.Stopped;
        }

        public async Task<bool> UpdateServerById(string serverId, string adminID, string adminName)
        {
            var server = GetServerTableById(serverId);
            if (server == null) { return false; }

            DiscordBotLog($"Discord: Receive UPDATE action | {adminName} ({adminID})");
            await GameServer_Update(server);
            return GetServerMetadata(server.ID).ServerStatus == ServerStatus.Stopped;
        }

        private void Switch_DiscordBotAutoStart_Click(object sender, RoutedEventArgs e)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\WindowsGSM", true))
            {
                key?.SetValue("DiscordBotAutoStart", MahAppSwitch_DiscordBotAutoStart.IsOn.ToString());
            }
        }

        private void Button_DiscordBotInvite_Click(object sender, RoutedEventArgs e)
        {
            string inviteLink = g_DiscordBot.GetInviteLink();
            if (!string.IsNullOrWhiteSpace(inviteLink))
            {
                Process.Start(new ProcessStartInfo(g_DiscordBot.GetInviteLink()) { UseShellExecute = true });
            }
        }
        #endregion

        /// <summary>Hide others Flyout and toggle the flyout</summary>
        /// <param name="flyout"></param>
        private void ToggleMahappFlyout(Flyout flyout)
        {
            MahAppFlyout_DiscordAlert.IsOpen = flyout == MahAppFlyout_DiscordAlert && !MahAppFlyout_DiscordAlert.IsOpen;
            MahAppFlyout_EditConfig.IsOpen = flyout == MahAppFlyout_EditConfig && !MahAppFlyout_EditConfig.IsOpen;
            MahAppFlyout_ImportGameServer.IsOpen = flyout == MahAppFlyout_ImportGameServer && !MahAppFlyout_ImportGameServer.IsOpen;
            MahAppFlyout_InstallGameServer.IsOpen = flyout == MahAppFlyout_InstallGameServer && !MahAppFlyout_InstallGameServer.IsOpen;
            MahAppFlyout_ManageAddons.IsOpen = flyout == MahAppFlyout_ManageAddons && !MahAppFlyout_ManageAddons.IsOpen;
            MahAppFlyout_RestoreBackup.IsOpen = flyout == MahAppFlyout_RestoreBackup && !MahAppFlyout_RestoreBackup.IsOpen;
            MahAppFlyout_SetAffinity.IsOpen = flyout == MahAppFlyout_SetAffinity && !MahAppFlyout_SetAffinity.IsOpen;
            MahAppFlyout_Settings.IsOpen = flyout == MahAppFlyout_Settings && !MahAppFlyout_Settings.IsOpen;
            MahAppFlyout_ViewPlugins.IsOpen = flyout == MahAppFlyout_ViewPlugins && !MahAppFlyout_ViewPlugins.IsOpen;
        }

        private void HamburgerMenu_ItemClick(object sender, ItemClickEventArgs e)
        {
            HamburgerMenuControl.IsPaneOpen = false;

            hMenu_Home.Visibility = (HamburgerMenuControl.SelectedIndex == 0) ? Visibility.Visible : Visibility.Hidden;
            hMenu_Dashboard.Visibility = (HamburgerMenuControl.SelectedIndex == 1) ? Visibility.Visible : Visibility.Hidden;
            hMenu_Discordbot.Visibility = (HamburgerMenuControl.SelectedIndex == 2) ? Visibility.Visible : Visibility.Hidden;

            if (HamburgerMenuControl.SelectedIndex == 2)
            {
                label_DiscordBotCommands.Content = DiscordBot.Configs.GetCommandsList();
                button_DiscordBotPrefixEdit.Content = "Edit";
                textBox_DiscordBotPrefix.IsEnabled = false;
                textBox_DiscordBotPrefix.Text = DiscordBot.Configs.GetBotPrefix();
                textBox_DiscordBotName.Text = DiscordBot.Configs.GetBotName();

                button_DiscordBotTokenEdit.Content = "Edit";
                textBox_DiscordBotToken.IsEnabled = false;
                textBox_DiscordBotToken.Text = DiscordBot.Configs.GetBotToken();
                //textBox_DiscordBotDashboard.Text = DiscordBot.Configs.GetDashboardChannel();
                //numericUpDown_DiscordRefreshRate.Value = DiscordBot.Configs.GetDashboardRefreshRate();

                Refresh_DiscordBotAdminList(listBox_DiscordBotAdminList.SelectedIndex);

                if (listBox_DiscordBotAdminList.Items.Count > 0 && listBox_DiscordBotAdminList.SelectedItem == null)
                {
                    listBox_DiscordBotAdminList.SelectedItem = listBox_DiscordBotAdminList.Items[0];
                }
            }
        }

        private async void HamburgerMenu_OptionsItemClick(object sender, ItemClickEventArgs e)
        {
            if (HamburgerMenuControl.SelectedOptionsIndex == 0)
            {
                ToggleMahappFlyout(MahAppFlyout_ViewPlugins);
            }
            else if (HamburgerMenuControl.SelectedOptionsIndex == 1)
            {
                ToggleMahappFlyout(MahAppFlyout_Settings);
            }

            HamburgerMenuControl.SelectedOptionsIndex = -1;

            await Task.Delay(1); // Delay 0.001 sec due to UI not sync
            if (hMenu_Home.Visibility == Visibility.Visible)
            {
                HamburgerMenuControl.SelectedIndex = 0;
            }
            else if (hMenu_Dashboard.Visibility == Visibility.Visible)
            {
                HamburgerMenuControl.SelectedIndex = 1;
            }
            else if (hMenu_Discordbot.Visibility == Visibility.Visible)
            {
                HamburgerMenuControl.SelectedIndex = 2;
            }
        }

        private async void HamburgerMenu_Loaded(object sender, RoutedEventArgs e)
        {
            HamburgerMenuControl.Visibility = Visibility.Visible;
            hMenu_Home.Visibility = Visibility.Visible;
            hMenu_Dashboard.Visibility = Visibility.Hidden;
            hMenu_Discordbot.Visibility = Visibility.Hidden;

            await Task.Delay(1); // Delay 0.001 sec due to a bug
            HamburgerMenuControl.SelectedIndex = 0;
        }

        private void Button_AutoScroll_Click(object sender, RoutedEventArgs e)
        {
            var server = (ServerTable)ServerGrid.SelectedItem;
            if (server == null) { return; }

            Button_AutoScroll.Content = Button_AutoScroll.Content.ToString() == "✔️ AUTO SCROLL" ? "❌ AUTO SCROLL" : "✔️ AUTO SCROLL";
            _serverMetadata[int.Parse(server.ID)].AutoScroll = Button_AutoScroll.Content.ToString().Contains("✔️");
            ServerConfig.SetSetting(server.ID, ServerConfig.SettingName.AutoScroll, GetServerMetadata(server.ID).AutoScroll ? "1" : "0");
        }

        /** 
         * Updates the Crontab Gui Element for the next time it a restart scedule will trigger. It is not necesarly the one set in the gui if you set up other restart crontabs
         * You can not call this from any onther thread than the main one. Anything accessing GUI components besides Main thread will immediatly kill that thread without exception or trace
         */
        public void UpdateCrontabTime(string id, string expression)
        {
            var currentRow = (ServerTable)ServerGrid.SelectedItem;
            if (currentRow.ID == id)
            {
                textBox_nextcrontab.Text = CrontabSchedule.TryParse(expression)?.GetNextOccurrence(DateTime.Now).ToString("ddd, MM/dd/yyyy HH:mm:ss");
            }

        }
    }
}
