using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WindowsGSM.Functions
{
    static class ProcessManagement
    {
        internal const int CTRL_C_EVENT = 0;
        [DllImport("kernel32.dll")]
        internal static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern bool FreeConsole();
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);
        delegate Boolean ConsoleCtrlDelegate(uint CtrlType);

        //If you use that in plugins, make sure to mention that the users NEED to use WGSM versions based on this fork
        //sends a gracefull shutdown signal to the given process.
        //this should work for most if not all servers to close cleanly with hopefully a save before (most servers implement that to be compatible with OS reboots and stuff)
        public static bool SendStopSignal(Process p)
        {
            if (AttachConsole((uint)p.Id))
            {
                SetConsoleCtrlHandler(null, true);
                try
                {
                    if (!GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0))
                        return false;
                    p.WaitForExit(10000);
                }
                finally
                {
                    SetConsoleCtrlHandler(null, false);
                    FreeConsole();
                }
                return true;
            }
            return false;
        }

        //Try to gracefully shutdown the process and kills it if it fails to do so
        public static void StopProcess(Process p)
        {
            if (!SendStopSignal(p))
                p.Kill();
        }

        public static async Task<string> GetCommandLineByApproximatePath(string path)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string query = $"SELECT CommandLine FROM Win32_Process WHERE ExecutablePath LIKE '%{path.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").Replace("'", @"\'")}%'";
                    using (ManagementObjectSearcher mos = new ManagementObjectSearcher(query))
                    using (ManagementObjectCollection moc = mos.Get())
                    {
                        return (from mo in moc.Cast<ManagementObject>() select mo["CommandLine"]).First().ToString();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    return null;
                }
            });
        }
    }
}
