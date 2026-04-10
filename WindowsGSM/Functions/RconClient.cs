using CoreRCON;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace WindowsGSM.Functions
{
    public class RconClient
    {
        const string LogFilePath = "logs/RconLogs.log";
        private RCON connection = null;
        public async Task<bool> Connect(string ip, int port, string password)
        {
            connection = new RCON(GetEndpoint(ip, port), password);
            await connection.ConnectAsync();
            if (connection == null || !connection.Connected)
            {
                await File.AppendAllTextAsync(LogFilePath, $"Connection could not be established to {connection.IPEndpoint.ToString()}!");
            }
            return true;
        }

        private static IPEndPoint GetEndpoint(string ip, int port)
        {
            var endpoint = IPEndPoint.Parse(ip);
            endpoint.Port = port;
            return endpoint;
        }

        public async Task<bool> Send(string command)
        {
            if (connection == null)
            {
                await File.AppendAllTextAsync(LogFilePath, $"Connection could not be established, Connect was not called!");
            }
            else if (!connection.Connected)
            {
                await File.AppendAllTextAsync(LogFilePath, $"Connection failed to be established to {connection.IPEndpoint.ToString()}!");
            }
            return true;
        }

        public static async Task SendCommandAsync(string ip, int port, string password, string command)
        {
            var connection = new RCON(GetEndpoint(ip, port), password);
            await connection.ConnectAsync();
            if (connection == null || !connection.Connected)
            {
                await File.AppendAllTextAsync(LogFilePath, $"Connection could not be established to {connection.IPEndpoint.ToString()}!");
            }
            await connection.SendCommandAsync(command);
        }
    }
}
