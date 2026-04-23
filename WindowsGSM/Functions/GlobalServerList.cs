namespace WindowsGSM.Functions
{
    static class GlobalServerList
    {
        public static bool IsServerOnSteamServerList(string publicIP, string port)
        {
            try
            {
                string json = Http.DownloadString("http://api.steampowered.com/ISteamApps/GetServersAtAddress/v0001?addr=" + publicIP + "&format=json");
                string matchString = "\"addr\":\"" + publicIP + ":" + port + "\"";

                return json.Contains(matchString);
            }
            catch
            {
                return false;
            }
        }
    }
}
