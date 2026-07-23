using Microsoft.AspNetCore.Builder;
using System.IO;
using System.Threading;
using System.Windows;

namespace AstroDeviceHub.Desktop;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private WebApplication? _server;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, @"Local\AstroDeviceHub-Desktop", out var ownsInstance);
        if (!ownsInstance)
        {
            MessageBox.Show("Astro Device Hub 已经在运行。", "Astro Device Hub", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        try
        {
            var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            _server = HubWebApplication.Build(
                HubWebApplication.ResolveBindingArguments(Array.Empty<string>()),
                AppContext.BaseDirectory,
                webRoot);
            await _server.StartAsync();
            new MainWindow().Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show("启动设备服务失败。\r\n\r\n" + ex.Message, "Astro Device Hub", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_server is not null)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { _server.StopAsync(timeout.Token).GetAwaiter().GetResult(); } catch { }
            _server.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        if (_singleInstance is not null)
        {
            try { _singleInstance.ReleaseMutex(); } catch (ApplicationException) { }
            _singleInstance.Dispose();
        }
        base.OnExit(e);
    }
}
