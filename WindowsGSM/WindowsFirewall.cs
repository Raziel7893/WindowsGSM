using System;
using System.Collections;
using System.Threading.Tasks;

namespace WindowsGSM
{
    class WindowsFirewall
    {
        private readonly string Name;
        private readonly string Path;

        public WindowsFirewall(string name, string path)
        {
            Name = name;
            Path = path;
        }

        private dynamic GetFirewallManager()
        {
            return Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwMgr"));
        }

        private dynamic CreateFirewallAuthorizedApp()
        {
            return Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwAuthorizedApplication"));
        }

        public async Task<bool> IsRuleExist()
        {
            return await Task.Run(() =>
            {
                try
                {
                    dynamic netFwMgr = GetFirewallManager();
                    IEnumerable apps = netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications as IEnumerable;

                    if (apps != null)
                    {
                        foreach (dynamic app in apps)
                        {
                            if (((string)app.ProcessImageFileName).ToLower() == Path.ToLower())
                            {
                                return true;
                            }
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

        public async Task<bool> AddRule()
        {
            return await Task.Run(() =>
            {
                try
                {
                    dynamic netFwMgr = GetFirewallManager();
                    dynamic app = CreateFirewallAuthorizedApp();
                    app.Name = Name;
                    app.ProcessImageFileName = Path;
                    app.Enabled = true;
                    netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications.Add(app);
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public void RemoveRule()
        {
            try
            {
                dynamic netFwMgr = GetFirewallManager();
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
                    dynamic netFwMgr = GetFirewallManager();
                    IEnumerable apps = netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications as IEnumerable;

                    if (apps != null)
                    {
                        foreach (dynamic app in apps)
                        {
                            string filename = ((string)app.ProcessImageFileName).ToLower();
                            if (filename.Contains(Path.ToLower()))
                            {
                                netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications.Remove(app.ProcessImageFileName);
                            }
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
