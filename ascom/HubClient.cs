using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace AstroDeviceHub.Ascom
{
    internal sealed class HubClient : IDisposable
    {
        private readonly HttpClient http;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private string deviceId;

        internal HubClient(string baseUrl, string clientId, string kind)
        {
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1:5000" : baseUrl.TrimEnd('/');
            ClientId = clientId;
            Kind = kind;
            http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        }

        internal string BaseUrl { get; private set; }
        internal string ClientId { get; private set; }
        internal string Kind { get; private set; }
        internal bool Connected { get; private set; }

        internal void Connect()
        {
            EnsureHubAvailable();
            var device = GetObject("/api/devices/by-kind/" + Kind);
            deviceId = Convert.ToString(device["id"]);
            Post("/api/devices/" + deviceId + "/connect", new Dictionary<string, object> { ["clientId"] = ClientId });
            Connected = true;
        }

        internal void Disconnect()
        {
            if (!Connected || string.IsNullOrEmpty(deviceId)) return;
            try { Post("/api/devices/" + deviceId + "/disconnect", new Dictionary<string, object> { ["clientId"] = ClientId }); }
            finally { Connected = false; }
        }

        internal string Command(string action, int? value = null, string payload = null)
        {
            EnsureConnected();
            var body = new Dictionary<string, object> { ["action"] = action, ["clientId"] = ClientId };
            if (value.HasValue) body["value"] = value.Value;
            if (payload != null) body["payload"] = payload;
            var result = Post("/api/devices/" + deviceId + "/command", body);
            return Convert.ToString(result["response"]);
        }

        private Dictionary<string, object> GetObject(string path)
        {
            using (var response = http.GetAsync(BaseUrl + path).GetAwaiter().GetResult())
                return ReadResponse(response);
        }

        private void EnsureHubAvailable()
        {
            if (IsHealthy()) return;
            var executable = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server", "AstroDeviceHub.exe"));
            if (File.Exists(executable))
            {
                Process.Start(new ProcessStartInfo { FileName = executable, WorkingDirectory = Path.GetDirectoryName(executable), UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
                for (var attempt = 0; attempt < 20; attempt++) { Thread.Sleep(250); if (IsHealthy()) return; }
            }
            throw new InvalidOperationException("Astro Device Hub 服务未启动。请运行 AstroDeviceHub.exe。");
        }

        private bool IsHealthy()
        {
            try
            {
                using (var response = http.GetAsync(BaseUrl + "/api/health").GetAwaiter().GetResult()) return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private Dictionary<string, object> Post(string path, Dictionary<string, object> value)
        {
            var content = new StringContent(json.Serialize(value), Encoding.UTF8, "application/json");
            using (var response = http.PostAsync(BaseUrl + path, content).GetAwaiter().GetResult())
                return ReadResponse(response);
        }

        private Dictionary<string, object> ReadResponse(HttpResponseMessage response)
        {
            var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                string detail = text;
                try
                {
                    var error = json.Deserialize<Dictionary<string, object>>(text);
                    if (error.ContainsKey("message")) detail = Convert.ToString(error["message"]);
                    else if (error.ContainsKey("detail")) detail = Convert.ToString(error["detail"]);
                }
                catch { }
                throw new InvalidOperationException("Astro Device Hub 返回 " + (int)response.StatusCode + ": " + detail);
            }
            return json.Deserialize<Dictionary<string, object>>(text) ?? new Dictionary<string, object>();
        }

        private void EnsureConnected()
        {
            if (!Connected) throw new ASCOM.NotConnectedException("尚未连接 Astro Device Hub。");
        }

        public void Dispose() { Disconnect(); http.Dispose(); }
    }

    internal static class HubSettings
    {
        internal const string BaseUrl = "http://127.0.0.1:5000";
        internal static string NewClientId(string deviceType) => deviceType + "-" + Guid.NewGuid().ToString("N");
    }
}
