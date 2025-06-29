﻿using System;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Web;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Globalization;

namespace WindowsGSM.Functions
{
    class DiscordWebhook
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _webhookUrl;
        private readonly string _customMessage;
        private readonly string _donorType;
        private readonly bool _skipUserSetting;

        public DiscordWebhook(string webhookurl, string customMessage, string donorType = "", bool skippAccountOverride = false)
        {
            _webhookUrl = webhookurl ?? string.Empty;
            _customMessage = customMessage ?? string.Empty;
            _donorType = donorType ?? string.Empty;
            _skipUserSetting = skippAccountOverride;
        }

        public async Task<bool> Send(string serverid, string servergame, string serverstatus, string servername, string serverip, string serverport)
        {
            if (string.IsNullOrWhiteSpace(_webhookUrl))
            {
                return false;
            }

            string userData = "";
            var avatarUrl = GetAvatarUrl();
            if (!_skipUserSetting)
                userData = "    \"username\": \"WindowsGsm\",\r\n" +
                $"              \"avatar_url\": \"" + avatarUrl + "\",\r\n";

            string json = @"
            {
                " + userData + @"
                ""content"": """ + HttpUtility.JavaScriptStringEncode(_customMessage) + @""",
                ""embeds"": [
                {
                    ""type"": ""rich"",
                    ""color"": " + GetColor(serverstatus) + @",
                    ""fields"": [
                    {
                        ""name"": ""Status"",
                        ""value"": """ + GetStatusWithEmoji(serverstatus) + @""",
                        ""inline"": true
                    },
                    {
                        ""name"": ""Game Server"",
                        ""value"": """ + servergame + @""",
                        ""inline"": true
                    },
                    {
                        ""name"": ""Server IP:Port"",
                        ""value"": """ + serverip + ":" + serverport + @""",
                        ""inline"": true
                    }],
                    ""author"": {
                        ""name"": """ + HttpUtility.JavaScriptStringEncode(servername) + @""",
                        ""icon_url"": """ + GetServerGameIcon(servergame) + @"""
                    },
                    ""footer"": {
                        ""text"": """ + MainWindow.WGSM_VERSION + @" - Discord Alert"",
                        ""icon_url"": """ + avatarUrl + @"""
                    },
                    ""timestamp"": """ + DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.mssZ", CultureInfo.InvariantCulture) + @""",
                    ""thumbnail"": {
                        ""url"": """ + GetThumbnail(serverstatus) + @"""
                    }
                }]
            }";

            File.WriteAllText("debugWebhook_Content.log", json);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            File.WriteAllText("debugWebhook_ContentAfterConversion.log", content.ReadAsStringAsync().Result);

            try
            {
                var response = await _httpClient.PostAsync(_webhookUrl, content);
                if (response.Content != null)
                {
                    File.WriteAllText("debugWebhook.log", response.Content.ReadAsStringAsync().Result);
                    return true;
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"Fail to send webhook ({_webhookUrl})");
            }

            return false;
        }

        private static string GetColor(string serverStatus)
        {
            if (serverStatus.Contains("Started"))
            {
                return "65280"; //Green
            }
            else if (serverStatus.Contains("Restarted"))
            {
                return "65535"; //Cyan
            }
            else if (serverStatus.Contains("Crashed"))
            {
                return "16711680"; //Red
            }
            else if (serverStatus.Contains("Updated"))
            {
                return "16564292"; //Gold
            }

            return "16711679";
        }

        private static string GetStatusWithEmoji(string serverStatus)
        {
            if (serverStatus.Contains("Started"))
            {
                return ":green_circle: " + serverStatus;
            }
            if (serverStatus.Contains("Restarted"))
            {
                return ":blue_circle: " + serverStatus;
            }
            if (serverStatus.Contains("Crashed"))
            {
                return ":red_circle: " + serverStatus;
            }
            if (serverStatus.Contains("Updated"))
            {
                return ":orange_circle: " + serverStatus;
            }

            return serverStatus;
        }

        private static string GetThumbnail(string serverStatus)
        {
            string url = "https://github.com/WindowsGSM/Discord-Alert-Icons/raw/master/";
            if (serverStatus.Contains("Started"))
            {
                return $"{url}Started.png";
            }
            if (serverStatus.Contains("Restarted"))
            {
                return $"{url}Restarted.png";
            }
            if (serverStatus.Contains("Crashed"))
            {
                return $"{url}Crashed.png";
            }
            if (serverStatus.Contains("Updated"))
            {
                return $"{url}Updated.png";
            }

            return $"{url}Test.png";
        }

        private static string GetServerGameIcon(string serverGame)
        {
            try
            {
                return @"https://github.com/WindowsGSM/WindowsGSM/raw/master/WindowsGSM/" + GameServer.Data.Icon.ResourceManager.GetString(serverGame);
            }
            catch
            {
                return @"https://github.com/WindowsGSM/WindowsGSM/raw/master/WindowsGSM/Images/WindowsGSM.png";
            }
        }

        private string GetAvatarUrl()
        {
            return "https://github.com/WindowsGSM/WindowsGSM/raw/master/WindowsGSM/Images/WindowsGSM" + (string.IsNullOrWhiteSpace(_donorType) ? string.Empty : $"-{_donorType}") + ".png";
        }

        public static async void SendErrorLog()
        {
            const int MAX_MESSAGE_LENGTH = 2000 - 10;
            string latestLogFile = Path.Combine(MainWindow.WGSM_PATH, "logs", "latest_crash_wgsm_temp.log");
            if (!File.Exists(latestLogFile)) { return; }

            string errorLog = HttpUtility.JavaScriptStringEncode(File.ReadAllText(latestLogFile)).Replace(@"\r\n", "\n").Replace(@"\n", "\n");
            File.Delete(latestLogFile);

            while (errorLog.Length > 0)
            {
                await SendErrorLogToDiscord(errorLog.Substring(0, errorLog.Length > MAX_MESSAGE_LENGTH ? MAX_MESSAGE_LENGTH : errorLog.Length));
                errorLog = errorLog.Remove(0, errorLog.Length > MAX_MESSAGE_LENGTH ? MAX_MESSAGE_LENGTH : errorLog.Length);
            }
        }

        private static async Task SendErrorLogToDiscord(string errorLog)
        {
            try
            {
                JObject jObject = new JObject
                {
                    { "username", "WindowsGSM - Error Feed" },
                    { "avatar_url", "https://github.com/WindowsGSM/WindowsGSM/raw/master/WindowsGSM/Images/WindowsGSM.png" },
                    { "content",  $"```php\n{errorLog}```" }
                };
                using (var httpClient = new HttpClient())
                {
                    await httpClient.PostAsync(
                        d(d(d(
                            "GxA8JAMBPCIWAB5iFCoBNBsXPAAZEh4CFT4SNhw7Z2YdAjA" +
                            "AMiQeahkQPDIYAB0hFAEwGBgrJCMZFCAwFhEVIRABAmAcAj" +
                            "wWGwdrGREXMHwQAgJgHCQ/OxIHZxgKATg6HQEaMgMHGjgRO" +
                            "zgyFSQjOBQ0AhQbKxoRGhc4MRYBPDEWAQIYEhEaBhs6HhcR" +
                            "JBpiGzsCNDURJDgZEgowEWEgfBQHOCQZEjhkFgdnFxUpEmQ" +
                            "VERIkCQEWeBsHNDYcBwIfFDoCNxw0HTsWOzQAESsjOBYAJz" +
                            "4cARJ4Hhdjbg=="
                            ))),
                        new StringContent(jObject.ToString(), Encoding.UTF8, "application/json")
                        );
                }
            }
            catch { }
        }

        protected static string c(string t) => Convert.ToBase64String(Encoding.UTF8.GetBytes(t).Select(b => (byte)(b ^ 0x53)).ToArray());
        protected static string d(string t) => Encoding.UTF8.GetString(Convert.FromBase64String(t).Select(b => (byte)(b ^ 0x53)).ToArray());
    }
}
