using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WindowsGSM.GameServer.Query
{
    public class EOS : IDisposable
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string DeploymentId { get; set; }
        public string EpicApi { get; set; } = "https://api.epicgames.dev";
        public bool AuthByExternalToken { get; set; }
        public bool WildcardMatchmaking { get; set; }

        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        private readonly object _addrLock = new object();
        private string _address;
        private int _port;
        private int _timeoutMs = 15000;

        private readonly object _tokenLock = new object();
        private string _accessToken;

        public EOS() { }

        public EOS(string clientId, string clientSecret, string deploymentId, bool authByExternalToken = false, bool wildcardMatchmaking = false)
        {
            ClientId = clientId;
            ClientSecret = clientSecret;
            DeploymentId = deploymentId;
            AuthByExternalToken = authByExternalToken;
            WildcardMatchmaking = wildcardMatchmaking;
        }

        public void SetAddressPort(string address, int port, int timeout = 5)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));

            lock (_addrLock)
            {
                _address = address;
                _port = port;
                _timeoutMs = timeout > 0 ? timeout * 1000 : 15000;
            }
        }

        public async Task<Dictionary<string, string>> GetInfo()
        {
            string address;
            int port;
            int timeoutMs;

            lock (_addrLock)
            {
                address = _address;
                port = _port;
                timeoutMs = _timeoutMs;
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }

            return await GetInfo(address, port, timeoutMs).ConfigureAwait(false);
        }

        public async Task<string> GetPlayersAndMaxPlayers()
        {
            try
            {
                var kv = await GetInfo().ConfigureAwait(false);
                if (kv == null) return null;

                if (!kv.TryGetValue("Players", out var players) || !kv.TryGetValue("MaxPlayers", out var maxPlayers))
                {
                    return null;
                }

                return players + "/" + maxPlayers;
            }
            catch
            {
                return null;
            }
        }

        private async Task<Dictionary<string, string>> GetInfo(string address, int port, int timeoutMs)
        {
            string token = null;

            try
            {
                using (var cts = new CancellationTokenSource(timeoutMs))
                {
                    token = await GetAccessTokenAsync(cts.Token).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(token))
                    {
                        return null;
                    }

                    var session = await FindSessionAsync(address, port, token, cts.Token).ConfigureAwait(false);
                    if (session == null)
                    {
                        return null;
                    }

                    var attrs = session["attributes"] as JObject;

                    return new Dictionary<string, string>
                    {
                        ["Name"] = attrs?.Value<string>("CUSTOMSERVERNAME_s") ?? string.Empty,
                        ["Map"] = attrs?.Value<string>("MAPNAME_s") ?? string.Empty,
                        ["Players"] = (session.Value<int?>("totalPlayers") ?? 0).ToString(),
                        ["MaxPlayers"] = (session["settings"]?.Value<int?>("maxPublicPlayers") ?? 0).ToString(),
                        ["Password"] = (attrs?.Value<bool?>("SERVERPASSWORD_b") ?? false) ? "True" : "False"
                    };
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                // Clear token to avoid sharing across concurrent queries / servers.
                // Protected so one query finishing doesn't clobber another's token mid-flight.
                lock (_tokenLock)
                {
                    if (!string.IsNullOrEmpty(_accessToken) && string.Equals(_accessToken, token, StringComparison.Ordinal))
                    {
                        _accessToken = null;
                    }
                }
            }
        }

        private async Task<string> GetAccessTokenAsync(CancellationToken ct)
        {
            lock (_tokenLock)
            {
                if (!string.IsNullOrEmpty(_accessToken))
                {
                    return _accessToken;
                }
            }

            var token = AuthByExternalToken
                ? await GetExternalAccessTokenAsync(ct).ConfigureAwait(false)
                : await GetClientAccessTokenAsync(ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            lock (_tokenLock)
            {
                _accessToken = token;
                return _accessToken;
            }
        }

        private async Task<string> GetClientAccessTokenAsync(CancellationToken ct)
        {
            var url = $"{EpicApi.TrimEnd('/')}/auth/v1/oauth/token";
            var bodyPairs = new[]
            {
                new KeyValuePair<string,string>("grant_type","client_credentials"),
                new KeyValuePair<string,string>("deployment_id", DeploymentId ?? string.Empty)
            };

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new FormUrlEncodedContent(bodyPairs);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}")));
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                var resp = await _http.SendAsync(request, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return null;

                var json = JObject.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
                return json.Value<string>("access_token");
            }
        }

        private async Task<string> GetExternalAccessTokenAsync(CancellationToken ct)
        {
            var deviceIdToken = await GetDeviceIdTokenAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(deviceIdToken))
            {
                return null;
            }

            var url = $"{EpicApi.TrimEnd('/')}/auth/v1/oauth/token";
            var bodyParts = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "external_auth"),
                new KeyValuePair<string, string>("external_auth_type", "deviceid_access_token"),
                new KeyValuePair<string, string>("external_auth_token", deviceIdToken),
                new KeyValuePair<string, string>("nonce", "ABCHFA3qgUCJ1XTPAoGDEF"),
                new KeyValuePair<string, string>("deployment_id", DeploymentId ?? string.Empty),
                new KeyValuePair<string, string>("display_name", "User")
            };

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new FormUrlEncodedContent(bodyParts);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}")));
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                var resp = await _http.SendAsync(request, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return null;

                var json = JObject.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
                return json.Value<string>("access_token");
            }
        }

        private async Task<string> GetDeviceIdTokenAsync(CancellationToken ct)
        {
            var url = $"{EpicApi.TrimEnd('/')}/auth/v1/accounts/deviceid";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new StringContent("deviceModel=PC", Encoding.UTF8, "application/x-www-form-urlencoded");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}")));
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                var resp = await _http.SendAsync(request, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return null;

                var json = JObject.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
                return json.Value<string>("access_token");
            }
        }

        private async Task<JObject> FindSessionAsync(string address, int port, string bearerToken, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(DeploymentId))
            {
                return null;
            }

            var baseUrl = EpicApi.TrimEnd('/');
            if (WildcardMatchmaking)
            {
                baseUrl = baseUrl + "/wildcard";
            }

            var url = $"{baseUrl}/matchmaking/v1/{DeploymentId}/filter";

            var requestBody = new JObject(
                new JProperty("criteria", new JArray(
                    new JObject(
                        new JProperty("key", "attributes.ADDRESS_s"),
                        new JProperty("op", "EQUAL"),
                        new JProperty("value", address)
                    )
                ))
            );

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var resp = await _http.SendAsync(request, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return null;

                var json = JObject.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
                var sessions = json["sessions"] as JArray;
                if (sessions == null || sessions.Count == 0) return null;

                var exact = sessions.Children<JObject>()
                    .FirstOrDefault(s =>
                    {
                        var attrs = s["attributes"] as JObject;
                        if (attrs == null) return false;

                        var addressBound = attrs.Value<string>("ADDRESSBOUND_s") ?? string.Empty;
                        var gsPort = attrs.Value<int?>("GAMESERVER_PORT_l");

                        if (gsPort != null && gsPort.Value == port) return true;
                        if (string.Equals(addressBound, $"0.0.0.0:{port}", StringComparison.Ordinal)) return true;
                        if (string.Equals(addressBound, $"{address}:{port}", StringComparison.Ordinal)) return true;

                        return false;
                    });

                if (exact != null)
                {
                    return exact;
                }

                return sessions.Children<JObject>()
                    .FirstOrDefault(s =>
                    {
                        var attrs = s["attributes"] as JObject;
                        if (attrs == null) return false;

                        var address_s = attrs.Value<string>("ADDRESS_s") ?? string.Empty;
                        if (string.Equals(address_s, address, StringComparison.Ordinal)) return true;

                        var addressDev = attrs.Value<string>("ADDRESSDEV_s") ?? string.Empty;
                        var devParts = addressDev.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim());
                        return devParts.Any(p => string.Equals(p, address, StringComparison.Ordinal));
                    });
            }
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }
}