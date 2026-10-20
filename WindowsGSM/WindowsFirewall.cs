using NetFwTypeLib;
using System;
using System.Threading.Tasks;

namespace WindowsGSM
{
    class WindowsFirewall
    {
        private readonly string Name;
        private readonly string Path;
        public string Error { get; private set; }

        public WindowsFirewall(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public async Task<bool> IsRuleExist()
        {
            return await Task.Run(() =>
            {
                try
                {
                    INetFwMgr netFwMgr = (INetFwMgr)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwMgr"));

                    foreach (INetFwAuthorizedApplication app in netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications)
                    {
                        if (app.ProcessImageFileName.ToLower() == Path.ToLower())
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    return false;
                }

                return false;
            });
        }

        public Task<bool> AddRule() => SetEnabled(true);

        public async Task<bool> SetEnabled(bool enabled)
        {
            return await Task.Run(() =>
            {
                Error = null;
                try
                {
                    var netFwMgr = (INetFwMgr)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwMgr"));
                    var applications = netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications;
                    bool found = false;

                    foreach (INetFwAuthorizedApplication application in applications)
                    {
                        if (!string.Equals(application.ProcessImageFileName, Path, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (enabled)
                        {
                            if (!application.Enabled)
                            {
                                application.Enabled = true;
                            }
                        }
                        else
                        {
                            applications.Remove(application.ProcessImageFileName);
                        }

                        found = true;
                        break;
                    }

                    if (enabled && !found)
                    {
                        var app = (INetFwAuthorizedApplication)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwAuthorizedApplication"));
                        app.Name = Name;
                        app.ProcessImageFileName = Path;
                        app.Enabled = true;
                        applications.Add(app);
                    }

                    netFwMgr = (INetFwMgr)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwMgr"));
                    foreach (INetFwAuthorizedApplication application in netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications)
                    {
                        if (string.Equals(application.ProcessImageFileName, Path, StringComparison.OrdinalIgnoreCase))
                        {
                            if (enabled && application.Enabled)
                            {
                                return true;
                            }

                            Error = "The automatic firewall rule could not be changed.";
                            return false;
                        }
                    }

                    if (!enabled) { return true; }
                    Error = "The automatic firewall rule could not be enabled.";
                    return false;
                }
                catch (Exception ex)
                {
                    Error = ex.Message;
                    return false;
                }
            });
        }

        public void RemoveRule()
        {
            try
            {
                INetFwMgr netFwMgr = (INetFwMgr)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwMgr"));
                netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications.Remove(Path);
            }
            catch
            {
                // ignore
            }
        }

        //Remove the firewall rule by similar path
        public async void RemoveRuleEx()
        {
            await Task.Run(() =>
            {
                try
                {
                    INetFwMgr netFwMgr = (INetFwMgr)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwMgr"));

                    foreach (INetFwAuthorizedApplication app in netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications)
                    {
                        string filename = app.ProcessImageFileName.ToLower();
                        if (filename.Contains(Path.ToLower()))
                        {
                            netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications.Remove(app.ProcessImageFileName);
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            });
        }
    }
}
