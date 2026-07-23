using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AstroDeviceHub;

public sealed class DesktopPresenceTracker
{
    private long _lastHeartbeatUtcTicks;

    public DateTimeOffset? LastHeartbeatUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastHeartbeatUtcTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public void RecordHeartbeat() => Interlocked.Exchange(ref _lastHeartbeatUtcTicks, DateTimeOffset.UtcNow.Ticks);

    public void RecordRestartAttempt(DateTimeOffset now) => Interlocked.Exchange(ref _lastHeartbeatUtcTicks, now.UtcTicks);
}

public static class DesktopRestartPolicy
{
    public static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(8);

    public static bool ShouldRestart(int activeClientCount, DateTimeOffset? lastHeartbeatUtc, DateTimeOffset now)
        => activeClientCount > 0
           && (lastHeartbeatUtc is null || now - lastHeartbeatUtc.Value >= HeartbeatTimeout);
}

internal sealed class DesktopSupervisor(
    DeviceHub hub,
    DesktopPresenceTracker presence,
    ILogger<DesktopSupervisor> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            if (DesktopRestartPolicy.ShouldRestart(hub.ActiveClientCount, presence.LastHeartbeatUtc, now))
            {
                presence.RecordRestartAttempt(now);
                if (DesktopApplicationLauncher.TryStart(logger))
                    logger.LogInformation("Restarted Astro Device Hub desktop because {ClientCount} application client(s) remain connected.", hub.ActiveClientCount);
            }

            try { await Task.Delay(CheckInterval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }
    }
}

internal static class DesktopApplicationLauncher
{
    public static bool TryStart(ILogger logger)
    {
        var executable = FindDesktopExecutable();
        if (executable is null)
        {
            logger.LogWarning("Astro Device Hub desktop executable was not found; the desktop window cannot be restarted.");
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to restart the Astro Device Hub desktop window.");
            return false;
        }
    }

    private static string? FindDesktopExecutable()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; directory is not null && depth < 3; depth++, directory = directory.Parent)
        {
            var direct = Path.Combine(directory.FullName, "AstroDeviceHub.App.exe");
            if (File.Exists(direct)) return direct;

            var appDirectory = Path.Combine(directory.FullName, "app", "AstroDeviceHub.App.exe");
            if (File.Exists(appDirectory)) return appDirectory;
        }
        return null;
    }
}
