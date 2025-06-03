using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WindowsGSM.GameServer.Query
{
    public interface QueryTemplate
    {
        void SetAddressPort(string address, int port, int timeout = 5);
        Task<Dictionary<string, string>> GetInfo();
        Task<List<PlayerData>> GetPlayersData();
        Task<string> GetPlayersAndMaxPlayers();
    }

    public struct PlayerData
    {
        public int Id;
        public string Name;
        public long Score;
        public TimeSpan? TimeConnected;

        public PlayerData(int id, string name, long score = 0, TimeSpan? timeConnected = null)
        {
            Id = id;
            Name = name;
            Score = score;
            TimeConnected = timeConnected;
        }
        public override string ToString() 
        {
            return $"{Id}:{Name}, Score:{Score}, connected:{TimeConnected?.TotalMinutes}";
        }
    }
}
