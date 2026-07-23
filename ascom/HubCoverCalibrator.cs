using ASCOM.DeviceInterface;
using ASCOM.Utilities;
using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace AstroDeviceHub.Ascom
{
    [Guid("372AFB70-FF2F-4868-BD91-7CCB7DA48EA2")]
    [ProgId("ASCOM.AstroDeviceHub.CoverCalibrator")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class HubCoverCalibrator : ICoverCalibratorV2
    {
        private readonly HubClient hub = new HubClient(HubSettings.BaseUrl, HubSettings.NewClientId("ASCOM-CoverCalibrator"), "CoverCalibrator");
        private bool connecting;

        public bool Connected { get => hub.Connected; set { if (value == hub.Connected) return; if (value) Connect(); else Disconnect(); } }
        public bool Connecting => connecting;
        public string Description => "Astro Device Hub 电动平场板驱动";
        public string DriverInfo => "InDLCoverCalibrator · Astro Device Hub ASCOM Local Server";
        public string DriverVersion => "0.2.0";
        public short InterfaceVersion => 2;
        public string Name => "InDLCoverCalibrator";
        public ArrayList SupportedActions => DriverSupport.NoActions;
        public CoverStatus CoverState => (CoverStatus)Query("cover-state");
        public CalibratorStatus CalibratorState => (CalibratorStatus)Query("calibrator-state");
        public int Brightness => Query("brightness-get");
        public int MaxBrightness => Query("max-brightness");
        public bool CoverMoving => CoverState == CoverStatus.Moving;
        public bool CalibratorChanging => CalibratorState == CalibratorStatus.NotReady;
        public IStateValueCollection DeviceState
        {
            get
            {
                var state = new StateValueCollection();
                state.Add("Connected", Connected);
                state.Add("Connecting", Connecting);
                if (Connected)
                {
                    state.Add("CoverState", CoverState);
                    state.Add("CalibratorState", CalibratorState);
                    state.Add("Brightness", Brightness);
                }
                state.AddUtcDateTime();
                return state;
            }
        }

        public void Connect() { connecting = true; try { hub.Connect(); } finally { connecting = false; } }
        public void Disconnect() { hub.Disconnect(); }
        public void OpenCover() { DriverSupport.Require(Connected, "OpenCover"); hub.Command("open"); }
        public void CloseCover() { DriverSupport.Require(Connected, "CloseCover"); hub.Command("close"); }
        public void HaltCover() { DriverSupport.Require(Connected, "HaltCover"); hub.Command("halt"); }
        public void CalibratorOn(int brightness)
        {
            DriverSupport.Require(Connected, "CalibratorOn");
            var maximum = MaxBrightness;
            if (brightness < 0 || brightness > maximum) throw new ASCOM.InvalidValueException("CalibratorOn", brightness.ToString(), "0 to " + maximum);
            hub.Command("brightness", brightness);
        }
        public void CalibratorOff() { DriverSupport.Require(Connected, "CalibratorOff"); hub.Command("light-off"); }
        public void SetupDialog() { DriverSupport.ShowSetup(); }
        public string Action(string actionName, string actionParameters) { throw new ASCOM.ActionNotImplementedException(actionName); }
        public void CommandBlind(string command, bool raw) { CommandString(command, raw); }
        public bool CommandBool(string command, bool raw) { return CommandString(command, raw) != "?"; }
        public string CommandString(string command, bool raw) { DriverSupport.Require(Connected, "CommandString"); return hub.Command("raw", null, command); }
        public void Dispose() { hub.Dispose(); }
        private int Query(string action) { DriverSupport.Require(Connected, action); return DriverSupport.ParseInteger(hub.Command(action)); }
    }
}
