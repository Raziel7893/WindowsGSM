using CoreRCON;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace WindowsGSM.Functions
{
    public class RconClient
    {
        static string LogFilePath = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "logs", "RconLogs.log");
        private RCON Connection = null;
        public async Task<bool> Connect(string ip, int port, string password)
        {
            try
            {
                Connection = new RCON(GetEndpoint(ip, port), password);
                await Connection.ConnectAsync();
            }
            catch (Exception e)
            {
                string logPath = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "logs");
                Directory.CreateDirectory(logPath);

                await File.AppendAllTextAsync(LogFilePath, $"Connection could not be established to {Connection.IPEndpoint.ToString()}! {e.Message}\n");
                return false;
            }
            return true;
        }

        private static IPEndPoint GetEndpoint(string ip, int port)
        {
            var endpoint = IPEndPoint.Parse(ip);
            endpoint.Port = port;
            return endpoint;
        }

        public async Task<string> Send(string command)
        {
            if (Connection == null)
            {
                await File.AppendAllTextAsync(LogFilePath, $"Connection could not be established, Connect was not called!\n");
                return "CONNECTION FAILED";
            }
            else if (!Connection.Connected)
            {
                await File.AppendAllTextAsync(LogFilePath, $"Connection failed to be established to {Connection.IPEndpoint.ToString()}!\n");
                return "CONNECTION FAILED";
            }

            var response = await Connection.SendCommandAsync(command, TimeSpan.FromSeconds(5));
            await File.AppendAllTextAsync(LogFilePath, $"Send command \"{command}\" with response \"{response}\"\n");
            return response;
        }

        public static async Task<string> SendCommandAsync(string ip, int port, string password, string command)
        {
            var connection = new RCON(GetEndpoint(ip, port), password);

            try
            {
                await connection.ConnectAsync();

                var response = await connection.SendCommandAsync(command, TimeSpan.FromSeconds(10));
                await File.AppendAllTextAsync(LogFilePath, $"Send command \"{command}\" with response \"{response}\"\n");
                connection.Dispose();
                return response;
            }
            catch (Exception e)
            {
                await File.AppendAllTextAsync(LogFilePath, $"Connection could not be established to {connection.IPEndpoint.ToString()}! {e.Message}\n");
                return e.Message;
            }

        }
    }
}
