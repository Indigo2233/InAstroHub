using ASCOM.DeviceInterface;
using System;
using System.Collections;
using System.Linq;
using System.Runtime.InteropServices;

namespace AstroDeviceHub.Ascom
{
    [Guid("D6538AB1-D9BF-421A-A2B7-3A2CF11B727D")]
    [ProgId("ASCOM.AstroDeviceHub.FilterWheel")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class HubFilterWheel : IFilterWheelV2
    {
        private const int SlotCount = 7;
        private readonly HubClient hub;
        private readonly int ascomSlot;
        public HubFilterWheel() : this(1) { }
        protected HubFilterWheel(int ascomSlot)
        {
            this.ascomSlot = ascomSlot;
            hub = new HubClient(HubSettings.BaseUrl, HubSettings.NewClientId("ASCOM-FilterWheel-Device" + ascomSlot), "FilterWheel", ascomSlot);
        }
        public bool Connected { get => hub.Connected; set { if (value == hub.Connected) return; if (value) hub.Connect(); else hub.Disconnect(); } }
        public string Description => "Astro Device Hub 电动滤镜轮驱动";
        public string DriverInfo => "InEFilterWheel-Device" + ascomSlot + " · Astro Device Hub ASCOM Local Server";
        public string DriverVersion => "0.2.0";
        public short InterfaceVersion => 2;
        public string Name => "InEFilterWheel-Device" + ascomSlot;
        public ArrayList SupportedActions => DriverSupport.NoActions;
        public string[] Names => Enumerable.Range(1, SlotCount).Select(i => "Filter " + i).ToArray();
        public int[] FocusOffsets => new int[SlotCount];
        public short Position
        {
            get { DriverSupport.Require(Connected, "Position"); return (short)DriverSupport.ParseInteger(hub.Command("position")); }
            set { DriverSupport.Require(Connected, "Position"); if (value < 0 || value >= SlotCount) throw new ASCOM.InvalidValueException("Position", value.ToString(), "0 to " + (SlotCount - 1)); hub.Command("goto", value); }
        }
        public void SetupDialog() { DriverSupport.ShowSetup(); }
        public string Action(string actionName, string actionParameters) { throw new ASCOM.ActionNotImplementedException(actionName); }
        public void CommandBlind(string command, bool raw) { CommandString(command, raw); }
        public bool CommandBool(string command, bool raw) { return CommandString(command, raw).StartsWith("OK", StringComparison.OrdinalIgnoreCase); }
        public string CommandString(string command, bool raw) { DriverSupport.Require(Connected, "CommandString"); return hub.Command("raw", null, command); }
        public void Dispose() { hub.Dispose(); }
    }
}
