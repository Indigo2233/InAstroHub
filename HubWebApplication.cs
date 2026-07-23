namespace AstroDeviceHub;

public static class HubWebApplication
{
    public static string[] ResolveBindingArguments(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--urls", StringComparison.OrdinalIgnoreCase)
            || arg.StartsWith("--urls=", StringComparison.OrdinalIgnoreCase))) return args;
        var settings = new ServerAccessStore().Read();
        var binding = settings.RemoteAccessEnabled ? "http://0.0.0.0:5000" : "http://127.0.0.1:5000";
        return args.Concat(["--urls", binding]).ToArray();
    }

    public static WebApplication Build(string[] args, string? contentRoot = null, string? webRoot = null)
    {
        var options = new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = contentRoot ?? Directory.GetCurrentDirectory(),
            WebRootPath = webRoot
        };
        var builder = WebApplication.CreateBuilder(options);
        builder.Services.AddSingleton<DeviceHub>();
        builder.Services.AddSingleton<FirmwareService>();
        builder.Services.AddSingleton<ServerAccessStore>();

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            var remoteAddress = context.Connection.RemoteIpAddress;
            var access = context.RequestServices.GetRequiredService<ServerAccessStore>();
            if (remoteAddress is not null && !System.Net.IPAddress.IsLoopback(remoteAddress)
                && !access.IsValidRemoteToken(context.Request.Headers["X-Astro-Access-Token"]))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "局域网访问需要有效的访问令牌。" });
                return;
            }
            await next();
        });
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/api/health", () => Results.Ok(new { status = "ok", product = "Astro Device Hub", version = "0.3.0", serverTime = DateTimeOffset.Now }));
        app.MapGet("/api/server", (DeviceHub hub) => Results.Ok(hub.ServerStatus()));
        app.MapGet("/api/server/access", (ServerAccessStore access) =>
        {
            var settings = access.Read();
            return Results.Ok(new { settings.RemoteAccessEnabled, tokenConfigured = !string.IsNullOrWhiteSpace(settings.AccessToken) });
        });
        app.MapPut("/api/server/access", (ServerAccessStore access, UpdateServerAccessRequest request) =>
        {
            var settings = access.Update(request);
            return Results.Ok(new
            {
                settings.RemoteAccessEnabled,
                accessToken = settings.AccessToken,
                restartRequired = true
            });
        });
        app.MapGet("/api/devices", (DeviceHub hub) => Results.Ok(hub.List()));
        app.MapGet("/api/devices/by-kind/{kind}", (DeviceHub hub, DeviceKind kind) => hub.FindByKind(kind));
        app.MapGet("/api/devices/by-kind/{kind}/slot/{slot:int}", (DeviceHub hub, DeviceKind kind, int slot) => hub.FindByKindAndSlot(kind, slot));
        app.MapGet("/api/ports", () => Results.Ok(DeviceHub.ListPorts()));
        app.MapPost("/api/devices", (DeviceHub hub, DeviceRequest request) => hub.Add(request));
        app.MapDelete("/api/devices/{id}", (DeviceHub hub, string id) => hub.Remove(id));
        app.MapPost("/api/devices/{id}/connect", async (DeviceHub hub, string id, DeviceLeaseRequest lease) => await hub.ConnectAsync(id, lease));
        app.MapPost("/api/devices/{id}/disconnect", (DeviceHub hub, string id, DeviceLeaseRequest lease) => hub.Disconnect(id, lease));
        app.MapPost("/api/devices/{id}/command", async (DeviceHub hub, string id, DeviceCommand request) => await hub.CommandAsync(id, request));
        app.MapPost("/api/firmware/validate", async (FirmwareService firmware, IFormFile file) => await firmware.ValidateAsync(file));
        app.MapPost("/api/devices/{id}/firmware", async (DeviceHub hub, string id, IFormFile file) => await hub.StageFirmwareAsync(id, file));
        app.MapPost("/api/devices/{id}/firmware/flash", async (DeviceHub hub, string id, IFormFile file, string target, string port, bool motorPowerDisconnected) =>
            await hub.FlashFirmwareAsync(id, file, target, port, motorPowerDisconnected));

        return app;
    }
}
