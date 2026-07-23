using System.Runtime.InteropServices;

namespace AstroDeviceHub.Ascom
{
    [Guid("72498CA9-1273-4D43-BDF3-CF04F4229AEA")]
    [ProgId("ASCOM.AstroDeviceHub.Rotator.Device2")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class CaaRotatorDevice2 : CaaRotator { public CaaRotatorDevice2() : base(2) { } }

    [Guid("3B39569E-0E39-4258-8E64-EC83A3E48EC5")]
    [ProgId("ASCOM.AstroDeviceHub.Rotator.Device3")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class CaaRotatorDevice3 : CaaRotator { public CaaRotatorDevice3() : base(3) { } }

    [Guid("D5122B00-57EA-46DE-90F2-67DE9217FECE")]
    [ProgId("ASCOM.AstroDeviceHub.Focuser.Device2")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class HubFocuserDevice2 : HubFocuser { public HubFocuserDevice2() : base(2) { } }

    [Guid("DCFDB900-F13E-45B0-B017-8617102BC38D")]
    [ProgId("ASCOM.AstroDeviceHub.Focuser.Device3")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class HubFocuserDevice3 : HubFocuser { public HubFocuserDevice3() : base(3) { } }

    [Guid("D7C65762-D90D-4F73-95FD-C662F2F3E904")]
    [ProgId("ASCOM.AstroDeviceHub.CoverCalibrator.Device2")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class HubCoverCalibratorDevice2 : HubCoverCalibrator { public HubCoverCalibratorDevice2() : base(2) { } }

    [Guid("3C78680B-7844-478B-B23C-EBFBF6689385")]
    [ProgId("ASCOM.AstroDeviceHub.CoverCalibrator.Device3")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class HubCoverCalibratorDevice3 : HubCoverCalibrator { public HubCoverCalibratorDevice3() : base(3) { } }

    [Guid("6A86C34F-1F3A-4C83-944C-996D0D796CF4")]
    [ProgId("ASCOM.AstroDeviceHub.FilterWheel.Device2")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class HubFilterWheelDevice2 : HubFilterWheel { public HubFilterWheelDevice2() : base(2) { } }

    [Guid("C084479D-F023-4F91-9C54-F17898AC66A7")]
    [ProgId("ASCOM.AstroDeviceHub.FilterWheel.Device3")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class HubFilterWheelDevice3 : HubFilterWheel { public HubFilterWheelDevice3() : base(3) { } }
}
