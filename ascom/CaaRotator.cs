using ASCOM.DeviceInterface;
using System;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;

namespace AstroDeviceHub.Ascom
{
    [Guid("3F914162-A0BA-49BF-B43F-B2477966F64B")]
    [ProgId("ASCOM.AstroDeviceHub.Rotator")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class CaaRotator : IRotatorV2
    {
        private const double StepsPerDegree = 100.0;
        private const double CenterSteps = 200.0 * StepsPerDegree;
        private readonly HubClient hub = new HubClient(HubSettings.BaseUrl, HubSettings.NewClientId("ASCOM-Rotator"), "Caa");
        private bool reverse;
        private float target;

        public bool Connected { get => hub.Connected; set { if (value == hub.Connected) return; if (value) hub.Connect(); else hub.Disconnect(); } }
        public string Description => "Astro Device Hub 电动 CAA 驱动";
        public string DriverInfo => "InECAA · Astro Device Hub ASCOM Local Server";
        public string DriverVersion => "0.2.0";
        public short InterfaceVersion => 2;
        public string Name => "InECAA";
        public ArrayList SupportedActions => DriverSupport.NoActions;
        public bool CanReverse => true;
        public bool IsMoving { get { int p; bool moving; ReadMotion(out p, out moving); return moving; } }
        public float Position { get { int steps; bool moving; ReadMotion(out steps, out moving); return Normalize((float)((steps - CenterSteps) / StepsPerDegree)); } }
        public float TargetPosition => target;
        public float StepSize => (float)(1.0 / StepsPerDegree);
        public bool Reverse { get => reverse; set { DriverSupport.Require(Connected, "Reverse"); hub.Command("reverse", value ? 1 : 0); reverse = value; } }

        public void Move(float position) { MoveAbsolute(Position + position); }
        public void MoveAbsolute(float position)
        {
            DriverSupport.Require(Connected, "MoveAbsolute");
            target = Normalize(position);
            hub.Command("move", (int)Math.Round(CenterSteps + target * StepsPerDegree));
        }
        public void Halt() { DriverSupport.Require(Connected, "Halt"); hub.Command("halt"); }
        public void SetupDialog() { DriverSupport.ShowSetup(); }
        public string Action(string actionName, string actionParameters) { throw new ASCOM.ActionNotImplementedException(actionName); }
        public void CommandBlind(string command, bool raw) { CommandString(command, raw); }
        public bool CommandBool(string command, bool raw) { var value = CommandString(command, raw); bool result; return bool.TryParse(value.Trim().TrimEnd('#'), out result) && result; }
        public string CommandString(string command, bool raw) { DriverSupport.Require(Connected, "CommandString"); return hub.Command("raw", null, command); }
        public void Dispose() { hub.Dispose(); }

        private void ReadMotion(out int position, out bool moving) { DriverSupport.Require(Connected, "Status"); DriverSupport.ParseMotion(hub.Command("status"), out position, out moving); }
        private static float Normalize(float angle) { angle %= 360; if (angle < 0) angle += 360; return angle; }
    }
}
