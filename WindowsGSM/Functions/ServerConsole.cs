using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace WindowsGSM.Functions
{
    public class ServerConsole
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_CHAR = 0x0102;
        private const int WM_GETTEXT = 0x000D;
        private const int WM_GETTEXTLENGTH = 0x000E;

        private const int MAX_LINE = 500;
        private readonly List<string> _consoleList = new List<string>();
        private readonly List<string> _recorderConsoleList = new List<string>();
        private readonly string _serverId;
        private int _lineNumber = 0;

        public string JoinCodeLine = "";

        public ServerConsole(string serverId)
        {
            _serverId = serverId;
        }

        public ServerConsole(int serverId)
        {
            _serverId = serverId.ToString();
        }

        public async void AddOutput(object sender, DataReceivedEventArgs args)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                MainWindow._serverMetadata[int.Parse(_serverId)].ServerConsole.Add(args.Data);
            });
        }

        public async void Input(Process process, string text, IntPtr mainWindow)
        {
            if (!process.HasExited)
            {
                if (process.StartInfo.RedirectStandardInput)
                {
                    try
                    {
                        process.StandardInput.WriteLine(text);
                        Add(text);
                    }
                    catch
                    {
                        //ignore
                    }
                }
                else
                {
                    await Task.Run(() =>
                    {
                        if (!process.HasExited && process.ProcessName == "7DaysToDieServer")
                        {
                            SetForegroundWindow(mainWindow);
                            var current = GetForegroundWindow();
                            var wgsmWindow = Process.GetCurrentProcess().MainWindowHandle;
                            if (current != wgsmWindow)
                            {
                                SendWaitToMainWindow("{TAB}");
                                SendWaitToMainWindow(text);
                                SendWaitToMainWindow("{TAB}");
                                SendWaitToMainWindow(text);
                                SendWaitToMainWindow("{ENTER}");
                                SetForegroundWindow(wgsmWindow);
                            }
                        }
                        else
                        {
                            SendMessageToMainWindow(mainWindow, text);
                        }
                    });
                }
            }
        }

        public void Clear()
        {
            _consoleList.Clear();
        }

        public string Get()
        {
            return string.Join(Environment.NewLine, _consoleList.ToArray());
        }

        public string GetPreviousCommand()
        {
            --_lineNumber;
            return (_consoleList.Count == 0) ? string.Empty : _consoleList[GetLineNumber()];
        }

        public string GetNextCommand()
        {
            ++_lineNumber;
            return (_consoleList.Count == 0) ? string.Empty : _consoleList[GetLineNumber()];
        }

        public int GetLineNumber()
        {
            if (_lineNumber < 0)
            {
                _lineNumber = 0;
            }
            else if (_lineNumber >= _consoleList.Count)
            {
                _lineNumber = (_consoleList.Count <= 0) ? 0 : _consoleList.Count - 1;
            }

            return _lineNumber;
        }

        public void Add(string text)
        {
            if (_serverId == "0")
            {
                _lineNumber = _consoleList.Count + 1;

                if (_consoleList.Count > 0 && text == _consoleList[_consoleList.Count - 1])
                {
                    return;
                }
            }

            if (_recorderConsoleList.Any())
            {
                _recorderConsoleList.Add(text);

                if (_recorderConsoleList.Count > MAX_LINE)
                {
                    _recorderConsoleList.RemoveAt(0);
                }
            }

            //check for known join codes
            if (text.Contains("join code", StringComparison.InvariantCultureIgnoreCase) || text.Contains("joincode",StringComparison.InvariantCultureIgnoreCase))
            {
                JoinCodeLine = text;
                SendWebhookAsync(text);
            }

            _consoleList.Add(text);
            if (_consoleList.Count > MAX_LINE)
            {
                _consoleList.RemoveAt(0);
            }
        }

        public async Task SendWebhookAsync(string text)
        {
            await System.Windows.Application.Current.Dispatcher.Invoke(async () =>
            {
                MainWindow WindowsGSM = (MainWindow)System.Windows.Application.Current.MainWindow;
                if (WindowsGSM.IsServerExist(_serverId))
                {
                    MainWindow.ServerStatus serverStatus = WindowsGSM.GetServerStatus(_serverId);
                    var server = WindowsGSM.GetServerTableById(_serverId);
                    if (WindowsGSM.GetServerMetadata(_serverId).DiscordAlert && StartAlertsEnabled(WindowsGSM))
                    {
                        var webhook = new DiscordWebhook(WindowsGSM.GetServerMetadata(_serverId).DiscordWebhook, WindowsGSM.GetServerMetadata(_serverId).DiscordMessage, WindowsGSM.g_DonorType, WindowsGSM.GetServerMetadata(_serverId).SkipUserSetup);
                        await webhook.SendPlain($"Server {_serverId}, {server.Name}:  Join Code Found: {text}");
                    }
                }
            });
        }

 

        public static void SendMessageToMainWindow(IntPtr hWnd, string message)
        {
            // Here is a minor error on PostMessage, when it sends repeated char, some char may disappear. Example: send 1111111, windows may receive 1111 or 11111
            for (int i = 0; i < message.Length; i++)
            {
                // This is the solution for the error stated above
                if (i > 0 && message[i] == message[i - 1])
                {
                    // Send a None key, break the repeat bug
                    PostMessage(hWnd, WM_KEYDOWN, (IntPtr)Keys.None, (IntPtr)0);
                }

                PostMessage(hWnd, WM_CHAR, (IntPtr)message[i], (IntPtr)0);
            }

            // Send enter
            PostMessage(hWnd, WM_KEYDOWN, (IntPtr)Keys.Enter, (IntPtr)(0 << 29 | 0));
        }

        public static void SetMainWindow(IntPtr hWnd)
        {
            SetForegroundWindow(hWnd);
        }

        public static void SendWaitToMainWindow(string keys)
        {
            try
            {
                SendKeys.SendWait(keys);
            }
            catch
            {
                /*
                    System.ComponentModel.Win32Exception (0x80004005): Access is denied
                    at System.Windows.Forms.SendKeys.SendInput(Byte[] oldKeyboardState, Queue previousEvents)
                    at System.Windows.Forms.SendKeys.Send(String keys, Control control, Boolean wait)
                    at System.Windows.Forms.SendKeys.SendWait(String keys)

                    This error may happen in Windows Server R2, UAC problem, not sure how to fix

                    https://github.com/WindowsGSM/WindowsGSM/issues/14
                */
            }
        }

        public void StartRecorder()
        {
            _recorderConsoleList.Clear();
            _recorderConsoleList.Add("Start:");
        }

        public string StopRecorder()
        {
            var text = string.Join(Environment.NewLine, _recorderConsoleList.ToArray());
            _recorderConsoleList.Clear();
            return text;
        }

        private bool StartAlertsEnabled(MainWindow WindowsGSM)
        {
            return (WindowsGSM.GetServerMetadata(_serverId).AutoStartAlert || WindowsGSM.GetServerMetadata(_serverId).AutoRestartAlert || WindowsGSM.GetServerMetadata(_serverId).RestartCrontabAlert);
        }
    }
}
