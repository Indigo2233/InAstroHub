using System;
using System.Threading.Tasks;
using Xunit;

namespace AstroDeviceHub.Tests;

public sealed class ProtocolTests
{
    [Fact]
    public void CaaCommandsUseHashTerminator()
    {
        Assert.Equal("M 24500#", Protocol.Command(DeviceKind.Caa, new DeviceCommand("move", "test", 24500)));
        Assert.Equal("G#", Protocol.Command(DeviceKind.Caa, new DeviceCommand("status", "test")));
    }

    [Fact]
    public void CoverCommandsUseAngleBrackets()
    {
        Assert.Equal("<T128>", Protocol.Command(DeviceKind.CoverCalibrator, new DeviceCommand("brightness", "test", 128)));
        Assert.Equal("<P>", Protocol.Command(DeviceKind.CoverCalibrator, new DeviceCommand("cover-state", "test")));
    }

    [Fact]
    public void FilterWheelCommandsUseNewline()
    {
        Assert.Equal("GOTO 3\n", Protocol.Command(DeviceKind.FilterWheel, new DeviceCommand("goto", "test", 3)));
        Assert.Equal("\n", Protocol.Terminator(DeviceKind.FilterWheel));
    }

    [Fact]
    public void RawCommandsReceiveExactlyOneFrameTerminator()
    {
        Assert.Equal("V#", Protocol.Command(DeviceKind.Caa, new DeviceCommand("raw", "test", Payload: "V#")));
        Assert.Equal("<B>", Protocol.Command(DeviceKind.CoverCalibrator, new DeviceCommand("raw", "test", Payload: "B")));
    }

    [Fact]
    public void CommandsThatNeedValuesRejectMissingValues()
    {
        Assert.Throws<ArgumentException>(() => Protocol.Command(DeviceKind.Focuser, new DeviceCommand("move", "test")));
    }
}

public sealed class ClientLeaseSetTests
{
    [Fact]
    public void CaaAllowsTwoDistinctApplications()
    {
        var leases = new ClientLeaseSet(2);
        leases.Acquire("NINA");
        leases.Acquire("PHD2");
        Assert.Equal(2, leases.Count);
    }

    [Fact]
    public void CaaRejectsThirdApplication()
    {
        var leases = new ClientLeaseSet(2);
        leases.Acquire("NINA");
        leases.Acquire("PHD2");
        Assert.Throws<InvalidOperationException>(() => leases.Acquire("Console"));
    }

    [Fact]
    public void OtherDevicesRejectSecondApplication()
    {
        var leases = new ClientLeaseSet(1);
        leases.Acquire("NINA");
        Assert.Throws<InvalidOperationException>(() => leases.Acquire("Console"));
    }

    [Fact]
    public void ReleasingLeaseMakesCapacityAvailable()
    {
        var leases = new ClientLeaseSet(1);
        leases.Acquire("NINA");
        leases.Release("NINA");
        leases.Acquire("Console");
        Assert.True(leases.Contains("Console"));
    }
}

public sealed class DeviceSlotTests
{
    [Fact]
    public void FirstAvailableSlotUsesLowestVacantSlot()
    {
        Assert.Equal(2, DeviceHub.NextAvailableSlot(new[] { 1, 3 }));
    }

    [Fact]
    public void NoSlotIsAvailableAfterThreeDevices()
    {
        Assert.Null(DeviceHub.NextAvailableSlot(new[] { 1, 2, 3 }));
    }
}

public sealed class DesktopRestartPolicyTests
{
    [Fact]
    public void RestartsWhenAClientIsConnectedAndDesktopHeartbeatExpired()
    {
        var now = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
        Assert.True(DesktopRestartPolicy.ShouldRestart(1, now - DesktopRestartPolicy.HeartbeatTimeout, now));
    }

    [Fact]
    public void DoesNotRestartWhenNoApplicationIsConnected()
    {
        var now = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
        Assert.False(DesktopRestartPolicy.ShouldRestart(0, null, now));
    }

    [Fact]
    public void DoesNotRestartWhileDesktopHeartbeatIsFresh()
    {
        var now = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
        Assert.False(DesktopRestartPolicy.ShouldRestart(1, now - TimeSpan.FromSeconds(2), now));
    }
}

public sealed class FirmwareServiceTests
{
    [Fact]
    public async Task FlashRejectsInvalidSerialPortBeforeStartingTool()
    {
        var result = await FirmwareService.FlashAsync(null, DeviceKind.Caa, "Nano", "not-a-port");
        Assert.False(result.Ok);
        Assert.Contains("COM", result.Message);
    }
}
