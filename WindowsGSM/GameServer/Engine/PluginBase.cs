using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;

namespace WindowsGSM.GameServer.Engine
{
    public abstract class PluginBase
    {
        public abstract bool loginAnonymous { get; }
        public abstract string AppId { get; }
        public abstract string StartPath { get; }

        private readonly ServerConfig serverData;
        public string Error, Notice;

        // - Game server Fixed variables
        public string FullName = "";
        public bool AllowsEmbedConsole = false;
        public int PortIncrements = 1;
        public object QueryMethod = null;


        // - Game server default values
        public string Port = "";
        public string QueryPort = "";
        public string Defaultmap = "";
        public string Maxplayers = "";
        public string Additional = "";


        public abstract void CreateServerCFG();
        public abstract Task<Process> Start();

        public abstract Task Stop(Process p);
        public abstract Task<Process> Install();

        public abstract Task<Process> Update(bool validate = false, string custom = null);
        public abstract string GetLocalBuild();
        public abstract Task<string> GetRemoteBuild();
        public abstract bool IsInstallValid();
        public abstract bool IsImportValid(string path);
    }
}
