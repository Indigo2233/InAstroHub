using ASCOM.DeviceInterface;
using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace AstroDeviceHub.Ascom
{
    [Guid("6AE095B1-5051-4892-9071-59A26D50C1FA")]
    [ProgId("ASCOM.AstroDeviceHub.Focuser")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class HubFocuser : IFocuserV3
    {
        private const int Maximum = 20000;
        private readonly HubClient hub = new HubClient(HubSettings.BaseUrl, HubSettings.NewClientId("ASCOM-Focuser"), "Focuser");

        public bool Connected { get => hub.Connected; set { if (value == hub.Connected) return; if (value) hub.Connect(); else hub.Disconnect(); } }
        public bool Link { get => Connected; set => Connected = value; }
        public string Description => "Astro Device Hub 电动调焦器驱动";
        public string DriverInfo => "InEFucoser · Astro Device Hub ASCOM Local Server";
        public string DriverVersion => "0.2.0";
        public short InterfaceVersion => 3;
        public string Name => "InEFucoser";
        public ArrayList SupportedActions => DriverSupport.NoActions;
        public bool Absolute => true;
        public bool IsMoving { get { int p; bool moving; ReadMotion(out p, out moving); return moving; } }
        public int MaxIncrement => Maximum;
        public int MaxStep => Maximum;
        public int Position { get { int position; bool moving; ReadMotion(out position, out moving); return Math.Max(0, Math.Min(Maximum, position)); } }
        public double StepSize { get { throw new ASCOM.PropertyNotImplementedException("StepSize", false); } }
        public bool TempComp { get => false; set { if (value) throw new ASCOM.PropertyNotImplementedException("TempComp", true); } }
        public bool TempCompAvailable => false;
        public double Temperature { get { throw new ASCOM.PropertyNotImplementedException("Temperature", false); } }

        public void Move(int position)
        {
            DriverSupport.Require(Connected, "Move");
            if (position < 0 || position > Maximum) throw new ASCOM.InvalidValueException("Move", position.ToString(), "0 to " + Maximum);
            hub.Command("move", position);
        }
        public void Halt() { DriverSupport.Require(Connected, "Halt"); hub.Command("halt"); }
        public void SetupDialog() { DriverSupport.ShowSetup(); }
        public string Action(string actionName, string actionParameters) { throw new ASCOM.ActionNotImplementedException(actionName); }
        public void CommandBlind(string command, bool raw) { CommandString(command, raw); }
        public bool CommandBool(string command, bool raw) { bool value; return bool.TryParse(CommandString(command, raw).Trim().TrimEnd('#'), out value) && value; }
        public string CommandString(string command, bool raw) { DriverSupport.Require(Connected, "CommandString"); return hub.Command("raw", null, command); }
        public void Dispose() { hub.Dispose(); }
        private void ReadMotion(out int position, out bool moving) { DriverSupport.Require(Connected, "Status"); DriverSupport.ParseMotion(hub.Command("status"), out position, out moving); }
    }
}
