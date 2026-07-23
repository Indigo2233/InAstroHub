using ASCOM.Utilities;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AstroDeviceHub.Ascom
{
    [ComVisible(false)]
    internal static class LocalServer
    {
        private const string AppId = "{F26D63EC-04A7-4776-88E4-2A3B9526CC77}";
        private const string DotNetCategory = "{62C8FE65-4EBB-45E7-B440-6E39B2CDBF29}";
        private static readonly DriverRegistration[] Drivers =
        {
            new DriverRegistration(typeof(CaaRotator), "ASCOM.AstroDeviceHub.Rotator", "InECAA", "Rotator"),
            new DriverRegistration(typeof(HubFocuser), "ASCOM.AstroDeviceHub.Focuser", "InEFucoser", "Focuser"),
            new DriverRegistration(typeof(HubCoverCalibrator), "ASCOM.AstroDeviceHub.CoverCalibrator", "InDLCoverCalibrator", "CoverCalibrator"),
            new DriverRegistration(typeof(HubFilterWheel), "ASCOM.AstroDeviceHub.FilterWheel", "InEFilterWheel", "FilterWheel")
        };

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                var command = args[0].ToLowerInvariant();
                var silent = args.Any(arg => string.Equals(arg, "/silent", StringComparison.OrdinalIgnoreCase));
                if (command == "/regserver" || command == "-regserver") { RegisterServer(silent); return; }
                if (command == "/unregserver" || command == "-unregserver") { UnregisterServer(silent); return; }
            }

            bool ownsMutex;
            using (var mutex = new Mutex(true, @"Local\AstroDeviceHub-ASCOM-LocalServer", out ownsMutex))
            {
                if (!ownsMutex) return;
                Application.EnableVisualStyles();
                var factories = new List<ClassFactory>();
                try
                {
                    foreach (var registration in Drivers)
                    {
                        var factory = new ClassFactory(registration.Type);
                        factory.Register();
                        factories.Add(factory);
                    }
                    ClassFactory.ResumeAll();
                    Application.Run();
                }
                finally
                {
                    ClassFactory.SuspendAll();
                    foreach (var factory in factories) factory.Revoke();
                    try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
                }
            }
        }

        private static void RegisterServer(bool silent = false)
        {
            try
            {
                RegisterView(RegistryView.Registry32);
                if (Environment.Is64BitOperatingSystem) RegisterView(RegistryView.Registry64);
                foreach (var registration in Drivers)
                {
                    using (var profile = new Profile())
                    {
                        profile.DeviceType = registration.DeviceType;
                        if (profile.IsRegistered(registration.ProgId)) profile.Unregister(registration.ProgId);
                        profile.Register(registration.ProgId, registration.DisplayName);
                    }
                }
                if (!silent) MessageBox.Show("Astro Device Hub 的四类 ASCOM 驱动已注册。", "Astro Device Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (!silent) MessageBox.Show("ASCOM 注册失败，请以管理员身份运行。\r\n\r\n" + ex.Message, "Astro Device Hub", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
            }
        }

        private static void UnregisterServer(bool silent = false)
        {
            try
            {
                UnregisterView(RegistryView.Registry32);
                if (Environment.Is64BitOperatingSystem) UnregisterView(RegistryView.Registry64);
                foreach (var registration in Drivers)
                {
                    using (var profile = new Profile())
                    {
                        profile.DeviceType = registration.DeviceType;
                        if (profile.IsRegistered(registration.ProgId)) profile.Unregister(registration.ProgId);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!silent) MessageBox.Show("ASCOM 注销失败，请以管理员身份运行。\r\n\r\n" + ex.Message, "Astro Device Hub", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
            }
        }

        private static void RegisterView(RegistryView view)
        {
            var executable = Assembly.GetExecutingAssembly().Location;
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            using (var classes = machine.CreateSubKey(@"Software\Classes"))
            {
                using (var app = classes.CreateSubKey(@"AppID\" + AppId)) app.SetValue(null, "Astro Device Hub ASCOM Local Server");
                using (var exe = classes.CreateSubKey(@"AppID\" + Path.GetFileName(executable))) exe.SetValue("AppID", AppId);
                foreach (var registration in Drivers)
                {
                    var clsid = "{" + registration.Type.GUID.ToString().ToUpperInvariant() + "}";
                    DeleteTree(classes, @"CLSID\" + clsid);
                    DeleteTree(classes, registration.ProgId);
                    using (var key = classes.CreateSubKey(@"CLSID\" + clsid))
                    {
                        key.SetValue(null, registration.DisplayName);
                        key.SetValue("AppID", AppId);
                        using (key.CreateSubKey(@"Implemented Categories\" + DotNetCategory)) { }
                        using (var p = key.CreateSubKey("ProgID")) p.SetValue(null, registration.ProgId);
                        using (key.CreateSubKey("Programmable")) { }
                        using (var local = key.CreateSubKey("LocalServer32")) local.SetValue(null, "\"" + executable + "\"");
                    }
                    using (var progId = classes.CreateSubKey(registration.ProgId))
                    {
                        progId.SetValue(null, registration.DisplayName);
                        using (var c = progId.CreateSubKey("CLSID")) c.SetValue(null, clsid);
                    }
                }
            }
        }

        private static void UnregisterView(RegistryView view)
        {
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            using (var classes = machine.OpenSubKey(@"Software\Classes", true))
            {
                if (classes == null) return;
                foreach (var registration in Drivers)
                {
                    DeleteTree(classes, @"CLSID\{" + registration.Type.GUID.ToString().ToUpperInvariant() + "}");
                    DeleteTree(classes, registration.ProgId);
                }
                DeleteTree(classes, @"AppID\" + AppId);
                DeleteTree(classes, @"AppID\" + Path.GetFileName(Assembly.GetExecutingAssembly().Location));
            }
        }

        private static void DeleteTree(RegistryKey root, string name) { try { root.DeleteSubKeyTree(name, false); } catch (ArgumentException) { } }

        private sealed class DriverRegistration
        {
            internal DriverRegistration(Type type, string progId, string displayName, string deviceType) { Type = type; ProgId = progId; DisplayName = displayName; DeviceType = deviceType; }
            internal Type Type { get; private set; }
            internal string ProgId { get; private set; }
            internal string DisplayName { get; private set; }
            internal string DeviceType { get; private set; }
        }
    }
}
