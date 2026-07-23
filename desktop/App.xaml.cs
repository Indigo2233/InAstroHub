using System.Diagnostics;
using System.Net.Http;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace AstroDeviceHub.Desktop;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private readonly HttpClient _serverClient = new() { BaseAddress = new Uri("http://127.0.0.1:5000"), Timeout = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _heartbeatTimer = new() { Interval = TimeSpan.FromSeconds(2) };

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
            await EnsureServerAvailableAsync();
            _heartbeatTimer.Tick += async (_, _) => await SendHeartbeatAsync();
            _heartbeatTimer.Start();
            await SendHeartbeatAsync();
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
        _heartbeatTimer.Stop();
        _serverClient.Dispose();
        if (_singleInstance is not null)
        {
            try { _singleInstance.ReleaseMutex(); } catch (ApplicationException) { }
            _singleInstance.Dispose();
        }
        base.OnExit(e);
    }

    private async Task EnsureServerAvailableAsync()
    {
        if (await IsServerHealthyAsync()) return;

        var executable = Path.Combine(AppContext.BaseDirectory, "server", "AstroDeviceHub.exe");
        if (!File.Exists(executable))
            throw new FileNotFoundException("找不到本地服务程序。请重新安装 Astro Device Hub。", executable);

        Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(250);
            if (await IsServerHealthyAsync()) return;
        }
        throw new InvalidOperationException("本地服务启动超时。请检查端口 5000 是否被占用。");
    }

    private async Task<bool> IsServerHealthyAsync()
    {
        try
        {
            using var response = await _serverClient.GetAsync("/api/health");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private async Task SendHeartbeatAsync()
    {
        try
        {
            using var response = await _serverClient.PostAsync("/api/desktop/heartbeat", null);
        }
        catch { }
    }
}
