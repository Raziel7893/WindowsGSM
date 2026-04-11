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
        private RCON Connection = null;
        public async Task<string> Connect(string ip, int port, string password)
        {
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "logs"));
            try
            {
                Connection = new RCON(GetEndpoint(ip, port), password);
                await Connection.ConnectAsync();
            }
            catch (Exception e)
            {
                string logPath = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "logs");
                Directory.CreateDirectory(logPath);
                //we don't have the correct Log here, so we just return and let the caller take care of it
                return e.Message;
            }
            return "";
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
                return "CONNECTION FAILED";
            }
            else if (!Connection.Connected)
            {
                return "CONNECTION FAILED";
            }

            var response = await Connection.SendCommandAsync(command, TimeSpan.FromSeconds(5));
            return response;
        }

        public static async Task<string> SendCommandAsync(string ip, int port, string password, string command)
        {
            var connection = new RCON(GetEndpoint(ip, port), password);

            try
            {
                await connection.ConnectAsync();

                var response = await connection.SendCommandAsync(command, TimeSpan.FromSeconds(10));
                connection.Dispose();
                return response;
            }
            catch (Exception e)
            {
                return e.Message;
            }

        }
    }
}
