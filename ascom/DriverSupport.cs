using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;

namespace AstroDeviceHub.Ascom
{
    internal static class DriverSupport
    {
        internal static readonly ArrayList NoActions = new ArrayList();
        internal static void ShowSetup()
        {
            try { Process.Start(HubSettings.BaseUrl); }
            catch { MessageBox.Show("请先启动 Astro Device Hub，然后打开 " + HubSettings.BaseUrl, "Astro Device Hub", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }

        internal static void Require(bool connected, string member)
        {
            if (!connected) throw new ASCOM.NotConnectedException(member + ": 设备尚未连接。请先启动 Astro Device Hub。 ");
        }

        internal static void ParseMotion(string response, out int position, out bool moving)
        {
            position = 0; moving = false;
            var clean = (response ?? "").Trim().TrimEnd('#');
            foreach (var field in clean.Split(';'))
            {
                var parts = field.Trim().Split(new[] { ' ' }, 2);
                if (parts.Length != 2) continue;
                if (parts[0] == "P") int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out position);
                if (parts[0] == "M") bool.TryParse(parts[1], out moving);
            }
        }

        internal static int ParseInteger(string response)
        {
            var value = (response ?? "").Trim().Trim('<', '>', '#');
            if (value.StartsWith("OK ", StringComparison.OrdinalIgnoreCase)) value = value.Substring(3).Trim();
            int result;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                throw new ASCOM.DriverException("设备返回了无效数值: " + response);
            return result;
        }
    }
}
